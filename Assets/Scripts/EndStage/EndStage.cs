using System.Collections;
using UnityEngine;

public abstract class EndStage : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip enterSfx;

    [Header("Music")]
    public AudioClip endStageMusic;

    protected bool isActivated;
    protected bool isUnlocked;

    protected GameManager gameManager;

    protected virtual void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            gameManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;
            StartCoroutine(InitialEnemyCheckNextFrame());
        }

        OnStartSetup();
    }

    protected virtual void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
    }

    IEnumerator InitialEnemyCheckNextFrame()
    {
        yield return null;

        if (gameManager != null && gameManager.EnemiesAlive <= 0)
            HandleAllEnemiesDefeated();
    }

    void HandleAllEnemiesDefeated()
    {
        if (isUnlocked)
            return;

        isUnlocked = true;
        OnUnlocked();
    }

    protected virtual void OnStartSetup() { }

    protected abstract void OnUnlocked();

    protected virtual bool CanTrigger(Collider2D other)
    {
        if (!isUnlocked || isActivated)
            return false;

        if (!other)
            return false;

        if (!other.CompareTag("Player"))
            return false;

        var triggerMovement = other.GetComponent<MovementController>();
        if (triggerMovement == null)
            return false;

        if (triggerMovement.isDead || triggerMovement.IsEndingStage)
            return false;

        return true;
    }

    protected virtual Vector2 GetPortalCenterWorld(Collider2D triggeredBy)
    {
        return new Vector2(
            Mathf.Round(transform.position.x),
            Mathf.Round(transform.position.y)
        );
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanTrigger(other))
            return;

        isActivated = true;

        var triggerMovement = other.GetComponent<MovementController>();

        Vector2 portalCenter = GetPortalCenterWorld(other);

        var players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null) continue;
            if (!m.CompareTag("Player")) continue;
            if (!m.gameObject.activeInHierarchy) continue;
            if (m.isDead || m.IsEndingStage) continue;

            var bombController = m.GetComponent<BombController>();

            PlayerPersistentStats.StageCaptureFromRuntime(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            bool snapThisOne = (triggerMovement != null && m == triggerMovement);
            m.PlayEndStageSequence(portalCenter, snapThisOne);
        }

        PlayerPersistentStats.CommitStage();

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
