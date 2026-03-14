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

    private const float Good1Volume = 0.5f;
    private const float Good2Volume = 0.5f;
    private const float Good3Volume = 1f;

    private static bool s_goodSfxPlayedThisStage;
    private static AudioClip[] s_goodClips;
    private static AudioClip s_skullClip;

    protected bool isActivated;
    protected bool isUnlocked;

    protected GameManager gameManager;

    protected virtual void Start()
    {
        s_goodSfxPlayedThisStage = false;

        gameManager = FindFirstObjectByType<GameManager>();

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

    private static void EnsureGoodClipsLoaded()
    {
        if (s_goodClips != null)
            return;

        s_goodClips = new AudioClip[3];
        s_goodClips[0] = Resources.Load<AudioClip>("Sounds/good1");
        s_goodClips[1] = Resources.Load<AudioClip>("Sounds/good2");
        s_goodClips[2] = Resources.Load<AudioClip>("Sounds/good3");
    }

    private static void EnsureSkullClipLoaded()
    {
        if (s_skullClip != null)
            return;

        s_skullClip = Resources.Load<AudioClip>("Sounds/skull");
    }

    private float GetGoodClipVolume(int clipIndex)
    {
        return clipIndex switch
        {
            0 => Good1Volume,
            1 => Good2Volume,
            2 => Good3Volume,
            _ => 1f,
        };
    }

    private void PlayEndStageVoiceOnce(AudioSource audio, bool hasNightmareBomber)
    {
        if (!playRandomGoodSfx)
            return;

        if (s_goodSfxPlayedThisStage)
            return;

        if (audio == null)
            return;

        if (playSkullForNightmareBomber && hasNightmareBomber)
        {
            EnsureSkullClipLoaded();

            if (s_skullClip != null)
            {
                s_goodSfxPlayedThisStage = true;
                audio.PlayOneShot(s_skullClip, skullVolume);
                return;
            }
        }

        EnsureGoodClipsLoaded();

        int count = 0;
        for (int i = 0; i < s_goodClips.Length; i++)
        {
            if (s_goodClips[i] != null)
                count++;
        }

        if (count <= 0)
            return;

        int pick = Random.Range(0, s_goodClips.Length);
        for (int tries = 0; tries < s_goodClips.Length && s_goodClips[pick] == null; tries++)
            pick = (pick + 1) % s_goodClips.Length;

        AudioClip clip = s_goodClips[pick];
        if (clip == null)
            return;

        float volume = GetGoodClipVolume(pick);

        s_goodSfxPlayedThisStage = true;
        audio.PlayOneShot(clip, volume);
    }

    private bool HasAnyActiveNightmareBomber(MovementController[] players)
    {
        if (players == null || players.Length == 0)
            return false;

        PlayerPersistentStats.EnsureSessionBooted();

        for (int i = 0; i < players.Length; i++)
        {
            MovementController movement = players[i];
            if (movement == null)
                continue;

            if (!movement.CompareTag("Player"))
                continue;

            if (!movement.gameObject.activeInHierarchy)
                continue;

            if (movement.isDead || movement.IsEndingStage)
                continue;

            int playerId = 1;

            if (movement.TryGetComponent<PlayerIdentity>(out var identity) && identity != null)
                playerId = Mathf.Clamp(identity.playerId, 1, 4);

            var state = PlayerPersistentStats.GetRuntime(playerId);
            if (state == null)
                state = PlayerPersistentStats.Get(playerId);

            if (state == null)
                continue;

            if (state.Skin == BomberSkin.Nightmare)
                return true;
        }

        return false;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanTrigger(other))
            return;

        isActivated = true;

        MovementController triggerMovement = other.GetComponent<MovementController>();

        Vector2 portalCenter = GetPortalCenterWorld(other);

        MovementController[] players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        bool hasNightmareBomber = HasAnyActiveNightmareBomber(players);

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

        PlayEndStageVoiceOnce(audio, hasNightmareBomber);

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