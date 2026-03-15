using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEndStageSequence : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip endStageMusic;

    [Header("End Stage - Random Good SFX (Resources/Sounds)")]
    [SerializeField] private bool playRandomGoodSfx = true;

    [Header("End Stage - Nightmare Bomber Override")]
    [SerializeField] private bool playSkullForNightmareBomber = true;
    [SerializeField] private float skullVolume = 1f;

    [Header("Timing")]
    [Min(0f)] public float delayBeforeStart = 1f;
    [Min(0f)] public float celebrationSeconds = 5f;
    [Min(0f)] public float fadeDuration = 3f;

    private const float Good1Volume = 0.5f;
    private const float Good2Volume = 0.5f;
    private const float Good3Volume = 1f;

    private static bool s_goodSfxPlayedThisStage;
    private static AudioClip[] s_goodClips;
    private static AudioClip s_skullClip;

    GameManager gameManager;
    bool sequenceStarted;
    bool progressMarked;

    readonly List<Collider2D> cachedPlayerColliders = new();
    readonly List<bool> cachedColliderEnabled = new();

    void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        s_goodSfxPlayedThisStage = false;
    }

    public void StartBossDefeatedSequence()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;

        var runnerGo = new GameObject("BossEndStageSequenceRunner");
        var runner = runnerGo.AddComponent<BossEndStageSequence>();

        runner.endStageMusic = endStageMusic;
        runner.delayBeforeStart = delayBeforeStart;
        runner.celebrationSeconds = celebrationSeconds;
        runner.fadeDuration = fadeDuration;
        runner.gameManager = gameManager;
        runner.playRandomGoodSfx = playRandomGoodSfx;
        runner.playSkullForNightmareBomber = playSkullForNightmareBomber;
        runner.skullVolume = skullVolume;

        runner.StartCoroutine(runner.RunEndStageRoutine());

        enabled = false;
    }

    IEnumerator RunEndStageRoutine()
    {
        if (delayBeforeStart > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeStart);

        var alivePlayers = FindAlivePlayers();

        if (alivePlayers.Count == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        var audio = GetComponent<AudioSource>();
        if (audio == null)
        {
            var sourceGo = alivePlayers[0] != null ? alivePlayers[0].gameObject : null;
            if (sourceGo != null)
                audio = sourceGo.GetComponent<AudioSource>();
        }

        bool hasNightmareBomber = HasAnyActiveNightmareBomber(alivePlayers);
        PlayEndStageVoiceOnce(audio, hasNightmareBomber);

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var m = alivePlayers[i];
            if (m == null || m.isDead || m.IsEndingStage)
                continue;

            if (m.TryGetComponent<PowerGloveAbility>(out var glove) && glove != null)
                glove.DestroyHeldBombIfHolding();

            var bombController = m.GetComponent<BombController>();
            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            Vector2 center = new(
                Mathf.Round(m.transform.position.x),
                Mathf.Round(m.transform.position.y)
            );

            m.PlayEndStageSequence(center, snapToPortalCenter: false);
            MakePlayerSafeForEnding(m);
        }

        MarkStageProgressIfNeeded();

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (endStageMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);

        if (celebrationSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < celebrationSeconds)
            {
                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;

                yield return null;
            }
        }

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);

        if (gameManager != null)
            gameManager.EndStage();

        Destroy(gameObject);
    }

    void MarkStageProgressIfNeeded()
    {
        if (progressMarked)
            return;

        progressMarked = true;

        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isPerfectClear = PlayerPersistentStats.IsCurrentStagePerfectClear();

        StageUnlockProgress.UnlockCurrentAndNext(currentSceneName);

        if (isPerfectClear)
            StageUnlockProgress.MarkPerfect(currentSceneName);
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

    private bool HasAnyActiveNightmareBomber(List<MovementController> players)
    {
        if (players == null || players.Count == 0)
            return false;

        PlayerPersistentStats.EnsureSessionBooted();

        for (int i = 0; i < players.Count; i++)
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

    List<MovementController> FindAlivePlayers()
    {
        var list = new List<MovementController>(4);

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                if (!id.TryGetComponent(out MovementController m))
                    m = id.GetComponentInChildren<MovementController>(true);

                if (m == null) continue;
                if (!m.gameObject.activeInHierarchy) continue;
                if (!m.CompareTag("Player")) continue;
                if (m.isDead) continue;

                list.Add(m);
            }

            return list;
        }

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var m = players[i];
            if (m == null) continue;
            if (!m.gameObject.activeInHierarchy) continue;
            if (!m.CompareTag("Player")) continue;
            if (m.isDead) continue;

            list.Add(m);
        }

        return list;
    }

    void MakePlayerSafeForEnding(MovementController player)
    {
        if (player == null)
            return;

        player.SetExplosionInvulnerable(true);
        player.SetInputLocked(true, false);

        if (player.TryGetComponent<Collider2D>(out var col))
        {
            cachedPlayerColliders.Add(col);
            cachedColliderEnabled.Add(col.enabled);
            col.enabled = false;
        }

        if (player.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.StopInvulnerability();
    }
}