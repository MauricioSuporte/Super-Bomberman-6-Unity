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
            obstacleMask = LayerMask.GetMask("Bomb", "Stage");
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
            TakeDamage(1);
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

        rb.velocity = Vector2.zero;
        var col = GetComponent<Collider2D>();
        if (col != null)
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

            var sr = spriteDeath.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.flipX = false;
        }

        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.NotifyEnemyDied();
        }

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

            var sr = activeSprite.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.flipX = (dir == Vector2.right);
        }
    }

    protected void DecideNextTile()
    {
        Vector2 forwardTile = rb.position + direction * tileSize;

        bool blockedForward = Physics2D.OverlapBox(
            forwardTile,
            Vector2.one * (tileSize * 0.8f),
            0f,
            obstacleMask
        );

        if (!blockedForward)
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
            Collider2D hit = Physics2D.OverlapBox(
                checkTile,
                Vector2.one * (tileSize * 0.8f),
                0f,
                obstacleMask
            );

            if (hit == null)
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
            direction = -direction;
        else
            direction = freeDirs[Random.Range(0, freeDirs.Count)];

        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            rb.position + direction * tileSize,
            Vector2.one * (tileSize * 0.8f)
        );
    }
}
