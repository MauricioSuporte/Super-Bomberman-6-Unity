using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class GameMusicController : MonoBehaviour
{
    const string BattleModeMusicResourcesPath = "Sounds/BattleModeMusics";

    static readonly BattleModeMusicConfig[] BattleModeMusicConfigs =
    {
        new(BattleModeRules.BattleMusicSelection.SB1Battle, "SB1 - Battle", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB2Battle1, "SB2 - Battle1", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB2Battle2, "SB2 - Battle2", 0.5f, "SB2 - Battle2 Loop", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB2Battle3, "SB2 - Battle3", 0.5f, "SB2 - Battle3 Loop", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB3Battle, "SB3 - Battle", 0.3f, "SB3 - Battle Loop", 0.3f),
        new(BattleModeRules.BattleMusicSelection.SB4Battle, "SB4 - Battle", 0.5f, "SB4 - Battle Loop", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB5Battle1, "SB5 - Battle1", 0.5f, "SB5 - Battle1 Loop", 0.5f, "SB5 - Battle1 Critical", 0.5f),
        new(BattleModeRules.BattleMusicSelection.SB5Battle2, "SB5 - Battle2", 0.5f, null, 0.5f, "SB5 - Battle2 Critical", 0.5f),
    };

    public static GameMusicController Instance;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    Coroutine musicTransitionRoutine;
    Coroutine preloadAndPlayRoutine;
    bool musicPausedForGamePause;

    public AudioClip defaultMusic;
    public AudioClip defaultMusicLoop;
    public AudioClip deathMusic;
    AudioClip battleCriticalMusic;

    [Range(0f, 1f)]
    public float defaultMusicVolume = 1f;

    [Range(0f, 1f)]
    public float defaultMusicLoopVolume = 1f;

    float battleCriticalMusicVolume = 1f;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();

        if (Instance != null && Instance != this)
        {
            Instance.ApplySceneMusicSettingsFrom(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.clip = null;
            musicSource.pitch = 1f;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.clip = null;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!ConfigureBattleModeMusicForScene(scene))
            return;

        PreloadSelectedBattleMusicThenPlay();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!ConfigureBattleModeMusicForScene(scene))
            return;

        PreloadSelectedBattleMusicThenPlay();
    }

    void ApplySceneMusicSettingsFrom(GameMusicController sceneMusicController)
    {
        if (sceneMusicController == null)
            return;

        defaultMusic = sceneMusicController.defaultMusic;
        defaultMusicLoop = sceneMusicController.defaultMusicLoop;
        battleCriticalMusic = sceneMusicController.battleCriticalMusic;
        defaultMusicVolume = sceneMusicController.defaultMusicVolume;
        defaultMusicLoopVolume = sceneMusicController.defaultMusicLoopVolume;
        battleCriticalMusicVolume = sceneMusicController.battleCriticalMusicVolume;

        if (sceneMusicController.deathMusic != null)
            deathMusic = sceneMusicController.deathMusic;
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true, float pitch = 1f, bool restart = true)
    {
        if (clip == null || musicSource == null)
            return;

        StopMusicTransitionRoutine();

        bool sameClip = musicSource.clip == clip;

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.pitch = pitch;

        if (restart || !sameClip || !musicSource.isPlaying)
            musicSource.time = 0f;

        musicSource.Play();
        ApplyMusicPauseStateIfNeeded();
    }

    public void PlayMusicIntroThenLoop(
        AudioClip introClip,
        float introVolume,
        AudioClip loopClip,
        float loopVolume,
        float pitch = 1f,
        bool restart = true)
    {
        if (introClip == null || musicSource == null)
            return;

        StopMusicTransitionRoutine();

        if (loopClip == null)
        {
            PlayMusic(introClip, introVolume, true, pitch, restart);
            return;
        }

        bool sameClip = musicSource.clip == introClip;

        musicSource.loop = false;
        musicSource.clip = introClip;
        musicSource.volume = introVolume;
        musicSource.pitch = pitch;

        if (restart || !sameClip || !musicSource.isPlaying)
            musicSource.time = 0f;

        musicSource.Play();
        ApplyMusicPauseStateIfNeeded();
        musicTransitionRoutine = StartCoroutine(PlayLoopAfterIntroRoutine(introClip, loopClip, loopVolume, pitch));
    }

    public void PlayDefaultMusic(bool restart = true)
    {
        if (defaultMusic == null || musicSource == null)
            return;

        PlayMusicIntroThenLoop(defaultMusic, defaultMusicVolume, defaultMusicLoop, defaultMusicLoopVolume, 1f, restart);
    }

    public void PlayDefaultMusicWithPitch(float pitch, bool restart = true)
    {
        if (defaultMusic == null || musicSource == null)
            return;

        PlayMusicIntroThenLoop(defaultMusic, defaultMusicVolume, defaultMusicLoop, defaultMusicLoopVolume, pitch, restart);
    }

    public bool PlayBattleCriticalMusic(bool restart = true)
    {
        if (battleCriticalMusic == null || musicSource == null)
            return false;

        PlayMusic(battleCriticalMusic, battleCriticalMusicVolume, true, 1f, restart);
        return true;
    }

    public void PlayDeathMusic(bool restart = true)
    {
        if (deathMusic == null || musicSource == null)
            return;

        PlayMusic(deathMusic, defaultMusicVolume, true, 1f, restart);
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, volume);
    }

    public void StopMusic()
    {
        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
        musicSource.pitch = 1f;
        musicPausedForGamePause = false;
        StopMusicTransitionRoutine();
    }

    public void StopSfx()
    {
        if (sfxSource == null)
            return;

        sfxSource.Stop();
        sfxSource.clip = null;
    }

    public void PauseMusic()
    {
        if (musicSource == null)
            return;

        musicPausedForGamePause = true;

        if (musicSource.isPlaying)
            musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (musicSource == null)
            return;

        if (GamePauseController.IsPaused)
            return;

        if (!musicPausedForGamePause)
            return;

        musicPausedForGamePause = false;

        if (musicSource.clip == null)
            return;

        musicSource.UnPause();
    }

    public AudioSource GetMusicSource()
    {
        return musicSource;
    }

    public void PlayOneShotSfx(AudioClip clip, float volume = 1f)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip, volume);
    }

    public void ResumeMusicWithPitch(float pitch)
    {
        PlayDefaultMusicWithPitch(pitch, true);
    }

    public void ResetMusicPitch()
    {
        if (musicSource == null)
            return;

        musicSource.pitch = 1f;
    }

    IEnumerator PlayLoopAfterIntroRoutine(AudioClip introClip, AudioClip loopClip, float loopVolume, float pitch)
    {
        while (musicSource != null && musicSource.clip == introClip)
        {
            if (GamePauseController.IsPaused || musicPausedForGamePause)
            {
                ApplyMusicPauseStateIfNeeded();
                yield return null;
                continue;
            }

            if (!musicSource.isPlaying || musicSource.time >= introClip.length)
                break;

            yield return null;
        }

        if (musicSource == null || musicSource.clip != introClip)
        {
            musicTransitionRoutine = null;
            yield break;
        }

        musicTransitionRoutine = null;
        PlayMusic(loopClip, loopVolume, true, pitch, true);
    }

    void StopMusicTransitionRoutine()
    {
        if (musicTransitionRoutine == null)
            return;

        StopCoroutine(musicTransitionRoutine);
        musicTransitionRoutine = null;
    }

    bool ConfigureBattleModeMusicForScene(Scene scene)
    {
        if (!IsBattleModeScene(scene))
            return false;

        AudioClip[] battleModeClips = Resources.LoadAll<AudioClip>(BattleModeMusicResourcesPath);
        if (battleModeClips == null || battleModeClips.Length <= 0)
            return false;

        BattleModeMusicConfig[] availableConfigs = GetAvailableBattleModeMusicConfigs(battleModeClips);
        if (availableConfigs.Length <= 0)
            return false;

        BattleModeMusicConfig selectedConfig = SelectBattleModeMusicConfig(availableConfigs);
        AudioClip selectedIntroClip = FindClipByName(battleModeClips, selectedConfig.IntroClipName);
        if (selectedIntroClip == null)
            return false;

        defaultMusic = selectedIntroClip;
        defaultMusicVolume = selectedConfig.IntroVolume;
        defaultMusicLoop = null;
        defaultMusicLoopVolume = selectedConfig.LoopVolume;
        battleCriticalMusic = null;
        battleCriticalMusicVolume = selectedConfig.CriticalVolume;

        if (!string.IsNullOrWhiteSpace(selectedConfig.LoopClipName))
            defaultMusicLoop = FindClipByName(battleModeClips, selectedConfig.LoopClipName);

        if (!string.IsNullOrWhiteSpace(selectedConfig.CriticalClipName))
            battleCriticalMusic = FindClipByName(battleModeClips, selectedConfig.CriticalClipName);

        PreloadSelectedBattleMusic();

        return true;
    }

    void PreloadSelectedBattleMusicThenPlay()
    {
        if (preloadAndPlayRoutine != null)
            StopCoroutine(preloadAndPlayRoutine);

        preloadAndPlayRoutine = StartCoroutine(PreloadSelectedBattleMusicThenPlayRoutine());
    }

    IEnumerator PreloadSelectedBattleMusicThenPlayRoutine()
    {
        PreloadSelectedBattleMusic();

        float timeoutAt = Time.realtimeSinceStartup + 5f;
        while (!IsAudioDataLoaded(defaultMusic) ||
               !IsAudioDataLoaded(defaultMusicLoop) ||
               !IsAudioDataLoaded(battleCriticalMusic))
        {
            if (Time.realtimeSinceStartup >= timeoutAt)
                break;

            yield return null;
        }

        while (GamePauseController.IsPaused)
            yield return null;

        preloadAndPlayRoutine = null;
        PlayDefaultMusic(true);
    }

    void ApplyMusicPauseStateIfNeeded()
    {
        if (musicSource == null)
            return;

        if (!GamePauseController.IsPaused && !musicPausedForGamePause)
            return;

        musicPausedForGamePause = true;

        if (musicSource.isPlaying)
            musicSource.Pause();
    }

    void PreloadSelectedBattleMusic()
    {
        PreloadAudioData(defaultMusic);
        PreloadAudioData(defaultMusicLoop);
        PreloadAudioData(battleCriticalMusic);
    }

    static void PreloadAudioData(AudioClip clip)
    {
        if (clip == null)
            return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    static bool IsAudioDataLoaded(AudioClip clip)
    {
        return clip == null ||
               clip.loadState == AudioDataLoadState.Loaded ||
               clip.loadState == AudioDataLoadState.Failed;
    }

    static BattleModeMusicConfig SelectBattleModeMusicConfig(BattleModeMusicConfig[] availableConfigs)
    {
        BattleModeRules.BattleMusicSelection selection = BattleModeRules.Instance != null
            ? BattleModeRules.Instance.CurrentBattleMusic
            : BattleModeRules.BattleMusicSelection.Random;

        if (selection != BattleModeRules.BattleMusicSelection.Random)
        {
            for (int i = 0; i < availableConfigs.Length; i++)
            {
                if (availableConfigs[i].Selection == selection)
                    return availableConfigs[i];
            }
        }

        return availableConfigs[Random.Range(0, availableConfigs.Length)];
    }

    static bool IsBattleModeScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.name.StartsWith("BattleMode_", System.StringComparison.OrdinalIgnoreCase);
    }

    static BattleModeMusicConfig[] GetAvailableBattleModeMusicConfigs(AudioClip[] clips)
    {
        int count = 0;

        for (int i = 0; i < BattleModeMusicConfigs.Length; i++)
        {
            if (FindClipByName(clips, BattleModeMusicConfigs[i].IntroClipName) != null)
                count++;
        }

        BattleModeMusicConfig[] availableConfigs = new BattleModeMusicConfig[count];
        int next = 0;

        for (int i = 0; i < BattleModeMusicConfigs.Length; i++)
        {
            BattleModeMusicConfig config = BattleModeMusicConfigs[i];
            if (FindClipByName(clips, config.IntroClipName) == null)
                continue;

            availableConfigs[next] = config;
            next++;
        }

        return availableConfigs;
    }

    static AudioClip FindClipByName(AudioClip[] clips, string clipName)
    {
        if (clips == null || string.IsNullOrWhiteSpace(clipName))
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip != null &&
                string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        return null;
    }

    readonly struct BattleModeMusicConfig
    {
        public readonly BattleModeRules.BattleMusicSelection Selection;
        public readonly string IntroClipName;
        public readonly float IntroVolume;
        public readonly string LoopClipName;
        public readonly float LoopVolume;
        public readonly string CriticalClipName;
        public readonly float CriticalVolume;

        public BattleModeMusicConfig(BattleModeRules.BattleMusicSelection selection, string introClipName, float introVolume)
            : this(selection, introClipName, introVolume, null, introVolume, null, introVolume)
        {
        }

        public BattleModeMusicConfig(
            BattleModeRules.BattleMusicSelection selection,
            string introClipName,
            float introVolume,
            string loopClipName,
            float loopVolume,
            string criticalClipName = null,
            float criticalVolume = -1f)
        {
            Selection = selection;
            IntroClipName = introClipName;
            IntroVolume = Mathf.Clamp01(introVolume);
            LoopClipName = loopClipName;
            LoopVolume = Mathf.Clamp01(loopVolume >= 0f ? loopVolume : introVolume);
            CriticalClipName = criticalClipName;
            CriticalVolume = Mathf.Clamp01(criticalVolume >= 0f ? criticalVolume : introVolume);
        }
    }
}
