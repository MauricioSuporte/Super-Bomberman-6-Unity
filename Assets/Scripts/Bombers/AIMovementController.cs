using UnityEngine;

public class AIMovementController : MovementController
{
    [Header("AI Control")]
    public Vector2 aiDirection = Vector2.zero;
    public bool isBoss = false;

    protected override void HandleInput()
    {
        Vector2 dir = aiDirection;

        if (dir.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                dir = new Vector2(Mathf.Sign(dir.x), 0f);
            else
                dir = new Vector2(0f, Mathf.Sign(dir.y));
        }
        else
        {
            dir = Vector2.zero;
        }

        ApplyDirectionFromVector(dir);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (isBoss && TryGetComponent<BossBomberHealth>(out var bossHealth))
                bossHealth.TakeDamage(1);
            else
                Kill();

            return;
        }

        if (layer == LayerMask.NameToLayer("Enemy"))
        {
            if (!isBoss)
                Kill();

            return;
        }

        base.OnTriggerEnter2D(other);
    }

    public void SetAIDirection(Vector2 dir)
    {
        aiDirection = dir;
    }
}
