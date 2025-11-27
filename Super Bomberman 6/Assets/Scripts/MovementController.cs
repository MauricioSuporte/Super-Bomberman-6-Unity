using UnityEngine;

public class MovementController : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip deathSfx;

    public new Rigidbody2D rigidbody { get; private set; }
    private Vector2 direction = Vector2.down;
    public float speed = 5f;

    private AudioSource audioSource;

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
    private bool isDying;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (inputLocked || GamePauseController.IsPaused)
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
        if (inputLocked || GamePauseController.IsPaused)
            return;

        Vector2 position = rigidbody.position;
        Vector2 translation = speed * Time.fixedDeltaTime * direction;
        rigidbody.MovePosition(position + translation);
    }

    private void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        direction = newDirection;

        spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;
        spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;

        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDying)
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
        if (isDying)
            return;

        isDying = true;
        inputLocked = true;

        var bombController = GetComponent<BombController>();
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

        var bombController = GetComponent<BombController>();
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
