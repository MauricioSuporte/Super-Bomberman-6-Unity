using System.Collections.Generic;
using UnityEngine;

public sealed class LouieEggQueue : MonoBehaviour
{
    [Header("History (Distance Based)")]
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

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Player";
    [SerializeField] private int eggSortingOrder = 3;

    [Header("Layer")]
    [SerializeField] private int eggGameObjectLayer = 3;

    [Header("Queue")]
    [SerializeField, Range(0, 10)] private int maxEggsInQueue = 5;
    public int MaxEggs => Mathf.Max(0, maxEggsInQueue);
    public bool IsFull => MaxEggs > 0 && _eggs.Count >= MaxEggs;

    [Header("Queue - Join/Shift Animation")]
    [SerializeField, Range(0.01f, 1f)] private float joinSeconds = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float joinExtraDelayPerEgg = 0.05f;
    [SerializeField, Range(0.01f, 1f)] private float shiftSeconds = 0.35f;

    [Header("Egg Visual (Prefab)")]
    [Tooltip("Prefab do EggFollower (com filhos Up/Down/Left/Right + EggFollowerDirectionalVisual). Se definido, a animação fica sempre a mesma independente do tipo do ovo.")]
    [SerializeField] private GameObject eggFollowerPrefab;

    [Header("Idle - Dequeue Style Follow")]
    [SerializeField] private bool idleDequeueStyle = true;

    [Tooltip("Se true, no modo idle o ovo encaixa no slot alvo no mesmo frame (teleporta). Se false, usa MoveTowards normal.")]
    [SerializeField] private bool idleDequeueSnap = false;

    [Header("Idle - Enter Delay (IMPORTANT)")]
    [Tooltip("Só liga o modo de colapsar no player depois que o player ficar realmente parado por este tempo (evita colapsar a cada tile).")]
    [SerializeField, Range(0f, 0.5f)] private float idleEnterSeconds = 0.18f;

    [Tooltip("Mantém o estado 'movendo' por um curto tempo após detectar movimento (suaviza micro-pausas).")]
    [SerializeField, Range(0f, 0.25f)] private float movingHoldSeconds = 0.06f;

    [Header("Idle - Hysteresis")]
    [SerializeField, Range(0, 30)] private int idleGraceFrames = 3;

    [Tooltip("Quantos frames de 'movimento' são necessários para sair do idleShift (evita oscilar liga/desliga).")]
    [SerializeField, Range(0, 30)] private int idleExitGraceFrames = 2;

    [Header("Idle - Collapse To Player")]
    [Tooltip("Tempo (segundos) para os ovos 'colapsarem' do espaçamento normal até chegar no player quando ele fica idle. 0 = instantâneo.")]
    [SerializeField, Range(0f, 2f)] private float idleCollapseSeconds = 0.35f;

    [Tooltip("A partir deste fator (0..1), o anti-collapse é desativado para permitir todos no mesmo tile do player.")]
    [SerializeField, Range(0.5f, 1f)] private float idleDisableAntiCollapseAt = 0.95f;

    [Header("Idle - Direction Stabilization")]
    [Tooltip("Multiplicador do deadzone do directional durante idleShift (reduz troca Left/Up/Down por deltas minúsculos).")]
    [SerializeField, Range(1f, 20f)] private float idleShiftDirectionalDeadZoneMul = 6f;

    [Header("Owner Move Detection")]
    [SerializeField] private bool preferRigidbodyVelocityForIdle = true;

    [Tooltip("Velocidade mínima (sqrMagnitude) para considerar que está andando quando usamos Rigidbody2D.velocity.")]
    [SerializeField, Range(0.000001f, 0.01f)] private float ownerVelocityEpsilon = 0.0001f;

    [Header("History - When Owner Stops")]
    [Tooltip("Se false, NÃO avança o histórico artificialmente quando o player fica parado (evita ovos colarem no player durante idle curto).")]
    [SerializeField] private bool advanceHistoryWhenIdle = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField, Range(0.05f, 5f)] private float debugLogEverySeconds = 0.5f;
    [SerializeField] private bool debugOnlyWhenIdleIssue = true;

    [Header("Debug - Directional Visual Issue")]
    [SerializeField] private bool debugDirectionalWhenIdleShift = true;
    [SerializeField] private bool debugDirectionalOnlyOnChange = true;
    [SerializeField] private bool debugDirectionalIncludeTargets = true;

    struct EggEntry
    {
        public ItemPickup.ItemType type;

        public Transform rootTr;

        public EggFollowerDirectionalVisual directional;
        public SpriteRenderer[] allSpriteRenderers;

        public AnimatedSpriteRenderer legacyAnim;
        public SpriteRenderer legacySr;

        public bool isAnimating;
        public float animStartTime;
        public float animDuration;
        public Vector3 animFromWorld;

        public bool hasLastPos;
        public Vector3 lastPosWorld;

        public AudioClip mountSfx;
        public float mountVolume;

        public bool hasLastDirDebug;
        public bool lastIdleApplied;
        public string lastActiveRendererName;
    }

    readonly List<EggEntry> _eggs = new();

    Vector3[] _history;
    int _historyHead;
    int _historyCount;

    Transform _ownerTr;
    Rigidbody2D _ownerRb;
    MovementController _ownerMove;

    Transform _worldRoot;

    float _distanceCarry;

    float _nextDebugTime;
    Vector3 _lastOwnerPos;
    bool _hasLastOwnerPos;

    int _idleFrames;
    int _movingFrames;

    bool _hasLastMoveDir;
    Vector3 _lastMoveDirWorld = Vector3.down;

    bool _hasLastRealOwnerPos;
    Vector3 _lastRealOwnerPos;

    bool _lastUseIdleShift;
    int _lastIdleShiftEggCount;

    bool _useIdleShiftState;
    float _idleShiftStartTime;

    float _lastMoveTime;

    public int Count => _eggs.Count;

    void OnValidate()
    {
        maxHistory = Mathf.Clamp(maxHistory, 10, 500);
        maxEggsInQueue = Mathf.Clamp(maxEggsInQueue, 0, 10);

        joinSeconds = Mathf.Clamp(joinSeconds, 0.01f, 1f);
        joinExtraDelayPerEgg = Mathf.Clamp(joinExtraDelayPerEgg, 0f, 0.5f);
        shiftSeconds = Mathf.Clamp(shiftSeconds, 0.01f, 1f);

        minTargetSeparation = Mathf.Clamp(minTargetSeparation, 0.0001f, 0.5f);

        historyPointSpacingWorld = Mathf.Clamp(historyPointSpacingWorld, 0.005f, 0.5f);
        jitterIgnoreDelta = Mathf.Clamp(jitterIgnoreDelta, 0.00001f, 0.05f);
        eggSpacingWorld = Mathf.Clamp(eggSpacingWorld, 0.05f, 5f);

        debugLogEverySeconds = Mathf.Clamp(debugLogEverySeconds, 0.05f, 5f);
        idleGraceFrames = Mathf.Clamp(idleGraceFrames, 0, 30);
        idleExitGraceFrames = Mathf.Clamp(idleExitGraceFrames, 0, 30);
        idleShiftDirectionalDeadZoneMul = Mathf.Clamp(idleShiftDirectionalDeadZoneMul, 1f, 20f);

        idleCollapseSeconds = Mathf.Clamp(idleCollapseSeconds, 0f, 2f);
        idleDisableAntiCollapseAt = Mathf.Clamp(idleDisableAntiCollapseAt, 0.5f, 1f);

        ownerVelocityEpsilon = Mathf.Clamp(ownerVelocityEpsilon, 0.000001f, 0.01f);

        idleEnterSeconds = Mathf.Clamp(idleEnterSeconds, 0f, 0.5f);
        movingHoldSeconds = Mathf.Clamp(movingHoldSeconds, 0f, 0.25f);
    }

    void Awake()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetDebugState();
    }

    void OnEnable()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow();
        ResetDebugState();
    }

    void ResetDebugState()
    {
        _nextDebugTime = 0f;
        _hasLastOwnerPos = false;
        _idleFrames = 0;
        _movingFrames = 0;
        _useIdleShiftState = false;
        _idleShiftStartTime = 0f;

        _lastUseIdleShift = false;
        _lastIdleShiftEggCount = 0;

        _lastMoveTime = Time.time;
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
            _idleFrames = 0;
            _movingFrames = 0;
            _useIdleShiftState = false;
            _idleShiftStartTime = 0f;
            _lastMoveTime = Time.time;

            if (debugLogs)
                Debug.Log($"[LouieEggQueue] BindOwner: ownerChanged={ownerChanged} -> SeedHistoryNow()", this);
        }
    }

    void BindOwnerAuto()
    {
        _ownerMove = GetComponentInParent<MovementController>();
        if (_ownerMove != null)
        {
            _ownerTr = _ownerMove.transform;
            _ownerRb = _ownerMove.Rigidbody != null ? _ownerMove.Rigidbody : _ownerMove.GetComponent<Rigidbody2D>();
        }
        else
        {
            _ownerRb = GetComponentInParent<Rigidbody2D>();
            _ownerTr = _ownerRb != null ? _ownerRb.transform : transform.root;
        }
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
            _distanceCarry = 0f;

            _hasLastRealOwnerPos = false;
        }
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
        _distanceCarry = 0f;

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

    float GetIdleCollapseT()
    {
        if (!_useIdleShiftState)
            return 0f;

        float secs = Mathf.Max(0f, idleCollapseSeconds);
        if (secs <= 0.000001f)
            return 1f;

        return Mathf.Clamp01((Time.time - _idleShiftStartTime) / secs);
    }

    void LateUpdate()
    {
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
            movedByPosThisFrame = (ownerPos - _lastOwnerPos).sqrMagnitude > (jitterIgnoreDelta * jitterIgnoreDelta);

        bool isMoving = IsOwnerMoving(ownerPos, movedByPosThisFrame);

        _lastOwnerPos = ownerPos;
        _hasLastOwnerPos = true;

        if (!isMoving)
        {
            _idleFrames++;
            _movingFrames = 0;
        }
        else
        {
            _movingFrames++;
            _idleFrames = 0;
        }

        int headBefore = _historyHead;
        int countBefore = _historyCount;
        float carryBefore = _distanceCarry;

        bool wroteHistory = TrackOwnerPositionDistanceBased(isMoving);

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

        if (debugLogs && Time.time >= _nextDebugTime)
        {
            _nextDebugTime = Time.time + debugLogEverySeconds;

            bool shouldLog =
                !debugOnlyWhenIdleIssue ||
                (!isMoving && _idleFrames >= 10);

            if (shouldLog)
            {
                string ownerName = _ownerTr != null ? _ownerTr.name : (_ownerRb != null ? _ownerRb.name : "none");
                float spacing = Mathf.Max(0.0001f, historyPointSpacingWorld);
                float virtualAdvance = GetOwnerWorldSpeedPerSecond() * Time.deltaTime;

                Debug.Log(
                    $"[LouieEggQueue] Tick owner={ownerName} eggs={_eggs.Count} idle={!isMoving} " +
                    $"useIdleShift={useIdleShift} idleFrames={_idleFrames} movingFrames={_movingFrames} " +
                    $"history(wrote={wroteHistory}, head {headBefore}->{_historyHead}, count {countBefore}->{_historyCount}) " +
                    $"carry {carryBefore:0.0000}->{_distanceCarry:0.0000} spacing={spacing:0.0000} vAdv={virtualAdvance:0.0000} " +
                    $"maxStep={maxStep:0.0000} idleCollapseT={idleCollapseT:0.00} overlap={allowOverlapOnPlayer}",
                    this
                );
            }
        }

        if (debugLogs && debugDirectionalWhenIdleShift)
        {
            bool idleShiftJustChanged = (useIdleShift != _lastUseIdleShift) || (_lastIdleShiftEggCount != _eggs.Count);
            if (idleShiftJustChanged)
            {
                Debug.Log(
                    $"[LouieEggQueue] IdleShiftState changed -> useIdleShift={useIdleShift} eggs={_eggs.Count} idleFrames={_idleFrames}",
                    this
                );

                _lastUseIdleShift = useIdleShift;
                _lastIdleShiftEggCount = _eggs.Count;
            }
        }

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            EnsureEggLayer(e.rootTr);

            if (e.allSpriteRenderers != null && e.allSpriteRenderers.Length > 0)
            {
                for (int r = 0; r < e.allSpriteRenderers.Length; r++)
                {
                    var srAny = e.allSpriteRenderers[r];
                    if (srAny == null) continue;

                    srAny.sortingLayerName = sortingLayerName;
                    srAny.sortingOrder = eggSortingOrder;
                }
            }

            if (e.legacySr != null)
            {
                e.legacySr.sortingLayerName = sortingLayerName;
                e.legacySr.sortingOrder = eggSortingOrder;
                e.legacySr.enabled = true;
            }

            _eggs[i] = e;
        }

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

            Vector3 targetWorld;

            if (useIdleShift)
            {
                float collapsedDist = Mathf.Lerp(backDist, 0f, idleCollapseT);
                targetWorld = SampleBackDistance(collapsedDist);
                targetWorld += (Vector3)worldOffset;
                targetWorld.z = 0f;
            }
            else
            {
                targetWorld = SampleBackDistance(backDist);
                targetWorld += (Vector3)worldOffset;
                targetWorld.z = 0f;
            }

            Vector3 before = e.rootTr.position;
            before.z = 0f;

            if (!e.hasLastPos)
            {
                e.hasLastPos = true;
                e.lastPosWorld = before;
            }

            bool usedAntiCollapseAdjust = false;

            if (!allowOverlapOnPlayer)
            {
                if (hasPrevTarget && (targetWorld - prevTarget).sqrMagnitude <= minSepSqr)
                {
                    targetWorld = prevTarget + behindDir * minSep;
                    targetWorld.z = 0f;
                    usedAntiCollapseAdjust = true;
                }
            }

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

            Vector3 delta = newWorld - before;
            delta.z = 0f;

            if (e.directional != null)
            {
                float dz = Mathf.Max(0.00000001f, e.directional.moveDeadZone);
                float dzMul = useIdleShift ? idleShiftDirectionalDeadZoneMul : 1f;
                float effectiveDz = dz * dzMul;
                float effectiveDzSqr = effectiveDz * effectiveDz;

                Vector3 dirSample = useIdleShift ? (targetWorld - before) : delta;
                dirSample.z = 0f;

                bool wouldBeMoving = dirSample.sqrMagnitude > effectiveDzSqr;

                if (!wouldBeMoving)
                    e.directional.ApplyMoveDelta(Vector3.zero);
                else
                    e.directional.ApplyMoveDelta(dirSample);

                if (debugLogs && debugDirectionalWhenIdleShift && useIdleShift)
                {
                    bool shouldInspect = Time.time >= _nextDebugTime || !debugOnlyWhenIdleIssue || !isMoving;
                    if (shouldInspect)
                    {
                        string activeName = GetActiveDirectionalRendererName(e.directional);
                        bool changed = !e.hasLastDirDebug ||
                                       e.lastIdleApplied != !wouldBeMoving ||
                                       (activeName != e.lastActiveRendererName);

                        if (!debugDirectionalOnlyOnChange || changed)
                        {
                            string ownerName = _ownerTr != null ? _ownerTr.name : (_ownerRb != null ? _ownerRb.name : "none");
                            string tgtPart = "";

                            if (debugDirectionalIncludeTargets)
                            {
                                Vector3 toTgt = targetWorld - before; toTgt.z = 0f;
                                tgtPart =
                                    $" target={targetWorld.x:0.00},{targetWorld.y:0.00} " +
                                    $"toTarget={toTgt.x:0.00},{toTgt.y:0.00} " +
                                    $"antiCollapseAdj={usedAntiCollapseAdjust}";
                            }

                            Debug.Log(
                                $"[LouieEggQueue] IdleShiftAnim eggIndex={i} type={e.type} owner={ownerName} " +
                                $"delta={delta.x:0.000},{delta.y:0.000} stepMax={maxStep:0.000} " +
                                $"moving={wouldBeMoving} active={activeName} " +
                                $"prevActive={(e.hasLastDirDebug ? e.lastActiveRendererName : "none")}{tgtPart}",
                                this
                            );
                        }

                        e.hasLastDirDebug = true;
                        e.lastIdleApplied = !wouldBeMoving;
                        e.lastActiveRendererName = activeName;
                    }
                }
            }

            e.lastPosWorld = newWorld;
            _eggs[i] = e;

            prevTarget = targetWorld;
            hasPrevTarget = true;
        }
    }

    static string GetActiveDirectionalRendererName(EggFollowerDirectionalVisual dv)
    {
        if (dv == null)
            return "none";

        string name = "none";

        if (dv.up != null && dv.up.enabled) name = "Up";
        if (dv.down != null && dv.down.enabled) name = (name == "none" ? "Down" : name + "+Down");
        if (dv.left != null && dv.left.enabled) name = (name == "none" ? "Left" : name + "+Left");
        if (dv.right != null && dv.right.enabled) name = (name == "none" ? "Right" : name + "+Right");

        return name;
    }

    float GetOwnerWorldSpeedPerSecond()
    {
        if (_ownerMove != null)
            return Mathf.Max(0.01f, _ownerMove.speed * _ownerMove.tileSize);

        return 5f;
    }

    bool TrackOwnerPositionDistanceBased(bool isMoving)
    {
        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        if (!_hasLastRealOwnerPos)
        {
            _lastRealOwnerPos = p;
            _hasLastRealOwnerPos = true;

            RecordHistory(p);
            _distanceCarry = 0f;
            return true;
        }

        float jitterSqr = jitterIgnoreDelta * jitterIgnoreDelta;

        Vector3 fromReal = _lastRealOwnerPos;
        Vector3 toReal = p;

        Vector3 seg = toReal - fromReal;
        seg.z = 0f;

        float segLen = seg.magnitude;
        float spacing = Mathf.Max(0.0001f, historyPointSpacingWorld);

        if (segLen * segLen > jitterSqr && segLen > 0.000001f)
        {
            _lastMoveDirWorld = seg / segLen;
            _lastMoveDirWorld.z = 0f;
            _hasLastMoveDir = _lastMoveDirWorld.sqrMagnitude > 0.000001f;
            if (_hasLastMoveDir) _lastMoveDirWorld.Normalize();
        }

        if (segLen * segLen <= jitterSqr)
        {
            _lastRealOwnerPos = p;

            if (!advanceHistoryWhenIdle || !isMoving)
                return false;

            float virtualAdvance = GetOwnerWorldSpeedPerSecond() * Time.deltaTime;
            if (virtualAdvance <= 0.0000001f)
                return false;

            _distanceCarry += virtualAdvance;

            Vector3 behindDir = GetFallbackBehindDir();

            int wroteIdle = 0;
            while (_distanceCarry >= spacing)
            {
                _distanceCarry -= spacing;
                wroteIdle++;

                Vector3 sample = p + behindDir * (spacing * wroteIdle);
                sample.z = 0f;

                RecordHistory(sample);
            }

            return wroteIdle > 0;
        }

        float dist = _distanceCarry + segLen;

        if (dist < spacing)
        {
            _distanceCarry = dist;
            _lastRealOwnerPos = p;
            return false;
        }

        Vector3 dirMove = seg / segLen;
        float traveledFromLast = _distanceCarry;

        int wrote = 0;

        while (traveledFromLast + spacing <= segLen + 0.000001f)
        {
            traveledFromLast += spacing;
            Vector3 sample = fromReal + dirMove * traveledFromLast;
            sample.z = 0f;

            RecordHistory(sample);
            wrote++;
        }

        float leftover = segLen - traveledFromLast;
        _distanceCarry = Mathf.Max(0f, leftover);

        _lastRealOwnerPos = p;
        return wrote > 0;
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

    Vector3 GetOwnerWorldPos()
    {
        if (_ownerTr != null)
            return _ownerTr.position;

        if (_ownerRb != null)
            return _ownerRb.position;

        return transform.position;
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

    public bool TryEnqueue(ItemPickup.ItemType type, Sprite idleSprite)
    {
        return TryEnqueue(type, idleSprite, null, 1f);
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

        Vector3 spawnWorld = GetOwnerWorldPos();
        spawnWorld.z = 0f;
        spawnWorld += (Vector3)worldOffset;
        spawnWorld.z = 0f;

        Transform rootTr;
        EggFollowerDirectionalVisual directional = null;
        SpriteRenderer[] allRenderers = null;

        AnimatedSpriteRenderer legacyAnim = null;
        SpriteRenderer legacySr = null;

        if (eggFollowerPrefab != null)
        {
            var rootGo = Instantiate(eggFollowerPrefab, spawnWorld, Quaternion.identity, _worldRoot);
            rootGo.name = $"EggFollower_{type}";
            rootTr = rootGo.transform;

            EnsureEggLayer(rootTr);

            directional = rootGo.GetComponent<EggFollowerDirectionalVisual>();
            if (directional == null)
                directional = rootGo.GetComponentInChildren<EggFollowerDirectionalVisual>(true);

            allRenderers = rootGo.GetComponentsInChildren<SpriteRenderer>(true);

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

            var visualGo = new GameObject("Visual");
            var visualTr = visualGo.transform;
            visualTr.SetParent(rootTr, false);
            visualTr.localPosition = Vector3.zero;
            visualTr.localRotation = Quaternion.identity;
            visualTr.localScale = Vector3.one;

            EnsureEggLayer(rootTr);

            legacySr = visualGo.AddComponent<SpriteRenderer>();
            legacySr.sprite = idleSprite;
            legacySr.enabled = true;
            legacySr.sortingLayerName = sortingLayerName;
            legacySr.sortingOrder = eggSortingOrder;

            legacyAnim = visualGo.AddComponent<AnimatedSpriteRenderer>();
            legacyAnim.idleSprite = idleSprite;
            legacyAnim.idle = true;
            legacyAnim.loop = false;
            legacyAnim.pingPong = false;
            legacyAnim.allowFlipX = false;
            legacyAnim.enabled = true;
            legacyAnim.RefreshFrame();
        }

        float durJoin = Mathf.Max(0.01f, joinSeconds) + Mathf.Max(0f, joinExtraDelayPerEgg) * _eggs.Count;

        var entry = new EggEntry
        {
            type = type,
            rootTr = rootTr,

            directional = directional,
            allSpriteRenderers = allRenderers,

            legacyAnim = legacyAnim,
            legacySr = legacySr,

            mountSfx = mountSfx,
            mountVolume = Mathf.Clamp01(mountVolume),

            isAnimating = true,
            animStartTime = Time.time,
            animDuration = durJoin,
            animFromWorld = spawnWorld,

            hasLastPos = true,
            lastPosWorld = spawnWorld,

            hasLastDirDebug = false,
            lastIdleApplied = true,
            lastActiveRendererName = "none",
        };

        _eggs.Insert(0, entry);
        AnimateShiftExceptNewest();

        if (debugLogs)
            Debug.Log($"[LouieEggQueue] Enqueue type={type} eggsNow={_eggs.Count} spawn={spawnWorld}", this);

        return true;
    }

    public bool TryDequeue(out ItemPickup.ItemType type)
    {
        return TryDequeue(out type, out _, out _);
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
        mountSfx = oldestClosest.mountSfx;
        mountVolume = oldestClosest.mountVolume;

        AnimateAllShift();

        if (debugLogs)
            Debug.Log($"[LouieEggQueue] Dequeue type={type} eggsNow={_eggs.Count}", this);

        return true;
    }

    public void ClearAll()
    {
        for (int i = 0; i < _eggs.Count; i++)
        {
            if (_eggs[i].rootTr != null)
                Destroy(_eggs[i].rootTr.gameObject);
        }

        _eggs.Clear();

        _historyHead = 0;
        _historyCount = 0;
        _distanceCarry = 0f;

        _hasLastRealOwnerPos = false;

        _idleFrames = 0;
        _movingFrames = 0;
        _useIdleShiftState = false;
        _idleShiftStartTime = 0f;

        _lastMoveTime = Time.time;

        if (debugLogs)
            Debug.Log("[LouieEggQueue] ClearAll()", this);
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
}
