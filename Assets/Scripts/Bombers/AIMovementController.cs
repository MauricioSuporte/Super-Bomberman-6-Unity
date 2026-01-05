using UnityEngine;

public class AIMovementController : MovementController
{
    [Header("AI Control")]
    public Vector2 aiDirection = Vector2.zero;
    public bool isBoss = false;

    [Header("Anti-Stuck")]
    [SerializeField] private float stuckSeconds = 0.30f;
    [SerializeField] private float stuckMoveEpsilon = 0.0015f;
    [SerializeField] private bool avoidWhenForwardBlocked = true;

    [Header("Decision Smoothing")]
    [SerializeField] private bool onlyTurnNearTileCenter = true;
    [SerializeField] private float turnCenterEpsilon = 0.08f;
    [SerializeField] private float commitSeconds = 0.12f;
    [SerializeField] private float minTurnInterval = 0.08f;

    [Header("Hazard Awareness")]
    [SerializeField] private bool avoidActiveExplosions = true;
    [SerializeField] private bool avoidBombTiles = false;
    [SerializeField] private string explosionLayerName = "Explosion";
    [SerializeField] private string bombLayerName = "Bomb";
    [SerializeField] private float hazardCheckBoxSize = 0.42f;

    private Vector2 lastPos;
    private float stuckTimer;

    private Vector2 committedDir = Vector2.zero;
    private Vector2 lastDetour = Vector2.zero;
    private float commitUntilTime;
    private float lastTurnTime;

    private int explosionLayer;
    private int bombLayer;
    private int explosionMask;
    private int bombMask;

    protected override void Awake()
    {
        base.Awake();

        lastPos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
        stuckTimer = 0f;

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        lastTurnTime = -999f;

        explosionLayer = LayerMask.NameToLayer(explosionLayerName);
        bombLayer = LayerMask.NameToLayer(bombLayerName);

        explosionMask = (explosionLayer >= 0) ? (1 << explosionLayer) : 0;
        bombMask = (bombLayer >= 0) ? (1 << bombLayer) : 0;

        if (isBoss)
        {
            if (obstacleMask.value == 0)
                obstacleMask = LayerMask.GetMask("Bomb", "Stage", "Enemy");
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        lastPos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
        stuckTimer = 0f;

        committedDir = Vector2.zero;
        lastDetour = Vector2.zero;
        commitUntilTime = 0f;
        lastTurnTime = -999f;
    }

    void LateUpdate()
    {
        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float moved = (pos - lastPos).sqrMagnitude;
        if (moved > (stuckMoveEpsilon * stuckMoveEpsilon))
        {
            stuckTimer = 0f;
            lastPos = pos;
            return;
        }

        if (aiDirection.sqrMagnitude > 0.01f && !inputLocked && !GamePauseController.IsPaused && !isDead)
            stuckTimer += Time.deltaTime;
        else
            stuckTimer = 0f;
    }

    protected override void HandleInput()
    {
        Vector2 desired = NormalizeCardinal(aiDirection);

        if (desired == Vector2.zero)
        {
            committedDir = Vector2.zero;
            lastDetour = Vector2.zero;
            ApplyDirectionFromVector(Vector2.zero);
            return;
        }

        bool isStuck = stuckTimer >= stuckSeconds;
        bool forwardBlocked = avoidWhenForwardBlocked && IsMoveBlocked(desired);

        if (Time.time < commitUntilTime && committedDir != Vector2.zero)
        {
            Vector2 safeCommitted = FilterUnsafeMove(committedDir);
            committedDir = safeCommitted;
            ApplyDirectionFromVector(safeCommitted);
            return;
        }

        bool nearCenter = !onlyTurnNearTileCenter || IsNearTileCenter();

        if (!nearCenter)
        {
            if (committedDir == Vector2.zero)
                committedDir = desired;

            Vector2 safeHold = FilterUnsafeMove(committedDir);
            committedDir = safeHold;
            ApplyDirectionFromVector(safeHold);
            return;
        }

        bool canTurnNow = (Time.time - lastTurnTime) >= minTurnInterval;

        if (canTurnNow && (forwardBlocked || isStuck))
        {
            Vector2 detour = PickDetourStable(desired);

            if (detour != Vector2.zero)
            {
                detour = FilterUnsafeMove(detour);
                Commit(detour);
                ApplyDirectionFromVector(detour);
                return;
            }

            Vector2 safeDesired = FilterUnsafeMove(desired);
            Commit(safeDesired);
            ApplyDirectionFromVector(safeDesired);
            return;
        }

        if (canTurnNow)
        {
            Vector2 safeDesired = FilterUnsafeMove(desired);

            if (committedDir != safeDesired)
                Commit(safeDesired);

            ApplyDirectionFromVector(safeDesired);
            return;
        }

        if (committedDir == Vector2.zero)
            committedDir = desired;

        Vector2 safeContinue = FilterUnsafeMove(committedDir);
        committedDir = safeContinue;
        ApplyDirectionFromVector(safeContinue);
    }

    private Vector2 FilterUnsafeMove(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2.zero;

        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float t = Mathf.Max(0.0001f, tileSize);
        Vector2 nextTile = new Vector2(
            Mathf.Round((pos.x + dir.x * t) / t) * t,
            Mathf.Round((pos.y + dir.y * t) / t) * t
        );

        if (avoidActiveExplosions && IsTileWithExplosion(nextTile))
            return Vector2.zero;

        if (avoidBombTiles && IsTileWithBomb(nextTile))
            return Vector2.zero;

        return dir;
    }

    private bool IsTileWithExplosion(Vector2 tileCenter)
    {
        if (explosionMask == 0)
            return false;

        float s = Mathf.Max(0.1f, hazardCheckBoxSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * s, 0f, explosionMask) != null;
    }

    private bool IsTileWithBomb(Vector2 tileCenter)
    {
        if (bombMask == 0)
            return false;

        float s = Mathf.Max(0.1f, hazardCheckBoxSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * s, 0f, bombMask) != null;
    }

    private void Commit(Vector2 dir)
    {
        committedDir = dir;
        commitUntilTime = Time.time + Mathf.Max(0.01f, commitSeconds);
        lastTurnTime = Time.time;
        stuckTimer = 0f;
    }

    private bool IsNearTileCenter()
    {
        Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;

        float t = Mathf.Max(0.0001f, tileSize);
        float cx = Mathf.Round(pos.x / t) * t;
        float cy = Mathf.Round(pos.y / t) * t;

        float eps = Mathf.Max(0.001f, turnCenterEpsilon);
        return Mathf.Abs(pos.x - cx) <= eps && Mathf.Abs(pos.y - cy) <= eps;
    }

    private Vector2 PickDetourStable(Vector2 desired)
    {
        Vector2 perpA, perpB;
        if (Mathf.Abs(desired.x) > 0.01f)
        {
            perpA = Vector2.up;
            perpB = Vector2.down;
        }
        else
        {
            perpA = Vector2.left;
            perpB = Vector2.right;
        }

        if (lastDetour != Vector2.zero && IsMoveOpen(lastDetour) && IsDetourSafe(lastDetour))
            return lastDetour;

        bool aOpen = IsMoveOpen(perpA) && IsDetourSafe(perpA);
        bool bOpen = IsMoveOpen(perpB) && IsDetourSafe(perpB);

        if (aOpen && !bOpen)
        {
            lastDetour = perpA;
            return perpA;
        }

        if (bOpen && !aOpen)
        {
            lastDetour = perpB;
            return perpB;
        }

        if (aOpen && bOpen)
        {
            Vector2 pos = (Rigidbody != null) ? Rigidbody.position : (Vector2)transform.position;
            int tx = Mathf.RoundToInt(pos.x / Mathf.Max(0.0001f, tileSize));
            int ty = Mathf.RoundToInt(pos.y / Mathf.Max(0.0001f, tileSize));
            Vector2 pick = (((tx + ty) & 1) == 0) ? perpA : perpB;

            lastDetour = pick;
            return pick;
        }

        Vector2 back = -desired;
        if (IsMoveOpen(back) && IsDetourSafe(back))
        {
            lastDetour = Vector2.zero;
            return back;
        }

        lastDetour = Vector2.zero;
        return Vector2.zero;
    }

    private bool IsDetourSafe(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return false;

        Vector2 safe = FilterUnsafeMove(dir);
        return safe != Vector2.zero;
    }

    public void SetAIDirection(Vector2 dir)
    {
        aiDirection = dir;
    }
}
