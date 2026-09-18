using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class IcicleIceProjectile : MonoBehaviour
{
    private const float ParticleOffsetPixels = 3f;
    private const float PixelsPerUnit = 16f;

    [SerializeField, Min(0.1f)] private float speed = 5f;
    [SerializeField, Min(0.1f)] private float lifetimeSeconds = 5f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float particleLifetimeSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float particleIntervalSeconds = 0.05f;
    [SerializeField, Range(0f, 1f)] private float particleChance = 0.5f;

    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private Vector2 direction;
    private GameObject owner;
    private Sprite particleSpriteOne;
    private Sprite particleSpriteTwo;
    private float nextParticleTime;
    private bool impacted;

    public static void Create(Vector2 position, Vector2 travelDirection, GameObject shotOwner, Sprite projectileSprite, Sprite particleOne, Sprite particleTwo)
    {
        GameObject projectile = new("IcicleIceProjectile");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            projectile.layer = enemyLayer;

        projectile.transform.position = position;

        Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.28f;

        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.sortingOrder = 6;

        IcicleIceProjectile iceProjectile = projectile.AddComponent<IcicleIceProjectile>();
        iceProjectile.Initialize(travelDirection, shotOwner, particleOne, particleTwo);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        Invoke(nameof(Expire), lifetimeSeconds);
    }

    private void FixedUpdate()
    {
        if (impacted)
            return;

        body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime);

        if (Time.time >= nextParticleTime)
        {
            nextParticleTime = Time.time + particleIntervalSeconds;
            if (Random.value <= particleChance)
                CreateParticle();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impacted || other == null || other.transform.IsChildOf(owner != null ? owner.transform : transform))
            return;

        Bomb bomb = other.GetComponentInParent<Bomb>();
        PlayerIdentity player = other.GetComponentInParent<PlayerIdentity>();
        bool hitStageTile = other.gameObject.layer == LayerMask.NameToLayer("Stage") || other.CompareTag("Destructibles");

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
            CharacterHealth playerHealth = player.GetComponent<CharacterHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);
        }
    }

    private void Initialize(Vector2 travelDirection, GameObject shotOwner, Sprite particleOne, Sprite particleTwo)
    {
        direction = travelDirection == Vector2.zero ? Vector2.down : travelDirection.normalized;
        owner = shotOwner;
        particleSpriteOne = particleOne;
        particleSpriteTwo = particleTwo;
        nextParticleTime = Time.time;
    }

    private void BeginImpact()
    {
        impacted = true;
        CancelInvoke(nameof(Expire));

        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (projectileCollider != null)
            projectileCollider.enabled = false;

        Destroy(gameObject);
    }

    private void Expire()
    {
        if (!impacted)
            Destroy(gameObject);
    }

    private void CreateParticle()
    {
        Sprite particleSprite = Random.value < 0.5f ? particleSpriteOne : particleSpriteTwo;
        if (particleSprite == null)
            return;

        GameObject particle = new("IcicleIceParticle");
        particle.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * (ParticleOffsetPixels / PixelsPerUnit));

        SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
        renderer.sprite = particleSprite;
        renderer.sortingOrder = 7;

        Destroy(particle, particleLifetimeSeconds);
    }
}
