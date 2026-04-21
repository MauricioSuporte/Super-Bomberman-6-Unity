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

    [Header("Falling Block Visual")]
    [SerializeField] private GameObject fallingBlockVisualPrefab;
    [SerializeField] private TileBase currentStageIndestructibleTile;
    [SerializeField] private float fallingHeight = 6f;
    [SerializeField] private float fallingDuration = 0.1f;
    [SerializeField] private float topScreenSpawnOffset = 0.5f;
    [SerializeField] private int visualSortingOrderOffset = 1;

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
    [SerializeField] private bool logDamageFlow = false;

    bool suddenDeathDropsStarted;
    float suddenDeathDropStartRemainingTime;

    readonly HashSet<Vector3Int> scheduledOrPlacedCells = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, Coroutine> damageCoroutines = new Dictionary<Vector3Int, Coroutine>();
    readonly List<Vector3Int> suddenDeathPath = new List<Vector3Int>();

    bool suddenDeathStarted;
    bool suddenDeathFinished;
    int nextDropIndex;

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

        LogFlow(
            $"Awake: grid={(stageGrid != null ? stageGrid.name : "NULL")}, " +
            $"indestructible={(indestructibleTilemap != null ? indestructibleTilemap.name : "NULL")}, " +
            $"destructible={(destructibleTilemap != null ? destructibleTilemap.name : "NULL")}, " +
            $"tile={(currentStageIndestructibleTile != null ? currentStageIndestructibleTile.name : "NULL")}");
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
        suddenDeathDropStartRemainingTime = Mathf.Clamp(
            remainingTimeAtStart - delayBeforeTileDrops,
            0f,
            SuddenDeathTriggerTime);

        BattleRevengeBomberBlocker.Block();
        RemoveAllRevengeBombersFromScene();
        BuildClosingPath();

        nextDropIndex = 0;

        LogFlow(
            $"BeginSuddenDeath: remainingStart={remainingTimeAtStart:0.000}, " +
            $"dropStartAt={suddenDeathDropStartRemainingTime:0.000}, " +
            $"pathCount={suddenDeathPath.Count}");

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

        float dropDuration = Mathf.Max(0.0001f, suddenDeathDropStartRemainingTime);
        float elapsedSinceDropsStarted = Mathf.Clamp(
            dropDuration - Mathf.Clamp(rawRemaining, 0f, dropDuration),
            0f,
            dropDuration);

        float progress = Mathf.Clamp01(elapsedSinceDropsStarted / dropDuration);

        int targetDropCount = Mathf.Clamp(
            Mathf.FloorToInt(progress * suddenDeathPath.Count),
            0,
            suddenDeathPath.Count);

        while (nextDropIndex < targetDropCount)
            DropNextTile();

        if (rawRemaining <= 0f)
        {
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

        Vector3Int cell = suddenDeathPath[nextDropIndex];
        nextDropIndex++;

        if (indestructibleTilemap.HasTile(cell))
        {
            LogVisual($"DropNextTile: célula {cell} já tinha tile. Pulando.");
            return;
        }

        if (currentStageIndestructibleTile == null)
        {
            LogError($"DropNextTile: currentStageIndestructibleTile NULL na célula {cell}");
            return;
        }

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
                    $"DropTileRoutine: queda visual criada. cell={cell}, start={spawnPosition}, end={worldCenter}, duration={fallingDuration:0.000}");

                yield return AnimateFallingVisual(visual.transform, spawnPosition, worldCenter);

                if (visual != null)
                    Destroy(visual);
            }
        }
        else
        {
            LogWarning($"DropTileRoutine: não foi possível obter sprite do tile para {cell}. Usando apenas delay.");
            yield return new WaitForSeconds(Mathf.Max(0.01f, fallingDuration));
        }

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

    IEnumerator AnimateFallingVisual(Transform visualTransform, Vector3 start, Vector3 end)
    {
        if (visualTransform == null)
            yield break;

        float duration = Mathf.Max(0.01f, fallingDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (visualTransform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            visualTransform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        if (visualTransform != null)
            visualTransform.position = end;
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

    void ApplyVisualSorting(SpriteRenderer sr)
    {
        if (sr == null)
            return;

        if (indestructibleTilemap == null)
            return;

        TilemapRenderer tilemapRenderer = indestructibleTilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            return;

        sr.sortingLayerID = tilemapRenderer.sortingLayerID;
        sr.sortingOrder = tilemapRenderer.sortingOrder + visualSortingOrderOffset;
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

        BoundsInt bounds = GetPlayableBounds();
        int left = bounds.xMin;
        int right = bounds.xMax - 1;
        int bottom = bounds.yMin;
        int top = bounds.yMax - 1;

        while (left <= right && bottom <= top)
        {
            for (int x = left; x <= right; x++)
                AddCellIfValid(new Vector3Int(x, top, 0));

            for (int y = top - 1; y >= bottom; y--)
                AddCellIfValid(new Vector3Int(right, y, 0));

            if (top > bottom)
            {
                for (int x = right - 1; x >= left; x--)
                    AddCellIfValid(new Vector3Int(x, bottom, 0));
            }

            if (left < right)
            {
                for (int y = bottom + 1; y < top; y++)
                    AddCellIfValid(new Vector3Int(left, y, 0));
            }

            left++;
            right--;
            bottom++;
            top--;
        }

        if (logSuddenDeathFlow)
        {
            if (suddenDeathPath.Count > 0)
            {
                LogFlow(
                    $"BuildClosingPath: total={suddenDeathPath.Count}, " +
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

        if (indestructibleTilemap.HasTile(cell))
            return;

        if (!scheduledOrPlacedCells.Add(cell))
            return;

        suddenDeathPath.Add(cell);
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