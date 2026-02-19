using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FloatingPlatform : MonoBehaviour
{
    [Header("Path (World Positions)")]
    [SerializeField] private Vector2 pointA = new Vector2(-7.5f, 3.5f);
    [SerializeField] private Vector2 pointB = new Vector2(-7.5f, 0.5f);

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopSeconds = 2f;

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

    private MovementController _rider;
    private Rigidbody2D _riderRb;

    private bool _isMoving;
    private bool _isStopping;
    private bool _atA = true;

    private float _nextAllowedMountTime;
    private int _lastUnmountFrame = -999;

    private Coroutine _loop;

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

    public bool HasRider => _rider != null;
    public bool IsRider(MovementController mc) => _rider != null && _rider == mc;

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

        if (_rider != null)
        {
            ridersOnPlatforms.Remove(_rider);
            riderToPlatform.Remove(_rider);
            ForceUnmountInternal();
        }
    }

    private void LateUpdate()
    {
        if (_rider == null) return;

        if (snapRiderToCenterOnMount)
            SnapRiderToPlatformCenter();
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

            UnlockRiderIfAny();

            float stop = Mathf.Max(0f, stopSeconds);
            if (stop > 0f) yield return new WaitForSeconds(stop);
            else yield return null;

            _isStopping = false;

            Vector2 target = _atA ? pointB : pointA;

            if (_rider != null)
                BeginCarryRider();

            _isMoving = true;

            while (Vector2.Distance(GetWorldPos2D(), target) > 0.001f)
            {
                Vector2 p = GetWorldPos2D();
                Vector2 np = Vector2.MoveTowards(p, target, moveSpeed * Time.deltaTime);
                SetWorldPos2D(np);
                yield return null;
            }

            SetWorldPos2D(target);

            _isMoving = false;
            _isStopping = true;

            UnlockRiderIfAny();

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

    private void SnapRiderToPlatformCenter()
    {
        if (_rider == null) return;

        Vector2 center = GetPlatformCenter() + followOffset + riderLocalOffset;

        if (_riderRb != null)
        {
            _riderRb.linearVelocity = Vector2.zero;
            _riderRb.position = center;
        }
        else
        {
            _rider.transform.position = center;
        }
    }

    private void BeginCarryRider()
    {
        if (_rider == null) return;

        if (snapRiderToCenterOnMount)
            SnapRiderToPlatformCenter();

        if (lockInputWhileMoving)
            _rider.SetInputLocked(true, forceIdleOnLock);
    }

    private void UnlockRiderIfAny()
    {
        if (_rider == null) return;

        if (lockInputWhileMoving)
            _rider.SetInputLocked(false);
    }

    public bool CanMount(MovementController mc)
    {
        if (mc == null) return false;
        if (HasRider) return false;
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

        _rider = mc;
        _riderRb = mc.Rigidbody;

        ridersOnPlatforms.Add(_rider);
        riderToPlatform[_rider] = this;

        if (snapRiderToCenterOnMount)
        {
            Vector2 center = GetPlatformCenter() + followOffset + riderLocalOffset;
            mc.SnapToWorldPoint(center, roundRiderToGridOnSnap);
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
        if (!HasRider || mover != _rider) return false;
        if (!IsIdleAtStop) return false;

        float tileSize = mover.tileSize > 0f ? mover.tileSize : 1f;

        bool upper = IsAtUpperStop;
        Vector2 offsetTiles = upper ? unmountOffsetUpperTiles : unmountOffsetLowerTiles;
        Vector2 offset = applyUnmountOffset ? offsetTiles * tileSize : Vector2.zero;

        Vector2 basePos = GetPlatformCenter() + followOffset + riderLocalOffset;
        Vector2 targetPos = basePos + offset;

        ForceUnmountInternal();

        _lastUnmountFrame = Time.frameCount;
        _nextAllowedMountTime = Time.time + Mathf.Max(0f, remountBlockSeconds);

        mover.SnapToWorldPoint(targetPos, roundRiderToGridOnSnap);
        return true;
    }

    private void ForceUnmountInternal()
    {
        if (_rider == null) return;

        var prev = _rider;

        if (lockInputWhileMoving)
            prev.SetInputLocked(false);

        ridersOnPlatforms.Remove(prev);
        riderToPlatform.Remove(prev);

        _rider = null;
        _riderRb = null;
    }

    private bool TryHandlePlatformInput(MovementController mc)
    {
        if (mc == null) return false;
        if (!mc.CompareTag("Player")) return false;
        if (!IsIdleAtStop) return false;

        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        bool up = input.Get(mc.PlayerId, PlayerAction.MoveUp);
        bool down = input.Get(mc.PlayerId, PlayerAction.MoveDown);

        bool isRider = HasRider && IsRider(mc);

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
