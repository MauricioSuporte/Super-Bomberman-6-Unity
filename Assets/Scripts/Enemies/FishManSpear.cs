using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class FishManSpear : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 5f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite spearUp;
    [SerializeField] private Sprite spearDown;
    [SerializeField] private Sprite spearLeft;

    private Rigidbody2D body;
    private Collider2D hitCollider;
    private Vector2 direction;
    private GameObject owner;
    private bool impacted;

    public void Init(Vector2 travelDirection, GameObject spearOwner)
    {
        direction = travelDirection == Vector2.zero ? Vector2.down : travelDirection.normalized;
        owner = spearOwner;
        ApplyDirectionSprite();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        hitCollider = GetComponent<Collider2D>();
        hitCollider.isTrigger = true;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        Invoke(nameof(Expire), lifetime);
    }

    private void FixedUpdate()
    {
        if (!impacted)
            body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impacted || other == null || other.transform.IsChildOf(owner != null ? owner.transform : transform))
            return;

        PlayerIdentity player = other.GetComponentInParent<PlayerIdentity>();
        bool hitStageTile = other.gameObject.layer == LayerMask.NameToLayer("Stage");
        if (player == null && !hitStageTile)
            return;

        impacted = true;
        if (hitCollider != null)
            hitCollider.enabled = false;

        if (player != null && player.TryGetComponent(out CharacterHealth targetHealth))
            targetHealth.TakeDamage(damage);

        Destroy(gameObject);
    }

    private void ApplyDirectionSprite()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = direction == Vector2.right;
        spriteRenderer.sprite = direction == Vector2.up ? spearUp : direction == Vector2.down ? spearDown : spearLeft;
    }

    private void Expire()
    {
        if (!impacted)
            Destroy(gameObject);
    }
}
