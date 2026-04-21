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
    [SerializeField] private float fallingDuration = 0.15f;

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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logEveryDrop = true;
    [SerializeField] private bool logDamageTicks;
    [SerializeField] private bool logHeartbeat;

    [SerializeField] private float delayBeforeTileDrops = 2f;

    bool suddenDeathDropsStarted;
    float suddenDeathDropStartRemainingTime;

    float nextHeartbeatLogAt;

    readonly HashSet<Vector3Int> scheduledOrPlacedCells = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, Coroutine> damageCoroutines = new Dictionary<Vector3Int, Coroutine>();
    readonly List<Vector3Int> suddenDeathPath = new List<Vector3Int>();

    bool suddenDeathStarted;
    bool suddenDeathFinished;
    int nextDropIndex;

    void Awake()
    {
        Log("Awake iniciado.");

        if (stageGrid == null)
            stageGrid = GetComponentInParent<Grid>();

        if (indestructibleTilemap == null)
            indestructibleTilemap = FindIndestructibleTilemap();

        if (destructibleTilemap == null)
            destructibleTilemap = FindDestructibleTilemap();

        if (currentStageIndestructibleTile == null && indestructibleTilemap != null)
            currentStageIndestructibleTile = TryGetAnyExistingIndestructibleTile();

        Log(
            $"Awake finalizado. stageGrid={(stageGrid != null ? stageGrid.name : "NULL")}, " +
            $"indestructibleTilemap={(indestructibleTilemap != null ? indestructibleTilemap.name : "NULL")}, " +
            $"destructibleTilemap={(destructibleTilemap != null ? destructibleTilemap.name : "NULL")}, " +
            $"currentStageIndestructibleTile={(currentStageIndestructibleTile != null ? currentStageIndestructibleTile.name : "NULL")}");
    }

    void Start()
    {
        BattleRevengeBomberBlocker.Unblock();
        Log("Start: Revenge bomber desbloqueado para início de round.");
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

        if (logHeartbeat && Time.unscaledTime >= nextHeartbeatLogAt)
        {
            nextHeartbeatLogAt = Time.unscaledTime + 1f;
            Log(
                $"Heartbeat: suddenDeathStarted={suddenDeathStarted}, suddenDeathFinished={suddenDeathFinished}, " +
                $"remaining={remaining:0.000}, nextDropIndex={nextDropIndex}, pathCount={suddenDeathPath.Count}, " +
                $"componentEnabled={enabled}, gameObjectActive={gameObject.activeInHierarchy}");
        }

        ProcessTileDrops();
    }

    bool CanUseSuddenDeath()
    {
        if (BattleModeRules.Instance == null)
        {
            Log("CanUseSuddenDeath: BattleModeRules.Instance == null");
            return false;
        }

        if (!BattleModeRules.Instance.EnableSuddenDeath)
        {
            Log("CanUseSuddenDeath: Sudden Death desabilitado nas rules.");
            return false;
        }

        if (!BattleModeRules.Instance.UsesRoundTimer)
        {
            Log("CanUseSuddenDeath: round timer infinito/desabilitado.");
            return false;
        }

        if (GameManager.Instance == null)
        {
            Log("CanUseSuddenDeath: GameManager.Instance == null");
            return false;
        }

        if (!GameManager.Instance.HasBattleTimeLimit)
        {
            Log("CanUseSuddenDeath: battle sem limite de tempo.");
            return false;
        }

        if (indestructibleTilemap == null)
        {
            Log("CanUseSuddenDeath: indestructibleTilemap == null");
            return false;
        }

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

        Log($"BeginSuddenDeath: iniciado com remainingTimeAtStart={remainingTimeAtStart:0.000}");
        Log($"BeginSuddenDeath: delayBeforeTileDrops={delayBeforeTileDrops:0.000}, suddenDeathDropStartRemainingTime={suddenDeathDropStartRemainingTime:0.000}");

        BattleRevengeBomberBlocker.Block();
        Log("BeginSuddenDeath: novos revenge bombers bloqueados.");

        RemoveAllRevengeBombersFromScene();
        BuildClosingPath();

        nextDropIndex = 0;

        Log($"BeginSuddenDeath: caminho montado com {suddenDeathPath.Count} células.");

        StartCoroutine(PlayHurryUpAndResumeMusic());
    }

    void ProcessTileDrops()
    {
        if (nextDropIndex >= suddenDeathPath.Count)
        {
            if (!suddenDeathFinished)
                Log("ProcessTileDrops: sudden death finalizado porque todos os tiles já caíram.");

            suddenDeathFinished = true;
            return;
        }

        float rawRemaining = GetBattleTimeRemaining();

        if (!suddenDeathDropsStarted)
        {
            if (rawRemaining > suddenDeathDropStartRemainingTime)
            {
                Log(
                    $"ProcessTileDrops: aguardando início das quedas. " +
                    $"rawRemaining={rawRemaining:0.000}, startAt={suddenDeathDropStartRemainingTime:0.000}");
                return;
            }

            suddenDeathDropsStarted = true;
            Log(
                $"ProcessTileDrops: quedas iniciadas. " +
                $"rawRemaining={rawRemaining:0.000}, startAt={suddenDeathDropStartRemainingTime:0.000}");
        }

        float dropDuration = Mathf.Max(0.0001f, suddenDeathDropStartRemainingTime);
        float elapsedSinceDropsStarted = Mathf.Clamp(dropDuration - Mathf.Clamp(rawRemaining, 0f, dropDuration), 0f, dropDuration);
        float progress = Mathf.Clamp01(elapsedSinceDropsStarted / dropDuration);

        int targetDropCount = Mathf.Clamp(
            Mathf.FloorToInt(progress * suddenDeathPath.Count),
            0,
            suddenDeathPath.Count);

        Log(
            $"ProcessTileDrops: rawRemaining={rawRemaining:0.000}, dropDuration={dropDuration:0.000}, " +
            $"elapsedSinceDropsStarted={elapsedSinceDropsStarted:0.000}, progress={progress:0.000000}, " +
            $"targetDropCount={targetDropCount}, nextDropIndex={nextDropIndex}, total={suddenDeathPath.Count}");

        while (nextDropIndex < targetDropCount)
        {
            Log($"ProcessTileDrops: nextDropIndex {nextDropIndex} < targetDropCount {targetDropCount}. Chamando DropNextTile().");
            DropNextTile();
        }

        if (rawRemaining <= 0f)
        {
            Log("ProcessTileDrops: tempo zerou. Forçando queda do restante.");

            while (nextDropIndex < suddenDeathPath.Count)
                DropNextTile();

            suddenDeathFinished = true;
            Log("ProcessTileDrops: sudden death finalizado por tempo <= 0.");
        }
    }

    void DropNextTile()
    {
        if (nextDropIndex < 0 || nextDropIndex >= suddenDeathPath.Count)
        {
            Log($"DropNextTile: índice fora do intervalo. nextDropIndex={nextDropIndex}, count={suddenDeathPath.Count}");
            return;
        }

        if (indestructibleTilemap == null)
        {
            Log("DropNextTile: indestructibleTilemap NULL.");
            return;
        }

        Vector3Int cell = suddenDeathPath[nextDropIndex];
        Vector3 cellCenter = indestructibleTilemap.GetCellCenterWorld(cell);
        Vector3Int roundTripCell = indestructibleTilemap.WorldToCell(cellCenter);

        Log(
            $"DropNextTile: index={nextDropIndex}, cell={cell}, cellCenter={cellCenter}, " +
            $"roundTripCell={roundTripCell}, hasTileBefore={indestructibleTilemap.HasTile(cell)}");

        nextDropIndex++;

        if (indestructibleTilemap.HasTile(cell))
        {
            Log($"DropNextTile: célula {cell} já possui tile. Pulando.");
            return;
        }

        if (currentStageIndestructibleTile == null)
        {
            LogError($"DropNextTile: currentStageIndestructibleTile está NULL. Não é possível aplicar tile na célula {cell}.");
            return;
        }

        StartCoroutine(DropTileRoutine(cell));
    }

    void LogTilemapRenderingState(Vector3Int cell)
    {
        TilemapRenderer renderer = indestructibleTilemap != null
            ? indestructibleTilemap.GetComponent<TilemapRenderer>()
            : null;

        TilemapCollider2D collider = indestructibleTilemap != null
            ? indestructibleTilemap.GetComponent<TilemapCollider2D>()
            : null;

        TileBase tile = indestructibleTilemap != null
            ? indestructibleTilemap.GetTile(cell)
            : null;

        Log(
            $"TilemapRenderState: cell={cell}, tile={(tile != null ? tile.name : "NULL")}, " +
            $"tilemapColor={(indestructibleTilemap != null ? indestructibleTilemap.color.ToString() : "NULL")}, " +
            $"rendererEnabled={(renderer != null ? renderer.enabled.ToString() : "NULL")}, " +
            $"rendererLayer={(renderer != null ? renderer.sortingLayerName : "NULL")}, " +
            $"rendererOrder={(renderer != null ? renderer.sortingOrder.ToString() : "NULL")}, " +
            $"colliderEnabled={(collider != null ? collider.enabled.ToString() : "NULL")}");
    }

    void ClearOnlyCurrentCellIfNeeded(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
        {
            LogWarning($"ClearOnlyCurrentCellIfNeeded: indestructibleTilemap NULL para cell={cell}");
            return;
        }

        Vector3 worldCenter = indestructibleTilemap.GetCellCenterWorld(cell);

        Log(
            $"ClearOnlyCurrentCellIfNeeded: início. cell={cell}, worldCenter={worldCenter}, " +
            $"destructibleTilemap={(destructibleTilemap != null ? destructibleTilemap.name : "NULL")}");

        if (destructibleTilemap != null)
        {
            Vector3Int destructibleCell = destructibleTilemap.WorldToCell(worldCenter);
            TileBase destructibleTile = destructibleTilemap.GetTile(destructibleCell);

            Log(
                $"ClearOnlyCurrentCellIfNeeded: checando tile destrutível. " +
                $"destructibleCell={destructibleCell}, tile={(destructibleTile != null ? destructibleTile.name : "NULL")}");

            if (destructibleTile != null)
            {
                destructibleTilemap.SetTile(destructibleCell, null);
                destructibleTilemap.RefreshTile(destructibleCell);

                TilemapCollider2D destructibleCollider = destructibleTilemap.GetComponent<TilemapCollider2D>();
                if (destructibleCollider != null)
                    destructibleCollider.ProcessTilemapChanges();

                TileBase afterRemoveTile = destructibleTilemap.GetTile(destructibleCell);

                Log(
                    $"ClearOnlyCurrentCellIfNeeded: tile destrutível removido em {destructibleCell}. " +
                    $"tileDepois={(afterRemoveTile != null ? afterRemoveTile.name : "NULL")}");
            }
        }
        else
        {
            LogWarning($"ClearOnlyCurrentCellIfNeeded: destructibleTilemap NULL para cell={cell}");
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, cleanupOverlapSize, 0f);
        Log($"ClearOnlyCurrentCellIfNeeded: colliders encontrados na célula={hits.Length}");

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Log($"ClearOnlyCurrentCellIfNeeded: collider={hit.name}, type={hit.GetType().Name}");

            Destructible destructible = hit.GetComponent<Destructible>();
            if (destructible != null)
            {
                Destroy(destructible.gameObject);
                Log($"ClearOnlyCurrentCellIfNeeded: prefab Destructible destruído: {destructible.name}");
                continue;
            }

            ItemPickup item = hit.GetComponent<ItemPickup>();
            if (item != null)
            {
                Log($"ClearOnlyCurrentCellIfNeeded: item destruído pela queda do bloco: {item.name}");
                Destroy(item.gameObject);
            }
        }
    }

    IEnumerator DropTileRoutine(Vector3Int cell)
    {
        Vector3 worldCenter = indestructibleTilemap.GetCellCenterWorld(cell);

        Log(
            $"DropTileRoutine: célula={cell}, worldCenter={worldCenter}, " +
            $"fallingBlockVisualPrefab={(fallingBlockVisualPrefab != null ? fallingBlockVisualPrefab.name : "NULL")}, " +
            $"tile={(currentStageIndestructibleTile != null ? currentStageIndestructibleTile.name : "NULL")}");

        if (fallingBlockVisualPrefab != null)
        {
            GameObject visual = Instantiate(
                fallingBlockVisualPrefab,
                worldCenter + Vector3.up * fallingHeight,
                Quaternion.identity);

            Log($"DropTileRoutine: visual instanciado para célula {cell} em {visual.transform.position}");

            float elapsed = 0f;
            Vector3 start = visual.transform.position;
            Vector3 end = worldCenter;

            SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
            if (sr != null && currentStageIndestructibleTile is Tile tileAsset && tileAsset.sprite != null)
            {
                sr.sprite = tileAsset.sprite;
                Log($"DropTileRoutine: sprite do visual aplicado para célula {cell}: {tileAsset.sprite.name}");
            }
            else
            {
                Log($"DropTileRoutine: visual sem SpriteRenderer compatível ou tile sem sprite para célula {cell}");
            }

            while (elapsed < fallingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallingDuration);
                visual.transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            visual.transform.position = end;
            Destroy(visual);

            Log($"DropTileRoutine: animação visual concluída para célula {cell}");
        }
        else
        {
            Log($"DropTileRoutine: sem prefab visual. Aguardando fallingDuration={fallingDuration:0.000} antes de aplicar tile na célula {cell}");
            yield return new WaitForSeconds(fallingDuration);
        }

        ClearOnlyCurrentCellIfNeeded(cell);

        bool hadTileBeforeSet = indestructibleTilemap.HasTile(cell);
        Log($"DropTileRoutine: antes do SetTile célula {cell} hadTileBeforeSet={hadTileBeforeSet}");

        if (!hadTileBeforeSet)
        {
            indestructibleTilemap.SetTile(cell, currentStageIndestructibleTile);
            indestructibleTilemap.RefreshTile(cell);

            TilemapCollider2D collider = indestructibleTilemap.GetComponent<TilemapCollider2D>();
            if (collider != null)
                collider.ProcessTilemapChanges();

            bool hasTileAfterSet = indestructibleTilemap.HasTile(cell);
            TileBase placedTile = indestructibleTilemap.GetTile(cell);

            Log(
                $"DropTileRoutine: após SetTile célula {cell} hasTileAfterSet={hasTileAfterSet}, " +
                $"placedTile={(placedTile != null ? placedTile.name : "NULL")}");

            LogTilemapRenderingState(cell);
        }
        else
        {
            Log($"DropTileRoutine: célula {cell} já tinha tile logo antes do SetTile. Nenhuma alteração aplicada.");
        }

        PlayFallingSfx();
        StartDamageOnCell(cell);
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
        Vector3Int cell = new Vector3Int(-7, 4, 0);

        Log($"TestDropSingleTileNow: tentando célula {cell}");

        if (indestructibleTilemap == null)
        {
            LogError("TestDropSingleTileNow: indestructibleTilemap NULL");
            return;
        }

        if (currentStageIndestructibleTile == null)
        {
            LogError("TestDropSingleTileNow: currentStageIndestructibleTile NULL");
            return;
        }

        ClearOnlyCurrentCellIfNeeded(cell);

        indestructibleTilemap.SetTile(cell, currentStageIndestructibleTile);
        indestructibleTilemap.RefreshTile(cell);

        TilemapCollider2D collider = indestructibleTilemap.GetComponent<TilemapCollider2D>();
        if (collider != null)
            collider.ProcessTilemapChanges();

        LogTilemapRenderingState(cell);
    }

    void StartDamageOnCell(Vector3Int cell)
    {
        if (damageCoroutines.ContainsKey(cell))
        {
            Log($"StartDamageOnCell: dano já estava ativo na célula {cell}");
            return;
        }

        Coroutine routine = StartCoroutine(DamagePlayersOnCellRoutine(cell));
        damageCoroutines[cell] = routine;

        Log($"StartDamageOnCell: iniciou rotina de dano na célula {cell}");
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

            if (logDamageTicks)
                Log($"DamagePlayersStandingOnCell: player={player.name}, cell={cell}, damage={damagePerTick}");

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
        Log("PlayHurryUpAndResumeMusic: iniciando.");

        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.StopMusic();
            Log("PlayHurryUpAndResumeMusic: música parada.");
        }

        if (hurryUpSfx != null && GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayOneShotSfx(hurryUpSfx);
            Log($"PlayHurryUpAndResumeMusic: hurryUpSfx tocado: {hurryUpSfx.name}");
        }

        float wait = hurryUpSfx != null ? hurryUpSfx.length : 0f;
        wait += resumeMusicDelayAfterHurryUp;

        Log($"PlayHurryUpAndResumeMusic: aguardando {wait:0.000}s para retomar música acelerada.");

        yield return new WaitForSeconds(wait);

        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayDefaultMusicWithPitch(fastMusicPitch, true);
            Log($"PlayHurryUpAndResumeMusic: música retomada com pitch={fastMusicPitch:0.000}");
        }
    }

    void PlayFallingSfx()
    {
        if (fallingTileSfx == null || GameMusicController.Instance == null)
        {
            Log("PlayFallingSfx: fallingTileSfx ou GameMusicController.Instance NULL.");
            return;
        }

        GameMusicController.Instance.PlayOneShotSfx(fallingTileSfx);
        Log($"PlayFallingSfx: sfx tocado: {fallingTileSfx.name}");
    }

    void RemoveAllRevengeBombersFromScene()
    {
        BattleRevengeController[] revengeCarts = FindObjectsByType<BattleRevengeController>();
        Log($"RemoveAllRevengeBombersFromScene: revenge carts encontrados={revengeCarts.Length}");

        for (int i = 0; i < revengeCarts.Length; i++)
        {
            if (revengeCarts[i] != null)
            {
                Log($"RemoveAllRevengeBombersFromScene: destruindo cart {revengeCarts[i].name}");
                Destroy(revengeCarts[i].gameObject);
            }
        }

        BattleRevengeSystem[] revengeSystems = FindObjectsByType<BattleRevengeSystem>();
        Log($"RemoveAllRevengeBombersFromScene: revenge systems encontrados={revengeSystems.Length}");

        for (int i = 0; i < revengeSystems.Length; i++)
        {
            if (revengeSystems[i] != null)
            {
                revengeSystems[i].enabled = false;
                Log($"RemoveAllRevengeBombersFromScene: desabilitado system {revengeSystems[i].name}");
            }
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

        Log(
            $"BuildClosingPath: bounds=({bounds.xMin},{bounds.yMin}) até ({bounds.xMax - 1},{bounds.yMax - 1}), " +
            $"size=({bounds.size.x},{bounds.size.y}), usePlayableAreaFromBounds={usePlayableAreaFromBounds}");

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

        Log($"BuildClosingPath: total de células no caminho={suddenDeathPath.Count}");

        if (suddenDeathPath.Count > 0)
            Log($"BuildClosingPath: primeira célula={suddenDeathPath[0]}, última célula={suddenDeathPath[suddenDeathPath.Count - 1]}");
        else
            LogWarning("BuildClosingPath: nenhum tile foi agendado para queda.");
    }

    void AddCellIfValid(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
            return;

        if (indestructibleTilemap.HasTile(cell))
        {
            if (logEveryDrop)
                Log($"AddCellIfValid: célula {cell} ignorada porque já possui tile.");
            return;
        }

        if (!scheduledOrPlacedCells.Add(cell))
        {
            if (logEveryDrop)
                Log($"AddCellIfValid: célula {cell} ignorada porque já estava agendada.");
            return;
        }

        suddenDeathPath.Add(cell);

        if (logEveryDrop)
            Log($"AddCellIfValid: célula adicionada ao caminho {cell}");
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

            BoundsInt manualBounds = new BoundsInt(xMin, yMin, 0, width, height, 1);

            Log(
                $"GetPlayableBounds: usando bounds manuais. bottomLeftCell={bottomLeftCell}, topRightCell={topRightCell}, " +
                $"result=({manualBounds.xMin},{manualBounds.yMin}) até ({manualBounds.xMax - 1},{manualBounds.yMax - 1})");

            return manualBounds;
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

        BoundsInt autoBounds = new BoundsInt(autoXMin, autoYMin, 0, autoXMax - autoXMin, autoYMax - autoYMin, 1);

        Log(
            $"GetPlayableBounds: usando bounds do tilemap. cellBoundsOrig=({b.xMin},{b.yMin}) até ({b.xMax - 1},{b.yMax - 1}), " +
            $"result=({autoBounds.xMin},{autoBounds.yMin}) até ({autoBounds.xMax - 1},{autoBounds.yMax - 1})");

        return autoBounds;
    }

    Tilemap FindIndestructibleTilemap()
    {
        Tilemap[] maps = FindObjectsByType<Tilemap>();

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
        Tilemap[] maps = FindObjectsByType<Tilemap>();

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

        LogWarning("FindDestructibleTilemap: nenhum tilemap de destrutíveis encontrado.");
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