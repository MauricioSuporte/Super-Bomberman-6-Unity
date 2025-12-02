using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyMovementController : MonoBehaviour
{
    [Header("Stats")]
    public float speed = 2f;
    public int life = 1;
    public float tileSize = 1f;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteUp;
    public AnimatedSpriteRenderer spriteDown;
    public AnimatedSpriteRenderer spriteLeft;
    public AnimatedSpriteRenderer spriteDeath;

    [Header("Layers")]
    public LayerMask obstacleMask;
    public LayerMask bombLayerMask;
    public LayerMask enemyLayerMask;

    protected AnimatedSpriteRenderer activeSprite;
    protected Rigidbody2D rb;
    protected Vector2 direction;
    protected Vector2 targetTile;
    protected bool isDead;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSprite = spriteDown;

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Bomb", "Stage", "Enemy");

        if (bombLayerMask.value == 0)
            bombLayerMask = LayerMask.GetMask("Bomb");

        if (enemyLayerMask.value == 0)
            enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    protected virtual void Start()
    {
        SnapToGrid();
        ChooseInitialDirection();
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    protected virtual void FixedUpdate()
    {
        if (isDead)
            return;

        if (HasBombAt(targetTile))
        {
            HandleBombAhead();
        }

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            TakeDamage(1);
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            HandleBombCollisionOnContact();
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            var otherEnemy = other.GetComponent<EnemyMovementController>();
            HandleEnemyCollision(otherEnemy);
            return;
        }
    }

    public virtual void TakeDamage(int amount)
    {
        if (isDead)
            return;

        life -= amount;
        if (life <= 0)
            Die();
    }

    protected virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;

        rb.linearVelocity = Vector2.zero;
        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        spriteUp.enabled = false;
        spriteDown.enabled = false;
        spriteLeft.enabled = false;

        if (spriteDeath != null)
        {
            activeSprite = spriteDeath;
            spriteDeath.enabled = true;
            spriteDeath.idle = false;
            spriteDeath.animationTime = 0.1f;
            spriteDeath.loop = false;

            if (spriteDeath.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;
        }

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.NotifyEnemyDied();

        Invoke(nameof(OnDeathAnimationEnded), 0.7f);
    }

    protected virtual void OnDeathAnimationEnded()
    {
        Destroy(gameObject);
    }

    protected void SnapToGrid()
    {
        Vector2 pos = rb.position;
        pos.x = Mathf.Round(pos.x / tileSize) * tileSize;
        pos.y = Mathf.Round(pos.y / tileSize) * tileSize;
        rb.position = pos;
    }

    protected void MoveTowardsTile()
    {
        rb.MovePosition(Vector2.MoveTowards(rb.position, targetTile, speed * Time.fixedDeltaTime));
    }

    protected bool ReachedTile()
    {
        return Vector2.Distance(rb.position, targetTile) < 0.01f;
    }

    protected void ChooseInitialDirection()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        direction = dirs[Random.Range(0, dirs.Length)];
    }

    protected void UpdateSpriteDirection(Vector2 dir)
    {
        spriteUp.enabled = false;
        spriteDown.enabled = false;
        spriteLeft.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;

        if (dir == Vector2.up)
            activeSprite = spriteUp;
        else if (dir == Vector2.down)
            activeSprite = spriteDown;
        else if (dir == Vector2.left || dir == Vector2.right)
            activeSprite = spriteLeft;

        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.idle = false;

            if (activeSprite.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);
        }
    }

    protected void DecideNextTile()
    {
        Vector2 forwardTile = rb.position + direction * tileSize;

        if (!IsTileBlocked(forwardTile))
        {
            targetTile = forwardTile;
            return;
        }

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            if (dir == direction)
                continue;

            Vector2 checkTile = rb.position + dir * tileSize;
            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            targetTile = rb.position;
            UpdateSpriteDirection(direction);
            return;
        }

        direction = freeDirs[Random.Range(0, freeDirs.Count)];
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    protected bool IsTileBlocked(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            return true;
        }

        return false;
    }

    protected bool HasBombAt(Vector2 tileCenter)
    {
        Vector2 size = Vector2.one * (tileSize * 0.8f);
        var hit = Physics2D.OverlapBox(tileCenter, size, 0f, bombLayerMask);
        return hit != null;
    }

    protected void HandleBombAhead()
    {
        SnapToGrid();

        Vector2 backwardDir = -direction;
        Vector2 backwardTile = rb.position + backwardDir * tileSize;

        if (!IsTileBlocked(backwardTile))
        {
            direction = backwardDir;
            UpdateSpriteDirection(direction);
            targetTile = backwardTile;
            return;
        }

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            if (dir == direction || dir == -direction)
                continue;

            Vector2 checkTile = rb.position + dir * tileSize;
            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count > 0)
        {
            direction = freeDirs[Random.Range(0, freeDirs.Count)];
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
        }
        else
        {
            targetTile = rb.position;
        }
    }

    protected void HandleBombCollisionOnContact()
    {
        HandleBombAhead();
    }

    protected void HandleEnemyCollision(EnemyMovementController otherEnemy)
    {
        if (otherEnemy == null)
            return;

        if (GetInstanceID() < otherEnemy.GetInstanceID())
            return;

        SnapToGrid();

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var freeDirs = new List<Vector2>();

        foreach (var dir in dirs)
        {
            Vector2 checkTile = rb.position + dir * tileSize;
            if (!IsTileBlocked(checkTile))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
            return;

        direction = freeDirs[Random.Range(0, freeDirs.Count)];
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || rb == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            rb.position + direction * tileSize,
            Vector2.one * (tileSize * 0.8f)
        );
    }
}
