using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pré-intro do stage: teleporta os players para um ponto de origem e os move (A*) até o spawn final
/// resolvido pelo PlayersSpawner. Roda com Time.timeScale = 0 (usa unscaledDeltaTime).
///
/// Ajustes (2026-02-27):
/// - Move SEMPRE o "root" do player (PlayerIdentity.transform) para evitar caso onde o MovementController é filho
///   e o visual/root fica parado no spawn.
/// - Força Transform.position junto do Rigidbody2D.position quando existir (timeScale=0 não garante sync imediato).
/// - Adicionado PreSnapPlayersToOrigin(spawner) para "pré-teleport" enquanto a tela ainda está preta (fade alpha=1),
///   evitando mostrar o player no spawn antes de ir ao commonOrigin.
/// - Watch de teleporte externo não pode pegar a caminhada normal: agora só observa até ANTES do walk começar.
/// </summary>
[DisallowMultipleComponent]
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

    // watch: detectar "pisada" de outro sistema logo após o snap (somente antes do walk)
    [Header("Debug / Watch External Move")]
    [SerializeField, Min(0f)] private float watchAfterSnapSeconds = 0.35f;

    private readonly GridAStarPathfinder2D pathfinder = new();

    public bool IsEnabled => enabledPreIntro;

    private struct PlayerWalkData
    {
        public int playerId;
        public PlayerIdentity identity;
        public MovementController mover;
        public Transform root;
        public float tileSize;
    }

    // -------------------------
    // Public API
    // -------------------------

    /// <summary>
    /// Importante: chame isso enquanto a tela ainda está preta (fade alpha=1),
    /// para não aparecer o player no spawn antes de ir pro commonOrigin.
    /// Só faz o snap para o origin (não caminha).
    /// </summary>
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

            // snap no root
            SnapRootToWorld(root, originRounded);

            // zera RB do mover e do root
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

                // garante transform sync
                root.position = originRounded;
            }
        }
    }

    public IEnumerator Play(PlayersSpawner spawner)
    {
        Log($"PLAY_BEGIN enabledPreIntro={enabledPreIntro} spawner={(spawner ? spawner.name : "NULL")} commonOrigin={(commonOrigin ? commonOrigin.name : "NULL")} " +
            $"timeScale={Time.timeScale:0.###} paused={GamePauseController.IsPaused} t_unscaled={Time.unscaledTime:0.###}");

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

        // pega players vivos
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Log($"FOUND_PlayerIdentity count={(ids != null ? ids.Length : 0)}");

        if (ids == null || ids.Length == 0)
        {
            Log("PLAY_EARLY_EXIT reason=no_PlayerIdentity_found");
            yield break;
        }

        Vector2 commonOriginWorld = commonOrigin.position;
        Log($"ORIGIN commonOriginWorld={Fmt(commonOriginWorld)} commonOriginTransformPos={Fmt((Vector2)commonOrigin.position)} thisGO={name} thisPos={Fmt((Vector2)transform.position)}");

        // prepara lista
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

            // Root que realmente representa o player (o mesmo que o spawner costuma posicionar)
            Transform root = id.transform != null ? id.transform : move.transform;

            LogHierarchyState("HIER_BEFORE_SETUP", playerId, id, move, root);
            LogPlayerState("BEFORE_SETUP", playerId, move);

            // lock de input, mas deixa visível para a caminhada
            move.SetInputLocked(true, true);

            // manter enabled para poder aplicar direção/visual durante a caminhada
            move.enabled = true;

            // Em vez de ligar TUDO, volta pro modo exclusivo (1 sprite/animação por vez)
            move.EnableExclusiveFromState();

            // garante rigidbody utilizável (do MovementController)
            if (move.Rigidbody != null)
            {
                move.Rigidbody.simulated = true;
                move.Rigidbody.linearVelocity = Vector2.zero;
            }

            // move para origem
            Vector2 offset = GetOriginOffset(playerId);
            Vector2 originRaw = commonOriginWorld + offset;

            float tile = Mathf.Max(0.0001f, move.tileSize);
            Vector2 originRounded = RoundToGrid(originRaw, tile);

            Log($"P{playerId} ORIGIN_CALC common={Fmt(commonOriginWorld)} offset={Fmt(offset)} raw={Fmt(originRaw)} tile={tile:0.###} rounded={Fmt(originRounded)}");

            // SNAP NO ROOT
            SnapRootToWorld(root, originRounded);

            // zera RB do root se existir
            var rbRoot = root != null ? root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null)
            {
                rbRoot.simulated = true;
                rbRoot.linearVelocity = Vector2.zero;
                rbRoot.position = originRounded;

                // IMPORTANTÍSSIMO: força o Transform a refletir a posição mesmo com timeScale=0
                root.position = originRounded;
            }

            LogHierarchyState("HIER_AFTER_SNAP", playerId, id, move, root);

            Vector2 afterSnap = GetRootWorldPos(root, move);
            Log($"P{playerId} AFTER_SNAP rootPosNow={Fmt(afterSnap)} deltaToOrigin={Fmt(originRounded - afterSnap)} rootRB={(rbRoot ? "YES" : "NO")} rootRbSim={(rbRoot ? rbRoot.simulated.ToString() : "NA")}");

            // Watch: só faz sentido observar ANTES do walk começar (senão ele pega o movimento normal).
            float watchSeconds = 0f;
            if (debugLogs && watchAfterSnapSeconds > 0f)
            {
                // tenta observar apenas até o fim do "delayBeforeWalkSeconds" (menos uma folga)
                watchSeconds = Mathf.Min(watchAfterSnapSeconds, Mathf.Max(0f, delayBeforeWalkSeconds - 0.01f));
            }

            if (debugLogs && watchSeconds > 0f && root != null)
                StartCoroutine(WatchForExternalTeleport("WATCH_AFTER_SNAP", playerId, root, originRounded, watchSeconds));

            // checa depois de 1 frame (às vezes outro sistema pisa na posição)
            yield return null;

            Vector2 after1 = GetRootWorldPos(root, move);
            Log($"P{playerId} AFTER_1FRAME rootPosNow={Fmt(after1)} deltaFromOrigin={Fmt(after1 - originRounded)}");

            LogHierarchyState("HIER_AFTER_1FRAME", playerId, id, move, root);

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

        if (delayBeforeWalkSeconds > 0f)
        {
            Log($"WAIT_BEFORE seconds={delayBeforeWalkSeconds:0.###}");
            yield return WaitRealtime(delayBeforeWalkSeconds);
        }

        // Caminhar um por um (usando root como fonte de posição/movimento)
        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null || pw.root == null) continue;

            int pid = Mathf.Clamp(pw.playerId, 1, 4);

            Vector2 goal = spawner.GetResolvedSpawnPosition(pid);
            float tile = Mathf.Max(0.0001f, pw.tileSize);

            Vector2 start = RoundToGrid(GetRootWorldPos(pw.root, pw.mover), tile);
            Vector2 goalRounded = RoundToGrid(goal, tile);

            var usedMask = obstacleMask.value != 0 ? obstacleMask : pw.mover.obstacleMask;

            LogHierarchyState("HIER_BEFORE_WALK", pid, pw.identity, pw.mover, pw.root);

            Log($"P{pid} WALK_BEGIN start={Fmt(start)} goalRaw={Fmt(goal)} goalRounded={Fmt(goalRounded)} tile={tile:0.###} obstacleMask={(obstacleMask.value != 0 ? obstacleMask.value : pw.mover.obstacleMask.value)}");

            if (start == goalRounded)
            {
                Log($"P{pid} WALK_SKIP reason=start_equals_goal");
                continue;
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
                continue;
            }

            Log($"P{pid} PATH_OK nodes={path.Count} first={Fmt(path[0])} last={Fmt(path[path.Count - 1])}");

            for (int p = 1; p < path.Count; p++)
            {
                Log($"P{pid} STEP idx={p}/{path.Count - 1} target={Fmt(path[p])}");
                yield return MoveToTile(pw, path[p], tile);
            }

            SnapRootToWorld(pw.root, goalRounded);
            Log($"P{pid} WALK_DONE finalPos={Fmt(GetRootWorldPos(pw.root, pw.mover))}");
        }

        // Para a direção (idle)
        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null) continue;

            pw.mover.EnableExclusiveFromState();
            pw.mover.ApplyDirectionFromVector(Vector2.zero);

            if (pw.mover.Rigidbody != null) pw.mover.Rigidbody.linearVelocity = Vector2.zero;

            var rbRoot = pw.root != null ? pw.root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null) rbRoot.linearVelocity = Vector2.zero;

            LogPlayerState("AFTER_WALK_IDLE", pw.playerId, pw.mover);
            LogHierarchyState("HIER_AFTER_WALK_IDLE", pw.playerId, pw.identity, pw.mover, pw.root);
        }

        if (delayAfterWalkSeconds > 0f)
        {
            Log($"WAIT_AFTER seconds={delayAfterWalkSeconds:0.###}");
            yield return WaitRealtime(delayAfterWalkSeconds);
        }

        Log($"PLAY_END t_unscaled={Time.unscaledTime:0.###}");
    }

    // -------------------------
    // Movement using ROOT
    // -------------------------

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
                break;

            pw.mover.ApplyDirectionFromVector(desiredDir);

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
        yield return null;
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

            float step = walkSpeedUnitsPerSecond * Time.unscaledDeltaTime;
            Vector2 next = pos + dir * step;

            if (dir.x != 0f) next.y = Mathf.Round(next.y / tile) * tile;
            if (dir.y != 0f) next.x = Mathf.Round(next.x / tile) * tile;

            SetRootWorldPos(pw.root, next);

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // -------------------------
    // Origin offsets
    // -------------------------

    private Vector2 GetOriginOffset(int playerId)
    {
        int idx = Mathf.Clamp(playerId - 1, 0, 3);
        if (perPlayerOriginOffset == null || perPlayerOriginOffset.Length < 4)
            return Vector2.zero;
        return perPlayerOriginOffset[idx];
    }

    // -------------------------
    // Root positioning helpers
    // -------------------------

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

            // força o Transform a refletir mesmo com timeScale=0
            root.position = pos;
            return;
        }

        root.position = pos;
    }

    private static void SnapRootToWorld(Transform root, Vector2 pos)
    {
        SetRootWorldPos(root, pos);
    }

    // -------------------------
    // Small utils
    // -------------------------

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

    // -------------------------
    // Debug helpers
    // -------------------------

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

        // Threshold: qualquer coisa muito pequena pode ser flutuação.
        const float sqrThreshold = 0.01f * 0.01f; // 0.01 unidades

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

    private static string Fmt(Vector2 v) => $"({v.x:0.###},{v.y:0.###})";
}