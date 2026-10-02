using System.Collections;
using UnityEngine;

public sealed class HeliProjectile : MonoBehaviour
{
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private SpriteRenderer spriteRenderer;
    private Vector2 direction;
    private GameObject owner;
    private Sprite impactSprite;
    private Sprite impactEndSprite;
    private bool impacted;

    public static void Create(Vector2 position, Vector2 direction, GameObject owner, Sprite projectileSprite, Sprite impactSprite, Sprite impactEndSprite)
    {
        GameObject projectile = new("Heli Projectile") { layer = LayerMask.NameToLayer("Enemy") };
        projectile.transform.position = position;
        Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.28f;
        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.sortingOrder = 6;
        HeliProjectile behaviour = projectile.AddComponent<HeliProjectile>();
        behaviour.direction = direction.normalized;
        behaviour.owner = owner;
        behaviour.impactSprite = impactSprite;
        behaviour.impactEndSprite = impactEndSprite;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        StartCoroutine(ExpireAfterLifetime());
    }

    private void FixedUpdate()
    {
        if (!impacted) body.MovePosition(body.position + direction * 5f * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impacted || other == null || (owner != null && other.transform.IsChildOf(owner.transform))) return;
        PlayerIdentity player = other.GetComponentInParent<PlayerIdentity>();
        bool blocksShot = other.GetComponentInParent<Bomb>() != null || other.gameObject.layer == LayerMask.NameToLayer("Stage");
        if (player == null && !blocksShot) return;
        if (player != null) player.GetComponent<CharacterHealth>()?.TakeDamage(1);
        StartCoroutine(ImpactThenDestroy());
    }

    private IEnumerator ExpireAfterLifetime()
    {
        yield return new WaitForSeconds(5f);
        if (!impacted) yield return ImpactThenDestroy();
    }

    private IEnumerator ImpactThenDestroy()
    {
        if (impacted) yield break;
        impacted = true;
        projectileCollider.enabled = false;
        if (spriteRenderer != null && impactSprite != null) spriteRenderer.sprite = impactSprite;
        yield return new WaitForSeconds(0.1f);
        if (spriteRenderer != null && impactEndSprite != null) spriteRenderer.sprite = impactEndSprite;
        yield return new WaitForSeconds(0.1f);
        Destroy(gameObject);
    }
}
