using UnityEngine;

public class MovementController : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip deathSfx;

    [Header("Stats")]
    public float speed = 5f;
    public float tileSize = 1f;
    public LayerMask obstacleMask;

    public new Rigidbody2D rigidbody { get; private set; }
    private Vector2 direction = Vector2.down;

    private AudioSource audioSource;
    private BombController bombController;

    public KeyCode inputUp = KeyCode.W;
    public KeyCode inputDown = KeyCode.S;
    public KeyCode inputLeft = KeyCode.A;
    public KeyCode inputRight = KeyCode.D;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    public AnimatedSpriteRenderer spriteRendererEndStage;

    [Header("End Stage Animation")]
    public float endStageTotalTime = 1f;
    public int endStageFrameCount = 9;

    private AnimatedSpriteRenderer activeSpriteRenderer;
    private bool inputLocked;
    private bool isDead;

    private bool isXAxisLocked;
    private bool isYAxisLocked;
    private float lockedX;
    private float lockedY;

    private bool softAlignXActive;
    private float softAlignX;
    private bool softAlignYActive;
    private float softAlignY;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        bombController = GetComponent<BombController>();
        activeSpriteRenderer = spriteRendererDown;
        audioSource = GetComponent<AudioSource>();

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Stage", "Bomb");
    }

    private void Update()
    {
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        if (Input.GetKey(inputUp))
        {
            SetDirection(Vector2.up, spriteRendererUp);
        }
        else if (Input.GetKey(inputDown))
        {
            SetDirection(Vector2.down, spriteRendererDown);
        }
        else if (Input.GetKey(inputLeft))
        {
            SetDirection(Vector2.left, spriteRendererLeft);
        }
        else if (Input.GetKey(inputRight))
        {
            SetDirection(Vector2.right, spriteRendererRight);
        }
        else
        {
            SetDirection(Vector2.zero, activeSpriteRenderer);
        }
    }

    private void FixedUpdate()
    {
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        if (direction == Vector2.zero)
            return;

        Vector2 position = rigidbody.position;

        bool blockLeft = IsSolidAt(position + Vector2.left * tileSize);
        bool blockRight = IsSolidAt(position + Vector2.right * tileSize);
        bool blockUp = IsSolidAt(position + Vector2.up * tileSize);
        bool blockDown = IsSolidAt(position + Vector2.down * tileSize);

        bool canLockXNow = blockLeft && blockRight;
        bool canLockYNow = blockUp && blockDown;

        if (!isXAxisLocked)
        {
            if (Mathf.Abs(direction.y) > 0f && canLockXNow)
            {
                isXAxisLocked = true;
                lockedX = Mathf.Round(position.x / tileSize) * tileSize;
            }
        }
        else
        {
            if (!canLockXNow)
                isXAxisLocked = false;
        }

        if (!isYAxisLocked)
        {
            if (Mathf.Abs(direction.x) > 0f && canLockYNow)
            {
                isYAxisLocked = true;
                lockedY = Mathf.Round(position.y / tileSize) * tileSize;
            }
        }
        else
        {
            if (!canLockYNow)
                isYAxisLocked = false;
        }

        if (isXAxisLocked)
        {
            position.x = lockedX;
            softAlignXActive = false;
        }

        if (isYAxisLocked)
        {
            position.y = lockedY;
            softAlignYActive = false;
        }

        rigidbody.position = position;

        if (!isXAxisLocked && Mathf.Abs(direction.y) > 0f)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            if (!Mathf.Approximately(targetX, position.x))
            {
                softAlignXActive = true;
                softAlignX = targetX;
            }
        }
        else
        {
            softAlignXActive = false;
        }

        if (!isYAxisLocked && Mathf.Abs(direction.x) > 0f)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            if (!Mathf.Approximately(targetY, position.y))
            {
                softAlignYActive = true;
                softAlignY = targetY;
            }
        }
        else
        {
            softAlignYActive = false;
        }

        Vector2 translation = speed * Time.fixedDeltaTime * direction;

        if (isXAxisLocked)
            translation.x = 0f;

        if (isYAxisLocked)
            translation.y = 0f;

        Vector2 targetPosition = position + translation;

        float alignStep = speed * Time.fixedDeltaTime;

        if (softAlignXActive)
            targetPosition.x = Mathf.MoveTowards(targetPosition.x, softAlignX, alignStep);

        if (softAlignYActive)
            targetPosition.y = Mathf.MoveTowards(targetPosition.y, softAlignY, alignStep);

        if (!IsBlocked(targetPosition))
            rigidbody.MovePosition(targetPosition);
    }

    private bool IsSolidAt(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            {
                var bomb = hit.GetComponent<Bomb>();
                if (bomb != null && bomb.Owner == bombController)
                {
                    var bombCollider = bomb.GetComponent<Collider2D>();
                    if (bombCollider != null && bombCollider.isTrigger)
                        continue;
                }
            }

            if (hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    private bool IsBlocked(Vector2 targetPosition)
    {
        Vector2 size;

        if (Mathf.Abs(direction.x) > 0f)
            size = new Vector2(tileSize * 0.6f, tileSize * 0.2f);
        else
            size = new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject.layer == LayerMask.NameToLayer("Bomb"))
            {
                var bomb = hit.GetComponent<Bomb>();
                if (bomb != null && bomb.Owner == bombController)
                {
                    var bombCollider = bomb.GetComponent<Collider2D>();
                    if (bombCollider != null && bombCollider.isTrigger)
                        continue;
                }
            }

            return true;
        }

        return false;
    }

    private void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        direction = newDirection;

        spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;
        spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;

        activeSpriteRenderer = spriteRenderer;

        if (activeSpriteRenderer != null)
            activeSpriteRenderer.idle = direction == Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion") ||
            layer == LayerMask.NameToLayer("Enemy"))
        {
            DeathSequence();
        }
    }

    private void DeathSequence()
    {
        if (isDead)
            return;

        isDead = true;
        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        rigidbody.velocity = Vector2.zero;
        rigidbody.simulated = false;

        var col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.deathMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.deathMusic);
        }

        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;

        if (spriteRendererDeath != null)
        {
            spriteRendererDeath.enabled = true;
            spriteRendererDeath.idle = false;
            spriteRendererDeath.loop = false;
            activeSpriteRenderer = spriteRendererDeath;
        }

        Invoke(nameof(OnDeathSequenceEnded), 1f);
    }

    private void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);
        FindObjectOfType<GameManager>().CheckWinState();
    }

    public void PlayEndStageSequence(Vector2 portalCenter)
    {
        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        rigidbody.velocity = Vector2.zero;
        rigidbody.position = portalCenter;
        direction = Vector2.zero;

        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
        if (spriteRendererDeath != null)
            spriteRendererDeath.enabled = false;

        var endSprite = spriteRendererEndStage != null
            ? spriteRendererEndStage
            : spriteRendererDown;

        if (endSprite != null)
        {
            endSprite.enabled = true;
            endSprite.idle = false;
            endSprite.loop = false;

            if (endStageFrameCount > 0)
                endSprite.animationTime = endStageTotalTime / endStageFrameCount;

            activeSpriteRenderer = endSprite;

            Invoke(nameof(HideEndStageSprite), endStageTotalTime);
        }
    }

    private void HideEndStageSprite()
    {
        if (spriteRendererEndStage != null)
            spriteRendererEndStage.enabled = false;
    }
}
