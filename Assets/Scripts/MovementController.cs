using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(Rigidbody2D))]
public class MovementController : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip deathSfx;
    public AudioClip kickBombSfx;

    [Header("Stats")]
    public float speed = 5f;
    public float tileSize = 1f;
    public LayerMask obstacleMask;

    [Header("Abilities")]
    public bool canKickBombs = false;

    public Rigidbody2D Rigidbody { get; private set; }

    private Vector2 direction = Vector2.zero;

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

    private const float CenterEpsilon = 0.01f;

    private float SlideDeadZone => tileSize * 0.25f;

    private bool hasInput;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody2D>();
        bombController = GetComponent<BombController>();

        PlayerPersistentStats.LoadInto(this, bombController);

        activeSpriteRenderer = spriteRendererDown;
        direction = Vector2.zero;

        audioSource = GetComponent<AudioSource>();

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Stage", "Bomb");

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.clip = null;
        }
    }

    private void OnEnable()
    {
        direction = Vector2.zero;
        hasInput = false;
    }

    private void Update()
    {
        if (inputLocked || GamePauseController.IsPaused || isDead)
            return;

        hasInput = false;

        if (Input.GetKey(inputUp))
        {
            hasInput = true;
            SetDirection(Vector2.up, spriteRendererUp);
        }
        else if (Input.GetKey(inputDown))
        {
            hasInput = true;
            SetDirection(Vector2.down, spriteRendererDown);
        }
        else if (Input.GetKey(inputLeft))
        {
            hasInput = true;
            SetDirection(Vector2.left, spriteRendererLeft);
        }
        else if (Input.GetKey(inputRight))
        {
            hasInput = true;
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

        if (!hasInput || direction == Vector2.zero)
            return;

        float dt = Time.fixedDeltaTime;
        float moveSpeed = speed * dt;

        Vector2 position = Rigidbody.position;

        bool blockLeft = IsSolidAt(position + Vector2.left * (tileSize * 0.5f));
        bool blockRight = IsSolidAt(position + Vector2.right * (tileSize * 0.5f));
        bool blockUp = IsSolidAt(position + Vector2.up * (tileSize * 0.5f));
        bool blockDown = IsSolidAt(position + Vector2.down * (tileSize * 0.5f));

        bool movingVertical = Mathf.Abs(direction.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(direction.x) > 0.01f;

        if (movingVertical && blockLeft && blockRight)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed);
        }

        if (movingHorizontal && blockUp && blockDown)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            position.y = Mathf.MoveTowards(position.y, targetY, moveSpeed);
        }

        Vector2 targetPosition = position + direction * moveSpeed;

        if (!IsBlocked(targetPosition))
        {
            Rigidbody.MovePosition(targetPosition);
            return;
        }

        if (movingVertical)
        {
            float currentCenterX = Mathf.Round(position.x / tileSize) * tileSize;
            float offsetX = Mathf.Abs(position.x - currentCenterX);

            if (offsetX > SlideDeadZone)
                TrySlideHorizontally(position, moveSpeed);
        }
        else if (movingHorizontal)
        {
            float currentCenterY = Mathf.Round(position.y / tileSize) * tileSize;
            float offsetY = Mathf.Abs(position.y - currentCenterY);

            if (offsetY > SlideDeadZone)
                TrySlideVertically(position, moveSpeed);
        }
    }

    private void TrySlideHorizontally(Vector2 position, float moveSpeed)
    {
        float leftCenter = Mathf.Floor(position.x / tileSize) * tileSize;
        float rightCenter = Mathf.Ceil(position.x / tileSize) * tileSize;

        Vector2 verticalStep = new(0f, direction.y * moveSpeed);

        bool leftFree = !IsBlocked(new Vector2(leftCenter, position.y) + verticalStep);
        bool rightFree = !IsBlocked(new Vector2(rightCenter, position.y) + verticalStep);

        if (!leftFree && !rightFree)
            return;

        float targetX;

        if (leftFree && !rightFree)
            targetX = leftCenter;
        else if (rightFree && !leftFree)
            targetX = rightCenter;
        else
        {
            targetX = Mathf.Abs(position.x - leftCenter) <= Mathf.Abs(position.x - rightCenter)
                ? leftCenter
                : rightCenter;
        }

        if (Mathf.Abs(position.x - targetX) > CenterEpsilon)
        {
            float newX = Mathf.MoveTowards(position.x, targetX, moveSpeed);
            Rigidbody.MovePosition(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
    }

    private void TrySlideVertically(Vector2 position, float moveSpeed)
    {
        float bottomCenter = Mathf.Floor(position.y / tileSize) * tileSize;
        float topCenter = Mathf.Ceil(position.y / tileSize) * tileSize;

        Vector2 horizontalStep = new(direction.x * moveSpeed, 0f);

        bool bottomFree = !IsBlocked(new Vector2(position.x, bottomCenter) + horizontalStep);
        bool topFree = !IsBlocked(new Vector2(position.x, topCenter) + horizontalStep);

        if (!bottomFree && !topFree)
            return;

        float targetY;

        if (bottomFree && !topFree)
            targetY = bottomCenter;
        else if (topFree && !bottomFree)
            targetY = topCenter;
        else
        {
            targetY = Mathf.Abs(position.y - bottomCenter) <= Mathf.Abs(position.y - topCenter)
                ? bottomCenter
                : topCenter;
        }

        if (Mathf.Abs(position.y - targetY) > CenterEpsilon)
        {
            float newY = Mathf.MoveTowards(position.y, targetY, moveSpeed);
            Rigidbody.MovePosition(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            if (!IsBlocked(newPos))
                Rigidbody.MovePosition(newPos);
        }
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

                if (canKickBombs && bomb != null)
                {
                    LayerMask bombObstacles =
                        obstacleMask | LayerMask.GetMask("Enemy");

                    bool kicked = bomb.StartKick(
                        direction,
                        tileSize,
                        bombObstacles,
                        bombController != null ? bombController.destructibleTiles : null
                    );

                    if (kicked)
                    {
                        if (audioSource != null && kickBombSfx != null)
                            audioSource.PlayOneShot(kickBombSfx);

                        continue;
                    }
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

        canKickBombs = false;
        PlayerPersistentStats.CanKickBombs = false;

        if (bombController != null)
            bombController.enabled = false;

        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.simulated = false;

        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (audioSource != null && deathSfx != null)
            audioSource.PlayOneShot(deathSfx);

        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.deathMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.deathMusic, 1f, false);
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

        Invoke(nameof(OnDeathSequenceEnded), 2f);
    }

    private void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);

        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.CheckWinState();
    }

    public void PlayEndStageSequence(Vector2 portalCenter)
    {
        inputLocked = true;

        if (bombController != null)
            bombController.enabled = false;

        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.position = portalCenter;
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

    public void EnableBombKick()
    {
        canKickBombs = true;
    }
}
