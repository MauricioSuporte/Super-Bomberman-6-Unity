using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossEndStageSequence : MonoBehaviour
{
    private const string LOG = "[BossEndStageSequence]";

    [Header("Debug")]
    [SerializeField] private bool enableSurgicalLogs = true;

    [Header("Audio")]
    public AudioClip endStageMusic;

    [Header("End Stage - Random Good SFX (Resources/Sounds)")]
    [SerializeField] private bool playRandomGoodSfx = true;
    [SerializeField, Range(0f, 1f)] private float goodSfxVolume = 1f;

    [Header("Timing")]
    [Min(0f)] public float delayBeforeStart = 1f;
    [Min(0f)] public float celebrationSeconds = 5f;
    [Min(0f)] public float fadeDuration = 3f;

    private static bool s_goodSfxPlayedThisStage;
    private static AudioClip[] s_goodClips;

    GameManager gameManager;
    bool sequenceStarted;

    readonly List<Collider2D> cachedPlayerColliders = new();
    readonly List<bool> cachedColliderEnabled = new();

    void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        s_goodSfxPlayedThisStage = false;

        SLog($"Awake | instance={GetInstanceID()} | gameManager={(gameManager != null ? gameManager.name : "NULL")} | timeScale={Time.timeScale:0.###}");
    }

    void OnEnable()
    {
        SLog($"OnEnable | enabled={enabled} | timeScale={Time.timeScale:0.###}");
    }

    void OnDisable()
    {
        SLog("OnDisable");
    }

    public void StartBossDefeatedSequence()
    {
        SLog($"StartBossDefeatedSequence CALLED | sequenceStarted={sequenceStarted} | timeScale={Time.timeScale:0.###}");

        if (sequenceStarted)
        {
            SLog("IGNORED: sequence already started.");
            return;
        }

        sequenceStarted = true;

        var runnerGo = new GameObject("BossEndStageSequenceRunner");
        var runner = runnerGo.AddComponent<BossEndStageSequence>();

        runner.enableSurgicalLogs = enableSurgicalLogs;

        runner.endStageMusic = endStageMusic;
        runner.delayBeforeStart = delayBeforeStart;
        runner.celebrationSeconds = celebrationSeconds;
        runner.fadeDuration = fadeDuration;
        runner.gameManager = gameManager;

        runner.playRandomGoodSfx = playRandomGoodSfx;
        runner.goodSfxVolume = goodSfxVolume;

        SLog($"Spawned runner instance={runner.GetInstanceID()} | delayBeforeStart={delayBeforeStart:0.###} | celebration={celebrationSeconds:0.###} | fade={fadeDuration:0.###}");

        runner.StartCoroutine(runner.RunEndStageRoutine());

        enabled = false;
        SLog("Original component disabled (runner will execute).");
    }

    IEnumerator RunEndStageRoutine()
    {
        SLog($"RunEndStageRoutine BEGIN | delayBeforeStart={delayBeforeStart:0.###} | timeScale={Time.timeScale:0.###}");

        if (delayBeforeStart > 0f)
        {
            // REALTIME: se timeScale=0, WaitForSeconds travaria.
            yield return new WaitForSecondsRealtime(delayBeforeStart);
            SLog("DelayBeforeStart complete (Realtime).");
        }

        var alivePlayers = FindAlivePlayers();
        SLog($"AlivePlayers found: {alivePlayers.Count}");

        if (alivePlayers.Count == 0)
        {
            SLog("ABORT: no alive players -> Destroy(runner)");
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
        SLog($"AudioSource resolved: {(audio != null ? audio.gameObject.name : "NULL")}");

        PlayRandomGoodOnce(audio);

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var m = alivePlayers[i];
            if (m == null || m.isDead || m.IsEndingStage)
            {
                SLog($"Skip player[{i}] (null/dead/ending) | m={(m != null ? m.name : "NULL")} | isDead={(m != null && m.isDead)} | ending={(m != null && m.IsEndingStage)}");
                continue;
            }

            var bombController = m.GetComponent<BombController>();

            PlayerPersistentStats.SaveFrom(m, bombController);

            if (bombController != null)
                bombController.ClearPlantedBombsOnStageEnd(false);

            Vector2 center = new(
                Mathf.Round(m.transform.position.x),
                Mathf.Round(m.transform.position.y)
            );

            SLog($"Player[{i}] EndStageSequence | name={m.name} | center={center}");
            m.PlayEndStageSequence(center, snapToPortalCenter: false);

            MakePlayerSafeForEnding(m);
        }

        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.StopMusic();
            SLog("GameMusicController.StopMusic()");
        }
        else SLog("GameMusicController.Instance=NULL");

        if (endStageMusic != null && GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayMusic(endStageMusic, 1f, false);
            SLog($"GameMusicController.PlayMusic({endStageMusic.name})");
        }
        else SLog($"EndStageMusic={(endStageMusic != null ? endStageMusic.name : "NULL")} | GameMusicController.Instance={(GameMusicController.Instance != null ? "OK" : "NULL")}");

        if (celebrationSeconds > 0f)
        {
            SLog($"Celebration loop START | seconds={celebrationSeconds:0.###} | paused={GamePauseController.IsPaused} | timeScale={Time.timeScale:0.###}");

            float elapsed = 0f;
            while (elapsed < celebrationSeconds)
            {
                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;

                yield return null;
            }

            SLog("Celebration loop END");
        }

        if (StageIntroTransition.Instance != null)
        {
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);
            SLog($"StageIntroTransition.StartFadeOut({fadeDuration:0.###})");
        }
        else SLog("StageIntroTransition.Instance=NULL");

        if (gameManager != null)
        {
            gameManager.EndStage();
            SLog("GameManager.EndStage()");
        }
        else SLog("GameManager=NULL (EndStage not called)");

        SLog("RunEndStageRoutine DONE -> Destroy(runner)");
        Destroy(gameObject);
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
        {
            SLog("GoodSFX disabled (playRandomGoodSfx=false)");
            return;
        }

        if (s_goodSfxPlayedThisStage)
        {
            SLog("GoodSFX already played this stage (static gate)");
            return;
        }

        if (audio == null)
        {
            SLog("GoodSFX aborted: AudioSource NULL");
            return;
        }

        EnsureGoodClipsLoaded();

        int count = 0;
        for (int i = 0; i < s_goodClips.Length; i++)
            if (s_goodClips[i] != null) count++;

        if (count <= 0)
        {
            SLog("GoodSFX aborted: no clips loaded (Resources/Sounds/good1..3 missing?)");
            return;
        }

        int pick = Random.Range(0, s_goodClips.Length);
        for (int tries = 0; tries < s_goodClips.Length && s_goodClips[pick] == null; tries++)
            pick = (pick + 1) % s_goodClips.Length;

        var clip = s_goodClips[pick];
        if (clip == null)
        {
            SLog("GoodSFX aborted: picked clip NULL (all null?)");
            return;
        }

        s_goodSfxPlayedThisStage = true;
        audio.PlayOneShot(clip, goodSfxVolume);
        SLog($"GoodSFX PLAY -> {clip.name} vol={goodSfxVolume:0.###} on {audio.gameObject.name}");
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

    private void SLog(string msg)
    {
        if (!enableSurgicalLogs) return;
        Debug.Log($"{LOG} [t={Time.unscaledTime:0.00}] {msg}", this);
    }
}