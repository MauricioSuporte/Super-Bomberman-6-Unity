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
    [SerializeField, Range(0, 10)] private int maxEggsInQueue = 3;
    public int MaxEggs => Mathf.Max(0, maxEggsInQueue);
    public bool IsFull => MaxEggs > 0 && _eggs.Count >= MaxEggs;

    [Header("Queue - Join/Shift Animation")]
    [SerializeField, Range(0.01f, 1f)] private float joinSeconds = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float joinExtraDelayPerEgg = 0.05f;
    [SerializeField, Range(0.01f, 1f)] private float shiftSeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private float debugEverySeconds = 0.25f;
    float _nextDebug;

    struct EggEntry
    {
        public ItemPickup.ItemType type;
        public Transform rootTr;
        public Transform visualTr;
        public AnimatedSpriteRenderer anim;
        public SpriteRenderer sr;

        public bool isAnimating;
        public float animStartTime;
        public float animDuration;
        public Vector3 animFromWorld;
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

    float _distanceCarry;

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
    }

    void Awake()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow("Awake");
    }

    void OnEnable()
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();
        SeedHistoryNow("OnEnable");
    }

    public void BindOwner(MovementController ownerMove)
    {
        if (ownerMove == null)
            return;

        bool ownerChanged = _ownerMove != ownerMove;

        _ownerMove = ownerMove;
        _ownerTr = ownerMove.transform;
        _ownerRb = ownerMove.Rigidbody != null ? ownerMove.Rigidbody : ownerMove.GetComponent<Rigidbody2D>();

        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (ownerChanged || _historyCount == 0)
            SeedHistoryNow("BindOwner(EXPLICIT)");
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
            _hasLastRecorded = false;
            _distanceCarry = 0f;
        }
    }

    void SeedHistoryNow(string reason)
    {
        EnsureHistoryBuffer();

        _historyHead = 0;
        _historyCount = 0;
        _hasLastRecorded = false;
        _distanceCarry = 0f;

        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        RecordPosition(p);
        for (int i = 1; i < maxHistory; i++)
            RecordPosition(p);

        if (debugLogs)
            Debug.Log($"[EggQueue] SeedHistoryNow reason={reason} ownerPos={p} head={_historyHead} count={_historyCount}", this);
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

        TrackOwnerPositionDistanceBased();

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

            EnsureEggLayer(e.rootTr);

            if (e.sr != null)
            {
                e.sr.sortingLayerName = sortingLayerName;
                e.sr.sortingOrder = eggSortingOrder;
            }

            _eggs[i] = e;
        }

        float minSep = Mathf.Max(0.0001f, minTargetSeparation);
        float minSepSqr = minSep * minSep;

        Vector3 prevTarget = Vector3.positiveInfinity;
        bool hasPrevTarget = false;

        float followSpeed = GetOwnerWorldSpeedPerSecond();
        float maxStep = followSpeed * Time.deltaTime;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            float backDist = eggSpacingWorld * (i + 1);
            Vector3 targetWorld = SampleBackDistance(backDist);
            targetWorld += (Vector3)worldOffset;
            targetWorld.z = 0f;

            Vector3 before = e.rootTr.position;
            before.z = 0f;

            if (hasPrevTarget && (targetWorld - prevTarget).sqrMagnitude <= minSepSqr)
                targetWorld = before;

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
                newWorld = Vector3.MoveTowards(before, targetWorld, maxStep);
                newWorld.z = 0f;
            }

            e.rootTr.position = newWorld;
            _eggs[i] = e;

            prevTarget = targetWorld;
            hasPrevTarget = true;
        }
    }

    float GetOwnerWorldSpeedPerSecond()
    {
        if (_ownerMove != null)
            return Mathf.Max(0.01f, _ownerMove.speed * _ownerMove.tileSize);

        return 5f;
    }

    bool TrackOwnerPositionDistanceBased()
    {
        Vector3 p = GetOwnerWorldPos();
        p.z = 0f;

        if (!_hasLastRecorded)
        {
            RecordPosition(p);
            _distanceCarry = 0f;
            return true;
        }

        float jitterSqr = jitterIgnoreDelta * jitterIgnoreDelta;
        Vector3 from = _lastRecordedPos;
        Vector3 to = p;

        Vector3 seg = to - from;
        float segLen = seg.magnitude;

        if (segLen * segLen <= jitterSqr)
            return false;

        float spacing = Mathf.Max(0.0001f, historyPointSpacingWorld);

        float dist = _distanceCarry + segLen;

        if (dist < spacing)
        {
            _distanceCarry = dist;
            return false;
        }

        Vector3 dir = seg / segLen;
        float traveledFromLast = _distanceCarry;

        int wrote = 0;

        while (traveledFromLast + spacing <= segLen + 0.000001f)
        {
            traveledFromLast += spacing;
            Vector3 sample = from + dir * traveledFromLast;
            sample.z = 0f;
            RecordPosition(sample);
            wrote++;
        }

        float leftover = segLen - traveledFromLast;
        _distanceCarry = Mathf.Max(0f, leftover);

        return wrote > 0;
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
            Vector3 p = GetRecentHistory(k);
            p.z = 0f;

            float segLen = Vector3.Distance(prev, p);
            if (segLen <= 0.000001f)
            {
                prev = p;
                continue;
            }

            if (segLen >= remaining)
            {
                float t = remaining / segLen;
                Vector3 res = Vector3.Lerp(prev, p, t);
                res.z = 0f;
                return res;
            }

            remaining -= segLen;
            prev = p;
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

    public bool TryEnqueue(ItemPickup.ItemType type, Sprite idleSprite)
    {
        BindOwnerAuto();
        EnsureWorldRoot();
        EnsureHistoryBuffer();

        if (MaxEggs > 0 && _eggs.Count >= MaxEggs)
            return false;

        Vector3 spawnWorld = GetOwnerWorldPos();
        spawnWorld.z = 0f;
        spawnWorld += (Vector3)worldOffset;
        spawnWorld.z = 0f;

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

        EnsureEggLayer(rootTr);

        var sr = visualGo.AddComponent<SpriteRenderer>();
        sr.sprite = idleSprite;
        sr.enabled = true;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = eggSortingOrder;

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

            isAnimating = true,
            animStartTime = Time.time,
            animDuration = dur,
            animFromWorld = spawnWorld
        });

        return true;
    }

    public bool TryDequeue(out ItemPickup.ItemType type)
    {
        type = default;

        if (_eggs.Count == 0)
            return false;

        var first = _eggs[0];
        _eggs.RemoveAt(0);

        if (first.rootTr != null)
            Destroy(first.rootTr.gameObject);

        type = first.type;

        AnimateAllShift();
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
        _hasLastRecorded = false;
        _distanceCarry = 0f;
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
