using UnityEngine;

/// <summary>A Flame child cloned into a freely aimed, short-lived explosion.</summary>
public sealed class BubbleFuramaFlame : MonoBehaviour
{
    public const float Lifetime = 0.25f;
    const float DamageLengthTiles = 2f;

    Vector2 start;
    Vector2 end;
    Vector2 damageEnd;
    float elapsed;
    AnimatedSpriteRenderer flameAnimation;
    Collider2D hitbox;
    bool launched;

    public void Launch(AnimatedSpriteRenderer source, Vector2 direction, float tileSize,
        float lengthTiles, SpriteRenderer ownerRenderer, Collider2D[] ownerColliders)
    {
        flameAnimation = source;
        direction.Normalize();
        Vector2 origin = transform.position;
        start = origin + direction * tileSize;
        end = origin + direction * (lengthTiles * tileSize);
        damageEnd = origin + direction * (Mathf.Min(lengthTiles, DamageLengthTiles) * tileSize);
        transform.SetPositionAndRotation(start, Quaternion.FromToRotation(Vector3.up, direction));

        if (TryGetComponent(out SpriteRenderer renderer))
        {
            renderer.enabled = true;
            if (ownerRenderer != null)
            {
                renderer.sortingLayerID = ownerRenderer.sortingLayerID;
                renderer.sortingOrder = ownerRenderer.sortingOrder + (direction.y > 0.0001f ? -1 : 1);
            }
        }

        flameAnimation.loop = false;
        flameAnimation.idle = false;
        flameAnimation.useSequenceDuration = true;
        flameAnimation.sequenceDuration = Lifetime;
        flameAnimation.frameDurations = System.Array.Empty<float>();
        flameAnimation.SetManualAnimationUpdate(true);
        flameAnimation.enabled = true;
        flameAnimation.RestartAnimation();

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer < 0)
        {
            Destroy(gameObject);
            return;
        }
        gameObject.layer = explosionLayer;
        Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = Vector2.one * (tileSize * 0.9f);
        hitbox = box;
        foreach (Collider2D ownerCollider in ownerColliders)
            if (ownerCollider != null)
                Physics2D.IgnoreCollision(hitbox, ownerCollider);
        launched = true;
    }

    void Update()
    {
        if (!launched || GamePauseController.IsPaused || Time.deltaTime <= 0f)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= Lifetime)
        {
            hitbox.enabled = false;
            Destroy(gameObject);
            return;
        }

        transform.position = Vector2.Lerp(start, end, elapsed / Lifetime);
        // The visual still travels its full distance; only the damage hitbox
        // follows the shorter path, ending at the second tile.
        Vector2 damagePosition = Vector2.Lerp(start, damageEnd, elapsed / Lifetime);
        hitbox.offset = transform.InverseTransformPoint(damagePosition);
        flameAnimation.AdvanceAnimation(Time.deltaTime, Time.deltaTime);
    }
}
