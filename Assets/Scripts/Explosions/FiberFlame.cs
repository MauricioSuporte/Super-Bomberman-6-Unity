using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class FiberFlame : MonoBehaviour
{
    public AnimatedSpriteRenderer up;
    public AnimatedSpriteRenderer down;
    public AnimatedSpriteRenderer left;
    public AnimatedSpriteRenderer right;

    [Header("Collision")]
    [SerializeField, Min(1)] private int collisionTiles = 3;
    [SerializeField, Min(0.01f)] private float tileSize = 0.5f;

    [Header("Lifetime")]
    [SerializeField, Min(0.01f)] private float durationSeconds = 0.5f;

    private BoxCollider2D box;
    private AnimatedSpriteRenderer active;
    private Coroutine lifeRoutine;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    public void Play(Vector2 direction, float durationOverride = -1f)
    {
        if (durationOverride > 0f)
            durationSeconds = durationOverride;

        direction = NormalizeToCardinal(direction);

        DisableAll();

        active = Resolve(direction);
        if (active != null)
        {
            active.enabled = true;

            active.idle = false;

            active.loop = false;

            active.useSequenceDuration = true;
            active.sequenceDuration = Mathf.Max(0.01f, durationSeconds);

            active.CurrentFrame = 0;
            active.RefreshFrame();
        }

        ConfigureCollider(direction);

        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);

        lifeRoutine = StartCoroutine(LifeRoutine());
    }

    private void ConfigureCollider(Vector2 dir)
    {
        Vector2 size = Vector2.one * tileSize;
        Vector2 offset = Vector2.zero;

        float length = collisionTiles * tileSize;
        float half = (length - tileSize) * 0.5f;

        if (dir == Vector2.right)
        {
            size = new Vector2(length, tileSize);
            offset = new Vector2(half, 0f);
        }
        else if (dir == Vector2.left)
        {
            size = new Vector2(length, tileSize);
            offset = new Vector2(-half, 0f);
        }
        else if (dir == Vector2.up)
        {
            size = new Vector2(tileSize, length);
            offset = new Vector2(0f, half);
        }
        else if (dir == Vector2.down)
        {
            size = new Vector2(tileSize, length);
            offset = new Vector2(0f, -half);
        }

        box.size = size;
        box.offset = offset;
    }

    private IEnumerator LifeRoutine()
    {
        float d = Mathf.Max(0.01f, durationSeconds);

        if (active != null)
        {
            float freezeAt = Mathf.Max(0f, d - 0.01f);
            float t = 0f;

            while (t < freezeAt)
            {
                t += Time.deltaTime;
                yield return null;
            }

            active.SetFrozen(true);
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, 0.01f));
        Destroy(gameObject);
    }

    private AnimatedSpriteRenderer Resolve(Vector2 dir)
    {
        if (dir == Vector2.up) return up;
        if (dir == Vector2.down) return down;
        if (dir == Vector2.left) return left;
        if (dir == Vector2.right) return right;
        return up != null ? up : (down != null ? down : (left != null ? left : right));
    }

    private void DisableAll()
    {
        if (up != null) up.enabled = false;
        if (down != null) down.enabled = false;
        if (left != null) left.enabled = false;
        if (right != null) right.enabled = false;

        if (up != null) up.SetFrozen(false);
        if (down != null) down.SetFrozen(false);
        if (left != null) left.SetFrozen(false);
        if (right != null) right.SetFrozen(false);

        active = null;
    }

    private Vector2 NormalizeToCardinal(Vector2 dir)
    {
        if (dir == Vector2.up || dir == Vector2.down || dir == Vector2.left || dir == Vector2.right)
            return dir;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return dir.x >= 0f ? Vector2.right : Vector2.left;

        return dir.y >= 0f ? Vector2.up : Vector2.down;
    }
}
