using System.Collections;
using UnityEngine;

public abstract class EndStage : MonoBehaviour
{
    [Header("SFX")]
    public AudioClip enterSfx;

    [Header("Music")]
    public AudioClip endStageMusic;

    [Header("End Stage - Random Good SFX (Resources/Sounds)")]
    [SerializeField] private bool playRandomGoodSfx = true;

    [Header("End Stage - Nightmare Bomber Override")]
    [SerializeField] private bool playSkullForNightmareBomber = true;
    [SerializeField] private float skullVolume = 1f;

    [Header("Unlock Mode")]
    [SerializeField] private bool manualUnlockOnly = false;

    protected bool isActivated;
    protected bool isUnlocked;

    protected GameManager gameManager;

    protected virtual void Start()
    {
        EndStageVoiceSfx.ResetPlaybackState();

        gameManager = FindAnyObjectByType<GameManager>();

        if (!manualUnlockOnly && gameManager != null)
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

        if (gameManager != null && gameManager.AreAllEnemiesCleared())
            HandleAllEnemiesDefeated();
    }

    void HandleAllEnemiesDefeated()
    {
        if (isUnlocked)
            return;

        isUnlocked = true;
        OnUnlocked();
    }

    public void ForceUnlock()
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

        if (!other.TryGetComponent<MovementController>(out var triggerMovement))
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

        MovementController triggerMovement = other.GetComponent<MovementController>();

        Vector2 portalCenter = GetPortalCenterWorld(other);

        MovementController[] players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);

        bool hasNightmareBomber = EndStageVoiceSfx.HasAnyActiveNightmareBomber(players, "StageEnd");

        for (int i = 0; i < players.Length; i++)
        {
            MovementController m = players[i];
            if (m == null) continue;
            if (!m.CompareTag("Player")) continue;
            if (!m.gameObject.activeInHierarchy) continue;
            if (m.isDead || m.IsEndingStage) continue;

            if (m.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                glove.DestroyHeldBombIfHolding();

            if (m.TryGetComponent<BombController>(out var bombController))
                bombController.ClearPlantedBombsOnStageEnd(false);

            bool snapThisOne = triggerMovement != null && m == triggerMovement;
            m.PlayEndStageSequence(portalCenter, snapThisOne);
        }

        AudioSource audio = other.GetComponent<AudioSource>();

        EndStageVoiceSfx.TryPlayVictoryVoice(
            audio,
            hasNightmareBomber,
            playRandomGoodSfx,
            playSkullForNightmareBomber,
            skullVolume,
            "StageEnd");

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
