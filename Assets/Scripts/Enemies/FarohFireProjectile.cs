using UnityEngine;

/// <summary>
/// A visual Fire child detached from Faroh and turned into a short-lived,
/// forward-moving flame. Collision is checked explicitly because the source
/// child is only an animated visual.
/// </summary>
public sealed class FarohFireProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float remainingDistance;
    private float tileSize;
    private float hitboxSize;
    private LayerMask targetMask;

    public void Launch(
        Vector2 launchDirection,
        float travelDistanceTiles,
        float tileSize,
        float speed,
        float hitboxSizePercent,
        LayerMask targetMask)
    {
        this.tileSize = Mathf.Max(0.01f, tileSize);
        this.speed = Mathf.Max(0.01f, speed);
        this.remainingDistance = Mathf.Max(0.01f, travelDistanceTiles) * this.tileSize;
        this.hitboxSize = this.tileSize * Mathf.Clamp(hitboxSizePercent, 0.1f, 1f);
        this.targetMask = targetMask;
        direction = ToCardinal(launchDirection);

        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);

        if (TryGetComponent(out AnimatedSpriteRenderer animation))
        {
            animation.enabled = true;
            animation.idle = false;
            animation.loop = false;
            animation.RestartAnimation();
        }
    }

    private void FixedUpdate()
    {
        if (remainingDistance <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float step = Mathf.Min(speed * Time.fixedDeltaTime, remainingDistance);
        Vector2 nextPosition = (Vector2)transform.position + direction * step;

        ApplyHits(nextPosition);
        transform.position = nextPosition;
        remainingDistance -= step;
    }

    private void ApplyHits(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            position,
            Vector2.one * hitboxSize,
            0f,
            targetMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            CharacterHealth playerHealth = hit.GetComponentInParent<CharacterHealth>();
            if (playerHealth != null && hit.GetComponentInParent<MovementController>() != null)
            {
                playerHealth.TakeDamage(1);
            }

            Bomb bomb = hit.GetComponentInParent<Bomb>();
            if (bomb == null || bomb.HasExploded)
                continue;

            if (bomb.Owner != null)
                bomb.Owner.ExplodeBombChained(bomb.gameObject, position);
            else
                FindAnyObjectByType<BombController>()?.ExplodeBombChained(bomb.gameObject, position);

        }
    }

    private static Vector2 ToCardinal(Vector2 value)
    {
        if (Mathf.Abs(value.x) >= Mathf.Abs(value.y))
            return value.x >= 0f ? Vector2.right : Vector2.left;

        return value.y >= 0f ? Vector2.up : Vector2.down;
    }
}
