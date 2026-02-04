using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ClownStarProjectile : MonoBehaviour
{
    public float speed = 6f;
    public float lifeTime = 3f;
    public int damage = 1;
    public LayerMask obstacleMask;

    Rigidbody2D rb;
    Vector2 direction = Vector2.zero;
    bool initialized;

    public void Initialize(Vector2 dir, float customSpeed, float customLifetime)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        if (customSpeed > 0f)
            speed = customSpeed;

        if (customLifetime > 0f)
            lifeTime = customLifetime;

        initialized = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    void OnEnable()
    {
        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        if (!initialized)
            return;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Player") || other.CompareTag("Player"))
        {
            if (TryApplyPlayerHit(other))
                Destroy(gameObject);

            return;
        }

        if (((1 << layer) & obstacleMask.value) != 0)
            Destroy(gameObject);
    }

    bool TryApplyPlayerHit(Collider2D other)
    {
        if (!other.TryGetComponent<MovementController>(out var movement) || movement == null)
            return false;

        if (movement.isDead || movement.IsEndingStage)
            return false;

        CharacterHealth playerHealth = null;
        if (movement.TryGetComponent<CharacterHealth>(out var h) && h != null)
            playerHealth = h;

        if (playerHealth != null && playerHealth.IsInvulnerable)
            return false;

        if (movement.IsMountedOnLouie)
        {
            if (movement.TryGetComponent<PlayerLouieCompanion>(out var companion) && companion != null)
            {
                companion.OnMountedLouieHit(damage, false);
                return true;
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                return true;
            }

            movement.Kill();
            return true;
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return true;
        }

        movement.Kill();
        return true;
    }
}
