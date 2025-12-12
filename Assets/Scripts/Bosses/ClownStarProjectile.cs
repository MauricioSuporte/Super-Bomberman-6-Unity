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
            if (other.TryGetComponent<MovementController>(out var movement))
                movement.Kill();
            else if (other.TryGetComponent<CharacterHealth>(out var health))
                health.TakeDamage(damage);

            Destroy(gameObject);
            return;
        }

        if (((1 << layer) & obstacleMask.value) != 0)
        {
            Destroy(gameObject);
        }
    }
}
