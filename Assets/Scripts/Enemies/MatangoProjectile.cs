using UnityEngine;

/// <summary>
/// Short-lived Matango spore. It is an Enemy-layer projectile and stops on a
/// player, Louie, stage collider, or bomb.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class MatangoProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float expiresAt;
    private float radius;
    private int collisionMask;
    private bool stopped;

    public static MatangoProjectile Create(
        Vector2 position,
        Vector2 launchDirection,
        float speed,
        float lifetimeSeconds,
        float radius,
        AnimatedSpriteRenderer animationTemplate)
    {
        GameObject projectileObject = animationTemplate != null
            ? Instantiate(animationTemplate.gameObject)
            : new GameObject("Matango Projectile");

        projectileObject.name = "Matango Projectile";
        projectileObject.layer = LayerMask.NameToLayer("Enemy");
        projectileObject.transform.position = position;
        projectileObject.transform.rotation = Quaternion.identity;
        projectileObject.transform.SetParent(null, true);
        projectileObject.SetActive(true);

        if (!projectileObject.TryGetComponent(out SpriteRenderer renderer))
            renderer = projectileObject.AddComponent<SpriteRenderer>();

        renderer.enabled = true;
        renderer.sortingOrder = 6;

        if (projectileObject.TryGetComponent(out AnimatedSpriteRenderer animation))
        {
            animation.enabled = true;
            animation.idle = false;
            animation.loop = true;
            animation.RestartAnimation();
        }

        Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
        projectileBody.bodyType = RigidbodyType2D.Kinematic;
        projectileBody.gravityScale = 0f;
        projectileBody.freezeRotation = true;

        CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = Mathf.Max(0.01f, radius);

        MatangoProjectile projectile = projectileObject.AddComponent<MatangoProjectile>();
        projectile.Initialize(launchDirection, speed, lifetimeSeconds, radius);
        return projectile;
    }

    private void Initialize(Vector2 launchDirection, float launchSpeed, float lifetimeSeconds, float hitRadius)
    {
        direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.down;
        speed = Mathf.Max(0.01f, launchSpeed);
        radius = Mathf.Max(0.01f, hitRadius);
        expiresAt = Time.time + Mathf.Max(0.01f, lifetimeSeconds);
        collisionMask = LayerMask.GetMask("Player", "Louie", "Stage", "Bomb");
    }

    private void FixedUpdate()
    {
        if (stopped || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * Time.fixedDeltaTime;
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, direction, distance, collisionMask);
        if (hit.collider != null)
        {
            HandleCollision(hit.collider);
            return;
        }

        transform.position += (Vector3)(direction * distance);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other);
    }

    private void HandleCollision(Collider2D other)
    {
        if (stopped || other == null || ((1 << other.gameObject.layer) & collisionMask) == 0)
            return;

        stopped = true;

        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            CharacterHealth health = other.GetComponentInParent<CharacterHealth>();
            if (health != null)
                health.TakeDamage(1);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Louie"))
        {
            PlayerMountCompanion mount = other.GetComponentInParent<PlayerMountCompanion>();
            if (mount != null)
                mount.OnMountedLouieHit(1, fromExplosion: false);
            else if (other.TryGetComponent(out CharacterHealth health))
                health.TakeDamage(1);
        }

        if (TryGetComponent(out Collider2D projectileCollider))
            projectileCollider.enabled = false;

        Destroy(gameObject);
    }
}
