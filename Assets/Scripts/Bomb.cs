using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bomb : MonoBehaviour
{
    private BombController owner;
    public BombController Owner => owner;
    public bool HasExploded { get; private set; }
    public AudioSource audioSource;

    private Collider2D bombCollider;
    private Rigidbody2D rb;

    [Header("Kick")]
    public float kickSpeed = 7.5f;

    private bool isKicked;
    private Vector2 kickDirection;
    private float kickTileSize = 1f;
    private LayerMask kickObstacleMask;
    private Tilemap kickDestructibleTilemap;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        if (bombCollider != null)
            bombCollider.isTrigger = true;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        if (!isKicked || rb == null)
            return;

        Vector2 position = rb.position;
        Vector2 step = kickDirection * kickSpeed * Time.fixedDeltaTime;
        Vector2 target = position + step;

        if (IsKickBlocked(target))
        {
            isKicked = false;
            return;
        }

        rb.MovePosition(target);
    }

    private bool IsKickBlocked(Vector2 target)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.6f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size, 0f, kickObstacleMask);

        if (hits != null)
        {
            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                if (hit.gameObject == gameObject)
                    continue;

                return true;
            }
        }

        if (kickDestructibleTilemap != null)
        {
            Vector3Int cell = kickDestructibleTilemap.WorldToCell(target);
            if (kickDestructibleTilemap.GetTile(cell) != null)
                return true;
        }

        return false;
    }

    public void MarkAsExploded()
    {
        HasExploded = true;
    }

    public void Initialize(BombController owner)
    {
        this.owner = owner;
    }

    public bool StartKick(
        Vector2 direction,
        float tileSize,
        LayerMask obstacleMask,
        Tilemap destructibleTilemap)
    {
        if (HasExploded)
            return false;

        if (isKicked)
            return false;

        if (direction == Vector2.zero)
            return false;

        kickDirection = direction.normalized;
        kickTileSize = tileSize;
        kickObstacleMask = obstacleMask;
        kickDestructibleTilemap = destructibleTilemap;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 firstTarget = origin + kickDirection * kickTileSize;

        if (IsKickBlocked(firstTarget))
            return false;

        isKicked = true;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (owner != null)
            {
                owner.ExplodeBomb(gameObject);
            }
        }
    }
}
