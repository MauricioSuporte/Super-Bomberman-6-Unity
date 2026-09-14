using UnityEngine;

public sealed class EggFollowerDirectionalVisual : MonoBehaviour
{
    [Header("Directional Animations")]
    public AnimatedSpriteRenderer up;
    public AnimatedSpriteRenderer down;
    public AnimatedSpriteRenderer left;
    public AnimatedSpriteRenderer right;

    [Header("Facing")]
    public Vector2 facing = Vector2.down;

    [Header("Dead Zone")]
    public float moveDeadZone = 0.00005f;

    // Render frames between physics/history samples must not interrupt the walk cycle.
    const float MovementIdleGraceSeconds = 0.12f;
    const float FacingSampleDistance = 1f / 32f;
    float lastMovementTime = float.NegativeInfinity;
    Vector2 pendingFacingDelta;

    AnimatedSpriteRenderer active;
    bool lastIdle = true;

    public bool IsIdle => lastIdle;
    public bool IsActiveUp => active == up;
    public bool IsPlayingUpAnimation => active == up && !lastIdle;

    void OnEnable()
    {
        lastMovementTime = float.NegativeInfinity;
        pendingFacingDelta = Vector2.zero;
        ForceOnlyOneRendererVisibleImmediate();
        ApplyState(facing, idle: true, force: true);
    }

    public void ApplyMoveDelta(Vector3 deltaWorld)
    {
        Vector2 d = new(deltaWorld.x, deltaWorld.y);

        float deadZone = Mathf.Max(0.00000001f, moveDeadZone);
        bool isMoving = d.sqrMagnitude > deadZone * deadZone;

        if (isMoving)
        {
            lastMovementTime = Time.unscaledTime;
            pendingFacingDelta += d;
            if (pendingFacingDelta.sqrMagnitude >= FacingSampleDistance * FacingSampleDistance)
            {
                facing = NormalizeCardinal(pendingFacingDelta);
                pendingFacingDelta = Vector2.zero;
            }
        }

        bool idle = !isMoving && Time.unscaledTime - lastMovementTime >= MovementIdleGraceSeconds;
        if (idle)
            pendingFacingDelta = Vector2.zero;

        ApplyState(facing, idle, force: false);
    }

    public void ForceIdleFacing(Vector2 face)
    {
        lastMovementTime = float.NegativeInfinity;
        pendingFacingDelta = Vector2.zero;
        if (face != Vector2.zero)
            facing = NormalizeCardinal(face);

        ApplyState(facing, idle: true, force: true);
    }

    void ApplyState(Vector2 face, bool idle, bool force)
    {
        if (face == Vector2.zero)
            face = Vector2.down;

        var target = Get(face);
        if (target == null)
            target = down;

        if (!force && target == active && idle == lastIdle)
            return;

        lastIdle = idle;

        if (target != active)
        {
            DisableAllDirectionalRenderers();

            active = target;

            if (active != null)
                SetAnimEnabled(active, true);
        }
        else if (active != null)
        {
            SetAnimEnabled(active, true);
        }

        if (active != null)
        {
            active.loop = true;
            active.idle = idle;
            active.RefreshFrame();
        }
    }

    void ForceOnlyOneRendererVisibleImmediate()
    {
        DisableAllDirectionalRenderers();

        var target = Get(facing);
        if (target == null)
            target = down;

        active = target;

        if (active != null)
        {
            SetAnimEnabled(active, true);
            active.idle = true;
            active.RefreshFrame();
        }

        lastIdle = true;
    }

    void DisableAllDirectionalRenderers()
    {
        SetAnimEnabled(up, false);
        SetAnimEnabled(down, false);
        SetAnimEnabled(left, false);
        SetAnimEnabled(right, false);
    }

    AnimatedSpriteRenderer Get(Vector2 face)
    {
        if (face == Vector2.up) return up;
        if (face == Vector2.down) return down;
        if (face == Vector2.left) return left;
        if (face == Vector2.right) return right;
        return down;
    }

    void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null) return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }

    static Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }
}
