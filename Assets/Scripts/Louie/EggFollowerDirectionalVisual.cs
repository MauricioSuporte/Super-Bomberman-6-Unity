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

    AnimatedSpriteRenderer active;
    bool lastIdle = true;

    void OnEnable()
    {
        ApplyState(facing, idle: true, force: true);
    }

    public void ApplyMoveDelta(Vector3 deltaWorld)
    {
        Vector2 d = new(deltaWorld.x, deltaWorld.y);

        bool isMoving = d.sqrMagnitude > moveDeadZone;

        if (isMoving)
        {
            Vector2 dir = NormalizeCardinal(d);
            if (dir != Vector2.zero)
                facing = dir;
        }

        ApplyState(facing, idle: !isMoving, force: false);
    }

    public void ForceIdleFacing(Vector2 face)
    {
        if (face != Vector2.zero)
            facing = NormalizeCardinal(face);

        ApplyState(facing, idle: true, force: false);
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
            if (active != null)
                SetAnimEnabled(active, false);

            active = target;

            if (active != null)
                SetAnimEnabled(active, true);
        }

        if (active != null)
        {
            active.idle = idle;
            active.RefreshFrame();
        }
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
        if (dir.sqrMagnitude <= 0.000001f)
            return Vector2.zero;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);

        return new Vector2(0f, Mathf.Sign(dir.y));
    }
}
