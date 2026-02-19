using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FloatingPlatform : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool dbg = true;
    [SerializeField, Min(0.05f)] private float dbgMoveLogEverySeconds = 0.35f;

    [Header("Path (World Positions)")]
    [SerializeField] private Vector2 pointA = new Vector2(-7.5f, 3.5f);
    [SerializeField] private Vector2 pointB = new Vector2(-7.5f, 0.5f);

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopSeconds = 2f;

    [Header("Anchor")]
    [SerializeField, Min(0.001f)] private float anchorCheckDistance = 0.25f;

    [Header("Mount")]
    [SerializeField] private bool snapRiderToCenterOnMount = true;
    [SerializeField] private bool roundRiderToGridOnSnap = true;
    [SerializeField] private bool lockInputWhileMoving = true;
    [SerializeField] private bool forceIdleOnLock = true;

    [Header("Follow While Moving")]
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Remount Safety")]
    [SerializeField, Min(0f)] private float remountBlockSeconds = 0.15f;

    private BoxCollider2D _col;
    private Rigidbody2D _rb;

    private MovementController _rider;
    private Rigidbody2D _riderRb;

    private bool _isMoving;
    private bool _isStopping;
    private bool _atA = true;

    private float _nextAllowedMountTime;
    private int _lastUnmountFrame = -999;

    private MovementController _remountBlockedRider;
    private bool _blockRemountUntilExit;

    private Coroutine _loop;

    private static readonly HashSet<MovementController> ridersOnPlatforms = new();
    private static readonly Dictionary<MovementController, FloatingPlatform> riderToPlatform = new();

    private string DbgPrefix => $"[FloatingPlatform#{GetInstanceID()} '{name}']";

    private void Dbg(string msg)
    {
        if (!dbg) return;
        Debug.Log($"{DbgPrefix} {msg}", this);
    }

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
    public bool IsStopped => _isStopping && !_isMoving;
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

        Dbg($"Awake. pos={transform.position} A={pointA} B={pointB} rb={(_rb != null ? _rb.bodyType.ToString() : "NONE")}");

        _loop = StartCoroutine(MoveLoop());
    }

    private void OnEnable()
    {
        Dbg("OnEnable.");
        if (_loop == null && gameObject.activeInHierarchy)
            _loop = StartCoroutine(MoveLoop());
    }

    private void OnDisable()
    {
        Dbg("OnDisable.");

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
        if (_rider == null || _riderRb == null)
            return;

        if (_isMoving)
            SnapRiderToPlatformCenter();
    }

    private IEnumerator MoveLoop()
    {
        Dbg($"MoveLoop START. timeScale={Time.timeScale:0.###} active={isActiveAndEnabled}");

        Vector2 startPos = GetWorldPos2D();
        _atA = Vector2.Distance(startPos, pointA) <= Vector2.Distance(startPos, pointB);

        Dbg($"Initial: startPos={startPos} atA={_atA}");

        float nextMoveDbg = 0f;

        while (true)
        {
            _isMoving = false;
            _isStopping = true;

            UnlockRiderIfAny();

            float stop = Mathf.Max(0f, stopSeconds);
            Dbg($"STOP begin. stopSeconds={stop:0.###} pos={GetWorldPos2D()} rider={(_rider != null ? _rider.name : "none")}");

            if (stop > 0f) yield return new WaitForSeconds(stop);
            else yield return null;

            _isStopping = false;

            Vector2 target = _atA ? pointB : pointA;

            if (_rider != null)
                BeginCarryRider();

            _isMoving = true;
            Dbg($"MOVE begin -> target={target} moveSpeed={moveSpeed:0.###}");

            while (Vector2.Distance(GetWorldPos2D(), target) > 0.001f)
            {
                Vector2 p = GetWorldPos2D();
                Vector2 np = Vector2.MoveTowards(p, target, moveSpeed * Time.deltaTime);

                SetWorldPos2D(np);

                if (dbg && Time.time >= nextMoveDbg)
                {
                    nextMoveDbg = Time.time + Mathf.Max(0.05f, dbgMoveLogEverySeconds);
                    Dbg($"MOVE tick pos={np} dist={Vector2.Distance(np, target):0.###} dt={Time.deltaTime:0.###}");
                }

                yield return null;
            }

            SetWorldPos2D(target);

            _isMoving = false;
            _isStopping = true;

            UnlockRiderIfAny();

            _atA = !_atA;
            Dbg($"MOVE end. snappedTo={target} nowAtA={_atA}");
        }
    }

    private Vector2 GetWorldPos2D() => _rb != null ? _rb.position : (Vector2)transform.position;

    private void SetWorldPos2D(Vector2 p)
    {
        if (_rb != null)
        {
            _rb.MovePosition(p);
            return;
        }

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

        Vector2 center = GetPlatformCenter() + followOffset;

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

        Dbg($"BeginCarryRider rider={_rider.name} lockInput={lockInputWhileMoving} snap={snapRiderToCenterOnMount}");
    }

    private void UnlockRiderIfAny()
    {
        if (_rider == null) return;

        if (lockInputWhileMoving)
            _rider.SetInputLocked(false);

        Dbg($"UnlockRiderIfAny rider={_rider.name}");
    }

    public bool IsAnchoredAt(Vector2 worldPoint, out string reason)
    {
        Vector2 c = GetPlatformCenter();
        float d = Vector2.Distance(c, worldPoint);

        bool ok = d <= anchorCheckDistance;
        reason = ok ? "ok" : $"too far (d={d:0.000})";
        return ok;
    }

    public bool IsRemountBlockedFor(MovementController mc)
    {
        if (mc == null) return false;
        return _blockRemountUntilExit && _remountBlockedRider == mc;
    }

    public void ClearRemountBlock(MovementController mc)
    {
        if (mc == null) return;
        if (!_blockRemountUntilExit) return;
        if (_remountBlockedRider != mc) return;

        _blockRemountUntilExit = false;
        _remountBlockedRider = null;

        Dbg($"ClearRemountBlock for {mc.name}");
    }

    public bool CanMount(MovementController mc, out string reason)
    {
        if (mc == null) { reason = "mc NULL"; return false; }
        if (HasRider) { reason = "platform already has rider"; return false; }
        if (mc.isDead) { reason = "mc.isDead=true"; return false; }
        if (!mc.CompareTag("Player")) { reason = "mc tag != Player"; return false; }

        if (IsRidingPlatform(mc)) { reason = "mc already riding another platform"; return false; }
        if (IsRemountBlockedFor(mc)) { reason = "remount blocked until exit"; return false; }
        if (Time.frameCount == _lastUnmountFrame) { reason = "blocked same frame as unmount"; return false; }
        if (Time.time < _nextAllowedMountTime) { reason = "blocked by cooldown"; return false; }

        if (!IsIdleAtStop) { reason = $"platform not stopped (stopping={_isStopping} moving={_isMoving})"; return false; }

        reason = "ok";
        return true;
    }

    public bool TryMount(MovementController mc, out string reason)
    {
        if (!CanMount(mc, out var canReason))
        {
            reason = $"CanMount=false ({canReason})";
            Dbg($"TryMount FAIL rider={mc?.name} reason={reason}");
            return false;
        }

        _rider = mc;
        _riderRb = mc.Rigidbody;

        ridersOnPlatforms.Add(_rider);
        riderToPlatform[_rider] = this;

        if (snapRiderToCenterOnMount)
        {
            Vector2 center = GetPlatformCenter() + followOffset;
            mc.SnapToWorldPoint(center, roundRiderToGridOnSnap);
        }

        if (_isMoving && lockInputWhileMoving)
            mc.SetInputLocked(true, forceIdleOnLock);
        else
            mc.SetInputLocked(false);

        reason = "ok";
        Dbg($"TryMount OK rider={mc.name} pos={mc.transform.position} stopping={_isStopping} moving={_isMoving}");
        return true;
    }

    public bool TryUnmount(MovementController mover)
    {
        if (mover == null) return false;
        if (!HasRider || mover != _rider) return false;
        if (!IsIdleAtStop) return false;

        bool upper = IsAtUpperStop;
        float tile = mover.tileSize > 0f ? mover.tileSize : 1f;
        Vector2 unmountDelta = new Vector2(0f, upper ? tile : -tile);

        var prev = _rider;

        ForceUnmountInternal();

        Vector2 basePos = prev.Rigidbody != null ? prev.Rigidbody.position : (Vector2)prev.transform.position;
        Vector2 targetPos = basePos + unmountDelta;
        prev.SnapToWorldPoint(targetPos, roundRiderToGridOnSnap);

        _lastUnmountFrame = Time.frameCount;
        _nextAllowedMountTime = Time.time + Mathf.Max(0f, remountBlockSeconds);

        _blockRemountUntilExit = true;
        _remountBlockedRider = prev;

        Dbg($"TryUnmount OK rider={prev.name} upper={upper} delta={unmountDelta} blockSeconds={remountBlockSeconds:0.###} nextAllowed={_nextAllowedMountTime:0.###}");
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

        Dbg($"ForceUnmount rider={prev.name}");
    }

    public bool TryHandlePlatformInput(MovementController mc)
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
                return TryMount(mc, out _);

            if (isRider && down)
                return TryUnmount(mc);
        }

        if (IsAtUpperStop)
        {
            if (!isRider && down)
                return TryMount(mc, out _);

            if (isRider && up)
                return TryUnmount(mc);
        }

        return false;
    }
}
