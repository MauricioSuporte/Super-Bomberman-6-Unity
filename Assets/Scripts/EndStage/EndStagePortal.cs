using UnityEngine;

[RequireComponent(typeof(AnimatedSpriteRenderer))]
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

        gameManager = FindFirstObjectByType<GameManager>();

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
            gameManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
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

        if (!other || !other.CompareTag("Player"))
            return;

        var triggerMovement = other.GetComponent<MovementController>();
        if (triggerMovement == null || triggerMovement.isDead || triggerMovement.IsEndingStage)
            return;

        isActivated = true;

        Vector2 portalCenter = new(
            Mathf.Round(transform.position.x),
            Mathf.Round(transform.position.y)
        );

        var players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null)
                continue;

            if (!m.CompareTag("Player"))
                continue;

            if (!m.gameObject.activeInHierarchy)
                continue;

            if (m.isDead || m.IsEndingStage)
                continue;

            var bombController = m.GetComponent<BombController>();

            PlayerPersistentStats.SaveFrom(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            bool snapThisOne = (triggerMovement != null && m == triggerMovement);
            m.PlayEndStageSequence(portalCenter, snapThisOne);
        }

        var audio = other.GetComponent<AudioSource>();
        if (audio != null && enterSfx != null)
            audio.PlayOneShot(enterSfx);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (endStageMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(3f);

        if (gameManager != null)
            gameManager.EndStage();
    }
}
