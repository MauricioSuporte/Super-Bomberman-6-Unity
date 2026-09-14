using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MountEggQueue : MonoBehaviour
{
    // Routes contain only travelled segments. Each egg owns its unread history,
    // so neither a turn, a stop nor removing another egg can skip a waypoint.

    void ResetRuntimeState()
    {
        _lastOwnerPos = GetOwnerWorldPos();
        _lastOwnerPos.z = 0f;
        _hasLastOwnerPos = true;
        _lastOwnerMeasuredSpeed = 0f;
        _lastMoveTime = QTime();
        _lastRouteDirection = _ownerMove != null ? _ownerMove.FacingDirection : Vector2.zero;
    }

    void SeedHistoryNow() => ResetHistoryToCurrentOwnerPos();

    void ResetHistoryToCurrentOwnerPos()
    {
        ResetRuntimeState();
        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            e.route = new Queue<Vector3>();
            e.routeTail = _lastOwnerPos;
            e.groundPosition = _lastOwnerPos;
            e.remainingDistance = 0f;
            if (e.rootTr != null)
                e.rootTr.position = _lastOwnerPos + (Vector3)worldOffset;
            _eggs[i] = e;
        }
    }

    void AppendRoutePoint(ref EggEntry egg, Vector3 point)
    {
        float distance = Vector3.Distance(egg.routeTail, point);
        if (distance <= 0.000001f)
            return;
        egg.route.Enqueue(point);
        egg.remainingDistance += distance;
        egg.routeTail = point;
    }

    void AppendAxisSegment(ref EggEntry egg, Vector3 target, bool horizontal)
    {
        float tile = _ownerMove != null ? Mathf.Max(0.01f, _ownerMove.tileSize) : 1f;
        float start = horizontal ? egg.routeTail.x : egg.routeTail.y;
        float end = horizontal ? target.x : target.y;
        float sign = Mathf.Sign(end - start);
        float boundary = (sign > 0f ? Mathf.Floor(start / tile) + 1f : Mathf.Ceil(start / tile) - 1f) * tile;
        while (sign * (end - boundary) > 0.000001f)
        {
            Vector3 point = egg.routeTail;
            if (horizontal) point.x = boundary;
            else point.y = boundary;
            AppendRoutePoint(ref egg, point);
            boundary += sign * tile;
        }
        AppendRoutePoint(ref egg, target);
    }

    void UpdateTileQueue()
    {
        if (QDelta() <= 0f)
            return;
        Vector3 owner = GetOwnerWorldPos();
        owner.z = 0f;
        if (!_hasLastOwnerPos)
            ResetRuntimeState();
        Vector3 delta = owner - _lastOwnerPos;
        bool moved = delta.sqrMagnitude > jitterIgnoreDelta * jitterIgnoreDelta;
        _lastOwnerMeasuredSpeed = delta.magnitude / Mathf.Max(QDelta(), 0.0001f);
        bool moving = IsOwnerMoving(moved);
        bool draining = !moving && QTime() - _lastMoveTime >= idleEnterSeconds;


        for (int i = _eggs.Count - 1; i >= 0; i--)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;
            if (e.route == null)
            {
                e.route = new Queue<Vector3>();
                e.routeTail = _lastOwnerPos;
                e.groundPosition = _lastOwnerPos;
            }
            // Finish the previous travel axis before starting the next one.
            // This retains the corner when one frame spans both movements.
            Vector3 corner = e.routeTail;
            bool horizontalFirst = _lastRouteDirection != Vector2.zero
                ? Mathf.Abs(_lastRouteDirection.x) > Mathf.Abs(_lastRouteDirection.y)
                : Mathf.Abs(delta.x) < Mathf.Abs(delta.y);
            if (horizontalFirst) corner.x = owner.x;
            else corner.y = owner.y;
            AppendAxisSegment(ref e, corner, horizontalFirst);
            AppendAxisSegment(ref e, owner, !horizontalFirst);

            Vector3 before = e.rootTr.position;
            Vector3 position = e.groundPosition;
            float reserve = draining ? 0f : eggSpacingWorld * (_eggs.Count - i);
            float budget = Mathf.Min(GetOwnerWorldSpeedPerSecond() * QDelta(), Mathf.Max(0f, e.remainingDistance - reserve));
            while (e.route.Count > 0 && budget > 0f)
            {
                Vector3 target = e.route.Peek();
                float distance = Vector3.Distance(position, target);
                float step = Mathf.Min(distance, budget);
                position = Vector3.MoveTowards(position, target, step);
                budget -= step;
                e.remainingDistance = Mathf.Max(0f, e.remainingDistance - step);
                if (distance <= step + 0.000001f)
                {
                    e.route.Dequeue();
                }
                else
                    break;
            }
            e.groundPosition = position;
            float visualOffset = GetOwnerFollowWorldPos().y - owner.y;
            Vector3 newWorld = position + (Vector3)worldOffset + Vector3.up * visualOffset;
            e.rootTr.position = newWorld;
            UpdateDirectional(ref e, before, newWorld);
            _eggs[i] = e;
        }
        if (moved)
        {
            Vector2 direction = _ownerMove != null ? _ownerMove.Direction : Vector2.zero;
            _lastRouteDirection = direction != Vector2.zero ? direction : (Vector2)delta;
        }
        _lastOwnerPos = owner;
    }

    [SerializeField, Range(0.00001f, 0.05f)] float jitterIgnoreDelta = 0.0005f;

    [Header("Egg Spacing (World Units)")]
    [SerializeField, Range(0.05f, 5f)] float eggSpacingWorld = 1f;

    [Header("Follow (WORLD)")]
    [SerializeField] Vector2 worldOffset = new(0f, -0.15f);

    [Header("World Root")]
    [SerializeField] string worldRootName = "EggQueueWorldRoot";

    [Header("Layer")]
    [SerializeField] int eggGameObjectLayer = 3;

    [Header("Sorting (SpriteRenderer)")]
    [SerializeField] string eggSortingLayerName = "Default";
    [SerializeField] int eggBaseSortingOrder = 2;

    [Header("Queue")]
    [SerializeField, Range(0, 10)] int maxEggsInQueue = 10;
    public int MaxEggs => Mathf.Max(0, maxEggsInQueue);
    public bool IsFull => MaxEggs > 0 && _eggs.Count >= MaxEggs;
    public int Count => _eggs.Count;

    [Header("Egg Visual (Prefab)")]
    [SerializeField] GameObject eggFollowerPrefab;

    [Header("Idle - Enter Delay")]
    [SerializeField, Range(0f, 0.5f)] float idleEnterSeconds = 0.18f;

    [SerializeField, Range(0f, 0.25f)] float movingHoldSeconds = 0.06f;

    [Header("Owner Move Detection")]
    [SerializeField] bool preferRigidbodyVelocityForIdle = true;
    [SerializeField, Range(0.000001f, 0.01f)] float ownerVelocityEpsilon = 0.0001f;

    [Header("Mount SFX (Resources/Sounds)")]
    [SerializeField] string blueLouieMountSfxName = "MountBlueLouie";
    [SerializeField] string blackLouieMountSfxName = "MountBlackLouie";
    [SerializeField] string purpleLouieMountSfxName = "MountPurpleLouie";
    [SerializeField] string greenLouieMountSfxName = "MountGreenLouie";
    [SerializeField] string yellowLouieMountSfxName = "MountYellowLouie";
    [SerializeField] string pinkLouieMountSfxName = "MountPinkLouie";
    [SerializeField] string redLouieMountSfxName = "MountRedLouie";
    [SerializeField, Range(0f, 1f)] float defaultMountVolume = 1f;

    [Header("Dequeue - Destroy Visual")]
    [SerializeField, Range(0.05f, 2f)] float dequeueDestroySeconds = 0.5f;

    Transform _freezeAnchor;

    bool _hardFrozen;
    readonly List<Vector3> _hardFrozenEggWorld = new();
    readonly List<Vector2> _hardFrozenFacing = new();

    struct EggEntry
    {
        public ItemType type;
        public Transform rootTr;
        public EggFollowerDirectionalVisual directional;

        public AudioClip mountSfx;
        public float mountVolume;

        public Queue<Vector3> route;
        public Vector3 routeTail;
        public Vector3 groundPosition;
        public float remainingDistance;
    }

    readonly List<EggEntry> _eggs = new();

    int _ignoreOwnerInvulnerabilityUntilFrame = -1;
    bool _suppressedByRedBoat;

    Transform _ownerTr;
    Rigidbody2D _ownerRb;
    MovementController _ownerMove;
    CharacterHealth _ownerHealth;
    public bool OwnerIsInvulnerable
    {
        get
        {
            bool ignoreNow =
                _ignoreOwnerInvulnerability ||
                (_ignoreOwnerInvulnerabilityUntilFrame >= 0 && Time.frameCount <= _ignoreOwnerInvulnerabilityUntilFrame);

            return
                !ignoreNow &&
                _ownerHealth != null &&
                _ownerHealth.IsInvulnerable;
        }
    }

    Transform _worldRoot;

    Vector3 _lastOwnerPos;
    bool _hasLastOwnerPos;
    float _lastOwnerMeasuredSpeed;
    float _ownerVisualFollowYOffset;
    bool _ownerVisualFollowWorldOverrideActive;
    Vector3 _ownerVisualFollowWorldOverride;

    float _lastMoveTime;
    Vector2 _lastRouteDirection;

    bool _forcedHidden;

    readonly Dictionary<ItemType, AudioClip> _mountSfxCache = new();
    int _ownerPlayerId = -1;
    bool _ignoreOwnerInvulnerability;

    public bool IsForcedHidden => _forcedHidden;

    #region Unity

    bool UseUnscaledIntroTime()
    {
        return Time.timeScale == 0f && !GamePauseController.IsPaused;
    }

    float QTime() => UseUnscaledIntroTime() ? Time.unscaledTime : Time.time;
    float QDelta() => UseUnscaledIntroTime() ? Time.unscaledDeltaTime : Time.deltaTime;

    void OnValidate() => ClampInspector();

    void Awake() => InitializeRuntime();

    void OnEnable() => InitializeRuntime();

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.EggQueueUpdate.Auto();

        if (_eggs.Count == 0)
            return;

        ApplyEggLayerNow();
        ApplyEggSortingNow();

        if (PruneNullEggs())
            PostQueueChanged(animateShift: true);

        if (_hardFrozen)
        {
            ApplyHardFreezeFrame();
            return;
        }

        EnsureBound();

        bool ownerOnRedBoat = (_ownerMove != null && BoatRideZone.IsRidingBoat(_ownerMove));

        if (ownerOnRedBoat)
        {
            if (!_suppressedByRedBoat)
                SetEggVisualRenderersEnabled(false);

            return;
        }
        else
        {
            if (_suppressedByRedBoat)
            {
                SetEggVisualRenderersEnabled(true);

                ResetHistoryToCurrentOwnerPos();
                ResetRuntimeState();

                SnapAllToOwnerNow();
            }
        }

        UpdateTileQueue();
    }

    static Transform FindDeepChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == childName)
                return all[i];

        return null;
    }

    static void SetNamedAnimationEnabled(Transform eggRoot, string childName, bool enabled)
    {
        if (eggRoot == null || string.IsNullOrWhiteSpace(childName)) return;

        var t = FindDeepChildByName(eggRoot, childName);
        if (t == null) return;

        if (t.TryGetComponent<AnimatedSpriteRenderer>(out var ar)) ar.enabled = enabled;

        if (t.TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = enabled;
    }

    static void SetDestroyAnimationsEnabled(Transform eggRoot, bool enabled)
    {
        SetNamedAnimationEnabled(eggRoot, "DestroyAnimation", enabled);
        SetNamedAnimationEnabled(eggRoot, "ExplosionDestroyAnimation", enabled);
    }

    void ForceEggVisualToNormalIdle(Transform eggRoot, EggFollowerDirectionalVisual directional)
    {
        SetDestroyAnimationsEnabled(eggRoot, false);

        if (directional != null)
        {
            directional.enabled = true;
            directional.ForceIdleFacing(directional.facing);
        }
    }

    void SetEggVisualRenderersEnabled(bool enabled)
    {
        _suppressedByRedBoat = !enabled;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            var anims = e.rootTr.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int a = 0; a < anims.Length; a++)
            {
                var ar = anims[a];
                if (ar == null) continue;

                ar.enabled = enabled;

                if (ar.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
                    sr.enabled = enabled;
            }

            var srs = e.rootTr.GetComponentsInChildren<SpriteRenderer>(true);
            for (int s = 0; s < srs.Length; s++)
            {
                var sr = srs[s];
                if (sr == null) continue;
                sr.enabled = enabled;
            }

            if (enabled)
                ForceEggVisualToNormalIdle(e.rootTr, e.directional);
        }
    }

    #endregion

    #region Init / Binding

    void ClampInspector()
    {
        maxEggsInQueue = Mathf.Clamp(maxEggsInQueue, 0, 10);

        jitterIgnoreDelta = Mathf.Clamp(jitterIgnoreDelta, 0.00001f, 0.05f);
        eggSpacingWorld = Mathf.Clamp(eggSpacingWorld, 0.05f, 5f);

        idleEnterSeconds = Mathf.Clamp(idleEnterSeconds, 0f, 0.5f);
        movingHoldSeconds = Mathf.Clamp(movingHoldSeconds, 0f, 0.25f);

        ownerVelocityEpsilon = Mathf.Clamp(ownerVelocityEpsilon, 0.000001f, 0.01f);

        defaultMountVolume = Mathf.Clamp01(defaultMountVolume);
        eggGameObjectLayer = Mathf.Clamp(eggGameObjectLayer, 0, 31);

        dequeueDestroySeconds = Mathf.Clamp(dequeueDestroySeconds, 0.05f, 2f);
    }

    void InitializeRuntime()
    {
        if (_hardFrozen)
        {
            EnsureWorldRoot();

            ApplyEggLayerNow();
            ApplyEggSortingNow();
            ApplyForcedVisibility();
            return;
        }

        EnsureBound();
        SeedHistoryNow();
        ResetRuntimeState();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
        ApplyForcedVisibility();
    }

    public void BindOwner(MovementController ownerMove)
    {
        if (ownerMove == null)
            return;

        bool ownerChanged = _ownerMove != ownerMove;

        _ownerMove = ownerMove;
        _ownerTr = ownerMove.transform;
        _ownerRb = (_ownerMove.Rigidbody != null) ? _ownerMove.Rigidbody : ownerMove.GetComponent<Rigidbody2D>();
        _ownerHealth = null;
        _ownerMove.TryGetComponent(out _ownerHealth);

        EnsureWorldRoot();

        if (ownerChanged || !_hasLastOwnerPos)
        {
            SeedHistoryNow();
            ResetRuntimeState();
        }

        CacheOwnerIdentity();
        ApplyEggLayerNow();
        ApplyEggSortingNow();
        ApplyForcedVisibility();
    }

    void EnsureBound()
    {
        if (_hardFrozen)
            return;

        if (_ownerTr != null || _ownerRb != null)
        {
            EnsureWorldRoot();

            return;
        }

        BindOwnerAuto();
        EnsureWorldRoot();

    }

    void BindOwnerAuto()
    {
        if (_hardFrozen)
            return;

        _ownerMove = GetComponentInParent<MovementController>();
        if (_ownerMove != null)
        {
            _ownerTr = _ownerMove.transform;
            _ownerRb = (_ownerMove.Rigidbody != null) ? _ownerMove.Rigidbody : _ownerMove.GetComponent<Rigidbody2D>();

            _ownerHealth = null;
            _ownerMove.TryGetComponent(out _ownerHealth);

            CacheOwnerIdentity();
            return;
        }

        _ownerRb = GetComponentInParent<Rigidbody2D>();
        _ownerTr = _ownerRb != null ? _ownerRb.transform : transform.root;
        _ownerHealth = null;
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

        _worldRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _worldRoot.localScale = Vector3.one;
    }

    #endregion

    #region Owner / History

    Vector3 GetOwnerWorldPos()
    {
        if (_ownerTr != null) return _ownerTr.position;
        if (_ownerRb != null) return _ownerRb.position;
        return transform.position;
    }

    Vector3 GetOwnerFollowWorldPos()
    {
        if (_ownerVisualFollowWorldOverrideActive)
            return _ownerVisualFollowWorldOverride;

        Vector3 pos = GetOwnerWorldPos();
        pos.y += _ownerVisualFollowYOffset;
        return pos;
    }

    float GetOwnerWorldSpeedPerSecond()
    {
        float measuredSpeed = Mathf.Max(0f, _lastOwnerMeasuredSpeed);

        if (_ownerMove != null)
            return Mathf.Max(0.01f, _ownerMove.speed * _ownerMove.tileSize, measuredSpeed);

        return Mathf.Max(5f, measuredSpeed);
    }

    bool IsOwnerMoving(bool movedByPositionThisFrame)
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
            _lastMoveTime = QTime();

        float hold = Mathf.Max(0f, movingHoldSeconds);
        if (hold > 0f && (QTime() - _lastMoveTime) <= hold)
            return true;

        return movingNow;
    }

    #endregion

    #region Owner Visual Follow Offset

    public void SetOwnerVisualFollowYOffset(float worldYOffset)
    {
        _ownerVisualFollowWorldOverrideActive = false;
        _ownerVisualFollowYOffset = worldYOffset;
    }

    public void ClearOwnerVisualFollowYOffset()
    {
        _ownerVisualFollowYOffset = 0f;
        _ownerVisualFollowWorldOverrideActive = false;
    }

    public void SetOwnerVisualFollowWorldPosition(Vector3 worldPosition, bool exactFollow)
    {
        worldPosition.z = 0f;
        _ownerVisualFollowWorldOverride = worldPosition;
        _ownerVisualFollowWorldOverrideActive = true;
    }

    #endregion

    #region Directional Visual

    void UpdateDirectional(ref EggEntry e, Vector3 before, Vector3 newWorld)
    {
        if (e.directional == null)
            return;

        float dz = Mathf.Max(0.00000001f, e.directional.moveDeadZone);
        float effectiveDzSqr = dz * dz;

        Vector3 dirSample = newWorld - before;
        dirSample.z = 0f;

        bool wouldBeMoving = dirSample.sqrMagnitude > effectiveDzSqr;
        e.directional.ApplyMoveDelta(wouldBeMoving ? dirSample : Vector3.zero);
    }

    #endregion

    #region Freeze

    public void BeginHardFreeze()
    {
        _hardFrozen = true;

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
        ApplyForcedVisibility();
    }

    public void EndHardFreezeAndRebind(MovementController owner)
    {
        _hardFrozen = false;

        _hardFrozenEggWorld.Clear();
        _hardFrozenFacing.Clear();

        if (owner != null) BindOwner(owner);
        else BindOwnerAuto();

        SeedHistoryNow();
        ResetRuntimeState();

        ApplyEggLayerNow();
        ApplyEggSortingNow();
        ApplyForcedVisibility();
    }

    public void EndHardFreezeAndKeepWorld(Vector3 worldPos)
    {
        if (!_hardFrozen)
            BeginHardFreeze();

        FreezeOwnerAtWorldPosition(worldPos);

        ApplyEggLayerNow();
        ApplyEggSortingNow();
        ApplyForcedVisibility();
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

            _eggs[i] = e;
        }
    }

    #endregion

    #region Snap

    void SnapAllToOwnerNow()
    {
        Vector3 p = GetOwnerWorldPos() + (Vector3)worldOffset;
        p.z = 0f;

        for (int i = 0; i < _eggs.Count; i++)
        {
            var e = _eggs[i];
            if (e.rootTr == null)
                continue;

            e.rootTr.position = p;

            if (e.directional != null)
                e.directional.ForceIdleFacing(e.directional.facing);

            _eggs[i] = e;
        }
    }

    #endregion

    #region Sorting / Layer / Visibility

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
            if (_eggs[i].rootTr != null)
                EnsureEggLayer(_eggs[i].rootTr);
    }

    bool ShouldInvertSortingForUp()
    {
        if (_eggs.Count < 2)
            return false;

        for (int i = 0; i < _eggs.Count; i++)
            if (_eggs[i].directional != null && _eggs[i].directional.IsPlayingUpAnimation)
                return true;

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

            int order = baseOrder + (invertForUp ? _eggs.Count - 1 - i : i);

            EnsureEggSorting(e.rootTr, order);
        }
    }

    public void ForceVisible(bool visible)
    {
        _forcedHidden = !visible;
        ApplyForcedVisibility();
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

    void PostQueueChanged(bool animateShift)
    {

        ApplyEggLayerNow();
        ApplyEggSortingNow();
        ApplyForcedVisibility();
    }

    #endregion

    #region SFX

    AudioClip LoadMountSfx(ItemType eggType)
    {
        if (_mountSfxCache.TryGetValue(eggType, out var cached) && cached != null)
            return cached;

        string name = eggType switch
        {
            ItemType.BlueLouieEgg => blueLouieMountSfxName,
            ItemType.BlackLouieEgg => blackLouieMountSfxName,
            ItemType.PurpleLouieEgg => purpleLouieMountSfxName,
            ItemType.GreenLouieEgg => greenLouieMountSfxName,
            ItemType.YellowLouieEgg => yellowLouieMountSfxName,
            ItemType.PinkLouieEgg => pinkLouieMountSfxName,
            ItemType.RedLouieEgg => redLouieMountSfxName,
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

    #endregion

    #region Public Queue API

    public bool TryEnqueue(ItemType type, Sprite idleSprite, AudioClip mountSfx, float mountVolume)
    {
        EnsureBound();

        if (IsFull)
            return false;

        EnqueueInternal(type, idleSprite, mountSfx, mountVolume, animate: true);
        PostQueueChanged(animateShift: false);
        return true;
    }

    public bool TryDequeue(out ItemType type, out AudioClip mountSfx, out float mountVolume)
    {
        type = default;
        mountSfx = null;
        mountVolume = 0f;

        if (_eggs.Count == 0)
            return false;

        int lastIndex = _eggs.Count - 1;
        var oldestClosest = _eggs[lastIndex];
        _eggs.RemoveAt(lastIndex);

        type = oldestClosest.type;
        mountSfx = oldestClosest.mountSfx != null ? oldestClosest.mountSfx : LoadMountSfx(type);
        mountVolume = Mathf.Clamp01(oldestClosest.mountSfx != null ? oldestClosest.mountVolume : defaultMountVolume);

        var tr = oldestClosest.rootTr;
        if (tr != null)
        {
            if (_hardFrozen)
            {
                if (lastIndex < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(lastIndex);
                if (lastIndex < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(lastIndex);
            }

            Vector3 destroyAt = GetOwnerWorldPos() + (Vector3)worldOffset;
            destroyAt.z = 0f;
            tr.position = destroyAt;

            StartCoroutineSafe(DestroyEggRoutine(tr, Mathf.Max(0.05f, dequeueDestroySeconds)));
        }
        else
        {
            if (_hardFrozen)
            {
                if (lastIndex < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(lastIndex);
                if (lastIndex < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(lastIndex);
            }
        }

        PostQueueChanged(animateShift: true);
        return true;
    }

    public void GetQueuedEggTypesOldestToNewest(List<ItemType> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        for (int i = _eggs.Count - 1; i >= 0; i--)
            buffer.Add(_eggs[i].type);
    }

    public void RestoreQueuedEggTypesOldestToNewest(IReadOnlyList<ItemType> types, Sprite idleSpriteFallback = null)
    {
        ClearAllEggs();

        if (types == null || types.Count == 0)
            return;

        EnsureBound();

        ResetHistoryToCurrentOwnerPos();
        ResetRuntimeState();

        for (int i = 0; i < types.Count; i++)
            EnqueueInternal(types[i], idleSpriteFallback, mountSfx: null, mountVolume: defaultMountVolume, animate: false);

        SnapAllToOwnerNow();
        PostQueueChanged(animateShift: false);
    }

    public void RebindAndReseedNow(bool resetHistoryToOwnerNow)
    {
        if (_hardFrozen)
            return;

        BindOwnerAuto();
        EnsureWorldRoot();

        if (resetHistoryToOwnerNow)
            ResetHistoryToCurrentOwnerPos();
        else
            SeedHistoryNow();

        ResetRuntimeState();
        PostQueueChanged(animateShift: false);
    }

    Coroutine StartCoroutineSafe(IEnumerator routine)
    {
        if (routine == null)
            return null;

        if (isActiveAndEnabled)
            return StartCoroutine(routine);

        return GlobalCoroutineRunner.Run(routine);
    }

    public void SnapQueueToOwnerNow(bool resetHistoryToOwnerNow = true)
    {
        if (_hardFrozen)
            return;

        EnsureBound();

        if (resetHistoryToOwnerNow)
            ResetHistoryToCurrentOwnerPos();
        else
            SeedHistoryNow();

        ResetRuntimeState();

        SnapAllToOwnerNow();

        PostQueueChanged(animateShift: false);
    }

    #endregion

    #region Enqueue / Clear / Internal Spawn

    void ClearAllEggs()
    {
        for (int i = 0; i < _eggs.Count; i++)
            if (_eggs[i].rootTr != null)
                Destroy(_eggs[i].rootTr.gameObject);

        _eggs.Clear();
    }

    public void ClearQueueNow(bool resetHistoryToOwner = true, bool animateShift = false)
    {
        ClearAllEggs();

        if (_hardFrozen)
        {
            _hardFrozenEggWorld.Clear();
            _hardFrozenFacing.Clear();
        }

        if (resetHistoryToOwner)
            ResetHistoryToCurrentOwnerPos();

        ResetRuntimeState();
        PostQueueChanged(animateShift);
    }

    void EnqueueInternal(ItemType type, Sprite idleSprite, AudioClip mountSfx, float mountVolume, bool animate)
    {
        EnsureBound();

        if (IsFull)
        {
            return;
        }

        if (_eggs.Count == 0)
            ResetRuntimeState();

        Vector3 spawnWorld = GetOwnerWorldPos() + (Vector3)worldOffset;
        spawnWorld.z = 0f;

        Transform rootTr;
        EggFollowerDirectionalVisual directional = null;

        if (eggFollowerPrefab != null)
        {
            var rootGo = Instantiate(eggFollowerPrefab, spawnWorld, Quaternion.identity, _worldRoot);
            rootGo.name = $"EggFollower_{type}";
            rootTr = rootGo.transform;

            BindEggHitbox(rootGo, this);
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

        var entry = new EggEntry
        {
            type = type,
            rootTr = rootTr,
            directional = directional,

            mountSfx = mountSfx,
            mountVolume = Mathf.Clamp01(mountVolume),
            route = new Queue<Vector3>(),
            routeTail = spawnWorld - (Vector3)worldOffset,
            groundPosition = spawnWorld - (Vector3)worldOffset
        };

        _eggs.Insert(0, entry);

    }

    static void BindEggHitbox(GameObject eggRootGo, MountEggQueue queue)
    {
        if (eggRootGo == null || queue == null)
            return;

        if (!eggRootGo.TryGetComponent<EggQueueFollowerHitbox>(out var hitbox))
            hitbox = eggRootGo.GetComponentInChildren<EggQueueFollowerHitbox>(true);

        if (hitbox != null)
            hitbox.Bind(queue);
    }

    #endregion

    #region Transfer / World Queue Helpers

    public void TransferToDetachedLouieAndFreeze(GameObject detachedLouie, Vector3 freezeWorldPos)
    {
        if (detachedLouie == null || _eggs.Count == 0)
            return;

        if (!detachedLouie.TryGetComponent<MountEggQueue>(out var target))
            target = detachedLouie.AddComponent<MountEggQueue>();

        CopySettingsTo(target);

        target.EnsureWorldRoot();

        target._eggs.Clear();
        for (int i = 0; i < _eggs.Count; i++)
            target._eggs.Add(_eggs[i]);

        for (int i = 0; i < target._eggs.Count; i++)
        {
            var e = target._eggs[i];
            if (e.rootTr != null)
                BindEggHitbox(e.rootTr.gameObject, target);
        }

        _eggs.Clear();

        target._ownerPlayerId = -1;
        target.FreezeOwnerAtWorldPosition(freezeWorldPos);
        target.BeginHardFreeze();
        target.PostQueueChanged(animateShift: false);

        EnsureBound();
        SeedHistoryNow();
        ResetRuntimeState();
        PostQueueChanged(animateShift: false);
    }

    void CopySettingsTo(MountEggQueue q)
    {
        q.jitterIgnoreDelta = jitterIgnoreDelta;

        q.eggSpacingWorld = eggSpacingWorld;
        q.worldOffset = worldOffset;

        q.worldRootName = worldRootName;

        q.eggGameObjectLayer = eggGameObjectLayer;
        q.eggSortingLayerName = eggSortingLayerName;
        q.eggBaseSortingOrder = eggBaseSortingOrder;

        q.maxEggsInQueue = maxEggsInQueue;

        q.eggFollowerPrefab = eggFollowerPrefab;

        q.idleEnterSeconds = idleEnterSeconds;
        q.movingHoldSeconds = movingHoldSeconds;

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

        q.dequeueDestroySeconds = dequeueDestroySeconds;
    }

    #endregion

    #region Destroy / Consume Requests

    public void RequestDestroyEgg(Transform anyTransformOnEgg)
    {
        if (anyTransformOnEgg == null || _eggs.Count == 0)
            return;

        int idx = FindEggIndexByTransform(anyTransformOnEgg);
        if (idx < 0)
            return;

        var e = _eggs[idx];
        var tr = e.rootTr;

        RemoveEggAtIndexNoDestroy(idx);
        PostQueueChanged(animateShift: true);

        if (tr == null)
            return;

        StartCoroutineSafe(DestroyEggRoutine(tr, 0.5f, byExplosion: true));
    }

    IEnumerator DestroyEggRoutine(Transform tr, float seconds, bool byExplosion = false)
    {
        if (tr == null)
            yield break;

        if (!tr.TryGetComponent<EggFollowerDestroyVisual>(out var v))
            v = tr.GetComponentInChildren<EggFollowerDestroyVisual>(true);

        if (v != null)
        {
            if (byExplosion)
            {
                v.PlayExplosionDestroy();
                seconds = Mathf.Max(seconds, v.GetExplosionThenDestroyDuration());
            }
            else
            {
                v.PlayDestroy();
            }
        }

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
        if (anyTransformOnEgg == null || consumerPlayer == null || _eggs.Count == 0)
            return false;

        if (_ownerPlayerId == -1)
            CacheOwnerIdentity();

        int consumerId = ResolvePlayerIdFrom(consumerPlayer);
        if (_ownerPlayerId != -1 && consumerId != -1 && _ownerPlayerId == consumerId)
            return false;

        var rider = consumerPlayer.GetComponent<PlayerRidingController>();
        if (rider != null && rider.IsPlaying)
            return false;

        bool mountedByMovement =
            consumerPlayer.TryGetComponent<MovementController>(out var mv) &&
            mv != null &&
            mv.IsMounted;

        bool mountedByCompanion =
            consumerPlayer.TryGetComponent<PlayerMountCompanion>(out var compCheck) &&
            compCheck != null &&
            compCheck.HasMountedLouie();

        if (mountedByMovement || mountedByCompanion)
            return false;

        if (!consumerPlayer.TryGetComponent<PlayerMountCompanion>(out var comp) || comp == null)
            return false;

        int idx = FindEggIndexByTransform(anyTransformOnEgg);
        if (idx < 0)
            return false;

        var egg = _eggs[idx];
        var eggType = egg.type;

        bool isWorldQueue = _hardFrozen || (_freezeAnchor != null && _ownerTr == _freezeAnchor);

        if (isWorldQueue && idx > 0)
        {
            TransferNewerEggsToConsumer(consumerPlayer, idx);

            idx = FindEggIndexByTransform(anyTransformOnEgg);
            if (idx < 0)
                return false;

            egg = _eggs[idx];
            eggType = egg.type;
        }

        Transform consumedTr = _eggs[idx].rootTr;

        Vector3 startWorldPos = consumerPlayer.transform.position;
        Vector3 targetWorldPos = ResolveConsumedEggMountWorldPosition(consumedTr);
        if (!CanConsumerMountAtWorldPosition(consumerPlayer, targetWorldPos))
            return false;

        RemoveEggAtIndexNoDestroy(idx);
        PostQueueChanged(animateShift: true);

        MountedType mountedType = EggToMountedType(eggType);
        if (mountedType == MountedType.None)
        {
            if (consumedTr != null)
                StartCoroutineSafe(DestroyEggRoutine(consumedTr, Mathf.Max(0.05f, dequeueDestroySeconds), byExplosion: false));

            return false;
        }

        GameObject prefab = comp.GetMountPrefabForType(mountedType);
        if (prefab == null)
        {
            if (consumedTr != null)
                StartCoroutineSafe(DestroyEggRoutine(consumedTr, Mathf.Max(0.05f, dequeueDestroySeconds), byExplosion: false));

            return false;
        }

        GameObject louieWorld = Instantiate(prefab, targetWorldPos, Quaternion.identity);
        PrepareSpawnedLouieWorldForPickup(louieWorld, mountedType, ResolveMountFacingForConsumer(consumerPlayer));

        var sfx = egg.mountSfx != null ? egg.mountSfx : LoadMountSfx(eggType);
        var vol = Mathf.Clamp01(egg.mountSfx != null ? egg.mountVolume : defaultMountVolume);

        if (sfx != null)
            comp.SetNextMountSfx(sfx, vol);

        bool mounted = comp.TryMountExistingLouieFromWorldWithArc(
            louieWorldInstance: louieWorld,
            louieType: mountedType,
            worldQueueToAdopt: null,
            startWorldPos: startWorldPos,
            targetWorldPos: targetWorldPos
        );

        if (consumedTr != null)
            StartCoroutineSafe(DestroyEggRoutine(consumedTr, Mathf.Max(0.05f, dequeueDestroySeconds), byExplosion: false));

        if (!mounted && louieWorld != null)
            Destroy(louieWorld);

        return mounted;
    }

    Vector3 ResolveConsumedEggMountWorldPosition(Transform consumedEggRoot)
    {
        if (consumedEggRoot == null)
            return Vector3.zero;

        Vector3 worldPos = consumedEggRoot.position;

        var tm = ResolveGroundTilemapNear(worldPos);
        if (tm != null)
        {
            Vector3Int cell = tm.WorldToCell(worldPos);
            Vector3 center = tm.GetCellCenterWorld(cell);
            center.z = 0f;
            return center;
        }

        worldPos.z = 0f;
        return worldPos;
    }

    bool CanConsumerMountAtWorldPosition(GameObject consumerPlayer, Vector3 targetWorldPos)
    {
        if (consumerPlayer == null)
            return false;

        var gm = FindAnyObjectByType<GameManager>();
        Tilemap ground = gm != null ? gm.groundTilemap : ResolveGroundTilemapNear(targetWorldPos);
        Tilemap destructible = gm != null ? gm.destructibleTilemap : null;
        Tilemap indestructible = gm != null ? gm.indestructibleTilemap : null;

        Vector3Int cell;
        if (ground != null)
            cell = ground.WorldToCell(targetWorldPos);
        else if (destructible != null)
            cell = destructible.WorldToCell(targetWorldPos);
        else if (indestructible != null)
            cell = indestructible.WorldToCell(targetWorldPos);
        else
            return true;

        if (ground != null && ground.GetTile(cell) == null)
            return false;

        if (indestructible != null && indestructible.GetTile(cell) != null)
            return false;

        if (destructible != null &&
            destructible.GetTile(cell) != null &&
            !ConsumerCanPassDestructibles(consumerPlayer))
        {
            return false;
        }

        return true;
    }

    bool ConsumerCanPassDestructibles(GameObject consumerPlayer)
    {
        int playerId = ResolvePlayerIdFrom(consumerPlayer);
        if (playerId >= 1 && playerId <= 6 && PlayerPersistentStats.Get(playerId).CanPassDestructibles)
            return true;

        if (!consumerPlayer.TryGetComponent(out AbilitySystem abilitySystem) || abilitySystem == null)
            return false;

        abilitySystem.RebuildCache();
        return abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
    }

    Vector2 ResolveMountFacingForConsumer(GameObject consumerPlayer)
    {
        Vector2 face = Vector2.down;

        if (consumerPlayer != null &&
            consumerPlayer.TryGetComponent<MovementController>(out var mv) &&
            mv != null)
        {
            if (mv.Direction != Vector2.zero)
                face = mv.Direction;
            else if (mv.FacingDirection != Vector2.zero)
                face = mv.FacingDirection;
        }

        if (Mathf.Abs(face.x) >= Mathf.Abs(face.y))
            return face.x >= 0f ? Vector2.right : Vector2.left;

        return face.y >= 0f ? Vector2.up : Vector2.down;
    }

    MountedType EggToMountedType(ItemType eggType)
    {
        switch (eggType)
        {
            case ItemType.BlueLouieEgg: return MountedType.Blue;
            case ItemType.BlackLouieEgg: return MountedType.Black;
            case ItemType.PurpleLouieEgg: return MountedType.Purple;
            case ItemType.GreenLouieEgg: return MountedType.Green;
            case ItemType.YellowLouieEgg: return MountedType.Yellow;
            case ItemType.PinkLouieEgg: return MountedType.Pink;
            case ItemType.RedLouieEgg: return MountedType.Red;
            default: return MountedType.None;
        }
    }

    void PrepareSpawnedLouieWorldForPickup(GameObject louieWorld, MountedType mountedType, Vector2 facingDirection)
    {
        if (louieWorld == null)
            return;

        var pickup = louieWorld.GetComponent<MountWorldPickup>();
        if (pickup == null)
            pickup = louieWorld.AddComponent<MountWorldPickup>();

        pickup.Init(mountedType);

        if (louieWorld.TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = true;

        if (louieWorld.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (louieWorld.TryGetComponent<MountMovementController>(out var lm) && lm != null)
            lm.enabled = false;

        if (louieWorld.TryGetComponent<BombController>(out var bc) && bc != null)
            bc.enabled = false;

        if (louieWorld.TryGetComponent<MovementController>(out var mc) && mc != null)
        {
            mc.SetExplosionInvulnerable(false);
            mc.ForceIdleFacing(facingDirection, "QueueEggSpawnIdleFacing");
            mc.EnableExclusiveFromState();
        }
    }

    static Tilemap ResolveGroundTilemapNear(Vector3 worldPos)
    {
        var tilemaps = Object.FindObjectsByType<Tilemap>();
        if (tilemaps == null || tilemaps.Length == 0)
            return null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null)
                continue;

            var cell = tm.WorldToCell(worldPos);
            if (tm.GetTile(cell) != null)
                return tm;
        }

        return null;
    }

    void TransferNewerEggsToConsumer(GameObject consumerPlayer, int idxExclusive)
    {
        if (consumerPlayer == null || idxExclusive <= 0 || _eggs.Count == 0)
            return;

        if (!consumerPlayer.TryGetComponent<MountEggQueue>(out var consumerQueue) || consumerQueue == null)
            consumerQueue = consumerPlayer.AddComponent<MountEggQueue>();

        CopySettingsTo(consumerQueue);

        if (consumerPlayer.TryGetComponent<MovementController>(out var newOwnerMove))
            consumerQueue.BindOwner(newOwnerMove);
        else
            consumerQueue.BindOwnerAuto();

        var transfer = new List<EggEntry>(idxExclusive);
        for (int i = 0; i < idxExclusive; i++)
            transfer.Add(_eggs[i]);

        for (int i = 0; i < idxExclusive && _eggs.Count > 0; i++)
            _eggs.RemoveAt(0);

        for (int i = transfer.Count - 1; i >= 0; i--)
        {
            if (consumerQueue.IsFull)
                break;

            var e = transfer[i];

            consumerQueue._eggs.Insert(0, e);

            if (e.rootTr != null)
                BindEggHitbox(e.rootTr.gameObject, consumerQueue);
        }

        consumerQueue.EnsureWorldRoot();

        consumerQueue.SeedHistoryNow();
        consumerQueue.ResetRuntimeState();
        consumerQueue.PostQueueChanged(animateShift: false);
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

    void RemoveEggAtIndexNoDestroy(int idx)
    {
        if (idx < 0 || idx >= _eggs.Count)
            return;

        _eggs.RemoveAt(idx);

        if (_hardFrozen)
        {
            if (idx < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(idx);
            if (idx < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(idx);
        }
    }

    static void MountFromEggType(PlayerMountCompanion comp, ItemType eggType)
    {
        if (comp == null) return;

        switch (eggType)
        {
            case ItemType.BlueLouieEgg: comp.MountBlueLouie(); break;
            case ItemType.BlackLouieEgg: comp.MountBlackLouie(); break;
            case ItemType.PurpleLouieEgg: comp.MountPurpleLouie(); break;
            case ItemType.GreenLouieEgg: comp.MountGreenLouie(); break;
            case ItemType.YellowLouieEgg: comp.MountYellowLouie(); break;
            case ItemType.PinkLouieEgg: comp.MountPinkLouie(); break;
            case ItemType.RedLouieEgg: comp.MountRedLouie(); break;
        }
    }

    public void SetIgnoreOwnerInvulnerability(bool ignore)
    {
        _ignoreOwnerInvulnerability = ignore;
    }

    bool PruneNullEggs()
    {
        if (_eggs.Count == 0)
            return false;

        bool removed = false;

        for (int i = _eggs.Count - 1; i >= 0; i--)
        {
            if (_eggs[i].rootTr != null)
                continue;

            _eggs.RemoveAt(i);
            removed = true;

            if (_hardFrozen)
            {
                if (i < _hardFrozenEggWorld.Count) _hardFrozenEggWorld.RemoveAt(i);
                if (i < _hardFrozenFacing.Count) _hardFrozenFacing.RemoveAt(i);
            }
        }

        return removed;
    }

    public void AllowEggExplosionDamageForFrames(int frames = 2)
    {
        frames = Mathf.Clamp(frames, 1, 10);
        _ignoreOwnerInvulnerabilityUntilFrame = Time.frameCount + frames;
    }

    #endregion

    #region Freeze Owner / Absorb

    public void FreezeOwnerAtWorldPosition(Vector3 worldPos)
    {
        EnsureWorldRoot();

        worldPos.z = 0f;

        if (_freezeAnchor == null)
            _freezeAnchor = new GameObject("EggQueue_FreezeAnchor").transform;

        _freezeAnchor.SetParent(null, true);
        _freezeAnchor.SetPositionAndRotation(worldPos, Quaternion.identity);
        _freezeAnchor.localScale = Vector3.one;

        _ownerMove = null;
        _ownerRb = null;
        _ownerTr = _freezeAnchor;
        _ownerHealth = null;

        ResetRuntimeState();
    }

    public void AbsorbAllEggsFromWorldQueue(MountEggQueue worldQueue, MovementController newOwner)
    {
        if (worldQueue == null || newOwner == null)
            return;

        worldQueue._hardFrozen = false;

        worldQueue.CopySettingsTo(this);
        BindOwner(newOwner);

        for (int i = 0; i < worldQueue._eggs.Count; i++)
        {
            if (IsFull)
                break;

            var e = worldQueue._eggs[i];
            _eggs.Insert(Mathf.Min(i, _eggs.Count), e);

            if (e.rootTr != null)
                BindEggHitbox(e.rootTr.gameObject, this);
        }

        worldQueue._eggs.Clear();

        EnsureWorldRoot();

        SeedHistoryNow();
        ResetRuntimeState();
        PostQueueChanged(animateShift: false);
    }

    #endregion

    #region Player Id / Identity

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
        if (_ownerMove != null) _ownerPlayerId = ResolvePlayerIdFrom(_ownerMove.gameObject);
        else if (_ownerTr != null) _ownerPlayerId = ResolvePlayerIdFrom(_ownerTr.gameObject);
        else _ownerPlayerId = ResolvePlayerIdFrom(gameObject);

        if (_ownerPlayerId < 1 || _ownerPlayerId > 6)
            _ownerPlayerId = -1;
    }

    #endregion
}
