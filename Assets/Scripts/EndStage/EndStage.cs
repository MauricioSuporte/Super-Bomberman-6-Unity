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
    [SerializeField, Range(0f, 1f)] private float goodSfxVolume = 1f;

    private static bool s_goodSfxPlayedThisStage;
    private static AudioClip[] s_goodClips;

    protected bool isActivated;
    protected bool isUnlocked;

    protected GameManager gameManager;

    protected virtual void Start()
    {
        s_goodSfxPlayedThisStage = false;

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

    private static void EnsureGoodClipsLoaded()
    {
        if (s_goodClips != null)
            return;

        s_goodClips = new AudioClip[3];
        s_goodClips[0] = Resources.Load<AudioClip>("Sounds/good1");
        s_goodClips[1] = Resources.Load<AudioClip>("Sounds/good2");
        s_goodClips[2] = Resources.Load<AudioClip>("Sounds/good3");
    }

    private void PlayRandomGoodOnce(AudioSource audio)
    {
        if (!playRandomGoodSfx)
            return;

        if (s_goodSfxPlayedThisStage)
            return;

        if (audio == null)
            return;

        EnsureGoodClipsLoaded();

        int count = 0;
        for (int i = 0; i < s_goodClips.Length; i++)
            if (s_goodClips[i] != null) count++;

        if (count <= 0)
            return;

        int pick = Random.Range(0, s_goodClips.Length);
        for (int tries = 0; tries < s_goodClips.Length && s_goodClips[pick] == null; tries++)
            pick = (pick + 1) % s_goodClips.Length;

        var clip = s_goodClips[pick];
        if (clip == null)
            return;

        s_goodSfxPlayedThisStage = true;
        audio.PlayOneShot(clip, goodSfxVolume);
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

            if (m.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                glove.DestroyHeldBombIfHolding();

            var bombController = m.GetComponent<BombController>();

            PlayerPersistentStats.StageCaptureFromRuntime(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            bool snapThisOne = (triggerMovement != null && m == triggerMovement);
            m.PlayEndStageSequence(portalCenter, snapThisOne);
        }

        PlayerPersistentStats.CommitStage();

        var audio = other.GetComponent<AudioSource>();

        PlayRandomGoodOnce(audio);

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