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

    [Header("Pixel Perfect Step")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField] private bool useIntegerPixelSteps = true;

    private BoxCollider2D _col;
    private Rigidbody2D _rb;

    private bool _isMoving;
    private bool _isStopping;
    private bool _atA = true;

    private float _stopTimer;

    private float _nextAllowedMountTime;
    private int _lastUnmountFrame = -999;

    private readonly List<MovementController> _riders = new();
    private readonly Dictionary<MovementController, Rigidbody2D> _riderRb = new();

    private static readonly HashSet<MovementController> ridersOnPlatforms = new();
    private static readonly Dictionary<MovementController, FloatingPlatform> riderToPlatform = new();

    private float _accPixelsX;
    private float _accPixelsY;
    private Vector2 _lastMoveDirCardinal = Vector2.zero;

    public static bool TryGetPlatformForRider(MovementController mc, out FloatingPlatform platform)
    {
        platform = null;
        if (mc == null)
            return false;

        return riderToPlatform.TryGetValue(mc, out platform) && platform != null;
    }

    public static bool IsRidingPlatform(MovementController mc)
    {
        if (mc == null)
            return false;

        return ridersOnPlatforms.Contains(mc);
    }

    public bool IsMoving => _isMoving;
    public bool IsIdleAtStop => _isStopping && !_isMoving;

    public bool IsAtAStop => IsIdleAtStop && _atA;
    public bool IsAtBStop => IsIdleAtStop && !_atA;

    public bool LowerStopIsA => pointA.y <= pointB.y;
    public bool IsAtLowerStop => LowerStopIsA ? IsAtAStop : IsAtBStop;
    public bool IsAtUpperStop => LowerStopIsA ? IsAtBStop : IsAtAStop;

    private float PixelWorldStep => pixelsPerUnit > 0 ? (1f / pixelsPerUnit) : 0.0625f;

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
        _col.isTrigger = true;

        TryGetComponent(out _rb);

        pointA = QuantizeToPixelGrid(pointA);
        pointB = QuantizeToPixelGrid(pointB);

        Vector2 startPos = GetWorldPos2D();

        if (startPos == Vector2.zero)
        {
            startPos = pointA;
            SetWorldPos2DImmediate(startPos);
        }
        else
        {
            startPos = QuantizeToPixelGrid(startPos);
            SetWorldPos2DImmediate(startPos);
        }

        _atA = Vector2.Distance(startPos, pointA) <= Vector2.Distance(startPos, pointB);
        _isMoving = false;
        _isStopping = true;
        _stopTimer = Mathf.Max(0f, stopSeconds);

        ResetPixelAccumulators();
    }

    private void OnEnable()
    {
        ResetPixelAccumulators();
    }

    private void OnDisable()
    {
        ForceUnmountAllInternal();
        ResetPixelAccumulators();
    }

    private void LateUpdate()
    {
        if (_riders.Count == 0)
            return;

        if (snapRiderToCenterOnMount)
            SnapAllRidersToPlatformCenter();
    }

    private void FixedUpdate()
    {
        if (_isStopping)
        {
            TickStopState();
            return;
        }

        TickMoveState();
    }

    private void TickStopState()
    {
        _isMoving = false;
        _isStopping = true;

        UnlockAllRidersIfAny();
        ResetPixelAccumulators();

        if (_stopTimer > 0f)
        {
            float dt = GetStepDeltaTime();
            _stopTimer -= dt;

            if (_stopTimer > 0f)
                return;
        }

        _stopTimer = 0f;
        _isStopping = false;

        if (_riders.Count > 0)
            BeginCarryAllRiders();

        _isMoving = true;
    }

    private void TickMoveState()
    {
        Vector2 target = _atA ? pointB : pointA;
        target = QuantizeToPixelGrid(target);

        Vector2 position = QuantizeToPixelGrid(GetWorldPos2D());

        Vector2 moveDir = NormalizeCardinal(target - position);
        float rawStep = moveSpeed * GetStepDeltaTime();
        float moveWorld = GetQuantizedMoveWorldPerStep(moveDir, rawStep);

        if (moveWorld <= 0f)
        {
            MoveWorldPosPixelPerfect(position);
            return;
        }

        Vector2 next = Vector2.MoveTowards(position, target, moveWorld);
        next = QuantizeToPixelGrid(next);

        bool reached =
            Mathf.Abs(next.x - target.x) <= 0.0001f &&
            Mathf.Abs(next.y - target.y) <= 0.0001f;

        if (reached)
        {
            next = target;
            MoveWorldPosPixelPerfect(next);

            _isMoving = false;
            _isStopping = true;
            _atA = !_atA;
            _stopTimer = Mathf.Max(0f, stopSeconds);
            ResetPixelAccumulators();
            return;
        }

        MoveWorldPosPixelPerfect(next);
    }

    private float GetStepDeltaTime()
    {
        return ignoreTimeScale ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime;
    }

    private Vector2 GetWorldPos2D()
    {
        return _rb != null ? _rb.position : (Vector2)transform.position;
    }

    private void SetWorldPos2DImmediate(Vector2 p)
    {
        p = QuantizeToPixelGrid(p);

        if (_rb != null)
            _rb.position = p;
        else
            transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    private void MoveWorldPosPixelPerfect(Vector2 p)
    {
        p = QuantizeToPixelGrid(p);

        if (_rb != null)
            _rb.MovePosition(p);
        else
            transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    private Vector2 GetPlatformCenter()
    {
        if (_col != null)
            return QuantizeToPixelGrid((Vector2)_col.bounds.center);

        return QuantizeToPixelGrid(GetWorldPos2D());
    }

    private int GetPlayerId(MovementController mc)
    {
        if (mc == null)
            return 1;

        var pid = mc.GetComponentInParent<PlayerIdentity>();
        if (pid != null)
            return Mathf.Clamp(pid.playerId, 1, 4);

        return Mathf.Clamp(mc.PlayerId, 1, 4);
    }

    private Vector2 GetSharedRiderTargetWorldPos()
    {
        return QuantizeToPixelGrid(GetPlatformCenter() + followOffset + riderLocalOffset);
    }

    private void SnapAllRidersToPlatformCenter()
    {
        Vector2 target = GetSharedRiderTargetWorldPos();

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null)
                continue;

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
        if (_riders.Count == 0)
            return;

        if (snapRiderToCenterOnMount)
            SnapAllRidersToPlatformCenter();

        if (!lockInputWhileMoving)
            return;

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null)
                continue;

            mc.SetInputLocked(true, forceIdleOnLock);
        }
    }

    private void UnlockAllRidersIfAny()
    {
        if (_riders.Count == 0)
            return;

        if (!lockInputWhileMoving)
            return;

        for (int i = 0; i < _riders.Count; i++)
        {
            var mc = _riders[i];
            if (mc == null)
                continue;

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
        Vector2 targetPos = QuantizeToPixelGrid(basePos + offset);

        ForceUnmountInternal(mover);

        _lastUnmountFrame = Time.frameCount;
        _nextAllowedMountTime = Time.time + Mathf.Max(0f, remountBlockSeconds);

        mover.SnapToWorldPoint(targetPos, roundRiderToGridOnSnap);
        return true;
    }

    private void ForceUnmountInternal(MovementController mc)
    {
        if (mc == null)
            return;

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

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == null)
            return;

        var mc = other.GetComponentInParent<MovementController>();
        if (mc == null)
            return;

        if (mc.CompareTag("Player"))
            TryHandlePlatformInput(mc);
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

    private Vector2 QuantizeToPixelGrid(Vector2 world)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return world;

        float ppu = pixelsPerUnit;

        return new Vector2(
            Mathf.Round(world.x * ppu) / ppu,
            Mathf.Round(world.y * ppu) / ppu
        );
    }

    private void ResetPixelAccumulators()
    {
        _accPixelsX = 0f;
        _accPixelsY = 0f;
        _lastMoveDirCardinal = Vector2.zero;
    }

    private float GetQuantizedMoveWorldPerStep(Vector2 moveDir, float rawWorldStep)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return rawWorldStep;

        moveDir = NormalizeCardinal(moveDir);
        if (moveDir == Vector2.zero)
            return 0f;

        if (moveDir != _lastMoveDirCardinal)
        {
            _lastMoveDirCardinal = moveDir;
            _accPixelsX = 0f;
            _accPixelsY = 0f;
        }

        float rawPixels = rawWorldStep * pixelsPerUnit;

        if (Mathf.Abs(moveDir.x) > 0.01f)
        {
            _accPixelsX += rawPixels * Mathf.Sign(moveDir.x);

            int whole = (int)_accPixelsX;
            _accPixelsX -= whole;

            return Mathf.Abs(whole) * PixelWorldStep;
        }

        _accPixelsY += rawPixels * Mathf.Sign(moveDir.y);

        int wholeY = (int)_accPixelsY;
        _accPixelsY -= wholeY;

        return Mathf.Abs(wholeY) * PixelWorldStep;
    }

    private static Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.000001f)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }
}