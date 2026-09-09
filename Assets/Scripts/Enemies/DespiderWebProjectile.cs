using UnityEngine;

/// <summary>Enemy-layer hazards use MovementController's contact damage and mount protection.</summary>
public sealed class DespiderWebProjectile : MonoBehaviour
{
    public const float WebLifetimeSeconds = 3f;
    private const float TileIntervalSeconds = 0.5f;
    private AnimatedSpriteRenderer web;
    private AnimatedSpriteRenderer expansion;
    private BoxCollider2D hitbox;
    private Vector2 origin;
    private Vector2 direction;
    private float tileSize;
    private float elapsed;
    private int crossedTiles;
    private bool expanding;
    private bool trail;

    public static void Launch(AnimatedSpriteRenderer webTemplate, AnimatedSpriteRenderer expandTemplate, Vector2 origin, Vector2 direction, float tileSize)
    {
        var effect = Create(webTemplate, origin, tileSize);
        // Copy the expansion now so the projectile survives its owner's death.
        effect.expansion = Instantiate(expandTemplate, effect.transform);
        effect.expansion.transform.localPosition = Vector3.zero;
        effect.expansion.enabled = false;
        effect.expansion.GetComponent<SpriteRenderer>().enabled = false;
        effect.origin = origin;
        effect.direction = direction;
        // Keep an invisible coordinator/template alive independently of each Web.
        effect.web.GetComponent<SpriteRenderer>().enabled = false;
        effect.web.enabled = false;
        effect.hitbox.enabled = false;
        effect.crossedTiles = 1;
        var first = Create(effect.web, origin + direction * effect.tileSize, effect.tileSize);
        first.trail = true;
    }

    private static DespiderWebProjectile Create(AnimatedSpriteRenderer template, Vector2 position, float size)
    {
        var visualObject = new GameObject("Despider Web");
        visualObject.transform.position = position;
        var sourceRenderer = template.GetComponent<SpriteRenderer>();
        var renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder;
        var visual = visualObject.AddComponent<AnimatedSpriteRenderer>();
        visual.idleSprite = template.idleSprite;
        visual.animationSprite = template.animationSprite;
        visual.animationTime = template.animationTime;
        visual.useSequenceDuration = template.useSequenceDuration;
        visual.sequenceDuration = template.sequenceDuration;
        visual.frameDurations = template.frameDurations;
        visual.gameObject.name = "Despider Web";
        visual.gameObject.layer = LayerMask.NameToLayer("Enemy");
        visual.gameObject.SetActive(true);
        visual.enabled = true;
        visual.idle = false;
        visual.loop = true;
        visual.GetComponent<SpriteRenderer>().enabled = true;
        visual.RestartAnimation();
        var body = visual.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        var collider = visual.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one * (size * 0.8f);
        var effect = visual.gameObject.AddComponent<DespiderWebProjectile>();
        effect.web = visual;
        effect.hitbox = collider;
        effect.tileSize = Mathf.Max(0.01f, size);
        return effect;
    }

    private void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;
        if (trail)
        {
            if (elapsed >= WebLifetimeSeconds) Destroy(gameObject);
            return;
        }
        if (expanding)
        {
            int frame = Mathf.Min(3, Mathf.FloorToInt(elapsed / 0.25f));
            expansion.CurrentFrame = frame;
            expansion.RefreshFrame();
            hitbox.size = Vector2.one * (tileSize * (frame >= 2 ? 3f : 1f));
            // Four quarter-second frames, followed by three extra seconds.
            if (elapsed >= 4f) Destroy(gameObject);
            return;
        }

        int reached = Mathf.Min(5, 1 + Mathf.FloorToInt(elapsed / TileIntervalSeconds));
        while (crossedTiles < reached)
        {
            crossedTiles++;
            if (crossedTiles == 5) break;
            var segment = Create(web, origin + direction * (crossedTiles * tileSize), tileSize);
            // Create from a clean visual, never clone the live hazard's components.
            segment.trail = true;
        }
        if (crossedTiles < 5) return;
        transform.position = origin + direction * (5f * tileSize);
        expanding = true;
        elapsed = 0f;
        web.enabled = false;
        web.GetComponent<SpriteRenderer>().enabled = false;
        expansion.gameObject.SetActive(true);
        expansion.enabled = true;
        expansion.idle = false;
        expansion.loop = false;
        expansion.SetFrozen(true);
        expansion.CurrentFrame = 0;
        expansion.RefreshFrame();
        expansion.GetComponent<SpriteRenderer>().enabled = true;
        hitbox.size = Vector2.one * tileSize;
        hitbox.enabled = true;
    }
}
