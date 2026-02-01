using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MechaBossSequence : MonoBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds1 = new(1f);
    private static readonly WaitForSeconds _waitForSeconds2 = new(2f);

    public MovementController whiteMecha;
    public MovementController blackMecha;
    public MovementController goldenMecha;

    [Header("Move players to boss intro positions on golden flash")]
    public bool movePlayerToIntroPositionOnGoldenFlash = true;

    static readonly Vector2[] BossStagePositions =
    {
        new(-3f, -6f),
        new( 1f, -6f),
        new(-5f, -6f),
        new( 3f, -6f)
    };

    [Header("Music")]
    public AudioClip bossCheeringMusic;

    [Header("Gate")]
    public Tilemap indestructibleTilemap;
    public Vector3Int gateCell;
    public float gateStepDelay = 0.1f;

    [Header("Stands (Crowd / Empty)")]
    public Tilemap standsTilemap;
    public TileBase[] crowdTiles;
    public TileBase[] emptyTiles;

    [Header("Item Spawner")]
    public Tilemap groundTilemap;
    public Tilemap destructibleTilemap;
    public ItemPickup[] itemPrefabs;

    public int spawnMinX = -7;
    public int spawnMaxX = 5;
    public int spawnMinY = -6;
    public int spawnMaxY = 2;

    public float minSpawnInterval = 20f;
    public float maxSpawnInterval = 40f;
    public int maxTriesPerSpawn = 50;

    [Header("Boss End Drop")]
    public ItemPickup kickBombDropPrefabOverride;
    public float endStageWaitNoItemsSeconds = 3f;

    [Header("End Stage")]
    [Min(0f)] public float endStageDelayBeforeStart = 1f;
    [Min(0f)] public float endStageFadeDuration = 3f;
    public AudioClip endStageEnterSfx;

    MovementController[] mechas;
    GameManager gameManager;

    bool initialized;
    bool sequenceStarted;
    bool finalSequenceStarted;
    bool itemLoopStarted;
    bool itemSpawnEnabled = true;

    bool endStageLikeStarted;

    TileBase gateCenterTile;
    TileBase gateLeftTile;
    TileBase gateRightTile;

    Vector3Int gateLeftCell;
    Vector3Int gateRightCell;

    public static bool MechaIntroRunning { get; private set; }

    readonly Dictionary<Vector3Int, int> changedStandCells = new();
    readonly List<Vector3Int> spawnableCells = new();

    readonly List<MovementController> players = new();
    readonly List<BombController> playerBombs = new();
    readonly List<PlayerLouieCompanion> playerCompanions = new();

    readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    int playersSafetyLocks;

    void Awake()
    {
        mechas = new[] { whiteMecha, blackMecha, goldenMecha };
        gameManager = FindFirstObjectByType<GameManager>();

        foreach (var m in mechas)
        {
            if (m == null) continue;
            m.Died += OnMechaDied;
        }

        if (indestructibleTilemap != null)
        {
            gateCenterTile = indestructibleTilemap.GetTile(gateCell);

            gateLeftCell = new Vector3Int(gateCell.x - 1, gateCell.y, gateCell.z);
            gateRightCell = new Vector3Int(gateCell.x + 1, gateCell.y, gateCell.z);

            gateLeftTile = indestructibleTilemap.GetTile(gateLeftCell);
            gateRightTile = indestructibleTilemap.GetTile(gateRightCell);
        }
    }

    void Start()
    {
        initialized = true;

        EnsurePlayersRefs();

        LockPlayers(true);
        ForcePlayersMountedUpIfNeeded();

        for (int i = 0; i < mechas.Length; i++)
            if (mechas[i] != null)
                mechas[i].gameObject.SetActive(false);

        if (groundTilemap != null && itemPrefabs != null && itemPrefabs.Length > 0)
            RebuildSpawnableCells();
    }

    void OnEnable()
    {
        if (initialized && !sequenceStarted)
        {
            sequenceStarted = true;
            ForcePlayersMountedUpIfNeeded();
            StartCoroutine(SpawnFirstMechaAfterStageStart());
        }
    }

    IEnumerator SpawnFirstMechaAfterStageStart()
    {
        MechaIntroRunning = true;
        if (StageMechaIntroController.Instance != null)
            StageMechaIntroController.Instance.SetIntroRunning(true);

        EnsurePlayersRefs();
        SetLouieAbilitiesLockedForAll(true);

        if (StageIntroTransition.Instance != null)
        {
            PushPlayersSafety();
            try
            {
                while (StageIntroTransition.Instance.IntroRunning)
                {
                    ForcePlayersMountedUpIfNeeded();
                    yield return null;
                }
            }
            finally
            {
                while (playersSafetyLocks > 0)
                    PopPlayersSafety();
            }
        }

        while (GamePauseController.IsPaused)
            yield return null;

        LockPlayers(true);
        ForcePlayersMountedUpIfNeeded();

        PushPlayersSafety();
        yield return _waitForSeconds2;
        PopPlayersSafety();

        StartMechaIntro(0);
    }

    void StartMechaIntro(int index)
    {
        if (index < 0 || index >= mechas.Length) return;
        if (mechas[index] == null) return;

        MechaIntroRunning = true;
        if (StageMechaIntroController.Instance != null)
            StageMechaIntroController.Instance.SetIntroRunning(true);

        EnsurePlayersRefs();
        SetLouieAbilitiesLockedForAll(true);

        LockPlayers(true);

        BombController.ExplodeAllControlBombsInStage();

        ForcePlayersMountedUpIfNeeded();
        PushPlayersSafety();

        StartCoroutine(MechaIntroRoutine(mechas[index]));
    }

    IEnumerator MechaIntroRoutine(MovementController mecha)
    {
        MechaIntroRunning = true;
        if (StageMechaIntroController.Instance != null)
            StageMechaIntroController.Instance.SetIntroRunning(true);

        SetItemSpawnEnabled(false);

        EnsurePlayersRefs();
        LockPlayers(true);
        ForcePlayersMountedUpIfNeeded();

        try
        {
            yield return StartCoroutine(OpenGateRoutine());

            ForcePlayersMountedUpIfNeeded();

            bool isGolden = mecha == goldenMecha;

            if (isGolden)
            {
                yield return _waitForSeconds2;

                if (StageMechaIntroController.Instance != null)
                {
                    yield return StageMechaIntroController.Instance.FlashWithOnLastBlack(0.5f, 5, () =>
                    {
                        if (!movePlayerToIntroPositionOnGoldenFlash)
                            return;

                        EnsurePlayersRefs();
                        MoveAllPlayersToBossIntroPositions();
                        ForcePlayersMountedUpIfNeeded();
                    });
                }
                else
                {
                    if (movePlayerToIntroPositionOnGoldenFlash)
                    {
                        EnsurePlayersRefs();
                        MoveAllPlayersToBossIntroPositions();
                        ForcePlayersMountedUpIfNeeded();
                    }
                }
            }

            if (mecha == null) yield break;

            mecha.SetExplosionInvulnerable(true);

            var bossAI = mecha.GetComponent<BossBomberAI>();
            var aiMove = mecha.GetComponent<AIMovementController>();

            if (bossAI != null) bossAI.enabled = false;
            if (aiMove != null) aiMove.enabled = true;

            Vector2 startPos = new(-1f, 5f);
            Vector2 midPos = new(-1f, 4f);
            Vector2 endPos = new(-1f, 0f);

            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.simulated = true;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
                mecha.Rigidbody.position = startPos;
            }
            else
            {
                mecha.transform.position = startPos;
            }

            mecha.gameObject.SetActive(true);

            if (!isGolden)
            {
                if (aiMove != null)
                    aiMove.SetAIDirection(Vector2.down);

                while (true)
                {
                    if (mecha == null) yield break;

                    Vector2 pos = mecha.Rigidbody != null
                        ? mecha.Rigidbody.position
                        : (Vector2)mecha.transform.position;

                    if (pos.y <= endPos.y + 0.05f)
                        break;

                    if (aiMove != null)
                        aiMove.SetAIDirection(Vector2.down);

                    yield return null;
                }

                if (mecha.Rigidbody != null)
                {
                    mecha.Rigidbody.position = endPos;
                    mecha.Rigidbody.linearVelocity = Vector2.zero;
                }
                else
                {
                    mecha.transform.position = endPos;
                }
            }
            else
            {
                if (aiMove != null)
                {
                    aiMove.SetAIDirection(Vector2.zero);
                    aiMove.enabled = false;
                }

                float moveSpeed = mecha.speed > 0f ? mecha.speed : 2f;

                yield return MoveMechaVertically(mecha, startPos, midPos, moveSpeed);
                yield return _waitForSeconds2;
                yield return MoveMechaVertically(mecha, midPos, endPos, moveSpeed);

                if (aiMove != null)
                    aiMove.enabled = true;
            }

            if (aiMove != null)
                aiMove.SetAIDirection(Vector2.zero);

            yield return _waitForSeconds2;

            if (bossAI != null) bossAI.enabled = true;

            if (mecha != null)
                mecha.SetExplosionInvulnerable(false);

            yield return StartCoroutine(CloseGateRoutine());

            if (!itemLoopStarted && groundTilemap != null && itemPrefabs != null && itemPrefabs.Length > 0)
            {
                itemLoopStarted = true;
                StartCoroutine(ItemSpawnLoop());
            }

            SetItemSpawnEnabled(true);

            LockPlayers(false);
            SetLouieAbilitiesLockedForAll(false);

            MechaIntroRunning = false;
            if (StageMechaIntroController.Instance != null)
                StageMechaIntroController.Instance.SetIntroRunning(false);
        }
        finally
        {
            while (playersSafetyLocks > 0)
                PopPlayersSafety();

            SetItemSpawnEnabled(true);
            LockPlayers(false);
            SetLouieAbilitiesLockedForAll(false);

            MechaIntroRunning = false;
            if (StageMechaIntroController.Instance != null)
                StageMechaIntroController.Instance.SetIntroRunning(false);
        }
    }

    IEnumerator MoveMechaVertically(MovementController mecha, Vector2 from, Vector2 to, float speed)
    {
        if (mecha == null)
            yield break;

        mecha.ApplyDirectionFromVector(Vector2.down);

        float distance = Mathf.Abs(to.y - from.y);
        if (distance <= Mathf.Epsilon || speed <= 0f)
        {
            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.position = to;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                mecha.transform.position = to;
            }

            mecha.ApplyDirectionFromVector(Vector2.zero);
            yield break;
        }

        float duration = distance / speed;
        float t = 0f;

        while (t < duration)
        {
            if (mecha == null) yield break;

            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            float newY = Mathf.Lerp(from.y, to.y, lerp);

            if (mecha.Rigidbody != null)
            {
                Vector2 pos = mecha.Rigidbody.position;
                pos.x = from.x;
                pos.y = newY;
                mecha.Rigidbody.position = pos;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector3 pos = mecha.transform.position;
                pos.x = from.x;
                pos.y = newY;
                mecha.transform.position = pos;
            }

            yield return null;
        }

        if (mecha != null)
        {
            if (mecha.Rigidbody != null)
            {
                mecha.Rigidbody.position = to;
                mecha.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                mecha.transform.position = to;
            }
        }

        mecha.ApplyDirectionFromVector(Vector2.zero);
    }

    IEnumerator OpenGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateCell, null);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateLeftCell, null);
        indestructibleTilemap.SetTile(gateRightCell, null);
    }

    IEnumerator CloseGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateLeftCell, gateLeftTile);
        indestructibleTilemap.SetTile(gateRightCell, gateRightTile);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateCell, gateCenterTile);
    }

    public void SetStandsEmpty(bool empty)
    {
        ReplaceCrowdWithEmpty(empty);
    }

    void ReplaceCrowdWithEmpty(bool setEmpty)
    {
        if (standsTilemap == null) return;
        if (crowdTiles == null || emptyTiles == null) return;
        if (crowdTiles.Length != emptyTiles.Length) return;

        if (setEmpty)
        {
            changedStandCells.Clear();

            BoundsInt bounds = standsTilemap.cellBounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new(x, y, 0);
                    TileBase current = standsTilemap.GetTile(cell);
                    if (current == null) continue;

                    for (int i = 0; i < crowdTiles.Length; i++)
                    {
                        if (current == crowdTiles[i])
                        {
                            standsTilemap.SetTile(cell, emptyTiles[i]);
                            changedStandCells[cell] = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var kvp in changedStandCells)
            {
                Vector3Int cell = kvp.Key;
                int index = kvp.Value;

                if (index >= 0 && index < crowdTiles.Length)
                    standsTilemap.SetTile(cell, crowdTiles[index]);
            }

            changedStandCells.Clear();
        }
    }

    void RebuildSpawnableCells()
    {
        spawnableCells.Clear();

        if (groundTilemap == null)
            return;

        for (int x = spawnMinX; x <= spawnMaxX; x++)
        {
            for (int y = spawnMinY; y <= spawnMaxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (!groundTilemap.HasTile(cell))
                    continue;

                if (destructibleTilemap != null && destructibleTilemap.HasTile(cell))
                    continue;

                if (indestructibleTilemap != null && indestructibleTilemap.HasTile(cell))
                    continue;

                spawnableCells.Add(cell);
            }
        }
    }

    IEnumerator ItemSpawnLoop()
    {
        while (!finalSequenceStarted)
        {
            float waitTime = UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
            float elapsed = 0f;

            while (elapsed < waitTime && !finalSequenceStarted)
            {
                if (!GamePauseController.IsPaused && itemSpawnEnabled)
                    elapsed += Time.deltaTime;

                yield return null;
            }

            if (finalSequenceStarted || !itemSpawnEnabled)
                continue;

            TrySpawnItem();
        }
    }

    void TrySpawnItem()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
            return;

        if (groundTilemap == null)
            return;

        if (spawnableCells == null || spawnableCells.Count == 0)
            return;

        for (int i = 0; i < maxTriesPerSpawn; i++)
        {
            int index = UnityEngine.Random.Range(0, spawnableCells.Count);
            Vector3Int cell = spawnableCells[index];

            Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);
            Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.2f);

            if (hit != null && hit.GetComponent<ItemPickup>() != null)
                continue;

            ItemPickup prefab = itemPrefabs[UnityEngine.Random.Range(0, itemPrefabs.Length)];
            Instantiate(prefab, worldPos, Quaternion.identity);
            return;
        }
    }

    void SetItemSpawnEnabled(bool enabled)
    {
        itemSpawnEnabled = enabled;
    }

    void OnMechaDied(MovementController sender)
    {
        int currentIndex = Array.IndexOf(mechas, sender);
        int nextIndex = currentIndex + 1;

        if (nextIndex < mechas.Length && mechas[nextIndex] != null)
        {
            StartMechaIntro(nextIndex);
            return;
        }

        if (sender == goldenMecha && !finalSequenceStarted)
        {
            finalSequenceStarted = true;
            SetItemSpawnEnabled(false);
            StartCoroutine(FinalBossDropAndEndRoutine(sender));
            return;
        }

        if (gameManager != null)
            gameManager.CheckWinState();
    }

    IEnumerator FinalBossDropAndEndRoutine(MovementController deadBoss)
    {
        EnsurePlayersRefs();

        Vector3Int cell = Vector3Int.zero;
        bool hasCell = false;

        if (groundTilemap != null && deadBoss != null)
        {
            cell = groundTilemap.WorldToCell(deadBoss.transform.position);
            hasCell = true;
        }

        if (hasCell)
            SpawnKickBombAtCell(cell);

        float elapsed = 0f;
        float maxWait = Mathf.Max(0f, endStageWaitNoItemsSeconds);

        while (elapsed < maxWait)
        {
            if (!GamePauseController.IsPaused)
                elapsed += Time.deltaTime;

            if (!HasAnyItemPickupsInStage())
                break;

            yield return null;
        }

        yield return _waitForSeconds1;

        StartEndStageLike();
    }

    void StartEndStageLike()
    {
        if (endStageLikeStarted)
            return;

        endStageLikeStarted = true;

        StartCoroutine(EndStageLikeRoutine());
    }

    IEnumerator EndStageLikeRoutine()
    {
        if (endStageDelayBeforeStart > 0f)
            yield return new WaitForSeconds(endStageDelayBeforeStart);

        EnsurePlayersRefs();

        bool playedEnter = false;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (!p.gameObject.activeInHierarchy) continue;
            if (!p.CompareTag("Player")) continue;
            if (p.isDead) continue;
            if (p.IsEndingStage) continue;

            var bomb = p.GetComponent<BombController>();

            PlayerPersistentStats.SaveFrom(p, bomb);

            if (bomb != null)
                bomb.ClearPlantedBombsOnStageEnd(false);

            Vector2 center = new(
                Mathf.Round(p.transform.position.x),
                Mathf.Round(p.transform.position.y)
            );

            p.PlayEndStageSequence(center, snapToPortalCenter: false);

            p.SetExplosionInvulnerable(true);
            p.SetInputLocked(true, false);

            var col = p.GetComponent<Collider2D>();
            if (col != null)
            {
                cachedPlayerColliders[p] = col;
                cachedColliderEnabled[p] = col.enabled;
                col.enabled = false;
            }

            if (p.TryGetComponent<CharacterHealth>(out var health) && health != null)
                health.StopInvulnerability();

            if (!playedEnter && endStageEnterSfx != null)
            {
                var audio = p.GetComponent<AudioSource>();
                if (audio != null)
                {
                    audio.PlayOneShot(endStageEnterSfx);
                    playedEnter = true;
                }
            }
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (bossCheeringMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(bossCheeringMusic, 1f, false);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(endStageFadeDuration);

        if (gameManager != null)
            gameManager.EndStage();
    }

    bool HasAnyItemPickupsInStage()
    {
        var items = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return items != null && items.Length > 0;
    }

    void SpawnKickBombAtCell(Vector3Int cell)
    {
        if (groundTilemap == null)
            return;

        if (!groundTilemap.HasTile(cell))
            return;

        Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);

        Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.2f);
        if (hit != null && hit.GetComponent<ItemPickup>() != null)
            return;

        ItemPickup prefab = kickBombDropPrefabOverride;

        if (prefab == null && gameManager != null)
            prefab = gameManager.GetItemPrefab(ItemPickup.ItemType.BombKick);

        if (prefab == null)
            return;

        Instantiate(prefab, worldPos, Quaternion.identity);
    }

    void EnsurePlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();
        playerCompanions.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                MovementController move = null;
                if (!id.TryGetComponent(out move))
                    move = id.GetComponentInChildren<MovementController>(true);

                if (move == null) continue;
                if (!move.gameObject.activeInHierarchy) continue;
                if (!move.CompareTag("Player")) continue;
                if (move.isDead) continue;

                if (!players.Contains(move))
                    players.Add(move);
            }
        }
        else
        {
            var moves = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < moves.Length; i++)
                if (moves[i] != null && moves[i].CompareTag("Player") && moves[i].gameObject.activeInHierarchy && !moves[i].isDead)
                    players.Add(moves[i]);
        }

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            var bomb = p.GetComponent<BombController>();
            if (bomb != null) playerBombs.Add(bomb);

            var comp = p.GetComponent<PlayerLouieCompanion>();
            if (comp != null) playerCompanions.Add(comp);
        }
    }

    void MoveAllPlayersToBossIntroPositions()
    {
        EnsurePlayersRefs();

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            int pid = Mathf.Clamp(p.PlayerId, 1, 4);
            Vector2 target = BossStagePositions[pid - 1];

            if (p.Rigidbody != null)
            {
                p.Rigidbody.simulated = true;
                p.Rigidbody.linearVelocity = Vector2.zero;
                p.Rigidbody.position = target;
            }
            else
            {
                p.transform.position = new Vector3(target.x, target.y, p.transform.position.z);
            }
        }
    }

    void LockPlayers(bool locked)
    {
        EnsurePlayersRefs();

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            p.SetInputLocked(locked);
            p.SetExplosionInvulnerable(locked);
        }

        for (int i = 0; i < playerBombs.Count; i++)
            if (playerBombs[i] != null)
                playerBombs[i].enabled = !locked;
    }

    void SetLouieAbilitiesLockedForAll(bool locked)
    {
        EnsurePlayersRefs();

        for (int i = 0; i < playerCompanions.Count; i++)
            if (playerCompanions[i] != null)
                playerCompanions[i].SetLouieAbilitiesLocked(locked);
    }

    void ForcePlayersMountedUpIfNeeded()
    {
        EnsurePlayersRefs();

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (p.IsMountedOnLouie)
                p.ForceMountedUpExclusive();
            else
                p.ForceIdleUp();
        }
    }

    void PushPlayersSafety()
    {
        EnsurePlayersRefs();

        playersSafetyLocks++;

        if (playersSafetyLocks > 1)
            return;

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            p.SetExplosionInvulnerable(true);
            p.SetInputLocked(true, false);

            var col = p.GetComponent<Collider2D>();
            if (col != null)
            {
                cachedPlayerColliders[p] = col;
                cachedColliderEnabled[p] = col.enabled;
                col.enabled = false;
            }

            if (p.TryGetComponent<CharacterHealth>(out var health) && health != null)
                health.StopInvulnerability();
        }
    }

    void PopPlayersSafety()
    {
        EnsurePlayersRefs();

        if (playersSafetyLocks <= 0)
            return;

        playersSafetyLocks--;

        if (playersSafetyLocks > 0)
            return;

        foreach (var kv in cachedPlayerColliders)
        {
            var p = kv.Key;
            var col = kv.Value;

            if (p == null || col == null) continue;

            if (cachedColliderEnabled.TryGetValue(p, out bool wasEnabled))
                col.enabled = wasEnabled;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();
    }
}
