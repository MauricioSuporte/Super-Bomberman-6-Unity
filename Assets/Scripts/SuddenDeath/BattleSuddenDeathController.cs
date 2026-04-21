using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleSuddenDeathController : MonoBehaviour
{
    const float SuddenDeathTriggerTime = 60f;

    [Header("References")]
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private Tilemap destructibleTilemap;
    [SerializeField] private Grid stageGrid;

    [Header("Sudden Death")]
    [SerializeField] private bool usePlayableAreaFromBounds = true;
    [SerializeField] private Vector2Int bottomLeftCell = new Vector2Int(-7, -6);
    [SerializeField] private Vector2Int topRightCell = new Vector2Int(5, 4);
    [SerializeField] private SuddenDeathDropPattern dropPattern = SuddenDeathDropPattern.Spiral;
    [SerializeField] private bool randomizeDropPattern = true;
    [SerializeField] private bool randomizeStartCorner = true;
    [SerializeField] private bool randomizeRotationDirection = true;

    [Header("Falling Block Visual")]
    [SerializeField] private GameObject fallingBlockVisualPrefab;
    [SerializeField] private TileBase currentStageIndestructibleTile;
    [SerializeField] private float fallingHeight = 6f;
    [SerializeField] private float fallingDuration = 0.1f;
    [SerializeField] private float topScreenSpawnOffset = 0.5f;
    [SerializeField] private int visualSortingOrderOffset = 1;

    [Header("Shadow")]
    [SerializeField] private bool enableShadowVisual = true;
    [SerializeField] private float shadowLeadTime = 0.25f;
    [SerializeField] private float shadowStartSize = 0.15f;
    [SerializeField] private float shadowEndSize = 1f;
    [SerializeField, Range(0f, 1f)] private float shadowAlpha = 0.65f;
    [SerializeField] private int shadowSortingOrderOffset = 100;
    [SerializeField] private Vector3 shadowWorldOffset = new Vector3(0f, 0f, 0f);

    [Header("Damage")]
    [SerializeField] private float damageTickInterval = 0.2f;
    [SerializeField] private int damagePerTick = 1;

    [Header("Cleanup")]
    [SerializeField] private Vector2 cleanupOverlapSize = new Vector2(0.6f, 0.6f);

    [Header("Audio")]
    [SerializeField] private AudioClip hurryUpSfx;
    [SerializeField] private AudioClip fallingTileSfx;

    [Header("Music")]
    [SerializeField] private float fastMusicPitch = 1.2f;
    [SerializeField] private float resumeMusicDelayAfterHurryUp = 0.6f;

    [Header("Timing")]
    [SerializeField] private float delayBeforeTileDrops = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logSuddenDeathFlow = true;
    [SerializeField] private bool logVisualFlow = true;
    [SerializeField] private bool logShadowFlow = true;
    [SerializeField] private bool logDamageFlow = false;

    bool suddenDeathDropsStarted;
    float suddenDeathDropStartRemainingTime;
    readonly HashSet<Vector3Int> scheduledShadowCells = new HashSet<Vector3Int>();
    struct QueuedShadowData
    {
        public GameObject ShadowObject;
        public float DropElapsedTime;
    }

    readonly Dictionary<Vector3Int, QueuedShadowData> queuedShadowVisuals = new Dictionary<Vector3Int, QueuedShadowData>();
    readonly HashSet<Vector3Int> scheduledOrPlacedCells = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, Coroutine> damageCoroutines = new Dictionary<Vector3Int, Coroutine>();
    readonly List<Vector3Int> suddenDeathPath = new List<Vector3Int>();

    static Sprite cachedWhitePixelSprite;

    bool suddenDeathStarted;
    bool suddenDeathFinished;
    int nextDropIndex;
    int existingIndestructibleSlotsInPath;
    int emptySlotsInPath;
    StartCorner selectedStartCorner;
    bool selectedClockwise = true;
    SuddenDeathDropPattern selectedDropPattern;

    enum SuddenDeathDropPattern
    {
        Spiral = 0,
        Horizontal = 1,
        Vertical = 2,
    }

    enum StartCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomRight = 2,
        BottomLeft = 3,
    }

    void Awake()
    {
        if (stageGrid == null)
            stageGrid = GetComponentInParent<Grid>();

        if (indestructibleTilemap == null)
            indestructibleTilemap = FindIndestructibleTilemap();

        if (destructibleTilemap == null)
            destructibleTilemap = FindDestructibleTilemap();

        if (currentStageIndestructibleTile == null && indestructibleTilemap != null)
            currentStageIndestructibleTile = TryGetAnyExistingIndestructibleTile();

        ClampShadowValues();

        LogFlow(
            $"Awake: grid={(stageGrid != null ? stageGrid.name : "NULL")}, " +
            $"indestructible={(indestructibleTilemap != null ? indestructibleTilemap.name : "NULL")}, " +
            $"destructible={(destructibleTilemap != null ? destructibleTilemap.name : "NULL")}, " +
            $"tile={(currentStageIndestructibleTile != null ? currentStageIndestructibleTile.name : "NULL")}");
    }

    void OnValidate()
    {
        ClampShadowValues();
    }

    void ClampShadowValues()
    {
        fallingDuration = Mathf.Max(0.01f, fallingDuration);
        shadowLeadTime = Mathf.Max(0f, shadowLeadTime);
        shadowStartSize = Mathf.Max(0.0001f, shadowStartSize);
        shadowEndSize = Mathf.Max(shadowStartSize, shadowEndSize);
    }

    void Start()
    {
        BattleRevengeBomberBlocker.Unblock();
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        if (suddenDeathFinished)
            return;

        if (!suddenDeathStarted && !CanUseSuddenDeath())
            return;

        float remaining = GetBattleTimeRemaining();

        if (!suddenDeathStarted && remaining <= SuddenDeathTriggerTime)
            BeginSuddenDeath(remaining);

        if (!suddenDeathStarted)
            return;

        ProcessTileDrops();
    }

    bool CanUseSuddenDeath()
    {
        if (BattleModeRules.Instance == null)
            return false;

        if (!BattleModeRules.Instance.EnableSuddenDeath)
            return false;

        if (!BattleModeRules.Instance.UsesRoundTimer)
            return false;

        if (GameManager.Instance == null)
            return false;

        if (!GameManager.Instance.HasBattleTimeLimit)
            return false;

        if (indestructibleTilemap == null)
            return false;

        return true;
    }

    float GetBattleTimeRemaining()
    {
        if (GameManager.Instance == null)
            return 0f;

        return Mathf.Max(0f, GameManager.Instance.BattleTimeRemainingSeconds);
    }

    void BeginSuddenDeath(float remainingTimeAtStart)
    {
        suddenDeathStarted = true;
        suddenDeathDropsStarted = false;

        scheduledShadowCells.Clear();

        foreach (QueuedShadowData queuedShadow in queuedShadowVisuals.Values)
        {
            if (queuedShadow.ShadowObject != null)
                Destroy(queuedShadow.ShadowObject);
        }

        queuedShadowVisuals.Clear();

        suddenDeathDropStartRemainingTime = Mathf.Clamp(
            remainingTimeAtStart - delayBeforeTileDrops,
            0f,
            SuddenDeathTriggerTime);

        BattleRevengeBomberBlocker.Block();
        RemoveAllRevengeBombersFromScene();

        selectedDropPattern = randomizeDropPattern
            ? (SuddenDeathDropPattern)Random.Range(0, 3)
            : dropPattern;

        selectedStartCorner = randomizeStartCorner
            ? (StartCorner)Random.Range(0, 4)
            : StartCorner.TopLeft;

        selectedClockwise = !randomizeRotationDirection || Random.value >= 0.5f;

        BuildClosingPath();

        nextDropIndex = 0;

        float slotDuration = suddenDeathPath.Count > 0
            ? suddenDeathDropStartRemainingTime / suddenDeathPath.Count
            : 0f;

        LogFlow(
            $"BeginSuddenDeath: remainingStart={remainingTimeAtStart:0.000}, " +
            $"dropStartAt={suddenDeathDropStartRemainingTime:0.000}, " +
            $"pathCount={suddenDeathPath.Count}, " +
            $"existingSlots={existingIndestructibleSlotsInPath}, " +
            $"emptySlots={emptySlotsInPath}, " +
            $"slotDuration={slotDuration:0.000}, " +
            $"pattern={selectedDropPattern}, startCorner={selectedStartCorner}, clockwise={selectedClockwise}, randomizeDropPattern={randomizeDropPattern}");

        StartCoroutine(PlayHurryUpAndResumeMusic());
    }

    void ProcessTileDrops()
    {
        if (nextDropIndex >= suddenDeathPath.Count)
        {
            suddenDeathFinished = true;
            LogFlow("ProcessTileDrops: finalizado, todos os tiles já caíram.");
            return;
        }

        float rawRemaining = GetBattleTimeRemaining();

        if (!suddenDeathDropsStarted)
        {
            if (rawRemaining > suddenDeathDropStartRemainingTime)
                return;

            suddenDeathDropsStarted = true;
            LogFlow($"ProcessTileDrops: iniciando quedas com remaining={rawRemaining:0.000}");
        }

        float dropWindowDuration = Mathf.Max(0.0001f, suddenDeathDropStartRemainingTime);
        float elapsedSinceDropsStarted = Mathf.Clamp(
            dropWindowDuration - Mathf.Clamp(rawRemaining, 0f, dropWindowDuration),
            0f,
            dropWindowDuration);

        float dropProgress = Mathf.Clamp01(elapsedSinceDropsStarted / dropWindowDuration);

        int targetDropCount = Mathf.Clamp(
            Mathf.FloorToInt(dropProgress * suddenDeathPath.Count),
            0,
            suddenDeathPath.Count);

        if (logSuddenDeathFlow)
        {
            float slotDuration = suddenDeathPath.Count > 0
                ? dropWindowDuration / suddenDeathPath.Count
                : 0f;

            LogFlow(
                $"ProcessTileDrops: rawRemaining={rawRemaining:0.000}, " +
                $"elapsed={elapsedSinceDropsStarted:0.000}, " +
                $"targetDropCount={targetDropCount}, " +
                $"nextDropIndex={nextDropIndex}, " +
                $"slotDuration={slotDuration:0.000}");
        }

        float previewLeadDuration = Mathf.Max(0f, shadowLeadTime);
        float previewElapsed = elapsedSinceDropsStarted + previewLeadDuration;
        float previewProgress = Mathf.Clamp01(previewElapsed / dropWindowDuration);

        int targetShadowCount = Mathf.Clamp(
            Mathf.CeilToInt(previewProgress * suddenDeathPath.Count),
            0,
            suddenDeathPath.Count);

        EnsureQueuedShadows(targetShadowCount, dropWindowDuration);
        UpdateQueuedShadows(elapsedSinceDropsStarted);

        while (nextDropIndex < targetDropCount)
            DropNextTile();

        if (rawRemaining <= 0f)
        {
            EnsureQueuedShadows(suddenDeathPath.Count, dropWindowDuration);
            UpdateQueuedShadows(dropWindowDuration);

            while (nextDropIndex < suddenDeathPath.Count)
                DropNextTile();

            suddenDeathFinished = true;
            LogFlow("ProcessTileDrops: tempo zerado, restante da arena foi fechado.");
        }
    }

    void DropNextTile()
    {
        if (nextDropIndex < 0 || nextDropIndex >= suddenDeathPath.Count)
        {
            LogWarning($"DropNextTile: índice inválido {nextDropIndex} para count={suddenDeathPath.Count}");
            return;
        }

        if (indestructibleTilemap == null)
        {
            LogError("DropNextTile: indestructibleTilemap NULL.");
            return;
        }

        int slotIndex = nextDropIndex;
        Vector3Int cell = suddenDeathPath[nextDropIndex];
        nextDropIndex++;

        float slotDuration = suddenDeathPath.Count > 0
            ? suddenDeathDropStartRemainingTime / suddenDeathPath.Count
            : 0f;

        if (indestructibleTilemap.HasTile(cell))
        {
            LogFlow(
                $"DropNextTile: slot ocupado consumido sem queda visual. " +
                $"slotIndex={slotIndex + 1}/{suddenDeathPath.Count}, cell={cell}, slotDuration={slotDuration:0.000}");

            return;
        }

        if (currentStageIndestructibleTile == null)
        {
            LogError($"DropNextTile: currentStageIndestructibleTile NULL na célula {cell}");
            return;
        }

        LogFlow(
            $"DropNextTile: slot com queda visual. " +
            $"slotIndex={slotIndex + 1}/{suddenDeathPath.Count}, cell={cell}, slotDuration={slotDuration:0.000}");

        StartCoroutine(DropTileRoutine(cell));
    }

    void ClearOnlyCurrentCellIfNeeded(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
        {
            LogWarning($"ClearOnlyCurrentCellIfNeeded: indestructibleTilemap NULL para {cell}");
            return;
        }

        Vector3 worldCenter = indestructibleTilemap.GetCellCenterWorld(cell);

        if (destructibleTilemap != null)
        {
            Vector3Int destructibleCell = destructibleTilemap.WorldToCell(worldCenter);
            TileBase destructibleTile = destructibleTilemap.GetTile(destructibleCell);

            if (destructibleTile != null)
            {
                destructibleTilemap.SetTile(destructibleCell, null);
                destructibleTilemap.RefreshTile(destructibleCell);

                TilemapCollider2D destructibleCollider = destructibleTilemap.GetComponent<TilemapCollider2D>();
                if (destructibleCollider != null)
                    destructibleCollider.ProcessTilemapChanges();
            }
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, cleanupOverlapSize, 0f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Destructible destructible = hit.GetComponent<Destructible>();
            if (destructible != null)
            {
                Destroy(destructible.gameObject);
                continue;
            }

            ItemPickup item = hit.GetComponent<ItemPickup>();
            if (item != null)
            {
                Destroy(item.gameObject);
                continue;
            }
        }
    }

    IEnumerator DropTileRoutine(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
        {
            LogError($"DropTileRoutine: indestructibleTilemap NULL para {cell}");
            yield break;
        }

        Vector3 worldCenter = indestructibleTilemap.GetCellCenterWorld(cell);

        GameObject visual = null;
        GameObject shadow = null;

        float duration = Mathf.Max(0.01f, fallingDuration);

        if (queuedShadowVisuals.TryGetValue(cell, out QueuedShadowData queuedShadowData))
        {
            shadow = queuedShadowData.ShadowObject;
            queuedShadowVisuals.Remove(cell);

            LogShadow($"DropTileRoutine: reutilizando sombra antecipada para {cell}");
        }

        if (TryGetCurrentTileSprite(out Sprite tileSprite))
        {
            Vector3 spawnPosition = GetFallingVisualSpawnPosition(worldCenter);

            visual = CreateFallingVisual(tileSprite, spawnPosition);
            if (visual == null)
            {
                LogError($"DropTileRoutine: falha ao criar visual de queda para {cell}");
            }
            else
            {
                LogVisual(
                    $"DropTileRoutine: visual criado. cell={cell}, start={spawnPosition}, end={worldCenter}, duration={duration:0.000}");
            }

            if (enableShadowVisual && shadow == null)
            {
                shadow = CreateShadowVisual(worldCenter + shadowWorldOffset);
                if (shadow == null)
                {
                    LogShadow($"DropTileRoutine: falha ao criar sombra para {cell}");
                }
                else
                {
                    LogShadow(
                        $"DropTileRoutine: sombra criada no momento da queda. cell={cell}, leadTime={shadowLeadTime:0.000}, " +
                        $"startSize={shadowStartSize:0.000}, endSize={shadowEndSize:0.000}, alpha={shadowAlpha:0.000}");
                }
            }

            yield return AnimateDropVisuals(cell, visual, shadow, spawnPosition, worldCenter);
        }
        else
        {
            LogWarning($"DropTileRoutine: não foi possível obter sprite do tile para {cell}. Usando apenas delay.");
            yield return new WaitForSeconds(duration);
        }

        if (visual != null)
            Destroy(visual);

        if (shadow != null)
            Destroy(shadow);

        ClearOnlyCurrentCellIfNeeded(cell);

        if (!indestructibleTilemap.HasTile(cell))
        {
            indestructibleTilemap.SetTile(cell, currentStageIndestructibleTile);
            indestructibleTilemap.RefreshTile(cell);

            TilemapCollider2D collider = indestructibleTilemap.GetComponent<TilemapCollider2D>();
            if (collider != null)
                collider.ProcessTilemapChanges();

            LogVisual($"DropTileRoutine: tile aplicado em {cell}");
        }
        else
        {
            LogWarning($"DropTileRoutine: a célula {cell} já possuía tile no momento da aplicação.");
        }

        PlayFallingSfx();
        StartDamageOnCell(cell);
    }

    IEnumerator AnimateDropVisuals(Vector3Int cell, GameObject visual, GameObject shadow, Vector3 start, Vector3 end)
    {
        float duration = Mathf.Max(0.01f, fallingDuration);
        float elapsed = 0f;

        bool loggedShadowStart = false;
        bool loggedShadowGrowth = false;

        Transform visualTransform = visual != null ? visual.transform : null;
        Transform shadowTransform = shadow != null ? shadow.transform : null;
        SpriteRenderer shadowRenderer = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;

        float shadowInitialSize = shadowTransform != null ? shadowTransform.localScale.x : shadowStartSize;
        float shadowInitialAlpha = shadowRenderer != null ? shadowRenderer.color.a : 0f;

        if (visualTransform != null)
            visualTransform.position = start;

        if (shadowTransform != null)
            shadowTransform.position = end + shadowWorldOffset;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float clampedElapsed = Mathf.Min(elapsed, duration);

            if (shadowTransform != null && shadowRenderer != null)
            {
                float shadowT = Mathf.Clamp01(clampedElapsed / duration);

                float size = Mathf.Lerp(shadowInitialSize, shadowEndSize, shadowT);
                float alpha = Mathf.Lerp(shadowInitialAlpha, shadowAlpha, shadowT);

                shadowTransform.localScale = new Vector3(size, size, 1f);
                shadowRenderer.color = new Color(0f, 0f, 0f, alpha);

                if (!loggedShadowStart)
                {
                    loggedShadowStart = true;
                    LogShadow($"AnimateDropVisuals: sombra ativa durante a queda. cell={cell}, elapsed={clampedElapsed:0.000}");
                }

                if (!loggedShadowGrowth && shadowT >= 0.5f)
                {
                    loggedShadowGrowth = true;
                    LogShadow(
                        $"AnimateDropVisuals: sombra em crescimento. cell={cell}, shadowT={shadowT:0.000}, size={size:0.000}");
                }
            }

            if (visualTransform != null)
            {
                float visualT = Mathf.Clamp01(clampedElapsed / duration);
                visualTransform.position = Vector3.Lerp(start, end, visualT);
            }

            yield return null;
        }

        if (visualTransform != null)
            visualTransform.position = end;

        if (shadowTransform != null)
            shadowTransform.localScale = new Vector3(shadowEndSize, shadowEndSize, 1f);

        if (shadowRenderer != null)
            shadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha);

        LogShadow($"AnimateDropVisuals: sombra concluída em {cell}");
    }

    Vector3 GetFallingVisualSpawnPosition(Vector3 worldCenter)
    {
        float spawnY = worldCenter.y + Mathf.Abs(fallingHeight);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 topWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(cam.transform.position.z - worldCenter.z)));
            spawnY = Mathf.Max(spawnY, topWorld.y + topScreenSpawnOffset);
        }
        else
        {
            LogWarning("GetFallingVisualSpawnPosition: Camera.main NULL. Usando fallingHeight como fallback.");
        }

        return new Vector3(worldCenter.x, spawnY, worldCenter.z);
    }

    GameObject CreateFallingVisual(Sprite tileSprite, Vector3 spawnPosition)
    {
        GameObject visual = null;

        if (fallingBlockVisualPrefab != null)
        {
            visual = Instantiate(fallingBlockVisualPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            visual = new GameObject("SuddenDeathFallingTileVisual");
            visual.transform.position = spawnPosition;
        }

        if (visual == null)
            return null;

        Collider2D[] colliders2D = visual.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
            colliders2D[i].enabled = false;

        Rigidbody2D[] rigidbodies2D = visual.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies2D.Length; i++)
            rigidbodies2D[i].simulated = false;

        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = visual.GetComponentInChildren<SpriteRenderer>();

        if (sr == null)
            sr = visual.AddComponent<SpriteRenderer>();

        sr.sprite = tileSprite;

        ApplyVisualSorting(sr);

        return visual;
    }

    GameObject CreateShadowVisual(Vector3 position)
    {
        Sprite pixelSprite = GetWhitePixelSprite();
        if (pixelSprite == null)
        {
            LogError("CreateShadowVisual: não foi possível criar/obter o sprite branco 1x1.");
            return null;
        }

        GameObject shadow = new GameObject("SuddenDeathFallingTileShadow");
        shadow.transform.position = position;
        shadow.transform.localScale = new Vector3(shadowStartSize, shadowStartSize, 1f);

        SpriteRenderer sr = shadow.AddComponent<SpriteRenderer>();
        sr.sprite = pixelSprite;
        sr.color = new Color(0f, 0f, 0f, 0f);

        ApplyShadowSorting(sr);

        LogShadow(
            $"CreateShadowVisual: criada em pos={shadow.transform.position}, " +
            $"scale={shadow.transform.localScale}, layer={sr.sortingLayerID}, order={sr.sortingOrder}");

        return shadow;
    }

    Sprite GetWhitePixelSprite()
    {
        if (cachedWhitePixelSprite != null)
            return cachedWhitePixelSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        tex.name = "SuddenDeathShadowPixel";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        cachedWhitePixelSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        return cachedWhitePixelSprite;
    }

    void ApplyVisualSorting(SpriteRenderer sr)
    {
        if (sr == null || indestructibleTilemap == null)
            return;

        TilemapRenderer tilemapRenderer = indestructibleTilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            return;

        sr.sortingLayerID = tilemapRenderer.sortingLayerID;
        sr.sortingOrder = tilemapRenderer.sortingOrder + visualSortingOrderOffset;
    }

    void ApplyShadowSorting(SpriteRenderer sr)
    {
        if (sr == null || indestructibleTilemap == null)
            return;

        TilemapRenderer tilemapRenderer = indestructibleTilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
        {
            LogShadow("ApplyShadowSorting: TilemapRenderer NULL.");
            return;
        }

        sr.sortingLayerID = tilemapRenderer.sortingLayerID;

        int visualOrder = tilemapRenderer.sortingOrder + visualSortingOrderOffset;

        if (shadowSortingOrderOffset > 0)
            sr.sortingOrder = visualOrder + shadowSortingOrderOffset;
        else
            sr.sortingOrder = visualOrder - 1;

        LogShadow(
            $"ApplyShadowSorting: tilemapLayer={tilemapRenderer.sortingLayerID}, " +
            $"tilemapOrder={tilemapRenderer.sortingOrder}, visualOrder={visualOrder}, " +
            $"shadowOffset={shadowSortingOrderOffset}, shadowOrder={sr.sortingOrder}");
    }

    bool TryGetCurrentTileSprite(out Sprite sprite)
    {
        sprite = null;

        if (currentStageIndestructibleTile is Tile tile && tile.sprite != null)
        {
            sprite = tile.sprite;
            return true;
        }

        if (currentStageIndestructibleTile != null && indestructibleTilemap != null)
        {
            BoundsInt bounds = indestructibleTilemap.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                TileBase tileBase = indestructibleTilemap.GetTile(pos);
                if (tileBase is Tile mapTile && mapTile.sprite != null)
                {
                    sprite = mapTile.sprite;
                    return true;
                }
            }
        }

        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (suddenDeathPath == null || indestructibleTilemap == null)
            return;

        for (int i = 0; i < suddenDeathPath.Count; i++)
        {
            Vector3 world = indestructibleTilemap.GetCellCenterWorld(suddenDeathPath[i]);

            float t = suddenDeathPath.Count <= 1
                ? 0f
                : (float)i / (suddenDeathPath.Count - 1);

            Gizmos.color = Color.Lerp(Color.green, Color.red, t);
            Gizmos.DrawWireCube(world, Vector3.one * 0.8f);
        }
    }

    [ContextMenu("TESTAR QUEDA TILE AGORA")]
    void TestDropSingleTileNow()
    {
        if (indestructibleTilemap == null)
        {
            LogError("TestDropSingleTileNow: indestructibleTilemap NULL");
            return;
        }

        Vector3Int cell = new Vector3Int(-7, 4, 0);
        StartCoroutine(DropTileRoutine(cell));
    }

    void StartDamageOnCell(Vector3Int cell)
    {
        if (damageCoroutines.ContainsKey(cell))
            return;

        Coroutine routine = StartCoroutine(DamagePlayersOnCellRoutine(cell));
        damageCoroutines[cell] = routine;
    }

    IEnumerator DamagePlayersOnCellRoutine(Vector3Int cell)
    {
        WaitForSeconds wait = new WaitForSeconds(damageTickInterval);

        while (true)
        {
            DamagePlayersStandingOnCell(cell);
            yield return wait;
        }
    }

    void DamagePlayersStandingOnCell(Vector3Int cell)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            GameObject player = players[i];
            if (player == null)
                continue;

            Vector3Int playerCell = indestructibleTilemap.WorldToCell(player.transform.position);
            if (playerCell != cell)
                continue;

            if (logDamageFlow)
                Log($"DamagePlayersStandingOnCell: {player.name} recebeu {damagePerTick} em {cell}");

            ApplyDamage(player, damagePerTick);
        }
    }

    void ApplyDamage(GameObject player, int amount)
    {
        if (player == null || amount <= 0)
            return;

        MovementController movement = player.GetComponent<MovementController>();
        CharacterHealth health = player.GetComponent<CharacterHealth>();
        PlayerMountCompanion companion = player.GetComponent<PlayerMountCompanion>();

        if (movement == null)
        {
            if (health != null && health.life > 0)
                health.TakeDamage(amount, false);

            return;
        }

        if (movement.isDead || movement.IsEndingStage || movement.InputLocked)
            return;

        if (health != null && health.IsInvulnerable)
            return;

        bool fromExplosion = false;

        if (movement.CompareTag("Player") && movement.IsRidingPlaying())
        {
            if (companion == null)
                player.TryGetComponent(out companion);

            if (companion != null)
            {
                companion.HandleDamageWhileMounting(amount);
                return;
            }
        }

        if (movement.CompareTag("Player") && movement.IsMounted)
        {
            CharacterHealth mountedHealth = GetMountedLouieHealth(player);
            if (mountedHealth != null && mountedHealth.IsInvulnerable)
                return;

            if (companion == null)
                player.TryGetComponent(out companion);

            if (companion != null)
            {
                companion.OnMountedLouieHit(amount, fromExplosion);
                return;
            }

            if (health != null)
            {
                health.TakeDamage(amount);
                return;
            }

            movement.Kill();
            return;
        }

        if (health != null)
        {
            health.TakeDamage(amount, fromExplosion);
            return;
        }

        movement.Kill();
    }

    CharacterHealth GetMountedLouieHealth(GameObject player)
    {
        if (player == null)
            return null;

        MountMovementController louieMove = player.GetComponentInChildren<MountMovementController>(true);
        if (louieMove == null)
            return null;

        return louieMove.GetComponent<CharacterHealth>();
    }

    IEnumerator PlayHurryUpAndResumeMusic()
    {
        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (hurryUpSfx != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayOneShotSfx(hurryUpSfx);

        float wait = hurryUpSfx != null ? hurryUpSfx.length : 0f;
        wait += resumeMusicDelayAfterHurryUp;

        yield return new WaitForSeconds(wait);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.PlayDefaultMusicWithPitch(fastMusicPitch, true);
    }

    void PlayFallingSfx()
    {
        if (fallingTileSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlayOneShotSfx(fallingTileSfx);
    }

    void RemoveAllRevengeBombersFromScene()
    {
        BattleRevengeController[] revengeCarts = FindObjectsByType<BattleRevengeController>(FindObjectsSortMode.None);
        for (int i = 0; i < revengeCarts.Length; i++)
        {
            if (revengeCarts[i] != null)
                Destroy(revengeCarts[i].gameObject);
        }

        BattleRevengeSystem[] revengeSystems = FindObjectsByType<BattleRevengeSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < revengeSystems.Length; i++)
        {
            if (revengeSystems[i] != null)
                revengeSystems[i].enabled = false;
        }
    }

    void BuildClosingPath()
    {
        suddenDeathPath.Clear();
        scheduledOrPlacedCells.Clear();

        existingIndestructibleSlotsInPath = 0;
        emptySlotsInPath = 0;

        BoundsInt bounds = GetPlayableBounds();
        int left = bounds.xMin;
        int right = bounds.xMax - 1;
        int bottom = bounds.yMin;
        int top = bounds.yMax - 1;

        switch (selectedDropPattern)
        {
            case SuddenDeathDropPattern.Spiral:
                BuildSpiralPath(left, right, bottom, top);
                break;

            case SuddenDeathDropPattern.Horizontal:
                BuildHorizontalPath(left, right, bottom, top);
                break;

            case SuddenDeathDropPattern.Vertical:
                BuildVerticalPath(left, right, bottom, top);
                break;
        }

        if (logSuddenDeathFlow)
        {
            if (suddenDeathPath.Count > 0)
            {
                float slotDuration = suddenDeathPath.Count > 0
                    ? suddenDeathDropStartRemainingTime / suddenDeathPath.Count
                    : 0f;

                LogFlow(
                    $"BuildClosingPath: totalSlots={suddenDeathPath.Count}, " +
                    $"existingSlots={existingIndestructibleSlotsInPath}, " +
                    $"emptySlots={emptySlotsInPath}, " +
                    $"slotDuration={slotDuration:0.000}, " +
                    $"pattern={selectedDropPattern}, startCorner={selectedStartCorner}, clockwise={selectedClockwise}, " +
                    $"first={suddenDeathPath[0]}, last={suddenDeathPath[suddenDeathPath.Count - 1]}");
            }
            else
            {
                LogWarning("BuildClosingPath: nenhum tile foi adicionado ao caminho.");
            }
        }
    }

    void AddCellIfValid(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
            return;

        if (!scheduledOrPlacedCells.Add(cell))
            return;

        suddenDeathPath.Add(cell);

        if (indestructibleTilemap.HasTile(cell))
            existingIndestructibleSlotsInPath++;
        else
            emptySlotsInPath++;
    }

    BoundsInt GetPlayableBounds()
    {
        if (!usePlayableAreaFromBounds)
        {
            int xMin = Mathf.Min(bottomLeftCell.x, topRightCell.x);
            int yMin = Mathf.Min(bottomLeftCell.y, topRightCell.y);
            int xMax = Mathf.Max(bottomLeftCell.x, topRightCell.x);
            int yMax = Mathf.Max(bottomLeftCell.y, topRightCell.y);

            int width = (xMax - xMin) + 1;
            int height = (yMax - yMin) + 1;

            return new BoundsInt(xMin, yMin, 0, width, height, 1);
        }

        if (indestructibleTilemap == null)
        {
            int xMin = Mathf.Min(bottomLeftCell.x, topRightCell.x);
            int yMin = Mathf.Min(bottomLeftCell.y, topRightCell.y);
            int xMax = Mathf.Max(bottomLeftCell.x, topRightCell.x);
            int yMax = Mathf.Max(bottomLeftCell.y, topRightCell.y);

            int width = (xMax - xMin) + 1;
            int height = (yMax - yMin) + 1;

            return new BoundsInt(xMin, yMin, 0, width, height, 1);
        }

        BoundsInt b = indestructibleTilemap.cellBounds;

        int autoXMin = b.xMin + 1;
        int autoXMax = b.xMax - 1;
        int autoYMin = b.yMin + 1;
        int autoYMax = b.yMax - 1;

        return new BoundsInt(autoXMin, autoYMin, 0, autoXMax - autoXMin, autoYMax - autoYMin, 1);
    }

    Tilemap FindIndestructibleTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            if (maps[i].CompareTag("Indestructibles"))
                return maps[i];

            if (maps[i].name == "Indestructibles")
                return maps[i];
        }

        return null;
    }

    Tilemap FindDestructibleTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            if (maps[i].CompareTag("Destructibles"))
                return maps[i];
        }

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            if (maps[i].name == "Destructibles")
                return maps[i];
        }

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            string lowerName = maps[i].name.ToLowerInvariant();
            if (lowerName.Contains("destruct") && !lowerName.Contains("indestruct"))
                return maps[i];
        }

        return null;
    }

    TileBase TryGetAnyExistingIndestructibleTile()
    {
        if (indestructibleTilemap == null)
            return null;

        BoundsInt bounds = indestructibleTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = indestructibleTilemap.GetTile(pos);
            if (tile != null)
                return tile;
        }

        return null;
    }

    void EnsureQueuedShadows(int targetShadowCount, float dropWindowDuration)
    {
        if (!enableShadowVisual || indestructibleTilemap == null)
            return;

        targetShadowCount = Mathf.Clamp(targetShadowCount, 0, suddenDeathPath.Count);

        for (int i = 0; i < targetShadowCount; i++)
        {
            Vector3Int cell = suddenDeathPath[i];

            if (!scheduledShadowCells.Add(cell))
                continue;

            if (indestructibleTilemap.HasTile(cell))
                continue;

            Vector3 worldCenter = indestructibleTilemap.GetCellCenterWorld(cell);
            GameObject shadow = CreateShadowVisual(worldCenter + shadowWorldOffset);

            if (shadow == null)
            {
                LogShadow($"EnsureQueuedShadows: falha ao criar sombra antecipada para {cell}");
                continue;
            }

            float dropElapsedTime = ((i + 1f) / Mathf.Max(1, suddenDeathPath.Count)) * dropWindowDuration;

            queuedShadowVisuals[cell] = new QueuedShadowData
            {
                ShadowObject = shadow,
                DropElapsedTime = dropElapsedTime
            };

            LogShadow(
                $"EnsureQueuedShadows: sombra antecipada criada. cell={cell}, " +
                $"queued={queuedShadowVisuals.Count}, targetShadowCount={targetShadowCount}, dropElapsed={dropElapsedTime:0.000}");
        }
    }

    void UpdateQueuedShadows(float currentElapsedSinceDropsStarted)
    {
        if (!enableShadowVisual || queuedShadowVisuals.Count == 0)
            return;

        List<Vector3Int> keys = new List<Vector3Int>(queuedShadowVisuals.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            Vector3Int cell = keys[i];

            if (!queuedShadowVisuals.TryGetValue(cell, out QueuedShadowData data))
                continue;

            if (data.ShadowObject == null)
                continue;

            float previewStartTime = Mathf.Max(0f, data.DropElapsedTime - shadowLeadTime);
            float previewDuration = Mathf.Max(0.0001f, data.DropElapsedTime - previewStartTime);

            float t = Mathf.Clamp01((currentElapsedSinceDropsStarted - previewStartTime) / previewDuration);

            Transform shadowTransform = data.ShadowObject.transform;
            SpriteRenderer shadowRenderer = data.ShadowObject.GetComponent<SpriteRenderer>();

            if (shadowTransform != null)
            {
                float size = Mathf.Lerp(shadowStartSize, shadowEndSize, t);
                shadowTransform.localScale = new Vector3(size, size, 1f);
            }

            if (shadowRenderer != null)
            {
                float alpha = Mathf.Lerp(0f, shadowAlpha, t);
                shadowRenderer.color = new Color(0f, 0f, 0f, alpha);
            }
        }
    }

    void BuildSpiralPath(int left, int right, int bottom, int top)
    {
        while (left <= right && bottom <= top)
        {
            List<Vector3Int> ring = new List<Vector3Int>();
            AddRingCells(ring, left, right, bottom, top);

            ring = ReorderRingFromCorner(ring, left, right, bottom, top, selectedStartCorner, selectedClockwise);

            for (int i = 0; i < ring.Count; i++)
                AddCellIfValid(ring[i]);

            left++;
            right--;
            bottom++;
            top--;
        }
    }

    void BuildHorizontalPath(int left, int right, int bottom, int top)
    {
        bool startFromTop = selectedStartCorner == StartCorner.TopLeft || selectedStartCorner == StartCorner.TopRight;
        bool startLeftToRight = selectedStartCorner == StartCorner.TopLeft || selectedStartCorner == StartCorner.BottomLeft;

        int layer = 0;

        while (bottom <= top)
        {
            bool takePrimarySideFirst = true;

            if (!selectedClockwise)
                takePrimarySideFirst = false;

            int firstRow;
            int secondRow;

            if (startFromTop)
            {
                firstRow = takePrimarySideFirst ? top : bottom;
                secondRow = takePrimarySideFirst ? bottom : top;
            }
            else
            {
                firstRow = takePrimarySideFirst ? bottom : top;
                secondRow = takePrimarySideFirst ? top : bottom;
            }

            bool firstRowLeftToRight = takePrimarySideFirst ? startLeftToRight : !startLeftToRight;
            bool secondRowLeftToRight = !firstRowLeftToRight;

            AddHorizontalRow(firstRow, left, right, firstRowLeftToRight);

            if (firstRow != secondRow)
                AddHorizontalRow(secondRow, left, right, secondRowLeftToRight);

            top--;
            bottom++;
            layer++;
        }
    }

    void BuildVerticalPath(int left, int right, int bottom, int top)
    {
        bool startFromLeft = selectedStartCorner == StartCorner.TopLeft || selectedStartCorner == StartCorner.BottomLeft;
        bool startTopToBottom = selectedStartCorner == StartCorner.TopLeft || selectedStartCorner == StartCorner.TopRight;

        int layer = 0;

        while (left <= right)
        {
            bool takePrimarySideFirst = true;

            if (!selectedClockwise)
                takePrimarySideFirst = false;

            int firstCol;
            int secondCol;

            if (startFromLeft)
            {
                firstCol = takePrimarySideFirst ? left : right;
                secondCol = takePrimarySideFirst ? right : left;
            }
            else
            {
                firstCol = takePrimarySideFirst ? right : left;
                secondCol = takePrimarySideFirst ? left : right;
            }

            bool firstColTopToBottom = takePrimarySideFirst ? startTopToBottom : !startTopToBottom;
            bool secondColTopToBottom = !firstColTopToBottom;

            AddVerticalColumn(firstCol, bottom, top, firstColTopToBottom);

            if (firstCol != secondCol)
                AddVerticalColumn(secondCol, bottom, top, secondColTopToBottom);

            left++;
            right--;
            layer++;
        }
    }

    void AddRingCells(List<Vector3Int> ring, int left, int right, int bottom, int top)
    {
        for (int x = left; x <= right; x++)
            ring.Add(new Vector3Int(x, top, 0));

        for (int y = top - 1; y >= bottom; y--)
            ring.Add(new Vector3Int(right, y, 0));

        if (top > bottom)
        {
            for (int x = right - 1; x >= left; x--)
                ring.Add(new Vector3Int(x, bottom, 0));
        }

        if (left < right)
        {
            for (int y = bottom + 1; y < top; y++)
                ring.Add(new Vector3Int(left, y, 0));
        }
    }

    List<Vector3Int> ReorderRingFromCorner(
        List<Vector3Int> ring,
        int left,
        int right,
        int bottom,
        int top,
        StartCorner startCorner,
        bool clockwise)
    {
        if (ring == null || ring.Count == 0)
            return ring;

        Vector3Int startCell = startCorner switch
        {
            StartCorner.TopLeft => new Vector3Int(left, top, 0),
            StartCorner.TopRight => new Vector3Int(right, top, 0),
            StartCorner.BottomRight => new Vector3Int(right, bottom, 0),
            _ => new Vector3Int(left, bottom, 0),
        };

        int startIndex = ring.FindIndex(c => c == startCell);
        if (startIndex < 0)
            startIndex = 0;

        List<Vector3Int> ordered = new List<Vector3Int>(ring.Count);

        if (clockwise)
        {
            for (int i = 0; i < ring.Count; i++)
                ordered.Add(ring[(startIndex + i) % ring.Count]);
        }
        else
        {
            for (int i = 0; i < ring.Count; i++)
            {
                int index = startIndex - i;
                if (index < 0)
                    index += ring.Count;

                ordered.Add(ring[index]);
            }
        }

        return ordered;
    }

    void AddHorizontalRow(int y, int left, int right, bool leftToRight)
    {
        if (leftToRight)
        {
            for (int x = left; x <= right; x++)
                AddCellIfValid(new Vector3Int(x, y, 0));
        }
        else
        {
            for (int x = right; x >= left; x--)
                AddCellIfValid(new Vector3Int(x, y, 0));
        }
    }

    void AddVerticalColumn(int x, int bottom, int top, bool topToBottom)
    {
        if (topToBottom)
        {
            for (int y = top; y >= bottom; y--)
                AddCellIfValid(new Vector3Int(x, y, 0));
        }
        else
        {
            for (int y = bottom; y <= top; y++)
                AddCellIfValid(new Vector3Int(x, y, 0));
        }
    }

    void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[BattleSuddenDeathController] {message}", this);
    }

    void LogFlow(string message)
    {
        if (!enableDebugLogs || !logSuddenDeathFlow)
            return;

        Debug.Log($"[BattleSuddenDeathController] {message}", this);
    }

    void LogVisual(string message)
    {
        if (!enableDebugLogs || !logVisualFlow)
            return;

        Debug.Log($"[BattleSuddenDeathController][Visual] {message}", this);
    }

    void LogShadow(string message)
    {
        if (!enableDebugLogs || !logShadowFlow)
            return;

        Debug.Log($"[BattleSuddenDeathController][Shadow] {message}", this);
    }

    void LogWarning(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.LogWarning($"[BattleSuddenDeathController] {message}", this);
    }

    void LogError(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.LogError($"[BattleSuddenDeathController] {message}", this);
    }
}