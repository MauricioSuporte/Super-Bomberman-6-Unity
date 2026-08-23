using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class JellyFishShot : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 5f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private AnimatedSpriteRenderer spriteAnimation;
    [SerializeField] private Sprite[] impactSprites;
    [SerializeField, Min(0.01f)] private float impactFrameSeconds = 0.1f;

    private Rigidbody2D body;
    private Collider2D hitCollider;
    private Vector2 direction;
    private GameObject owner;
    private bool impacted;

    public void Init(Vector2 travelDirection, GameObject shotOwner)
    {
        direction = travelDirection == Vector2.zero ? Vector2.down : travelDirection.normalized;
        owner = shotOwner;
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
        if (spriteAnimation == null)
            spriteAnimation = GetComponent<AnimatedSpriteRenderer>();
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

        Bomb bomb = other.GetComponentInParent<Bomb>();
        PlayerIdentity player = other.GetComponentInParent<PlayerIdentity>();
        bool hitStageTile = other.gameObject.layer == LayerMask.NameToLayer("Stage");

        // Ignore room bounds, enemies and other non-target colliders. The shot
        // only stops for a player, a bomb, or a solid/destructible stage tile.
        if (bomb == null && player == null && !hitStageTile)
            return;

        BeginImpact();

        if (bomb != null)
        {
            if (bomb.Owner != null)
                bomb.Owner.DestroyBombExternally(bomb.gameObject, refund: true);
            else
                Destroy(bomb.gameObject);
        }
        else if (player != null)
        {
            CharacterHealth targetHealth = player.GetComponent<CharacterHealth>();
            if (targetHealth != null)
                targetHealth.TakeDamage(damage);
        }
    }

    private void Expire()
    {
        if (impacted)
            return;

        Destroy(gameObject);
    }

    private void BeginImpact()
    {
        impacted = true;
        CancelInvoke(nameof(Expire));

        if (hitCollider != null)
            hitCollider.enabled = false;

        StartCoroutine(PlayImpactAnimationAndDestroy());
    }

    private System.Collections.IEnumerator PlayImpactAnimationAndDestroy()
    {
        if (spriteAnimation != null && impactSprites != null && impactSprites.Length > 0)
        {
            spriteAnimation.idleSprite = impactSprites[0];
            spriteAnimation.animationSprite = impactSprites;
            spriteAnimation.loop = false;
            spriteAnimation.idle = false;
            spriteAnimation.CurrentFrame = 0;
            spriteAnimation.RefreshFrame();

            yield return new WaitForSeconds(impactFrameSeconds * impactSprites.Length);
        }

        Destroy(gameObject);
    }
}
