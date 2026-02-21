using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FloatingPlatform : MonoBehaviour
{
    [Header("Path (World Positions)")]
    [SerializeField] private Vector2 pointA = new(-7.5f, 3.5f);
    [SerializeField] private Vector2 pointB = new(-7.5f, 0.5f);

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopSeconds = 2f;

    [Header("Time")]
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Mount")]
    [SerializeField] private bool snapRiderToCenterOnMount = true;
    [SerializeField] private bool roundRiderToGridOnSnap = true;
    [SerializeField] private bool lockInputWhileMoving = true;
    [SerializeField] private bool forceIdleOnLock = true;

    [Header("Follow While Moving")]
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Rider Position On Platform")]
    [SerializeField] private Vector2 riderLocalOffset = Vector2.zero;

    [Header("Remount Safety")]
    [SerializeField, Min(0f)] private float remountBlockSeconds = 0.15f;

    [Header("Unmount Offset")]
    [SerializeField] private bool applyUnmountOffset = true;
    [SerializeField] private Vector2 unmountOffsetUpperTiles = new(0f, 1f);
    [SerializeField] private Vector2 unmountOffsetLowerTiles = new(0f, -1f);

    private BoxCollider2D _col;
    private Rigidbody2D _rb;

    private bool _isMoving;
    private bool _isStopping;
    private bool _atA = true;

    private float _nextAllowedMountTime;
    private int _lastUnmountFrame = -999;

    private Coroutine _loop;

    private readonly List<MovementController> _riders = new();
    private readonly Dictionary<MovementController, Rigidbody2D> _riderRb = new();

    private static readonly HashSet<MovementController> ridersOnPlatforms = new();
    private static readonly Dictionary<MovementController, FloatingPlatform> riderToPlatform = new();

    public static bool TryGetPlatformForRider(MovementController mc, out FloatingPlatform platform)
    {
        platform = null;
        if (mc == null) return false;
        return riderToPlatform.TryGetValue(mc, out platform) && platform != null;
    }

    public static bool IsRidingPlatform(MovementController mc)
    {
        if (mc == null) return false;
        return ridersOnPlatforms.Contains(mc);
    }

    public bool IsMoving => _isMoving;
    public bool IsIdleAtStop => _isStopping && !_isMoving;

    public bool IsAtAStop => IsIdleAtStop && _atA;
    public bool IsAtBStop => IsIdleAtStop && !_atA;

    public bool LowerStopIsA => pointA.y <= pointB.y;
    public bool IsAtLowerStop => LowerStopIsA ? IsAtAStop : IsAtBStop;
    public bool IsAtUpperStop => LowerStopIsA ? IsAtBStop : IsAtAStop;

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;

        TryGetComponent(out _rb);

        if ((Vector2)transform.position == Vector2.zero)
            transform.position = pointA;

        _loop = StartCoroutine(MoveLoop());
    }

    private void OnEnable()
    {
        if (_loop == null && gameObject.activeInHierarchy)
            _loop = StartCoroutine(MoveLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        ForceUnmountAllInternal();
    }

    private void LateUpdate()
    {
        if (_riders.Count == 0) return;

        if (snapRiderToCenterOnMount)
            SnapAllRidersToPlatformCenter();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == null) return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null) return;

        if (mc.CompareTag("Player"))
            TryHandlePlatformInput(mc);
    }

    private IEnumerator MoveLoop()
    {
        Vector2 startPos = GetWorldPos2D();
        _atA = Vector2.Distance(startPos, pointA) <= Vector2.Distance(startPos, pointB);

        while (true)
        {
            _isMoving = false;
            _isStopping = true;

            UnlockAllRidersIfAny();

            float stop = Mathf.Max(0f, stopSeconds);
            if (stop > 0f)
            {
                if (ignoreTimeScale) yield return new WaitForSecondsRealtime(stop);
                else yield return new WaitForSeconds(stop);
            }
            else
            {
                yield return null;
            }

            _isStopping = false;

            Vector2 target = _atA ? pointB : pointA;

            if (_riders.Count > 0)
                BeginCarryAllRiders();

            _isMoving = true;

            while (Vector2.Distance(GetWorldPos2D(), target) > 0.001f)
            {
                float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

                Vector2 p = GetWorldPos2D();
                Vector2 np = Vector2.MoveTowards(p, target, moveSpeed * dt);
                SetWorldPos2D(np);

                yield return null;
            }

            SetWorldPos2D(target);

            _isMoving = false;
            _isStopping = true;

            UnlockAllRidersIfAny();

            _atA = !_atA;
        }
    }

    private Vector2 GetWorldPos2D() => _rb != null ? _rb.position : (Vector2)transform.position;

    private void SetWorldPos2D(Vector2 p)
    {
        if (_rb != null)
            _rb.MovePosition(p);
        else
            transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    private Vector2 GetPlatformCenter()
    {
        if (_col != null)
            return (Vector2)_col.bounds.center;

        return GetWorldPos2D();
    }

    private int GetPlayerId(MovementController mc)
    {
        if (mc == null) return 1;

        var pid = mc.GetComponentInParent<PlayerIdentity>();
        if (pid != null)
            return Mathf.Clamp(pid.playerId, 1, 4);

        return Mathf.Clamp(mc.PlayerId, 1, 4);
    }

    private Vector2 GetSharedRiderTargetWorldPos()
        => GetPlatformCenter() + followOffset + riderLocalOffset;

    private void SnapAllRidersToPlatformCenter()
    {
        Vector2 target = GetSharedRiderTargetWorldPos();

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null) continue;

            if (_riderRb.TryGetValue(mc, out var rb) && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = target;
            }
            else
            {
                mc.transform.position = target;
            }
        }
    }

    private void BeginCarryAllRiders()
    {
        if (_riders.Count == 0) return;

        if (snapRiderToCenterOnMount)
            SnapAllRidersToPlatformCenter();

        if (!lockInputWhileMoving) return;

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null) continue;
            mc.SetInputLocked(true, forceIdleOnLock);
        }
    }

    private void UnlockAllRidersIfAny()
    {
        if (_riders.Count == 0) return;
        if (!lockInputWhileMoving) return;

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null) continue;
            mc.SetInputLocked(false);
        }
    }

    public bool CanMount(MovementController mc)
    {
        if (mc == null) return false;
        if (mc.isDead) return false;
        if (!mc.CompareTag("Player")) return false;
        if (IsRidingPlatform(mc)) return false;
        if (Time.frameCount == _lastUnmountFrame) return false;
        if (Time.time < _nextAllowedMountTime) return false;
        if (!IsIdleAtStop) return false;

        return true;
    }

    public bool TryMount(MovementController mc)
    {
        if (!CanMount(mc))
            return false;

        if (_riders.Contains(mc))
            return false;

        _riders.Add(mc);
        _riderRb[mc] = mc.Rigidbody;

        ridersOnPlatforms.Add(mc);
        riderToPlatform[mc] = this;

        if (snapRiderToCenterOnMount)
        {
            Vector2 target = GetSharedRiderTargetWorldPos();
            mc.SnapToWorldPoint(target, roundRiderToGridOnSnap);
        }

        if (_isMoving && lockInputWhileMoving)
            mc.SetInputLocked(true, forceIdleOnLock);
        else
            mc.SetInputLocked(false);

        return true;
    }

    public bool TryUnmount(MovementController mover)
    {
        if (mover == null) return false;
        if (!_riders.Contains(mover)) return false;
        if (!IsIdleAtStop) return false;

        float tileSize = mover.tileSize > 0f ? mover.tileSize : 1f;

        bool upper = IsAtUpperStop;
        Vector2 offsetTiles = upper ? unmountOffsetUpperTiles : unmountOffsetLowerTiles;
        Vector2 offset = applyUnmountOffset ? offsetTiles * tileSize : Vector2.zero;

        Vector2 basePos = GetSharedRiderTargetWorldPos();
        Vector2 targetPos = basePos + offset;

        ForceUnmountInternal(mover);

        _lastUnmountFrame = Time.frameCount;
        _nextAllowedMountTime = Time.time + Mathf.Max(0f, remountBlockSeconds);

        mover.SnapToWorldPoint(targetPos, roundRiderToGridOnSnap);
        return true;
    }

    private void ForceUnmountInternal(MovementController mc)
    {
        if (mc == null) return;

        if (lockInputWhileMoving)
            mc.SetInputLocked(false);

        ridersOnPlatforms.Remove(mc);
        riderToPlatform.Remove(mc);

        _riderRb.Remove(mc);
        _riders.Remove(mc);
    }

    private void ForceUnmountAllInternal()
    {
        var copy = new List<MovementController>(_riders);
        for (int i = 0; i < copy.Count; i++)
            ForceUnmountInternal(copy[i]);

        _riders.Clear();
        _riderRb.Clear();
    }

    private bool TryHandlePlatformInput(MovementController mc)
    {
        if (mc == null) return false;
        if (!mc.CompareTag("Player")) return false;
        if (!IsIdleAtStop) return false;

        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        int pid = GetPlayerId(mc);

        bool up = input.Get(pid, PlayerAction.MoveUp);
        bool down = input.Get(pid, PlayerAction.MoveDown);

        bool isRider = _riders.Contains(mc);

        if (IsAtLowerStop)
        {
            if (!isRider && up)
                return TryMount(mc);

            if (isRider && down)
                return TryUnmount(mc);
        }

        if (IsAtUpperStop)
        {
            if (!isRider && down)
                return TryMount(mc);

            if (isRider && up)
                return TryUnmount(mc);
        }

        return false;
    }
}