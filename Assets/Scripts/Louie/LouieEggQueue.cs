using System.Collections.Generic;
using UnityEngine;

public sealed class LouieEggQueue : MonoBehaviour
{
    [Header("History")]
    [SerializeField, Range(10, 500)] private int maxHistory = 100;
    [SerializeField, Range(1, 50)] private int stepPerEgg = 10;
    [SerializeField, Range(0.00001f, 0.05f)] private float recordMinDelta = 0.0005f;

    [Header("Follow (WORLD)")]
    [SerializeField, Range(0.01f, 1f)] private float followLerp = 0.45f;
    [SerializeField] private Vector2 worldOffset = new(0f, -0.15f);

    [Header("Follow - Anti Collapse")]
    [SerializeField, Range(0.0001f, 0.5f)] private float minTargetSeparation = 0.06f;

    [Header("World Root")]
    [SerializeField] private string worldRootName = "EggQueueWorldRoot";

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Player";
    [SerializeField] private int baseOrderInLayer = 50;

    [Header("Queue")]
    [SerializeField, Range(0, 10)] private int maxEggsInQueue = 3;
    public int MaxEggs => Mathf.Max(0, maxEggsInQueue);
    public bool IsFull => MaxEggs > 0 && _eggs.Count >= MaxEggs;

    [Header("Queue - Join Animation")]
    [SerializeField, Range(0.01f, 1f)] private float joinSeconds = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float joinExtraDelayPerEgg = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private float debugEverySeconds = 0.25f;
    float _nextDebug;

    [Header("Debug - Collapse Detection")]
    [SerializeField, Range(0.0001f, 0.5f)] private float collapseDistance = 0.03f;
    Vector3 _lastOwnerPosLogged;
    bool _hasLastOwnerPosLogged;

    struct EggEntry
    {
        public ItemPickup.ItemType type;
        public Transform rootTr;
        public Transform visualTr;
        public AnimatedSpriteRenderer anim;
        public SpriteRenderer sr;
        public int recentIndex;

        public bool isJoining;
        public float joinStartTime;
        public float joinDuration;
        public Vector3 joinFromWorld;
    }

    readonly List<EggEntry> _eggs = new();

    Vector3[] _history;
    int _historyHead;
    int _historyCount;

    Transform _ownerTr;
    Rigidbody2D _ownerRb;
    MovementController _ownerMove;

    Transform _worldRoot;

    Vector3 _lastRecordedPos;
    bool _hasLastRecorded;

    int _seedCount;
    int _bindAutoCount;
    int _bindExplicitCount;

    public int Count => _eggs.Count;

    void OnValidate()
    {
        maxHistory = Mathf.Clamp(maxHistory, 10, 500);
        stepPerEgg = Mathf.Clamp(stepPerEgg, 1, 50);
        recordMinDelta = Mathf.Clamp(recordMinDelta, 0.00001f, 0.05f);

        followLerp = Mathf.Clamp01(followLerp);
        if (followLerp < 0.01f)
            followLerp = 0.45f;

        maxEggsInQueue = Mathf.Clamp(maxEggsInQueue, 0, 10);

        joinSeconds = Mathf.Clamp(joinSeconds, 0.01f, 1f);
        joinExtraDelayPerEgg = Mathf.Clamp(joinExtraDelayPerEgg, 0f, 0.5f);

        minTargetSeparation = Mathf.Clamp(minTargetSeparation, 0.0001f, 0.5f);
        recordMinDelta = Mathf.Clamp(recordMinDelta, 0.00001f, 0.05f);
    }

    void Awake()
    {
        if (debugLogs) Debug.Log($"[EggQueue] Awake on {name} (scene={gameObject.scene.name})", this);
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow("Awake");
    }

    void OnEnable()
    {
        if (debugLogs) Debug.Log($"[EggQueue] OnEnable on {name}", this);
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow("OnEnable");
    }

    public void BindOwner(MovementController ownerMove)
    {
        _bindExplicitCount++;

        if (ownerMove == null)
            return;

        bool ownerChanged = _ownerMove != ownerMove;

        _ownerMove = ownerMove;
        _ownerTr = ownerMove.transform;
        _ownerRb = ownerMove.Rigidbody != null ? ownerMove.Rigidbody : ownerMove.GetComponent<Rigidbody2D>();

        if (debugLogs)
        {
            Debug.Log(
                $"[EggQueue] BindOwner(EXPLICIT) #{_bindExplicitCount} ownerMove={ownerMove.name} ownerTr={_ownerTr.name} ownerRb={(_ownerRb ? _ownerRb.name : "NULL")} changed={ownerChanged}",
                this
            );
        }

        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (ownerChanged || _historyCount == 0)
            SeedHistoryNow("BindOwner(EXPLICIT)");
    }

    void BindOwnerAuto()
    {
        _bindAutoCount++;

        var prevOwnerTr = _ownerTr;
        var prevOwnerRb = _ownerRb;
        var prevOwnerMove = _ownerMove;

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

        if (debugLogs)
        {
            bool changed =
                prevOwnerTr != _ownerTr ||
                prevOwnerRb != _ownerRb ||
                prevOwnerMove != _ownerMove;

            if (changed)
            {
                Debug.Log(
                    $"[EggQueue] BindOwnerAuto #{_bindAutoCount} CHANGED: " +
                    $"move {(prevOwnerMove ? prevOwnerMove.name : "NULL")} -> {(_ownerMove ? _ownerMove.name : "NULL")} | " +
                    $"tr {(prevOwnerTr ? prevOwnerTr.name : "NULL")} -> {(_ownerTr ? _ownerTr.name : "NULL")} | " +
                    $"rb {(prevOwnerRb ? prevOwnerRb.name : "NULL")} -> {(_ownerRb ? _ownerRb.name : "NULL")}",
                    this
                );
            }
        }
    }

    void EnsureWorldRoot()
    {
        if (_worldRoot == null)
        {
            var existing = GameObject.Find(worldRootName);
            if (existing != null) _worldRoot = existing.transform;
            else _worldRoot = new GameObject(worldRootName).transform;

            if (debugLogs)
                Debug.Log($"[EggQueue] WorldRoot resolved: {_worldRoot.name}", this);
        }

        if (_worldRoot.parent != null)
            _worldRoot.SetParent(null, true);

        if (debugLogs && (_worldRoot.position != Vector3.zero || _worldRoot.rotation != Quaternion.identity))
            Debug.Log($"[EggQueue] WorldRoot was moved/rotated! pos={_worldRoot.position} rot={_worldRoot.rotation.eulerAngles}", this);

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
            _hasLastRecorded = false;

            if (debugLogs)
                Debug.Log($"[EggQueue] HistoryBuffer recreated size={maxHistory}", this);
        }
    }

    void SeedHistoryNow(string reason)
    {
        _seedCount++;

        EnsureHistoryBuffer();

        _historyHead = 0;
        _historyCount = 0;
        _hasLastRecorded = false;

        Vector3 p = GetOwnerWorldPos();
        RecordPosition(p);

        for (int i = 1; i < maxHistory; i++)
            RecordPosition(p);

        if (debugLogs)
        {
            Debug.Log(
                $"[EggQueue] SeedHistoryNow #{_seedCount} reason={reason} ownerPos={p} head={_historyHead} count={_historyCount}",
                this
            );
        }
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

        TrackOwnerPosition();

        bool doDebug = debugLogs && Time.time >= _nextDebug;
        if (doDebug)
        {
            _nextDebug = Time.time + Mathf.Max(0.05f, debugEverySeconds);
            Debug.Log($"[EggQueue] eggs={_eggs.Count} historyCount={_historyCount} head={_historyHead}", this);
        }

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            if (e.sr != null)
            {
                e.sr.sortingLayerName = sortingLayerName;
                e.sr.sortingOrder = baseOrderInLayer + i;
            }

            _eggs[i] = e;
        }

        float t = Mathf.Clamp01(followLerp);

        float minSep = Mathf.Max(0.0001f, minTargetSeparation);
        float minSepSqr = minSep * minSep;

        Vector3 prevTarget = Vector3.positiveInfinity;
        bool hasPrevTarget = false;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            Vector3 targetWorld = GetRecentPosition(e.recentIndex);
            targetWorld.z = 0f;
            targetWorld += (Vector3)worldOffset;
            targetWorld.z = 0f;

            Vector3 before = e.rootTr.position;
            before.z = 0f;

            if (hasPrevTarget && (targetWorld - prevTarget).sqrMagnitude <= minSepSqr)
                targetWorld = before;

            Vector3 newWorld;

            if (e.isJoining)
            {
                float elapsed = Time.time - e.joinStartTime;
                float u = e.joinDuration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / e.joinDuration);
                float smoothU = u * u * (3f - 2f * u);

                Vector3 from = e.joinFromWorld;
                from.z = 0f;

                Vector3 to = targetWorld;
                to.z = 0f;

                newWorld = Vector3.Lerp(from, to, smoothU);
                newWorld.z = 0f;

                if (u >= 1f)
                    e.isJoining = false;
            }
            else
            {
                newWorld = Vector3.Lerp(before, targetWorld, t);
                newWorld.z = 0f;
            }

            e.rootTr.position = newWorld;
            _eggs[i] = e;

            prevTarget = targetWorld;
            hasPrevTarget = true;
        }
    }

    bool TrackOwnerPosition()
    {
        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        if (!_hasLastRecorded)
        {
            if (debugLogs) Debug.Log($"[EggQueue] TrackOwner FIRST record p={p}", this);
            RecordPosition(p);
            return true;
        }

        float min = recordMinDelta * recordMinDelta;
        float dist = (p - _lastRecordedPos).sqrMagnitude;

        if (dist < min)
            return false;

        if (debugLogs)
        {
            if (!_hasLastOwnerPosLogged)
            {
                _lastOwnerPosLogged = p;
                _hasLastOwnerPosLogged = true;
            }
            else
            {
                float collapseSqr = collapseDistance * collapseDistance;
                float movedSqr = (p - _lastOwnerPosLogged).sqrMagnitude;
                if (movedSqr <= collapseSqr)
                {
                    Debug.Log($"[EggQueue] TrackOwner RECORD p={p} last={_lastRecordedPos} dist={dist} min={min}", this);
                }
                _lastOwnerPosLogged = p;
            }
        }

        RecordPosition(p);
        return true;
    }

    void RecordPosition(Vector3 p)
    {
        p.z = 0f;

        _history[_historyHead] = p;
        _historyHead = (_historyHead + 1) % _history.Length;
        _historyCount = Mathf.Min(_historyCount + 1, _history.Length);

        _lastRecordedPos = p;
        _hasLastRecorded = true;
    }

    Vector3 GetRecentPosition(int recentIndex)
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

    Vector3 GetOwnerWorldPos()
    {
        if (_ownerTr != null)
            return _ownerTr.position;

        if (_ownerRb != null)
            return _ownerRb.position;

        return transform.position;
    }

    public bool TryEnqueue(ItemPickup.ItemType type, Sprite idleSprite)
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (MaxEggs > 0 && _eggs.Count >= MaxEggs)
        {
            if (debugLogs)
                Debug.Log($"[EggQueue] TryEnqueue BLOCKED (FULL) type={type} eggs={_eggs.Count} max={MaxEggs}", this);

            return false;
        }

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            e.recentIndex = (stepPerEgg * (i + 1)) - 1;
            _eggs[i] = e;
        }

        int recentIndex = (stepPerEgg * (_eggs.Count + 1)) - 1;

        Vector3 spawnWorld = GetOwnerWorldPos();
        spawnWorld.z = 0f;
        spawnWorld += (Vector3)worldOffset;
        spawnWorld.z = 0f;

        if (debugLogs)
        {
            Debug.Log(
                $"[EggQueue] TryEnqueue ADD type={type} recentIndex={recentIndex} spawnWorld={spawnWorld} ownerPos={GetOwnerWorldPos()} eggsBefore={_eggs.Count} head={_historyHead} count={_historyCount}",
                this
            );
        }

        var rootGo = new GameObject($"EggFollower_{type}");
        var rootTr = rootGo.transform;
        rootTr.SetParent(_worldRoot, true);
        rootTr.localScale = Vector3.one;
        rootTr.position = spawnWorld;

        var visualGo = new GameObject("Visual");
        var visualTr = visualGo.transform;
        visualTr.SetParent(rootTr, false);
        visualTr.localPosition = Vector3.zero;
        visualTr.localRotation = Quaternion.identity;
        visualTr.localScale = Vector3.one;

        var sr = visualGo.AddComponent<SpriteRenderer>();
        sr.sprite = idleSprite;
        sr.enabled = true;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = baseOrderInLayer + _eggs.Count;

        var anim = visualGo.AddComponent<AnimatedSpriteRenderer>();
        anim.idleSprite = idleSprite;
        anim.idle = true;
        anim.loop = false;
        anim.pingPong = false;
        anim.allowFlipX = false;
        anim.enabled = true;
        anim.RefreshFrame();

        float dur = Mathf.Max(0.01f, joinSeconds) + Mathf.Max(0f, joinExtraDelayPerEgg) * _eggs.Count;

        _eggs.Add(new EggEntry
        {
            type = type,
            rootTr = rootTr,
            visualTr = visualTr,
            anim = anim,
            sr = sr,
            recentIndex = recentIndex,

            isJoining = true,
            joinStartTime = Time.time,
            joinDuration = dur,
            joinFromWorld = spawnWorld
        });

        return true;
    }

    public bool TryDequeue(out ItemPickup.ItemType type)
    {
        type = default;

        if (_eggs.Count == 0)
            return false;

        var first = _eggs[0];

        if (debugLogs)
        {
            Vector3 firstPos = first.rootTr != null ? first.rootTr.position : Vector3.positiveInfinity;
            Debug.Log($"[EggQueue] TryDequeue REMOVE type={first.type} pos={firstPos} eggsBefore={_eggs.Count}", this);
        }

        _eggs.RemoveAt(0);

        if (first.rootTr != null)
            Destroy(first.rootTr.gameObject);

        type = first.type;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            e.recentIndex = (stepPerEgg * (i + 1)) - 1;
            _eggs[i] = e;
        }

        return true;
    }

    public void ClearAll()
    {
        if (debugLogs) Debug.Log($"[EggQueue] ClearAll eggs={_eggs.Count}", this);

        for (int i = 0; i < _eggs.Count; i++)
        {
            if (_eggs[i].rootTr != null)
                Destroy(_eggs[i].rootTr.gameObject);
        }

        _eggs.Clear();

        _historyHead = 0;
        _historyCount = 0;
        _hasLastRecorded = false;

        _hasLastOwnerPosLogged = false;
    }
}
