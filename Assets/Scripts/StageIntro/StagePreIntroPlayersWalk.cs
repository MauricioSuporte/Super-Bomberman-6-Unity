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

    [Header("Entrance Pre-Intro (optional)")]
    [SerializeField] private bool enabledEntrancePreIntro = true;

    [Tooltip("Where players appear first (entrance cutscene start).")]
    [SerializeField] private Transform entranceOrigin;

    [Tooltip("Where players walk to and disappear (entrance gate).")]
    [SerializeField] private Transform entranceGate;

    [Tooltip("Camera used to show entrance cutscene.")]
    [SerializeField] private Camera entranceCamera;

    [Tooltip("Main stage camera (normal gameplay camera).")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("CameraFollowClamp2D attached to EntranceCamera. Used ONLY during transition to MainCamera.")]
    [SerializeField] private CameraFollowClamp2D entranceCameraFollow;

    [Tooltip("Seconds to keep players hidden after reaching EntranceGate (before teleport to commonOrigin).")]
    [SerializeField, Min(0f)] private float entranceGateHiddenSeconds = 1f;

    [Tooltip("How close EntranceCamera must be to MainCamera (XY) to swap cameras.")]
    [SerializeField, Min(0.001f)] private float cameraSwapEpsilon = 0.05f;

    [Tooltip("Safety timeout for camera transition.")]
    [SerializeField, Min(0.1f)] private float cameraTransitionMaxSeconds = 3.5f;

    [Tooltip("How long (seconds) the EntranceCamera moves to MainCamera before swapping.")]
    [SerializeField, Min(0.05f)] private float cameraTransitionSeconds = 1f;

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
    public bool EntranceEnabled => enabledEntrancePreIntro && entranceOrigin != null && entranceGate != null && entranceCamera != null && mainCamera != null;

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

        if (entranceCameraFollow != null)
            entranceCameraFollow.enabled = false;
    }

    public void PrepareEntranceCamerasForIntro()
    {
        if (!EntranceEnabled)
            return;

        SetCameraActive(entranceCamera, true);
        SetCameraActive(mainCamera, false);

        if (entranceCameraFollow != null)
            entranceCameraFollow.enabled = false;
    }

    public void PreSnapPlayersForSequence()
    {
        if (!enabledPreIntro)
            return;

        if (EntranceEnabled)
        {
            SnapPlayersToOrigin(entranceOrigin);
            return;
        }

        SnapPlayersToOrigin(commonOrigin);
    }

    private void SnapPlayersToOrigin(Transform originTransform)
    {
        if (originTransform == null)
            return;

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids == null || ids.Length == 0)
            return;

        Vector2 originWorld = originTransform.position;
        bool isEntrance = (entranceOrigin != null && originTransform == entranceOrigin);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (!id) continue;

            int playerId = Mathf.Clamp(id.playerId, 1, 4);

            if (!id.TryGetComponent(out MovementController move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move) continue;
            if (!move.CompareTag("Player")) continue;
            if (move.isDead) continue;

            Transform root = id.transform != null ? id.transform : move.transform;

            float tile = Mathf.Max(0.0001f, move.tileSize);

            Vector2 offset = GetOriginOffset(playerId);

            if (isEntrance)
            {
                float xTiles = playerId switch
                {
                    1 => 0f,
                    2 => 1f,
                    3 => -1f,
                    4 => 2f,
                    _ => 0f
                };

                offset += new Vector2(xTiles * tile, 0f);
            }

            Vector2 raw = originWorld + offset;
            Vector2 rounded = RoundToGrid(raw, tile);

            SnapRootToWorld(root, rounded);

            var queue = move.GetComponentInChildren<MountEggQueue>(true);
            if (queue != null)
            {
                queue.RebindAndReseedNow(resetHistoryToOwnerNow: true);
                queue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
            }

            if (move.Rigidbody != null)
            {
                move.Rigidbody.simulated = true;
                move.Rigidbody.linearVelocity = Vector2.zero;
                move.Rigidbody.position = rounded;
            }

            var rbRoot = root != null ? root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null)
            {
                rbRoot.simulated = true;
                rbRoot.linearVelocity = Vector2.zero;
                rbRoot.position = rounded;
                root.position = rounded;
            }
        }
    }

    public IEnumerator Play(PlayersSpawner spawner)
    {
        Log($"PLAY_BEGIN enabledPreIntro={enabledPreIntro} EntranceEnabled={EntranceEnabled} spawner={(spawner ? spawner.name : "NULL")}");

        StopWalkLoopSfx();

        if (!enabledPreIntro || spawner == null)
            yield break;

        if (!TryCollectAndSetupPlayers(out var players,
                out var cachedColliders,
                out var cachedColliderEnabled,
                out var cachedBombEnabled))
        {
            yield break;
        }

        players.Sort((a, b) => b.playerId.CompareTo(a.playerId));

        if (EntranceEnabled)
        {
            yield return RunEntrancePreIntro(players);

            SnapPlayersToOrigin(commonOrigin);

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].mover != null)
                    ShowPlayerVisualSpawnLike(players[i].mover);
            }

            StopWalkLoopSfx();

            yield return TransitionEntranceCameraToMain();
        }
        else
        {
            SnapPlayersToOrigin(commonOrigin);
        }

        if (delayBeforeWalkSeconds > 0f)
            yield return WaitRealtime(delayBeforeWalkSeconds);

        bool anyWillWalk = AnyPlayerNeedsWalk(players, spawner);
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

        RestorePlayersAfterWalk(players, cachedColliders, cachedColliderEnabled, cachedBombEnabled);

        if (delayAfterWalkSeconds > 0f)
            yield return WaitRealtime(delayAfterWalkSeconds);

        Log($"PLAY_END t_unscaled={Time.unscaledTime:0.###}");
    }

    private bool TryCollectAndSetupPlayers(
        out List<PlayerWalkData> players,
        out Dictionary<MovementController, Collider2D> cachedColliders,
        out Dictionary<MovementController, bool> cachedColliderEnabled,
        out Dictionary<MovementController, bool> cachedBombEnabled)
    {
        players = new List<PlayerWalkData>(4);
        cachedColliders = new Dictionary<MovementController, Collider2D>();
        cachedColliderEnabled = new Dictionary<MovementController, bool>();
        cachedBombEnabled = new Dictionary<MovementController, bool>();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids == null || ids.Length == 0)
            return false;

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

            move.SetExternalMovementOverride(true);
            move.SetInputLocked(true, true);

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

            if (move.TryGetComponent<BombController>(out var bc) && bc != null)
            {
                if (!cachedBombEnabled.ContainsKey(move))
                    cachedBombEnabled[move] = bc.enabled;

                bc.enabled = false;
            }

            move.enabled = true;
            move.EnableExclusiveFromState();

            if (move.Rigidbody != null)
            {
                move.Rigidbody.simulated = true;
                move.Rigidbody.linearVelocity = Vector2.zero;
            }

            float tile = Mathf.Max(0.0001f, move.tileSize);

            players.Add(new PlayerWalkData
            {
                playerId = playerId,
                identity = id,
                mover = move,
                root = root,
                tileSize = tile
            });
        }

        return players.Count > 0;
    }

    private IEnumerator RunEntrancePreIntro(List<PlayerWalkData> players)
    {
        SetCameraActive(entranceCamera, true);
        SetCameraActive(mainCamera, false);

        if (entranceCameraFollow != null)
            entranceCameraFollow.enabled = false;

        SnapPlayersToOrigin(entranceOrigin);

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].mover != null)
                ShowPlayerVisualSpawnLike(players[i].mover);
        }

        yield return null;

        var ordered = new List<PlayerWalkData>(players);
        ordered.Sort((a, b) => a.playerId.CompareTo(b.playerId));

        yield return WalkPlayersToCustomGoal(
            ordered,
            (pid) =>
            {
                Vector2 gate = entranceGate.position;
                return gate + GetOriginOffset(pid);
            },
            sequentialByPlayerId: true,
            stopSfxOnExit: false
        );

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].mover != null)
                HidePlayerVisual(players[i].mover);
        }

        if (entranceGateHiddenSeconds > 0f)
            yield return WaitRealtime(entranceGateHiddenSeconds);
    }

    private IEnumerator TransitionEntranceCameraToMain()
    {
        if (!EntranceEnabled || entranceCamera == null || mainCamera == null)
            yield break;

        if (entranceCameraFollow != null)
            entranceCameraFollow.enabled = false;

        Vector3 startPos = entranceCamera.transform.position;
        Vector3 endPos = mainCamera.transform.position;

        endPos.z = startPos.z;

        float duration = Mathf.Min(cameraTransitionSeconds, cameraTransitionMaxSeconds);
        duration = Mathf.Max(0.05f, duration);

        float t = 0f;

        while (t < duration)
        {
            if (!GamePauseController.IsPaused)
                t += Time.unscaledDeltaTime;

            float u = Mathf.Clamp01(t / duration);
            float s = u * u * (3f - 2f * u);

            entranceCamera.transform.position = Vector3.LerpUnclamped(startPos, endPos, s);
            yield return null;
        }

        entranceCamera.transform.position = endPos;

        SetCameraActive(entranceCamera, false);
        SetCameraActive(mainCamera, true);
    }

    private static void SetCameraActive(Camera cam, bool active)
    {
        if (cam == null) return;

        cam.enabled = active;

        if (cam.TryGetComponent<AudioListener>(out var al))
            al.enabled = active;
    }

    private IEnumerator WalkPlayersToCustomGoal(
        List<PlayerWalkData> players,
        System.Func<int, Vector2> goalByPlayerId,
        bool sequentialByPlayerId,
        bool stopSfxOnExit)
    {
        if (players == null || players.Count == 0)
            yield break;

        bool anyWillWalk = false;

        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null || pw.root == null) continue;

            int pid = Mathf.Clamp(pw.playerId, 1, 4);

            Vector2 goal = goalByPlayerId(pid);
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
            if (sequentialByPlayerId)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var pw = players[i];

                    if (pw.mover == null || pw.root == null)
                        continue;

                    yield return WalkSinglePlayerToGoal(
                        idx: i,
                        pw: pw,
                        goalRaw: goalByPlayerId(pw.playerId),
                        startDelay: 0f,
                        doneFlags: null,
                        onDone: null);

                    HidePlayerVisual(pw.mover);
                    yield return null;
                }

                yield break;
            }

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

                StartCoroutine(WalkSinglePlayerToGoal(idx, pw, goalByPlayerId(pw.playerId), startDelay, done, () => doneCount++));
            }

            while (doneCount < players.Count)
                yield return null;
        }
        finally
        {
            if (stopSfxOnExit)
                StopWalkLoopSfx();
        }
    }

    private IEnumerator WalkSinglePlayerToGoal(
        int idx,
        PlayerWalkData pw,
        Vector2 goalRaw,
        float startDelay,
        bool[] doneFlags,
        System.Action onDone)
    {
        if (startDelay > 0f)
            yield return WaitRealtime(startDelay);

        if (pw.mover == null || pw.root == null)
        {
            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        int pid = Mathf.Clamp(pw.playerId, 1, 4);

        float tile = Mathf.Max(0.0001f, pw.tileSize);

        Vector2 start = RoundToGrid(GetRootWorldPos(pw.root, pw.mover), tile);
        Vector2 goalRounded = RoundToGrid(goalRaw, tile);

        var usedMask = obstacleMask.value != 0 ? obstacleMask : pw.mover.obstacleMask;

        if (start == goalRounded)
        {
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
            yield return FallbackWalkTowards(pw, goalRounded, tile);
            SnapRootToWorld(pw.root, goalRounded);

            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        for (int p = 1; p < path.Count; p++)
            yield return MoveToTile(pw, path[p], tile);

        SnapRootToWorld(pw.root, goalRounded);
        pw.mover.ApplyDirectionFromVector(Vector2.zero);

        if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
        onDone?.Invoke();
    }

    private bool AnyPlayerNeedsWalk(List<PlayerWalkData> players, PlayersSpawner spawner)
    {
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
                return true;
        }

        return false;
    }

    private void RestorePlayersAfterWalk(
        List<PlayerWalkData> players,
        Dictionary<MovementController, Collider2D> cachedColliders,
        Dictionary<MovementController, bool> cachedColliderEnabled,
        Dictionary<MovementController, bool> cachedBombEnabled)
    {
        for (int i = 0; i < players.Count; i++)
        {
            var pw = players[i];
            if (pw.mover == null) continue;

            pw.mover.EnableExclusiveFromState();
            pw.mover.ApplyDirectionFromVector(Vector2.zero);

            if (pw.mover.Rigidbody != null) pw.mover.Rigidbody.linearVelocity = Vector2.zero;

            var rbRoot = pw.root != null ? pw.root.GetComponent<Rigidbody2D>() : null;
            if (rbRoot != null) rbRoot.linearVelocity = Vector2.zero;

            if (cachedColliders.TryGetValue(pw.mover, out var col) && col != null)
            {
                bool wasEnabled = true;
                if (cachedColliderEnabled.TryGetValue(pw.mover, out var prev))
                    wasEnabled = prev;

                col.enabled = wasEnabled;
            }

            if (pw.mover.TryGetComponent<BombController>(out var bc) && bc != null)
            {
                bool prev = true;
                if (cachedBombEnabled.TryGetValue(pw.mover, out var was))
                    prev = was;

                bc.enabled = prev;
            }

            pw.mover.SetExternalMovementOverride(false);
            pw.mover.SetInputLocked(false, false);
        }
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

        if (start == goalRounded)
        {
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
            yield return FallbackWalkTowards(pw, goalRounded, tile);
            SnapRootToWorld(pw.root, goalRounded);

            if (doneFlags != null && idx >= 0 && idx < doneFlags.Length) doneFlags[idx] = true;
            onDone?.Invoke();
            yield break;
        }

        for (int p = 1; p < path.Count; p++)
            yield return MoveToTile(pw, path[p], tile);

        SnapRootToWorld(pw.root, goalRounded);
        pw.mover.ApplyDirectionFromVector(Vector2.zero);

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

    private static void SnapRootToWorld(Transform root, Vector2 pos) => SetRootWorldPos(root, pos);

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

    private static void EnsureWalkLoopClipLoaded()
    {
        if (s_walkLoopClip != null)
            return;

        s_walkLoopClip = Resources.Load<AudioClip>(WalkLoopClipResourcesPath);
    }

    private void StartWalkLoopSfx()
    {
        EnsureWalkLoopClipLoaded();

        if (walkLoopSource == null) return;
        if (s_walkLoopClip == null) return;

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

        if (walkLoopSource != null && walkLoopSource.isPlaying)
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

    private static void ShowPlayerVisualSpawnLike(MovementController move)
    {
        if (move == null) return;

        var rider = move.GetComponentInChildren<MountVisualController>(true);
        if (rider != null)
        {
            rider.enabled = true;
            rider.Bind(move);
        }

        move.SetAllSpritesVisible(false);
        move.ApplyDirectionFromVector(Vector2.zero);
        move.EnableExclusiveFromState();

        SetEggQueueActive(move, true);

        if (move.Rigidbody != null)
            move.Rigidbody.linearVelocity = Vector2.zero;
    }

    private static void HidePlayerVisual(MovementController move)
    {
        if (move == null) return;

        move.ApplyDirectionFromVector(Vector2.zero);
        move.SetAllSpritesVisible(false);

        var rider = move.GetComponentInChildren<MountVisualController>(true);
        if (rider != null)
            rider.enabled = false;

        SetEggQueueActive(move, false);
    }

    private static void SetEggQueueActive(MovementController move, bool active)
    {
        if (move == null) return;

        var queue = move.GetComponentInChildren<MountEggQueue>(true);
        if (queue == null) return;

        if (!active)
        {
            queue.BeginHardFreeze();
            queue.ForceVisible(false);

            queue.enabled = false;
            return;
        }

        queue.enabled = true;

        queue.EndHardFreezeAndRebind(move);

        queue.ForceVisible(true);
        queue.RebindAndReseedNow(resetHistoryToOwnerNow: true);
        queue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
    }

    public void ForceMainCameraActive()
    {
        if (entranceCamera != null)
        {
            entranceCamera.enabled = false;
            if (entranceCamera.TryGetComponent<AudioListener>(out var al))
                al.enabled = false;
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            if (mainCamera.TryGetComponent<AudioListener>(out var al))
                al.enabled = true;
        }
    }
}