using UnityEngine;

public class KnightMovementController : ChargerPersecutingMovementController
{
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            Vector2 origin = other.transform.position;

            if (other.TryGetComponent<Explosion>(out var explosion))
                origin = explosion.Origin;

            if (IsExplosionOriginInFront(origin))
                return;
        }

        base.OnTriggerEnter2D(other);
    }

    private bool IsExplosionOriginInFront(Vector2 originPosition)
    {
        if (rb == null)
            return false;

        Vector2 forward = direction;
        if (forward == Vector2.zero)
            return false;

        Vector2 toOrigin = originPosition - rb.position;
        if (toOrigin.sqrMagnitude < 0.0001f)
            return false;

        forward.Normalize();
        toOrigin.Normalize();

        float dot = Vector2.Dot(forward, toOrigin);
        return dot > 0.7f;
    }
}
