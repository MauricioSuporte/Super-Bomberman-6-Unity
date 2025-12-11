using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bomb : MonoBehaviour
{
    private BombController owner;
    public BombController Owner => owner;

    public bool HasExploded { get; private set; }
    public bool IsBeingKicked => isKicked;

    private Collider2D bombCollider;
    private Rigidbody2D rb;

    [Header("Kick")]
    public float kickSpeed = 9f;

    [Header("Chain Explosion")]
    public float chainStepDelay = 0.1f;

    private bool isKicked;
    private Vector2 kickDirection;
    private float kickTileSize = 1f;
    private LayerMask kickObstacleMask;
    private Tilemap kickDestructibleTilemap;
    private Coroutine kickRoutine;
    private Vector2 currentTileCenter;
    private Vector2 lastPos;

    private readonly HashSet<Collider2D> charactersInside = new();
    private bool chainExplosionScheduled;

    private void Awake()
    {
        bombCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        bombCollider.isTrigger = true;

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.bodyType = RigidbodyType2D.Kinematic;

        lastPos = rb.position;
    }

    private bool TileHasBomb(Vector2 pos)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        Collider2D hit = Physics2D.OverlapBox(pos, Vector2.one * 0.6f, 0f, bombMask);
        return hit != null && hit.gameObject != gameObject;
    }

    private bool IsKickBlocked(Vector2 target)
    {
        Vector2 size = Vector2.one * (kickTileSize * 0.6f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size, 0f, kickObstacleMask);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bombLayer = LayerMask.NameToLayer("Bomb");

        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            if (hit.gameObject.layer == enemyLayer)
                return true;

            if (hit.gameObject.layer == bombLayer)
                return true;

            if (hit.isTrigger)
                continue;

            return true;
        }

        if (TileHasBomb(target))
            return true;

        if (kickDestructibleTilemap != null)
        {
            Vector3Int cell = kickDestructibleTilemap.WorldToCell(target);
            if (kickDestructibleTilemap.GetTile(cell) != null)
                return true;
        }

        return false;
    }

    public void Initialize(BombController owner)
    {
        this.owner = owner;
        lastPos = rb.position;

        charactersInside.Clear();

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int charMask = LayerMask.GetMask("Player", "Enemy");

        Collider2D[] cols = Physics2D.OverlapBoxAll(rb.position, Vector2.one * 0.4f, 0f, charMask);

        if (cols != null)
        {
            foreach (var c in cols)
            {
                if (c == null)
                    continue;

                if (c.gameObject.layer == playerLayer || c.gameObject.layer == enemyLayer)
                    charactersInside.Add(c);
            }
        }

        bombCollider.isTrigger = charactersInside.Count > 0;
    }

    public Vector2 GetLogicalPosition() => lastPos;

    public bool StartKick(Vector2 direction, float tileSize, LayerMask obstacleMask, Tilemap destructibleTilemap)
    {
        if (HasExploded || isKicked || direction == Vector2.zero)
            return false;

        kickDirection = direction.normalized;
        kickTileSize = tileSize;

        kickObstacleMask = obstacleMask | LayerMask.GetMask("Enemy");

        kickDestructibleTilemap = destructibleTilemap;

        if (IsKickBlocked(rb.position + kickDirection * kickTileSize))
            return false;

        Vector2 origin = rb.position;
        origin.x = Mathf.Round(origin.x / tileSize) * tileSize;
        origin.y = Mathf.Round(origin.y / tileSize) * tileSize;

        currentTileCenter = origin;
        lastPos = origin;

        rb.position = origin;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        isKicked = true;
        kickRoutine = StartCoroutine(KickRoutine());

        return true;
    }

    private IEnumerator KickRoutine()
    {
        while (true)
        {
            if (HasExploded)
                break;

            Vector2 next = currentTileCenter + kickDirection * kickTileSize;

            if (IsKickBlocked(next))
                break;

            float t = 0f;
            float travelTime = kickTileSize / kickSpeed;
            Vector2 start = currentTileCenter;

            while (t < travelTime)
            {
                t += Time.deltaTime;
                Vector2 pos = Vector2.Lerp(start, next, t / travelTime);
                lastPos = pos;
                rb.MovePosition(pos);
                yield return null;
            }

            currentTileCenter = next;
            lastPos = next;
        }

        rb.position = currentTileCenter;
        isKicked = false;
        kickRoutine = null;
    }

    public void MarkAsExploded()
    {
        HasExploded = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasExploded)
            return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (other.gameObject.layer == explosionLayer)
        {
            if (!chainExplosionScheduled && owner != null)
            {
                chainExplosionScheduled = true;
                StartCoroutine(DelayedChainExplosion(chainStepDelay));
            }
            return;
        }

        if (other.gameObject.layer == playerLayer ||
            other.gameObject.layer == enemyLayer)
        {
            charactersInside.Add(other);
        }
    }

    private IEnumerator DelayedChainExplosion(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!HasExploded && owner != null)
            owner.ExplodeBomb(gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (HasExploded || isKicked)
            return;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        int layer = other.gameObject.layer;
        if (layer != playerLayer && layer != enemyLayer)
            return;

        charactersInside.Remove(other);

        if (charactersInside.Count == 0)
            bombCollider.isTrigger = false;
    }
}
