using System.Collections.Generic;
using UnityEngine;

public sealed class LouieEggQueue : MonoBehaviour
{
    [Header("History (Time Based)")]
    [SerializeField, Range(10, 500)] private int maxHistory = 160;
    [SerializeField, Range(0.005f, 0.5f)] private float historyPointSpacingWorld = 0.06f;
    [SerializeField, Range(0.00001f, 0.05f)] private float jitterIgnoreDelta = 0.0005f;

    [Header("Egg Spacing (World Units)")]
    [SerializeField, Range(0.05f, 5f)] private float eggSpacingWorld = 1f;

    [Header("Follow (WORLD)")]
    [SerializeField] private Vector2 worldOffset = new(0f, -0.15f);

    [Header("Follow - Anti Collapse")]
    [SerializeField, Range(0.0001f, 0.5f)] private float minTargetSeparation = 0.06f;

    [Header("World Root")]
    [SerializeField] private string worldRootName = "EggQueueWorldRoot";

    [Header("Layer")]
    [SerializeField] private int eggGameObjectLayer = 3;

    [Header("Sorting (SpriteRenderer)")]
    [SerializeField] private string eggSortingLayerName = "Default";
    [SerializeField] private int eggBaseSortingOrder = 2;

    [Header("Queue")]
    [SerializeField, Range(0, 10)] private int maxEggsInQueue = 5;
    public int MaxEggs => Mathf.Max(0, maxEggsInQueue);
    public bool IsFull => MaxEggs > 0 && _eggs.Count >= MaxEggs;
    public int Count => _eggs.Count;

    [Header("Queue - Join/Shift Animation")]
    [SerializeField, Range(0.01f, 1f)] private float joinSeconds = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float joinExtraDelayPerEgg = 0.05f;
    [SerializeField, Range(0.01f, 1f)] private float shiftSeconds = 0.35f;

    [Header("Egg Visual (Prefab)")]
    [SerializeField] private GameObject eggFollowerPrefab;

    [Header("Idle - Dequeue Style Follow")]
    [SerializeField] private bool idleDequeueStyle = true;
    [SerializeField] private bool idleDequeueSnap = false;

    [Header("Idle - Enter Delay")]
    [SerializeField, Range(0f, 0.5f)] private float idleEnterSeconds = 0.18f;

    [SerializeField, Range(0f, 0.25f)] private float movingHoldSeconds = 0.06f;
    [SerializeField, Range(0, 30)] private int idleGraceFrames = 3;
    [SerializeField, Range(0, 30)] private int idleExitGraceFrames = 2;

    [Header("Idle - Collapse To Player")]
    [SerializeField, Range(0f, 2f)] private float idleCollapseSeconds = 0.35f;
    [SerializeField, Range(0.5f, 1f)] private float idleDisableAntiCollapseAt = 0.95f;

    [Header("Idle - Direction Stabilization")]
    [SerializeField, Range(1f, 20f)] private float idleShiftDirectionalDeadZoneMul = 6f;

    [Header("Owner Move Detection")]
    [SerializeField] private bool preferRigidbodyVelocityForIdle = true;
    [SerializeField, Range(0.000001f, 0.01f)] private float ownerVelocityEpsilon = 0.0001f;

    [Header("Mount SFX (Resources/Sounds)")]
    [SerializeField] private string blueLouieMountSfxName = "MountBlueLouie";
    [SerializeField] private string blackLouieMountSfxName = "MountBlackLouie";
    [SerializeField] private string purpleLouieMountSfxName = "MountPurpleLouie";
    [SerializeField] private string greenLouieMountSfxName = "MountGreenLouie";
    [SerializeField] private string yellowLouieMountSfxName = "MountYellowLouie";
    [SerializeField] private string pinkLouieMountSfxName = "MountPinkLouie";
    [SerializeField] private string redLouieMountSfxName = "MountRedLouie";
    [SerializeField, Range(0f, 1f)] private float defaultMountVolume = 1f;

    Transform _freezeAnchor;

    bool _hardFrozen;
    readonly List<Vector3> _hardFrozenEggWorld = new();
    readonly List<Vector2> _hardFrozenFacing = new();

    struct EggEntry
    {
        public ItemPickup.ItemType type;
        public Transform rootTr;
        public EggFollowerDirectionalVisual directional;

        public bool isAnimating;
        public float animStartTime;
        public float animDuration;
        public Vector3 animFromWorld;
    }

    readonly List<EggEntry> _eggs = new();

    Vector3[] _history;
    int _historyHead;
    int _historyCount;
    float _historyTimeCarry;

    Transform _ownerTr;
    Rigidbody2D _ownerRb;
    MovementController _ownerMove;

    Transform _worldRoot;

    Vector3 _lastOwnerPos;
    bool _hasLastOwnerPos;

    Vector3 _lastRealOwnerPos;
    bool _hasLastRealOwnerPos;

    bool _hasLastMoveDir;
    Vector3 _lastMoveDirWorld = Vector3.down;

    int _idleFrames;
    int _movingFrames;

    bool _useIdleShiftState;
    float _idleShiftStartTime;

    float _lastMoveTime;
    bool _wasMovingPrevFrame;

    bool _forcedHidden;

    readonly Dictionary<ItemPickup.ItemType, AudioClip> _mountSfxCache = new();

    int _ownerPlayerId = -1;

    void OnValidate()
    {
        maxHistory = Mathf.Clamp(maxHistory, 10, 500);
        maxEggsInQueue = Mathf.Clamp(maxEggsInQueue, 0, 10);

        historyPointSpacingWorld = Mathf.Clamp(historyPointSpacingWorld, 0.005f, 0.5f);
        jitterIgnoreDelta = Mathf.Clamp(jitterIgnoreDelta, 0.00001f, 0.05f);
        eggSpacingWorld = Mathf.Clamp(eggSpacingWorld, 0.05f, 5f);

        joinSeconds = Mathf.Clamp(joinSeconds, 0.01f, 1f);
        joinExtraDelayPerEgg = Mathf.Clamp(joinExtraDelayPerEgg, 0f, 0.5f);
        shiftSeconds = Mathf.Clamp(shiftSeconds, 0.01f, 1f);

        minTargetSeparation = Mathf.Clamp(minTargetSeparation, 0.0001f, 0.5f);

        idleGraceFrames = Mathf.Clamp(idleGraceFrames, 0, 30);
        idleExitGraceFrames = Mathf.Clamp(idleExitGraceFrames, 0, 30);
        idleEnterSeconds = Mathf.Clamp(idleEnterSeconds, 0f, 0.5f);
        movingHoldSeconds = Mathf.Clamp(movingHoldSeconds, 0f, 0.25f);

        idleCollapseSeconds = Mathf.Clamp(idleCollapseSeconds, 0f, 2f);
        idleDisableAntiCollapseAt = Mathf.Clamp(idleDisableAntiCollapseAt, 0.5f, 1f);

        idleShiftDirectionalDeadZoneMul = Mathf.Clamp(idleShiftDirectionalDeadZoneMul, 1f, 20f);
        ownerVelocityEpsilon = Mathf.Clamp(ownerVelocityEpsilon, 0.000001f, 0.01f);

        defaultMountVolume = Mathf.Clamp01(defaultMountVolume);
    }

    void Awake()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetRuntimeState();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void OnEnable()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetRuntimeState();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void ResetRuntimeState()
    {
        _historyTimeCarry = 0f;

        _hasLastOwnerPos = false;
        _hasLastRealOwnerPos = false;

        _idleFrames = 0;
        _movingFrames = 0;

        _useIdleShiftState = false;
        _idleShiftStartTime = 0f;

        _lastMoveTime = Time.time;
        _wasMovingPrevFrame = false;
    }

    public void BindOwner(MovementController ownerMove)
    {
        if (ownerMove == null)
            return;

        bool ownerChanged = _ownerMove != ownerMove;

        _ownerMove = ownerMove;
        _ownerTr = ownerMove.transform;
        _ownerRb = _ownerMove.Rigidbody != null ? _ownerMove.Rigidbody : ownerMove.GetComponent<Rigidbody2D>();

        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (ownerChanged || _historyCount == 0)
        {
            SeedHistoryNow();
            ResetRuntimeState();
        }

        ApplyEggLayerNow();
        ApplyEggSortingNow();
        CacheOwnerIdentity();
    }

    void BindOwnerAuto()
    {
        if (_hardFrozen)
            return;

        _ownerMove = GetComponentInParent<MovementController>();
        if (_ownerMove != null)
        {
            _ownerTr = _ownerMove.transform;
            _ownerRb = _ownerMove.Rigidbody != null ? _ownerMove.Rigidbody : _ownerMove.GetComponent<Rigidbody2D>();
            CacheOwnerIdentity();
            return;
        }

        _ownerRb = GetComponentInParent<Rigidbody2D>();
        _ownerTr = _ownerRb != null ? _ownerRb.transform : transform.root;
        CacheOwnerIdentity();
    }

    void EnsureWorldRoot()
    {
        if (_worldRoot == null)
        {
            var existing = GameObject.Find(worldRootName);
            _worldRoot = existing != null ? existing.transform : new GameObject(worldRootName).transform;
        }

        if (_worldRoot.parent != null)
            _worldRoot.SetParent(null, true);

        _worldRoot.position = Vector3.zero;
        _worldRoot.rotation = Quaternion.identity;
        _worldRoot.localScale = Vector3.one;
    }

    void EnsureHistoryBuffer()
    {
        maxHistory = Mathf.Clamp(maxHistory, 10, 500);

        if (_history == null || _history.Length != maxHistory)
        {
            _history = new Vector3[maxHistory];
            _historyHead = 0;
            _historyCount = 0;
            _historyTimeCarry = 0f;

            _hasLastRealOwnerPos = false;
        }
    }

    Vector3 GetOwnerWorldPos()
    {
        if (_ownerTr != null)
            return _ownerTr.position;

        if (_ownerRb != null)
            return _ownerRb.position;

        return transform.position;
    }

    float GetOwnerWorldSpeedPerSecond()
    {
        if (_ownerMove != null)
            return Mathf.Max(0.01f, _ownerMove.speed * _ownerMove.tileSize);

        return 5f;
    }

    bool IsOwnerMoving(Vector3 ownerPosWorld, bool movedByPositionThisFrame)
    {
        bool movingNow = false;

        if (preferRigidbodyVelocityForIdle)
        {
            Rigidbody2D rb = _ownerRb;
            if (_ownerMove != null && _ownerMove.Rigidbody != null)
                rb = _ownerMove.Rigidbody;

            if (rb != null)
                movingNow = rb.linearVelocity.sqrMagnitude > ownerVelocityEpsilon;
        }

        if (!movingNow)
            movingNow = movedByPositionThisFrame;

        if (movingNow)
            _lastMoveTime = Time.time;

        float hold = Mathf.Max(0f, movingHoldSeconds);
        if (hold > 0f && (Time.time - _lastMoveTime) <= hold)
            return true;

        return movingNow;
    }

    Vector3 GetFallbackBehindDir()
    {
        Vector3 dir = Vector3.down;

        if (_ownerMove != null)
        {
            Vector2 face = _ownerMove.FacingDirection;
            if (face != Vector2.zero)
                dir = new Vector3(face.x, face.y, 0f);
        }

        if (_hasLastMoveDir && _lastMoveDirWorld.sqrMagnitude > 0.000001f)
            dir = _lastMoveDirWorld;

        dir.z = 0f;
        if (dir.sqrMagnitude < 0.000001f)
            dir = Vector3.down;

        dir.Normalize();
        return -dir;
    }

    void SeedHistoryNow()
    {
        EnsureHistoryBuffer();

        _historyHead = 0;
        _historyCount = 0;
        _historyTimeCarry = 0f;

        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        float spacing = Mathf.Max(0.0001f, historyPointSpacingWorld);
        Vector3 behind = GetFallbackBehindDir();

        for (int i = 0; i < maxHistory; i++)
        {
            Vector3 sample = p + behind * (spacing * i);
            sample.z = 0f;
            RecordHistory(sample);
        }

        _lastRealOwnerPos = p;
        _hasLastRealOwnerPos = true;

        _lastMoveTime = Time.time;
    }

    void ResetHistoryToCurrentOwnerPos()
    {
        EnsureHistoryBuffer();

        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        for (int i = 0; i < _history.Length; i++)
            _history[i] = p;

        _historyHead = 0;
        _historyCount = _history.Length;
        _historyTimeCarry = 0f;

        _lastRealOwnerPos = p;
        _hasLastRealOwnerPos = true;
    }

    void RecordHistory(Vector3 p)
    {
        p.z = 0f;

        _history[_historyHead] = p;
        _historyHead = (_historyHead + 1) % _history.Length;
        _historyCount = Mathf.Min(_historyCount + 1, _history.Length);
    }

    Vector3 GetRecentHistory(int recentIndex)
    {
        if (_historyCount <= 0)
            return GetOwnerWorldPos();

        if (recentIndex < 0) recentIndex = 0;
        if (recentIndex >= _historyCount) recentIndex = _historyCount - 1;

        int idx = _historyHead - 1 - recentIndex;
        while (idx < 0) idx += _history.Length;
        idx %= _history.Length;

        return _history[idx];
    }

    void TrackOwnerPositionSpeedBased(bool isMoving)
    {
        if (!isMoving)
            return;

        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        if (!_hasLastRealOwnerPos)
        {
            _lastRealOwnerPos = p;
            _hasLastRealOwnerPos = true;
            RecordHistory(p);
            _historyTimeCarry = 0f;
            return;
        }

        Vector3 fromReal = _lastRealOwnerPos;
        Vector3 toReal = p;

        Vector3 seg = toReal - fromReal;
        seg.z = 0f;

        float segLen = seg.magnitude;
        if (segLen > 0.000001f)
        {
            Vector3 d = seg / segLen;
            d.z = 0f;

            if (d.sqrMagnitude > 0.000001f)
            {
                _lastMoveDirWorld = d.normalized;
                _hasLastMoveDir = true;
            }
        }

        _lastRealOwnerPos = p;

        float spacing = Mathf.Max(0.0001f, historyPointSpacingWorld);
        float speed = Mathf.Max(0.000001f, GetOwnerWorldSpeedPerSecond());
        float secondsPerPoint = Mathf.Max(0.000001f, spacing / speed);

        _historyTimeCarry += Time.deltaTime;

        while (_historyTimeCarry >= secondsPerPoint)
        {
            _historyTimeCarry -= secondsPerPoint;
            RecordHistory(p);
        }
    }

    Vector3 SampleBackDistance(float backDistanceWorld)
    {
        Vector3 head = GetOwnerWorldPos();
        head.z = 0f;

        if (_historyCount <= 0)
            return head;

        float remaining = Mathf.Max(0f, backDistanceWorld);

        Vector3 prev = head;

        for (int k = 0; k < _historyCount; k++)
        {
            Vector3 pt = GetRecentHistory(k);
            pt.z = 0f;

            float segLen = Vector3.Distance(prev, pt);
            if (segLen <= 0.000001f)
            {
                prev = pt;
                continue;
            }

            if (segLen >= remaining)
            {
                float t = remaining / segLen;
                Vector3 res = Vector3.Lerp(prev, pt, t);
                res.z = 0f;
                return res;
            }

            remaining -= segLen;
            prev = pt;
        }

        prev.z = 0f;
        return prev;
    }

    float GetIdleCollapseT()
    {
        if (!_useIdleShiftState)
            return 0f;

        float secs = Mathf.Max(0f, idleCollapseSeconds);
        if (secs <= 0.000001f)
            return 1f;

        return Mathf.Clamp01((Time.time - _idleShiftStartTime) / secs);
    }

    public void BeginHardFreeze()
    {
        _hardFrozen = true;

        StopAllAnimationsNow();

        _hardFrozenEggWorld.Clear();
        _hardFrozenFacing.Clear();

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];

            Vector3 pos = Vector3.zero;
            if (e.rootTr != null)
            {
                pos = e.rootTr.position;
                pos.z = 0f;
            }

            Vector2 face = Vector2.down;
            if (e.directional != null)
            {
                face = e.directional.facing;
                if (face == Vector2.zero)
                    face = Vector2.down;

                e.directional.ForceIdleFacing(face);
            }

            _hardFrozenEggWorld.Add(pos);
            _hardFrozenFacing.Add(face);
        }

        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    public void EndHardFreezeAndRebind(MovementController owner)
    {
        _hardFrozen = false;

        _hardFrozenEggWorld.Clear();
        _hardFrozenFacing.Clear();

        if (owner != null)
            BindOwner(owner);
        else
            BindOwnerAuto();

        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetRuntimeState();

        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    public void EndHardFreezeAndKeepWorld(Vector3 worldPos)
    {
        if (!_hardFrozen)
            BeginHardFreeze();

        FreezeOwnerAtWorldPosition(worldPos);

        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void ApplyHardFreezeFrame()
    {
        int n = _eggs.Count;

        if (_hardFrozenEggWorld.Count != n || _hardFrozenFacing.Count != n)
        {
            BeginHardFreeze();
            n = _eggs.Count;
        }

        for (int i = 0; i < n; i++)
        {
            var e = _eggs[i];

            if (e.rootTr != null)
            {
                Vector3 p = _hardFrozenEggWorld[i];
                p.z = 0f;
                e.rootTr.position = p;
            }

            if (e.directional != null)
            {
                Vector2 f = _hardFrozenFacing[i];
                if (f == Vector2.zero)
                    f = Vector2.down;

                e.directional.ForceIdleFacing(f);
            }

            e.isAnimating = false;
            e.animDuration = 0f;

            _eggs[i] = e;
        }
    }

    void LateUpdate()
    {
        ApplyEggLayerNow();
        ApplyEggSortingNow();

        if (_hardFrozen)
        {
            ApplyHardFreezeFrame();
            return;
        }

        if (_ownerTr == null && _ownerRb == null)
        {
            BindOwnerAuto();
            if (_ownerTr == null && _ownerRb == null)
                return;
        }

        EnsureWorldRoot();
        EnsureHistoryBuffer();

        Vector3 ownerPos = GetOwnerWorldPos();
        ownerPos.z = 0f;

        bool movedByPosThisFrame = false;
        if (_hasLastOwnerPos)
        {
            float j = jitterIgnoreDelta;
            movedByPosThisFrame = (ownerPos - _lastOwnerPos).sqrMagnitude > (j * j);
        }

        bool isMoving = IsOwnerMoving(ownerPos, movedByPosThisFrame);

        if (_wasMovingPrevFrame && !isMoving)
            ResetHistoryToCurrentOwnerPos();

        _wasMovingPrevFrame = isMoving;

        _lastOwnerPos = ownerPos;
        _hasLastOwnerPos = true;

        if (!isMoving) { _idleFrames++; _movingFrames = 0; }
        else { _movingFrames++; _idleFrames = 0; }

        TrackOwnerPositionSpeedBased(isMoving);

        float followSpeed = GetOwnerWorldSpeedPerSecond();
        float maxStep = followSpeed * Time.deltaTime;

        Vector3 behindDir = GetFallbackBehindDir();

        bool eligibleIdleByTime = (Time.time - _lastMoveTime) >= Mathf.Max(0f, idleEnterSeconds);
        bool wantIdleShift = idleDequeueStyle && _eggs.Count > 0 && !isMoving && eligibleIdleByTime;

        bool prevUseIdleShift = _useIdleShiftState;

        if (!wantIdleShift)
        {
            _useIdleShiftState = false;
        }
        else
        {
            if (!_useIdleShiftState)
            {
                if (_idleFrames >= Mathf.Max(0, idleGraceFrames))
                    _useIdleShiftState = true;
            }
            else
            {
                if (_movingFrames > Mathf.Max(0, idleExitGraceFrames))
                    _useIdleShiftState = false;
            }
        }

        bool useIdleShift = _useIdleShiftState;

        if (!prevUseIdleShift && useIdleShift)
            _idleShiftStartTime = Time.time;

        if (prevUseIdleShift && !useIdleShift)
            _idleShiftStartTime = 0f;

        float idleCollapseT = useIdleShift ? GetIdleCollapseT() : 0f;
        bool allowOverlapOnPlayer = useIdleShift && idleCollapseT >= idleDisableAntiCollapseAt;

        float minSep = Mathf.Max(0.0001f, minTargetSeparation);
        float minSepSqr = minSep * minSep;

        Vector3 prevTarget = Vector3.positiveInfinity;
        bool hasPrevTarget = false;

        for (int i = _eggs.Count - 1; i >= 0; i--)
        {
            var e = _eggs[i];

            if (e.rootTr == null)
                continue;

            int ageRank = (_eggs.Count - i);
            float backDist = eggSpacingWorld * ageRank;

            float effectiveBackDist = useIdleShift ? Mathf.Lerp(backDist, 0f, idleCollapseT) : backDist;

            Vector3 targetWorld = SampleBackDistance(effectiveBackDist);
            targetWorld += (Vector3)worldOffset;
            targetWorld.z = 0f;

            if (!allowOverlapOnPlayer && hasPrevTarget)
            {
                if ((targetWorld - prevTarget).sqrMagnitude <= minSepSqr)
                {
                    targetWorld = prevTarget + behindDir * minSep;
                    targetWorld.z = 0f;
                }
            }

            Vector3 before = e.rootTr.position;
            before.z = 0f;

            Vector3 newWorld;

            if (e.isAnimating)
            {
                float elapsed = Time.time - e.animStartTime;
                float u = e.animDuration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / e.animDuration);
                float smoothU = u * u * (3f - 2f * u);

                Vector3 from = e.animFromWorld; from.z = 0f;
                Vector3 to = targetWorld; to.z = 0f;

                newWorld = Vector3.Lerp(from, to, smoothU);
                newWorld.z = 0f;

                if (u >= 1f)
                {
                    e.isAnimating = false;
                    e.animDuration = 0f;
                }
            }
            else
            {
                if (useIdleShift && idleDequeueSnap)
                    newWorld = targetWorld;
                else
                    newWorld = Vector3.MoveTowards(before, targetWorld, maxStep);

                newWorld.z = 0f;
            }

            e.rootTr.position = newWorld;

            if (e.directional != null)
            {
                float dz = Mathf.Max(0.00000001f, e.directional.moveDeadZone);
                float dzMul = useIdleShift ? idleShiftDirectionalDeadZoneMul : 1f;
                float effectiveDz = dz * dzMul;
                float effectiveDzSqr = effectiveDz * effectiveDz;

                Vector3 dirSample = useIdleShift ? (targetWorld - before) : (newWorld - before);
                dirSample.z = 0f;

                bool wouldBeMoving = dirSample.sqrMagnitude > effectiveDzSqr;

                e.directional.ApplyMoveDelta(wouldBeMoving ? dirSample : Vector3.zero);
            }

            _eggs[i] = e;

            prevTarget = targetWorld;
            hasPrevTarget = true;
        }
    }

    void StartAnimateToTargetNow(int eggIndex, float duration)
    {
        if (eggIndex < 0 || eggIndex >= _eggs.Count)
            return;

        var e = _eggs[eggIndex];
        if (e.rootTr == null)
            return;

        e.isAnimating = true;
        e.animStartTime = Time.time;
        e.animDuration = Mathf.Max(0.01f, duration);

        Vector3 p = e.rootTr.position;
        p.z = 0f;
        e.animFromWorld = p;

        _eggs[eggIndex] = e;
    }

    void AnimateAllShift()
    {
        float dur = Mathf.Max(0.01f, shiftSeconds);

        for (int i = 0; i < _eggs.Count; i++)
            StartAnimateToTargetNow(i, dur);
    }

    void AnimateShiftExceptNewest()
    {
        float dur = Mathf.Max(0.01f, shiftSeconds);

        for (int i = 1; i < _eggs.Count; i++)
            StartAnimateToTargetNow(i, dur);
    }

    AudioClip LoadMountSfx(ItemPickup.ItemType eggType)
    {
        if (_mountSfxCache.TryGetValue(eggType, out var cached) && cached != null)
            return cached;

        string name = eggType switch
        {
            ItemPickup.ItemType.BlueLouieEgg => blueLouieMountSfxName,
            ItemPickup.ItemType.BlackLouieEgg => blackLouieMountSfxName,
            ItemPickup.ItemType.PurpleLouieEgg => purpleLouieMountSfxName,
            ItemPickup.ItemType.GreenLouieEgg => greenLouieMountSfxName,
            ItemPickup.ItemType.YellowLouieEgg => yellowLouieMountSfxName,
            ItemPickup.ItemType.PinkLouieEgg => pinkLouieMountSfxName,
            ItemPickup.ItemType.RedLouieEgg => redLouieMountSfxName,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            _mountSfxCache[eggType] = null;
            return null;
        }

        var clip = Resources.Load<AudioClip>($"Sounds/{name}");
        _mountSfxCache[eggType] = clip;
        return clip;
    }

    public bool TryEnqueue(ItemPickup.ItemType type, Sprite idleSprite, AudioClip mountSfx, float mountVolume)
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (MaxEggs > 0 && _eggs.Count >= MaxEggs)
            return false;

        _idleFrames = 0;
        _movingFrames = 0;
        _useIdleShiftState = false;
        _idleShiftStartTime = 0f;

        EnqueueInternal(type, idleSprite, animate: true);
        ApplyEggLayerNow();
        ApplyEggSortingNow();
        return true;
    }

    public bool TryDequeue(out ItemPickup.ItemType type, out AudioClip mountSfx, out float mountVolume)
    {
        type = default;
        mountSfx = null;
        mountVolume = 0f;

        if (_eggs.Count == 0)
            return false;

        int lastIndex = _eggs.Count - 1;
        var oldestClosest = _eggs[lastIndex];
        _eggs.RemoveAt(lastIndex);

        if (oldestClosest.rootTr != null)
            Destroy(oldestClosest.rootTr.gameObject);

        type = oldestClosest.type;

        mountSfx = LoadMountSfx(type);
        mountVolume = Mathf.Clamp01(defaultMountVolume);

        AnimateAllShift();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
        return true;
    }

    public void GetQueuedEggTypesOldestToNewest(List<ItemPickup.ItemType> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (_eggs.Count == 0)
            return;

        for (int i = _eggs.Count - 1; i >= 0; i--)
            buffer.Add(_eggs[i].type);
    }

    public void RestoreQueuedEggTypesOldestToNewest(IReadOnlyList<ItemPickup.ItemType> types, Sprite idleSpriteFallback = null)
    {
        ClearAllEggs();

        if (types == null || types.Count == 0)
            return;

        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        ResetHistoryToCurrentOwnerPos();
        ResetRuntimeState();

        for (int i = 0; i < types.Count; i++)
            EnqueueInternal(types[i], idleSpriteFallback, animate: false);

        StopAllAnimationsNow();
        SnapAllToOwnerNow();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void ClearAllEggs()
    {
        for (int i = 0; i < _eggs.Count; i++)
        {
            if (_eggs[i].rootTr != null)
                Destroy(_eggs[i].rootTr.gameObject);
        }

        _eggs.Clear();
    }

    void EnqueueInternal(ItemPickup.ItemType type, Sprite idleSprite, bool animate)
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (MaxEggs > 0 && _eggs.Count >= MaxEggs)
            return;

        _idleFrames = 0;
        _movingFrames = 0;
        _useIdleShiftState = false;
        _idleShiftStartTime = 0f;

        Vector3 spawnWorld = GetOwnerWorldPos();
        spawnWorld.z = 0f;
        spawnWorld += (Vector3)worldOffset;
        spawnWorld.z = 0f;

        Transform rootTr;
        EggFollowerDirectionalVisual directional = null;

        if (eggFollowerPrefab != null)
        {
            var rootGo = Instantiate(eggFollowerPrefab, spawnWorld, Quaternion.identity, _worldRoot);
            rootGo.name = $"EggFollower_{type}";
            rootTr = rootGo.transform;

            if (!rootGo.TryGetComponent<EggQueueFollowerHitbox>(out var hitbox))
                hitbox = rootGo.GetComponentInChildren<EggQueueFollowerHitbox>(true);

            if (hitbox != null)
                hitbox.Bind(this);

            EnsureEggLayer(rootTr);

            directional = rootGo.GetComponent<EggFollowerDirectionalVisual>();
            if (directional == null)
                directional = rootGo.GetComponentInChildren<EggFollowerDirectionalVisual>(true);

            if (directional != null)
                directional.ForceIdleFacing(Vector2.down);
        }
        else
        {
            var rootGo = new GameObject($"EggFollower_{type}");
            rootTr = rootGo.transform;
            rootTr.SetParent(_worldRoot, true);
            rootTr.localScale = Vector3.one;
            rootTr.position = spawnWorld;

            EnsureEggLayer(rootTr);

            var sr = rootGo.AddComponent<SpriteRenderer>();
            sr.sprite = idleSprite;
            sr.enabled = true;
        }

        float durJoin = animate
            ? Mathf.Max(0.01f, joinSeconds) + Mathf.Max(0f, joinExtraDelayPerEgg) * _eggs.Count
            : 0f;

        var entry = new EggEntry
        {
            type = type,
            rootTr = rootTr,
            directional = directional,
            isAnimating = animate,
            animStartTime = Time.time,
            animDuration = durJoin,
            animFromWorld = spawnWorld
        };

        _eggs.Insert(0, entry);

        if (animate)
            AnimateShiftExceptNewest();
        else
            StopAllAnimationsNow();

        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void StopAllAnimationsNow()
    {
        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            e.isAnimating = false;
            e.animDuration = 0f;
            _eggs[i] = e;
        }
    }

    void SnapAllToOwnerNow()
    {
        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;
        p += (Vector3)worldOffset;
        p.z = 0f;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            e.rootTr.position = p;

            if (e.directional != null)
                e.directional.ApplyMoveDelta(Vector3.zero);

            _eggs[i] = e;
        }
    }

    void EnsureEggLayer(Transform root)
    {
        if (root == null)
            return;

        int layer = Mathf.Clamp(eggGameObjectLayer, 0, 31);
        SetLayerRecursively(root, layer);
    }

    void SetLayerRecursively(Transform t, int layer)
    {
        if (t == null)
            return;

        if (t.gameObject.layer != layer)
            t.gameObject.layer = layer;

        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    void EnsureEggSorting(Transform root, int sortingOrder)
    {
        if (root == null)
            return;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (!string.IsNullOrEmpty(eggSortingLayerName))
                sr.sortingLayerName = eggSortingLayerName;

            sr.sortingOrder = sortingOrder;
        }
    }

    void ApplyEggLayerNow()
    {
        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr != null)
                EnsureEggLayer(e.rootTr);
        }
    }

    bool ShouldInvertSortingForUp()
    {
        if (_eggs.Count != 2)
            return false;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.directional != null && e.directional.IsPlayingUpAnimation)
                return true;
        }

        return false;
    }

    void ApplyEggSortingNow()
    {
        int baseOrder = eggBaseSortingOrder;

        bool invertForUp = ShouldInvertSortingForUp();

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            int sortingOrder;

            if (invertForUp && _eggs.Count == 2)
                sortingOrder = (i == 0) ? baseOrder + 1 : baseOrder;
            else
                sortingOrder = baseOrder + i;

            EnsureEggSorting(e.rootTr, sortingOrder);
        }
    }

    public void ForceVisible(bool visible)
    {
        _forcedHidden = !visible;
        ApplyForcedVisibility();
    }

    public void RebindAndReseedNow(bool resetHistoryToOwnerNow)
    {
        if (_hardFrozen)
            return;

        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (resetHistoryToOwnerNow)
            ResetHistoryToCurrentOwnerPos();
        else
            SeedHistoryNow();

        ResetRuntimeState();
        ApplyForcedVisibility();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
    }

    void ApplyForcedVisibility()
    {
        bool active = !_forcedHidden;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null) continue;

            if (e.rootTr.gameObject.activeSelf != active)
                e.rootTr.gameObject.SetActive(active);
        }
    }

    public void TransferToDetachedLouieAndFreeze(GameObject detachedLouie, Vector3 freezeWorldPos)
    {
        if (detachedLouie == null)
            return;

        if (_eggs.Count == 0)
            return;

        var target = detachedLouie.GetComponent<LouieEggQueue>();
        if (target == null)
            target = detachedLouie.AddComponent<LouieEggQueue>();

        CopySettingsTo(target);

        target.EnsureWorldRoot();
        target.EnsureHistoryBuffer();

        target._eggs.Clear();
        for (int i = 0; i < _eggs.Count; i++)
            target._eggs.Add(_eggs[i]);

        _eggs.Clear();

        target._ownerPlayerId = -1;
        target.FreezeOwnerAtWorldPosition(freezeWorldPos);
        target.BeginHardFreeze();
        target.ApplyForcedVisibility();
        target.ApplyEggLayerNow();
        target.ApplyEggSortingNow();

        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetRuntimeState();
        ApplyForcedVisibility();
    }

    void CopySettingsTo(LouieEggQueue q)
    {
        q.maxHistory = maxHistory;
        q.historyPointSpacingWorld = historyPointSpacingWorld;
        q.jitterIgnoreDelta = jitterIgnoreDelta;

        q.eggSpacingWorld = eggSpacingWorld;
        q.worldOffset = worldOffset;
        q.minTargetSeparation = minTargetSeparation;

        q.worldRootName = worldRootName;

        q.eggGameObjectLayer = eggGameObjectLayer;
        q.eggSortingLayerName = eggSortingLayerName;
        q.eggBaseSortingOrder = eggBaseSortingOrder;

        q.maxEggsInQueue = maxEggsInQueue;

        q.joinSeconds = joinSeconds;
        q.joinExtraDelayPerEgg = joinExtraDelayPerEgg;
        q.shiftSeconds = shiftSeconds;

        q.eggFollowerPrefab = eggFollowerPrefab;

        q.idleDequeueStyle = idleDequeueStyle;
        q.idleDequeueSnap = idleDequeueSnap;

        q.idleEnterSeconds = idleEnterSeconds;
        q.movingHoldSeconds = movingHoldSeconds;
        q.idleGraceFrames = idleGraceFrames;
        q.idleExitGraceFrames = idleExitGraceFrames;

        q.idleCollapseSeconds = idleCollapseSeconds;
        q.idleDisableAntiCollapseAt = idleDisableAntiCollapseAt;
        q.idleShiftDirectionalDeadZoneMul = idleShiftDirectionalDeadZoneMul;

        q.preferRigidbodyVelocityForIdle = preferRigidbodyVelocityForIdle;
        q.ownerVelocityEpsilon = ownerVelocityEpsilon;

        q.blueLouieMountSfxName = blueLouieMountSfxName;
        q.blackLouieMountSfxName = blackLouieMountSfxName;
        q.purpleLouieMountSfxName = purpleLouieMountSfxName;
        q.greenLouieMountSfxName = greenLouieMountSfxName;
        q.yellowLouieMountSfxName = yellowLouieMountSfxName;
        q.pinkLouieMountSfxName = pinkLouieMountSfxName;
        q.redLouieMountSfxName = redLouieMountSfxName;
        q.defaultMountVolume = defaultMountVolume;
    }

    public void RequestDestroyEgg(Transform anyTransformOnEgg)
    {
        if (anyTransformOnEgg == null) return;
        if (_eggs.Count == 0) return;

        int idx = -1;
        for (int i = 0; i < _eggs.Count; i++)
        {
            var rt = _eggs[i].rootTr;
            if (rt == null) continue;

            if (rt == anyTransformOnEgg || anyTransformOnEgg.IsChildOf(rt))
            {
                idx = i;
                break;
            }
        }

        if (idx < 0) return;

        var e = _eggs[idx];
        var tr = e.rootTr;

        _eggs.RemoveAt(idx);

        if (_hardFrozen)
        {
            if (idx < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(idx);
            if (idx < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(idx);
        }

        AnimateAllShift();
        ApplyEggLayerNow();
        ApplyEggSortingNow();

        if (tr == null) return;

        StartCoroutine(DestroyEggRoutine(tr, 0.5f));
    }

    System.Collections.IEnumerator DestroyEggRoutine(Transform tr, float seconds)
    {
        if (tr == null) yield break;

        if (!tr.TryGetComponent<EggFollowerDestroyVisual>(out var v))
            v = tr.GetComponentInChildren<EggFollowerDestroyVisual>(true);

        if (v != null)
            v.PlayDestroy();

        if (!tr.TryGetComponent<Collider2D>(out var col))
            col = tr.GetComponentInChildren<Collider2D>(true);

        if (col != null)
            col.enabled = false;

        yield return new WaitForSeconds(seconds);

        if (tr != null)
            Destroy(tr.gameObject);
    }

    public bool RequestConsumeEggForMountCollision(Transform anyTransformOnEgg, GameObject consumerPlayer)
    {
        if (anyTransformOnEgg == null) return false;
        if (consumerPlayer == null) return false;
        if (_eggs.Count == 0) return false;

        if (_ownerPlayerId == -1)
            CacheOwnerIdentity();

        int consumerId = ResolvePlayerIdFrom(consumerPlayer);
        if (_ownerPlayerId != -1 && consumerId != -1 && _ownerPlayerId == consumerId)
            return false;

        var rider = consumerPlayer.GetComponent<PlayerRidingController>();
        bool ridingPlaying = rider != null && rider.IsPlaying;

        bool mountedByMovement = consumerPlayer.TryGetComponent<MovementController>(out var mv) && mv != null && mv.IsMountedOnLouie;
        bool mountedByCompanion = consumerPlayer.TryGetComponent<PlayerLouieCompanion>(out var compCheck) && compCheck != null && compCheck.HasMountedLouie();
        bool isMounted = mountedByMovement || mountedByCompanion;

        if (ridingPlaying) return false;
        if (isMounted) return false;

        if (!consumerPlayer.TryGetComponent<PlayerLouieCompanion>(out var comp) || comp == null)
            return false;

        int idx = FindEggIndexByTransform(anyTransformOnEgg);
        if (idx < 0)
            return false;

        var egg = _eggs[idx];
        var eggType = egg.type;

        RemoveEggSilently(idx);

        var sfx = LoadMountSfx(eggType);
        if (sfx != null)
            comp.SetNextMountSfx(sfx, Mathf.Clamp01(defaultMountVolume));

        MountFromEggType(comp, eggType);

        return true;
    }

    int FindEggIndexByTransform(Transform anyTransformOnEgg)
    {
        for (int i = 0; i < _eggs.Count; i++)
        {
            var rt = _eggs[i].rootTr;
            if (rt == null) continue;

            if (rt == anyTransformOnEgg || anyTransformOnEgg.IsChildOf(rt))
                return i;
        }
        return -1;
    }

    static void MountFromEggType(PlayerLouieCompanion comp, ItemPickup.ItemType eggType)
    {
        if (comp == null) return;

        switch (eggType)
        {
            case ItemPickup.ItemType.BlueLouieEgg: comp.MountBlueLouie(); break;
            case ItemPickup.ItemType.BlackLouieEgg: comp.MountBlackLouie(); break;
            case ItemPickup.ItemType.PurpleLouieEgg: comp.MountPurpleLouie(); break;
            case ItemPickup.ItemType.GreenLouieEgg: comp.MountGreenLouie(); break;
            case ItemPickup.ItemType.YellowLouieEgg: comp.MountYellowLouie(); break;
            case ItemPickup.ItemType.PinkLouieEgg: comp.MountPinkLouie(); break;
            case ItemPickup.ItemType.RedLouieEgg: comp.MountRedLouie(); break;
        }
    }

    int ResolvePlayerIdFrom(GameObject go)
    {
        if (go == null) return -1;

        if (go.TryGetComponent<PlayerIdentity>(out var id) && id != null)
            return id.playerId;

        var parentId = go.GetComponentInParent<PlayerIdentity>();
        if (parentId != null)
            return parentId.playerId;

        if (go.TryGetComponent<MovementController>(out var mv) && mv != null)
            return mv.PlayerId;

        var mvParent = go.GetComponentInParent<MovementController>();
        if (mvParent != null)
            return mvParent.PlayerId;

        return -1;
    }

    void CacheOwnerIdentity()
    {
        if (_ownerMove != null)
            _ownerPlayerId = ResolvePlayerIdFrom(_ownerMove.gameObject);
        else if (_ownerTr != null)
            _ownerPlayerId = ResolvePlayerIdFrom(_ownerTr.gameObject);
        else
            _ownerPlayerId = ResolvePlayerIdFrom(gameObject);

        if (_ownerPlayerId < 1 || _ownerPlayerId > 4)
            _ownerPlayerId = -1;
    }

    void RemoveEggSilently(int idx)
    {
        if (idx < 0 || idx >= _eggs.Count)
            return;

        var e = _eggs[idx];
        var tr = e.rootTr;

        _eggs.RemoveAt(idx);

        if (_hardFrozen)
        {
            if (idx < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(idx);
            if (idx < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(idx);
        }

        AnimateAllShift();
        ApplyEggLayerNow();
        ApplyEggSortingNow();

        if (tr != null)
            Destroy(tr.gameObject);
    }

    public void FreezeOwnerAtWorldPosition(Vector3 worldPos)
    {
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        worldPos.z = 0f;

        if (_freezeAnchor == null)
        {
            var go = new GameObject("EggQueue_FreezeAnchor");
            _freezeAnchor = go.transform;
        }

        _freezeAnchor.SetParent(null, true);
        _freezeAnchor.SetPositionAndRotation(worldPos, Quaternion.identity);
        _freezeAnchor.localScale = Vector3.one;

        _ownerMove = null;
        _ownerRb = null;
        _ownerTr = _freezeAnchor;

        ResetHistoryToCurrentOwnerPos();
        ResetRuntimeState();
    }
}
