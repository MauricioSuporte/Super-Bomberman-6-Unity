using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class StagePreIntroPlayersWalk : MonoBehaviour
{
    private const string LOG = "[PreIntroWalk]";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Enable")]
    [SerializeField] private bool enabledPreIntro = true;

    [Header("Origin (where players start walking from)")]
    [Tooltip("If set, all players start from this Transform position (with optional per-player offsets).")]
    [SerializeField] private Transform commonOrigin;

    [Tooltip("Optional per-player offsets (index 0..3 => player 1..4).")]
    [SerializeField] private Vector2[] perPlayerOriginOffset = new Vector2[4];

    [Header("Pathfinding")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(16)] private int maxNodes = 4096;
    [SerializeField, Min(100)] private int maxExpandSteps = 20000;
    [SerializeField, Range(0.1f, 1f)] private float overlapBoxScale = 0.6f;

    [Header("Movement")]
    [Tooltip("World units per second. (tileSize=1 => 3 means 3 tiles/sec)")]
    [SerializeField, Min(0.1f)] private float walkSpeedUnitsPerSecond = 3.5f;
    [SerializeField, Min(0.01f)] private float reachEpsilon = 0.06f;

    [Header("Timings")]
    [SerializeField, Min(0f)] private float delayBeforeWalkSeconds = 0.15f;
    [SerializeField, Min(0f)] private float delayAfterWalkSeconds = 0.05f;

    [Header("Debug / Watch External Move")]
    [SerializeField, Min(0f)] private float watchAfterSnapSeconds = 0.35f;

    private const string WalkLoopClipResourcesPath = "Sounds/walk";
    private static AudioClip s_walkLoopClip;

    private const float WalkStepSfxIntervalSeconds = 0.3f;

    [SerializeField, Range(0f, 1f)] private float walkLoopVolume = 0.8f;

    private Coroutine walkSfxRoutine;
    private bool walkSfxActive;
    private float nextWalkStepSfxAtUnscaled;

    [Header("Queue Walk (fila)")]
    [SerializeField] private bool queueWalk = true;

    [Tooltip("Distância em tiles entre um player e o próximo (ex: 1 = 1 tile).")]
    [SerializeField, Min(0)] private int queueSpacingTiles = 1;

    private readonly GridAStarPathfinder2D pathfinder = new();
    private AudioSource walkLoopSource;

    public bool IsEnabled => enabledPreIntro;

    private struct PlayerWalkData
    {
        public int playerId;
        public PlayerIdentity identity;
        public MovementController mover;
        public Transform root;
        public float tileSize;
    }

    void Awake()
    {
        walkLoopSource = GetComponent<AudioSource>();
        if (walkLoopSource == null)
            walkLoopSource = gameObject.AddComponent<AudioSource>();

        walkLoopSource.playOnAwake = false;
        walkLoopSource.loop = false;
        walkLoopSource.spatialBlend = 0f;
    }

    public void PreSnapPlayersToOrigin()
    {
        if (!enabledPreIntro)
            return;

        if (commonOrigin == null)
            return;

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids == null || ids.Length == 0)
            return;

        Vector2 commonOriginWorld = commonOrigin.position;

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (!id) continue;

            int playerId = Mathf.Clamp(id.playerId, 1, 4);

            MovementController move = null;
            if (!id.TryGetComponent(out move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move) continue;
            if (!move.CompareTag("Player")) continue;
            if (move.isDead) continue;

            Transform root = id.transform != null ? id.transform : move.transform;

            float tile = Mathf.Max(0.0001f, move.tileSize);
            Vector2 offset = GetOriginOffset(playerId);
            Vector2 originRaw = commonOriginWorld + offset;
            Vector2 originRounded = RoundToGrid(originRaw, tile);

            SnapRootToWorld(root, originRounded);

            if (move.Rigidbody != null)
            {
                move.Rigidbody.simulated = true;
                move.Rigidbody.linearVelocity = Vector2.zero;
                move.Rigidbody.position = originRounded;
            }

            var rbRoot = root != null ? root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null)
            {
                rbRoot.simulated = true;
                rbRoot.linearVelocity = Vector2.zero;
                rbRoot.position = originRounded;

                root.position = originRounded;
            }
        }
    }

    public IEnumerator Play(PlayersSpawner spawner)
    {
        Log($"PLAY_BEGIN enabledPreIntro={enabledPreIntro} spawner={(spawner ? spawner.name : "NULL")} commonOrigin={(commonOrigin ? commonOrigin.name : "NULL")} " +
            $"timeScale={Time.timeScale:0.###} paused={GamePauseController.IsPaused} t_unscaled={Time.unscaledTime:0.###}");

        StopWalkLoopSfx();

        if (!enabledPreIntro)
        {
            Log("PLAY_EARLY_EXIT reason=enabledPreIntro_false");
            yield break;
        }

        if (spawner == null)
        {
            Log("PLAY_EARLY_EXIT reason=spawner_null");
            yield break;
        }

        if (commonOrigin == null)
        {
            Log("PLAY_EARLY_EXIT reason=commonOrigin_null");
            yield break;
        }

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Log($"FOUND_PlayerIdentity count={(ids != null ? ids.Length : 0)}");

        if (ids == null || ids.Length == 0)
        {
            Log("PLAY_EARLY_EXIT reason=no_PlayerIdentity_found");
            yield break;
        }

        Vector2 commonOriginWorld = commonOrigin.position;
        Log($"ORIGIN commonOriginWorld={Fmt(commonOriginWorld)} commonOriginTransformPos={Fmt((Vector2)commonOrigin.position)} thisGO={name} thisPos={Fmt((Vector2)transform.position)}");

        // Guardas de segurança (collider / bomb enable)
        var cachedColliders = new Dictionary<MovementController, Collider2D>();
        var cachedColliderEnabled = new Dictionary<MovementController, bool>();
        var cachedBombEnabled = new Dictionary<MovementController, bool>();

        var players = new List<PlayerWalkData>(4);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (!id) continue;

            int playerId = Mathf.Clamp(id.playerId, 1, 4);

            MovementController move = null;
            if (!id.TryGetComponent(out move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move)
            {
                Log($"P{playerId} SKIP reason=no_MovementController (idGO={id.name})");
                continue;
            }

            if (!move.CompareTag("Player"))
            {
                Log($"P{playerId} SKIP reason=not_tag_Player (tag={move.tag})");
                continue;
            }

            if (move.isDead)
            {
                Log($"P{playerId} SKIP reason=isDead_true");
                continue;
            }

            Transform root = id.transform != null ? id.transform : move.transform;

            LogHierarchyState("HIER_BEFORE_SETUP", playerId, id, move, root);
            LogPlayerState("BEFORE_SETUP", playerId, move);

            // --------- NOVO: trava de preintro (para não “bolear”) ----------
            // 1) impede o FixedUpdate do MovementController de brigar com o movimento manual
            move.SetExternalMovementOverride(true);

            // 2) trava input (você vai animar via ApplyDirectionFromVector)
            move.SetInputLocked(true, true);

            // 3) desliga collider para não colidir / slide / empurrões
            if (!cachedColliders.ContainsKey(move))
            {
                var col = move.GetComponent<Collider2D>();
                if (col != null)
                {
                    cachedColliders[move] = col;
                    cachedColliderEnabled[move] = col.enabled;
                    col.enabled = false;
                }
            }
            else
            {
                var col = cachedColliders[move];
                if (col != null) col.enabled = false;
            }

            // 4) (opcional, mas recomendado) desliga BombController durante preintro
            if (move.TryGetComponent<BombController>(out var bc) && bc != null)
            {
                if (!cachedBombEnabled.ContainsKey(move))
                    cachedBombEnabled[move] = bc.enabled;

                bc.enabled = false;
            }

            // Mantém o controller habilitado para atualizar animação ao chamar ApplyDirectionFromVector
            move.enabled = true;
            move.EnableExclusiveFromState();

            if (move.Rigidbody != null)
            {
                move.Rigidbody.simulated = true;
                move.Rigidbody.linearVelocity = Vector2.zero;
            }
            // ---------------------------------------------------------------

            Vector2 offset = GetOriginOffset(playerId);
            Vector2 originRaw = commonOriginWorld + offset;

            float tile = Mathf.Max(0.0001f, move.tileSize);
            Vector2 originRounded = RoundToGrid(originRaw, tile);

            Log($"P{playerId} ORIGIN_CALC common={Fmt(commonOriginWorld)} offset={Fmt(offset)} raw={Fmt(originRaw)} tile={tile:0.###} rounded={Fmt(originRounded)}");

            SnapRootToWorld(root, originRounded);

            var rbRoot = root != null ? root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null)
            {
                rbRoot.simulated = true;
                rbRoot.linearVelocity = Vector2.zero;
                rbRoot.position = originRounded;

                root.position = originRounded;
            }

            LogHierarchyState("HIER_AFTER_SNAP", playerId, id, move, root);

            Vector2 afterSnap = GetRootWorldPos(root, move);
            Log($"P{playerId} AFTER_SNAP rootPosNow={Fmt(afterSnap)} deltaToOrigin={Fmt(originRounded - afterSnap)} rootRB={(rbRoot ? "YES" : "NO")} rootRbSim={(rbRoot ? rbRoot.simulated.ToString() : "NA")}");

            float watchSeconds = 0f;
            if (debugLogs && watchAfterSnapSeconds > 0f)
                watchSeconds = Mathf.Min(watchAfterSnapSeconds, Mathf.Max(0f, delayBeforeWalkSeconds - 0.01f));

            if (debugLogs && watchSeconds > 0f && root != null)
                StartCoroutine(WatchForExternalTeleport("WATCH_AFTER_SNAP", playerId, root, originRounded, watchSeconds));

            yield return null;

            Vector2 after1 = GetRootWorldPos(root, move);
            Log($"P{playerId} AFTER_1FRAME rootPosNow={Fmt(after1)} deltaFromOrigin={Fmt(after1 - originRounded)}");

            LogHierarchyState("HIER_AFTER_1FRAME", playerId, id, move, root);

            if (debugLogs)
                LogActiveAnimSnapshot("ANIM_AFTER_SETUP", playerId, move, Vector2.zero, after1);

            players.Add(new PlayerWalkData
            {
                playerId = playerId,
                identity = id,
                mover = move,
                root = root,
                tileSize = tile
            });

            LogPlayerState("AFTER_SETUP", playerId, move);
        }

        Log($"READY_TO_WALK playersCount={players.Count}");
        if (players.Count == 0)
        {
            Log("PLAY_EARLY_EXIT reason=no_valid_players_after_filter");
            yield break;
        }

        players.Sort((a, b) => b.playerId.CompareTo(a.playerId));

        if (delayBeforeWalkSeconds > 0f)
        {
            Log($"WAIT_BEFORE seconds={delayBeforeWalkSeconds:0.###}");
            yield return WaitRealtime(delayBeforeWalkSeconds);
        }

        bool anyWillWalk = false;
        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null || pw.root == null) continue;

            int pid = Mathf.Clamp(pw.playerId, 1, 4);

            Vector2 goal = spawner.GetResolvedSpawnPosition(pid);
            float tile = Mathf.Max(0.0001f, pw.tileSize);

            Vector2 start = RoundToGrid(GetRootWorldPos(pw.root, pw.mover), tile);
            Vector2 goalRounded = RoundToGrid(goal, tile);

            if (start != goalRounded)
            {
                anyWillWalk = true;
                break;
            }
        }

        if (anyWillWalk)
            StartWalkLoopSfx();

        try
        {
            int doneCount = 0;
            bool[] done = new bool[players.Count];

            for (int i = 0; i < players.Count; i++)
            {
                int idx = i;
                var pw = players[idx];

                if (pw.mover == null || pw.root == null)
                {
                    done[idx] = true;
                    doneCount++;
                    continue;
                }

                float tile = Mathf.Max(0.0001f, pw.tileSize);

                float secondsPerTile = tile / Mathf.Max(0.0001f, walkSpeedUnitsPerSecond);
                float startDelay = (queueWalk && queueSpacingTiles > 0)
                    ? (idx * queueSpacingTiles * secondsPerTile)
                    : 0f;

                StartCoroutine(WalkSinglePlayer(idx, pw, spawner, startDelay, done, () => doneCount++));
            }

            while (doneCount < players.Count)
                yield return null;
        }
        finally
        {
            StopWalkLoopSfx();
        }

        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null) continue;

            pw.mover.EnableExclusiveFromState();

            pw.mover.ApplyDirectionFromVector(Vector2.up);
            pw.mover.ApplyDirectionFromVector(Vector2.zero);

            if (pw.mover.Rigidbody != null) pw.mover.Rigidbody.linearVelocity = Vector2.zero;

            var rbRoot = pw.root != null ? pw.root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null) rbRoot.linearVelocity = Vector2.zero;

            // Reabilita collider como estava
            if (cachedColliders.TryGetValue(pw.mover, out var col) && col != null)
            {
                bool wasEnabled = true;
                if (cachedColliderEnabled.TryGetValue(pw.mover, out var prev))
                    wasEnabled = prev;

                col.enabled = wasEnabled;
            }

            // Reabilita bomb controller como estava
            if (pw.mover.TryGetComponent<BombController>(out var bc) && bc != null)
            {
                bool prev = true;
                if (cachedBombEnabled.TryGetValue(pw.mover, out var was))
                    prev = was;

                bc.enabled = prev;
            }

            // Sai do override (volta controle normal)
            pw.mover.SetExternalMovementOverride(false);

            // Libera input só depois do resto do fluxo (ou deixe travado se a intro ainda vai segurar)
            pw.mover.SetInputLocked(false, false);

            LogPlayerState("AFTER_WALK_IDLE", pw.playerId, pw.mover);
            LogHierarchyState("HIER_AFTER_WALK_IDLE", pw.playerId, pw.identity, pw.mover, pw.root);

            if (debugLogs)
                LogActiveAnimSnapshot("ANIM_AFTER_WALK_IDLE", pw.playerId, pw.mover, Vector2.zero, GetRootWorldPos(pw.root, pw.mover));
        }
        // ---------------------------------------------------

        if (delayAfterWalkSeconds > 0f)
        {
            Log($"WAIT_AFTER seconds={delayAfterWalkSeconds:0.###}");
            yield return WaitRealtime(delayAfterWalkSeconds);
        }

        Log($"PLAY_END t_unscaled={Time.unscaledTime:0.###}");
    }

    private IEnumerator MoveToTile(PlayerWalkData pw, Vector2 tileCenter, float tile)
    {
        if (pw.mover == null || pw.root == null)
            yield break;

        float maxTime = 8f;
        float t = 0f;

        Vector2 startPos = GetRootWorldPos(pw.root, pw.mover);
        Vector2 delta0 = tileCenter - startPos;

        bool vertical = Mathf.Abs(delta0.y) >= Mathf.Abs(delta0.x);
        Vector2 desiredDir = vertical
            ? new Vector2(0f, Mathf.Sign(delta0.y))
            : new Vector2(Mathf.Sign(delta0.x), 0f);

        if (desiredDir == Vector2.zero)
            desiredDir = vertical ? Vector2.up : Vector2.right;

        float targetCoord = vertical ? tileCenter.y : tileCenter.x;
        float dirSign = vertical ? desiredDir.y : desiredDir.x;

        float stepEps = Mathf.Max(reachEpsilon, tile * 0.08f);

        // NEW: snapshot no começo do tile-step
        if (debugLogs)
            LogActiveAnimSnapshot("ANIM_TILESTEP_BEGIN", pw.playerId, pw.mover, desiredDir, startPos);

        while (t < maxTime)
        {
            if (GamePauseController.IsPaused)
            {
                pw.mover.ApplyDirectionFromVector(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetRootWorldPos(pw.root, pw.mover);

            float remaining = vertical ? (targetCoord - pos.y) : (targetCoord - pos.x);
            float absRemaining = Mathf.Abs(remaining);

            if (absRemaining <= stepEps || Mathf.Sign(remaining) != Mathf.Sign(dirSign))
            {
                // NEW: snapshot no momento que considera alcançado
                if (debugLogs)
                    LogActiveAnimSnapshot("ANIM_TILESTEP_REACHED_BREAK", pw.playerId, pw.mover, Vector2.zero, pos);
                break;
            }

            pw.mover.ApplyDirectionFromVector(desiredDir);

            // NEW: snapshot a cada frame de movimento (cuidado: pode logar bastante; use debugLogs só quando precisar)
            if (debugLogs)
                LogActiveAnimSnapshot("ANIM_TILESTEP_APPLYDIR", pw.playerId, pw.mover, desiredDir, pos);

            float step = walkSpeedUnitsPerSecond * Time.unscaledDeltaTime;
            Vector2 next = pos + desiredDir * step;

            if (vertical)
            {
                if (Mathf.Sign(targetCoord - next.y) != Mathf.Sign(dirSign))
                    next.y = targetCoord;
                next.x = tileCenter.x;
            }
            else
            {
                if (Mathf.Sign(targetCoord - next.x) != Mathf.Sign(dirSign))
                    next.x = targetCoord;
                next.y = tileCenter.y;
            }

            SetRootWorldPos(pw.root, next);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        SnapRootToWorld(pw.root, tileCenter);
        pw.mover.ApplyDirectionFromVector(Vector2.zero);

        if (debugLogs)
            LogActiveAnimSnapshot("ANIM_TILESTEP_END_SNAPPED", pw.playerId, pw.mover, Vector2.zero, tileCenter);

        yield return null;
    }

    private IEnumerator WalkSinglePlayer(
        int idx,
        PlayerWalkData pw,
        PlayersSpawner spawner,
        float startDelay,
        bool[] doneFlags,
        System.Action onDone)
    {
        if (startDelay > 0f)
            yield return WaitRealtime(startDelay);

        if (pw.mover == null || pw.root == null || spawner == null)
        {
            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        int pid = Mathf.Clamp(pw.playerId, 1, 4);

        Vector2 goal = spawner.GetResolvedSpawnPosition(pid);
        float tile = Mathf.Max(0.0001f, pw.tileSize);

        Vector2 start = RoundToGrid(GetRootWorldPos(pw.root, pw.mover), tile);
        Vector2 goalRounded = RoundToGrid(goal, tile);

        var usedMask = obstacleMask.value != 0 ? obstacleMask : pw.mover.obstacleMask;

        LogHierarchyState("HIER_BEFORE_WALK", pid, pw.identity, pw.mover, pw.root);
        Log($"P{pid} WALK_BEGIN(QUEUE) start={Fmt(start)} goalRaw={Fmt(goal)} goalRounded={Fmt(goalRounded)} tile={tile:0.###} " +
            $"startDelay={startDelay:0.###} obstacleMask={(obstacleMask.value != 0 ? obstacleMask.value : pw.mover.obstacleMask.value)}");

        // NEW: snapshot no começo
        if (debugLogs)
            LogActiveAnimSnapshot("ANIM_WALK_BEGIN", pid, pw.mover, (goalRounded - start), start);

        if (start == goalRounded)
        {
            Log($"P{pid} WALK_SKIP reason=start_equals_goal");
            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        var path = pathfinder.FindPath(
            start,
            goalRounded,
            tile,
            usedMask,
            pw.mover.gameObject,
            maxNodes,
            maxExpandSteps,
            overlapBoxScale);

        if (path == null || path.Count == 0)
        {
            Log($"P{pid} PATH_FAIL path={(path == null ? "NULL" : "EMPTY")} -> FALLBACK");
            yield return FallbackWalkTowards(pw, goalRounded, tile);
            SnapRootToWorld(pw.root, goalRounded);
            Log($"P{pid} FALLBACK_DONE finalPos={Fmt(GetRootWorldPos(pw.root, pw.mover))}");

            if (debugLogs)
                LogActiveAnimSnapshot("ANIM_FALLBACK_DONE", pid, pw.mover, Vector2.zero, goalRounded);

            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        Log($"P{pid} PATH_OK nodes={path.Count} first={Fmt(path[0])} last={Fmt(path[path.Count - 1])}");

        for (int p = 1; p < path.Count; p++)
        {
            Log($"P{pid} STEP idx={p}/{path.Count - 1} target={Fmt(path[p])}");
            yield return MoveToTile(pw, path[p], tile);
        }

        SnapRootToWorld(pw.root, goalRounded);

        // >>> FORÇA FACING UP AO TERMINAR
        pw.mover.ApplyDirectionFromVector(Vector2.up);
        pw.mover.ApplyDirectionFromVector(Vector2.zero);
        // <<<

        Log($"P{pid} WALK_DONE finalPos={Fmt(GetRootWorldPos(pw.root, pw.mover))}");

        if (debugLogs)
            LogActiveAnimSnapshot("ANIM_WALK_DONE", pid, pw.mover, Vector2.zero, goalRounded);

        if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
        onDone?.Invoke();
    }

    private IEnumerator FallbackWalkTowards(PlayerWalkData pw, Vector2 goal, float tile)
    {
        if (pw.mover == null || pw.root == null)
            yield break;

        float maxTime = 10f;
        float t = 0f;

        while (t < maxTime)
        {
            if (GamePauseController.IsPaused)
            {
                pw.mover.ApplyDirectionFromVector(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetRootWorldPos(pw.root, pw.mover);
            Vector2 delta = goal - pos;

            if (delta.sqrMagnitude <= (reachEpsilon * reachEpsilon))
                yield break;

            Vector2 dir = PickCardinal(delta);
            pw.mover.ApplyDirectionFromVector(dir);

            // NEW: snapshot durante fallback
            if (debugLogs)
                LogActiveAnimSnapshot("ANIM_FALLBACK_STEP", pw.playerId, pw.mover, dir, pos);

            float step = walkSpeedUnitsPerSecond * Time.unscaledDeltaTime;
            Vector2 next = pos + dir * step;

            if (dir.x != 0f) next.y = Mathf.Round(next.y / tile) * tile;
            if (dir.y != 0f) next.x = Mathf.Round(next.x / tile) * tile;

            SetRootWorldPos(pw.root, next);

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private Vector2 GetOriginOffset(int playerId)
    {
        int idx = Mathf.Clamp(playerId - 1, 0, 3);
        if (perPlayerOriginOffset == null || perPlayerOriginOffset.Length < 4)
            return Vector2.zero;
        return perPlayerOriginOffset[idx];
    }

    private static Vector2 GetRootWorldPos(Transform root, MovementController move)
    {
        if (root == null && move != null)
        {
            if (move.Rigidbody != null) return move.Rigidbody.position;
            return (Vector2)move.transform.position;
        }

        if (root == null)
            return Vector2.zero;

        var rb = root.GetComponent<Rigidbody2D>();
        if (rb != null) return rb.position;

        return (Vector2)root.position;
    }

    private static void SetRootWorldPos(Transform root, Vector2 pos)
    {
        if (root == null)
            return;

        var rb = root.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;

            root.position = pos;
            return;
        }

        root.position = pos;
    }

    private static void SnapRootToWorld(Transform root, Vector2 pos)
    {
        SetRootWorldPos(root, pos);
    }

    private static Vector2 PickCardinal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private static Vector2 RoundToGrid(Vector2 p, float tile)
        => new Vector2(Mathf.Round(p.x / tile) * tile, Mathf.Round(p.y / tile) * tile);

    private static IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (!GamePauseController.IsPaused)
                t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"{LOG} {msg}");
    }

    private void LogHierarchyState(string tag, int playerId, PlayerIdentity id, MovementController move, Transform root)
    {
        if (!debugLogs) return;

        Vector2 moveT = move ? (Vector2)move.transform.position : Vector2.zero;
        Vector2 moveRb = (move && move.Rigidbody) ? move.Rigidbody.position : moveT;

        Vector2 idT = id ? (Vector2)id.transform.position : Vector2.zero;
        Vector2 idRoot = id ? (Vector2)id.transform.root.position : Vector2.zero;

        Vector2 rootT = root ? (Vector2)root.position : Vector2.zero;
        var rbRoot = root ? root.GetComponent<Rigidbody2D>() : null;
        Vector2 rootRb = rbRoot ? rbRoot.position : rootT;

        var moveRoot = move ? move.transform.root : null;
        Vector2 moveRootPos = moveRoot ? (Vector2)moveRoot.position : Vector2.zero;

        Debug.Log($"{LOG} {tag} P{playerId} " +
                  $"moveGO={move?.name} moveT={Fmt(moveT)} moveRb={Fmt(moveRb)} moveRoot={Fmt(moveRootPos)} " +
                  $"idGO={id?.name} idT={Fmt(idT)} idRoot={Fmt(idRoot)} " +
                  $"root={root?.name} rootT={Fmt(rootT)} rootRb={Fmt(rootRb)}");
    }

    private IEnumerator WatchForExternalTeleport(string tag, int pid, Transform t, Vector2 expected, float seconds)
    {
        if (!debugLogs || t == null)
            yield break;

        var rb = t.GetComponent<Rigidbody2D>();
        float end = Time.unscaledTime + Mathf.Max(0f, seconds);

        const float sqrThreshold = 0.01f * 0.01f;

        while (Time.unscaledTime < end)
        {
            Vector2 now = rb != null ? rb.position : (Vector2)t.position;

            if ((now - expected).sqrMagnitude > sqrThreshold)
            {
                Debug.Log($"{LOG} {tag} P{pid} EXTERNAL_MOVE_DETECTED now={Fmt(now)} expected={Fmt(expected)}");
                yield break;
            }

            yield return null;
        }
    }

    private void LogPlayerState(string tag, int playerId, MovementController move)
    {
        if (!debugLogs) return;
        if (!move)
        {
            Debug.Log($"{LOG} {tag} P{playerId} move=NULL");
            return;
        }

        Vector2 pos = (move.Rigidbody != null) ? move.Rigidbody.position : (Vector2)move.transform.position;
        bool rb = move.Rigidbody != null;
        bool sim = rb && move.Rigidbody.simulated;

        int enabledAnim = 0;
        int enabledSR = 0;

        var anims = move.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims != null)
        {
            for (int i = 0; i < anims.Length; i++)
                if (anims[i] != null && anims[i].enabled)
                    enabledAnim++;
        }

        var srs = move.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs != null)
        {
            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null && srs[i].enabled)
                    enabledSR++;
        }

        Debug.Log(
            $"{LOG} {tag} P{playerId} pos={Fmt(pos)} moveEnabled={move.enabled} inputLocked={move.InputLocked} dead={move.isDead} " +
            $"rb={(rb ? "YES" : "NO")} rbSim={(rb ? sim.ToString() : "NA")} timeScale={Time.timeScale:0.###} paused={GamePauseController.IsPaused} " +
            $"enabledAnimatedSpriteRenderers={enabledAnim} enabledSpriteRenderers={enabledSR}"
        );
    }

    // -------------------------------------------------------
    // NEW: LOG CIRÚRGICO DO ANIM RENDERER ATIVO
    // -------------------------------------------------------
    private void LogActiveAnimSnapshot(string tag, int playerId, MovementController move, Vector2 desiredDir, Vector2 pos)
    {
        if (!debugLogs) return;
        if (!move) { Debug.Log($"{LOG} {tag} P{playerId} move=NULL"); return; }

        AnimatedSpriteRenderer best = null;

        var anims = move.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims != null)
        {
            for (int i = 0; i < anims.Length; i++)
            {
                var a = anims[i];
                if (a == null || !a.enabled) continue;

                // “ativo de verdade” = componente ligado e SpriteRenderer ligado (se existir)
                if (a.TryGetComponent<SpriteRenderer>(out var sr) && sr != null && !sr.enabled)
                    continue;

                best = a;
                break;
            }
        }

        if (best == null)
        {
            Debug.Log($"{LOG} {tag} P{playerId} NO_ACTIVE_AnimatedSpriteRenderer dir={Fmt(desiredDir)} pos={Fmt(pos)}");
            return;
        }

        string srDesc;
        if (best.TryGetComponent<SpriteRenderer>(out var bestSr) && bestSr != null)
            srDesc = $"sr=Y sprite={(bestSr.sprite ? bestSr.sprite.name : "NULL")} enabled={bestSr.enabled} flipX={bestSr.flipX}";
        else
            srDesc = "sr=N";

        Debug.Log($"{LOG} {tag} P{playerId} ACTIVE_ANIM={best.name} enabled={best.enabled} idle={best.idle} loop={best.loop} pingPong={best.pingPong} " +
                  $"animTime={best.animationTime:0.###} useSeq={best.useSequenceDuration} seqDur={best.sequenceDuration:0.###} " +
                  $"curFrame={best.CurrentFrame} animLen={(best.animationSprite != null ? best.animationSprite.Length : 0)} " +
                  $"useUnscaled={best.UseUnscaledTime} respectPause={best.RespectGamePause} " +
                  $"{srDesc} dir={Fmt(desiredDir)} pos={Fmt(pos)}");
    }

    private static string Fmt(Vector2 v) => $"({v.x:0.###},{v.y:0.###})";

    private static void EnsureWalkLoopClipLoaded()
    {
        if (s_walkLoopClip != null)
            return;

        s_walkLoopClip = Resources.Load<AudioClip>(WalkLoopClipResourcesPath);
    }

    private void StartWalkLoopSfx()
    {
        EnsureWalkLoopClipLoaded();

        if (walkLoopSource == null)
            return;

        if (s_walkLoopClip == null)
            return;

        walkLoopSource.loop = false;
        walkLoopSource.volume = walkLoopVolume;

        if (walkSfxRoutine != null)
            return;

        walkSfxActive = true;
        nextWalkStepSfxAtUnscaled = Time.unscaledTime;
        walkSfxRoutine = StartCoroutine(WalkStepSfxLoop());
    }

    private void StopWalkLoopSfx()
    {
        walkSfxActive = false;

        if (walkSfxRoutine != null)
        {
            StopCoroutine(walkSfxRoutine);
            walkSfxRoutine = null;
        }

        if (walkLoopSource == null)
            return;

        if (walkLoopSource.isPlaying)
            walkLoopSource.Stop();
    }

    private IEnumerator WalkStepSfxLoop()
    {
        while (walkSfxActive)
        {
            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float now = Time.unscaledTime;
            if (now >= nextWalkStepSfxAtUnscaled)
            {
                if (walkLoopSource != null && s_walkLoopClip != null)
                {
                    walkLoopSource.Stop();
                    walkLoopSource.PlayOneShot(s_walkLoopClip, walkLoopVolume);
                }

                nextWalkStepSfxAtUnscaled = now + WalkStepSfxIntervalSeconds;
            }

            yield return null;
        }
    }
}