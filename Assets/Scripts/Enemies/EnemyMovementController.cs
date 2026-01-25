using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(StunReceiver))]
public class EnemyMovementController : MonoBehaviour, IKillable
{
    [Header("Stats")]
    public float speed = 2f;
    public float tileSize = 1f;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteUp;
    public AnimatedSpriteRenderer spriteDown;
    public AnimatedSpriteRenderer spriteLeft;
    public AnimatedSpriteRenderer spriteDeath;
    public AnimatedSpriteRenderer spriteDamaged;

    [Header("Layers")]
    public LayerMask obstacleMask;
    public LayerMask bombLayerMask;
    public LayerMask enemyLayerMask;

    [Header("SFX")]
    public AudioClip deathSfx;

    protected AnimatedSpriteRenderer activeSprite;
    protected Rigidbody2D rb;
    protected Vector2 direction;
    protected Vector2 targetTile;
    protected bool isDead;

    CharacterHealth health;
    AudioSource audioSource;

    bool isInDamagedLoop;

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

        health = GetComponent<CharacterHealth>();

        if (health != null)
        {
            health.HitInvulnerabilityStarted += OnHitInvulnerabilityStarted;
            health.HitInvulnerabilityEnded += OnHitInvulnerabilityEnded;
            health.Died += OnHealthDied;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    protected virtual void OnDestroy()
    {
        if (health != null)
        {
            health.HitInvulnerabilityStarted -= OnHitInvulnerabilityStarted;
            health.HitInvulnerabilityEnded -= OnHitInvulnerabilityEnded;
            health.Died -= OnHealthDied;
        }
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

        if (TryGetComponent<StunReceiver>(out var stun) && stun != null && stun.IsStunned)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

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

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (health != null)
                health.TakeDamage(1);
            return;
        }

        if (layer == LayerMask.NameToLayer("Bomb"))
        {
            HandleBombCollisionOnContact();
            return;
        }

        if (layer == LayerMask.NameToLayer("Enemy"))
        {
            var otherEnemy = other.GetComponent<EnemyMovementController>();
            HandleEnemyCollision(otherEnemy);
            return;
        }
    }

    public void Kill()
    {
        Die();
    }

    void OnHitInvulnerabilityStarted(float seconds)
    {
        if (health == null || !health.playDamagedLoopInsteadOfBlink)
            return;

        if (spriteDamaged == null)
            return;

        isInDamagedLoop = true;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        DisableAllDirectionalSprites();

        activeSprite = spriteDamaged;
        spriteDamaged.enabled = true;
        spriteDamaged.idle = false;
        spriteDamaged.loop = true;
    }

    void OnHitInvulnerabilityEnded()
    {
        if (!isInDamagedLoop)
            return;

        isInDamagedLoop = false;

        if (spriteDamaged != null)
            spriteDamaged.enabled = false;

        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    void OnHealthDied()
    {
        isInDamagedLoop = false;

        if (spriteDamaged != null)
            spriteDamaged.enabled = false;
    }

    protected virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (TryGetComponent<StunReceiver>(out var stun))
            stun.CancelStunForDeath();

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        DisableAllDirectionalSprites();

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

    void DisableAllDirectionalSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;
    }

    protected virtual void OnDeathAnimationEnded()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            Destroy(gameObject, 0.05f);
            return;
        }

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

    protected virtual void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop)
            return;

        AnimatedSpriteRenderer previousSprite = activeSprite;

        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null)
            spriteDamaged.enabled = false;

        if (spriteDeath != null && spriteDeath != activeSprite)
            spriteDeath.enabled = false;

        if (dir == Vector2.up)
            activeSprite = spriteUp;
        else if (dir == Vector2.down)
            activeSprite = spriteDown;
        else if (dir == Vector2.left || dir == Vector2.right)
            activeSprite = spriteLeft;

        if (activeSprite == null)
            activeSprite = spriteDown != null ? spriteDown :
                          spriteLeft != null ? spriteLeft :
                          spriteUp != null ? spriteUp :
                          activeSprite;

        if (activeSprite == null)
        {
            Debug.LogError($"{name}: Any sprite renderer configured (Up/Down/Left).", this);
            return;
        }

        if (previousSprite != null && previousSprite != activeSprite)
        {
            int frame = previousSprite.CurrentFrame;

            if (activeSprite.animationSprite != null && activeSprite.animationSprite.Length > 0)
            {
                if (frame >= activeSprite.animationSprite.Length)
                    frame = frame % activeSprite.animationSprite.Length;
            }

            activeSprite.CurrentFrame = frame;
            activeSprite.idle = previousSprite.idle;
            activeSprite.RefreshFrame();
        }

        activeSprite.enabled = true;
        activeSprite.idle = false;

        if (activeSprite.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = (dir == Vector2.right);
    }

    protected virtual void DecideNextTile()
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

    protected virtual bool IsTileBlocked(Vector2 tileCenter)
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
