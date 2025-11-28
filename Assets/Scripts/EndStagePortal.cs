using UnityEngine;

public class EndStagePortal : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip enterSfx;

    [Header("Music")]
    public AudioClip endStageMusic;

    [Header("Visual")]
    public AnimatedSpriteRenderer idleRenderer;

    private bool isActivated;
    private bool isUnlocked;

    private GameManager gameManager;

    private void Awake()
    {
        if (idleRenderer == null)
            idleRenderer = GetComponent<AnimatedSpriteRenderer>();
    }

    private void Start()
    {
        if (idleRenderer != null)
        {
            idleRenderer.idle = true;
            idleRenderer.loop = false;
        }

        gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;

            if (gameManager.EnemiesAlive <= 0)
                HandleAllEnemiesDefeated();
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
        }
    }

    private void HandleAllEnemiesDefeated()
    {
        isUnlocked = true;

        if (idleRenderer != null)
        {
            idleRenderer.idle = false;
            idleRenderer.loop = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isUnlocked || isActivated)
            return;

        if (!other.CompareTag("Player"))
            return;

        isActivated = true;

        var movement = other.GetComponent<MovementController>();
        if (movement != null)
        {
            Vector2 portalCenter = new Vector2(
                Mathf.Round(transform.position.x),
                Mathf.Round(transform.position.y)
            );
            movement.PlayEndStageSequence(portalCenter);
        }

        var audio = other.GetComponent<AudioSource>();
        if (audio != null && enterSfx != null)
        {
            audio.PlayOneShot(enterSfx);
        }

        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.StopMusic();
        }

        if (endStageMusic != null && GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);
        }

        if (StageIntroTransition.Instance != null)
        {
            StageIntroTransition.Instance.StartFadeOut(3f);
        }

        if (gameManager != null)
        {
            gameManager.EndStage();
        }
    }
}
