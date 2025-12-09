using UnityEngine;

public class PaladinMovementController : MetalHornMovementController
{
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (IsExplosionInFront(other.transform.position))
                return;
        }

        base.OnTriggerEnter2D(other);
    }

    bool IsExplosionInFront(Vector2 explosionPosition)
    {
        if (rb == null)
            return false;

        Vector2 forward = direction;
        if (forward == Vector2.zero)
            return false;

        Vector2 toExplosion = explosionPosition - rb.position;
        if (toExplosion.sqrMagnitude < 0.0001f)
            return false;

        forward.Normalize();
        toExplosion.Normalize();

        float dot = Vector2.Dot(forward, toExplosion);
        return dot > 0.7f;
    }
}
