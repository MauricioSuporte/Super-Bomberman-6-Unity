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
        new("SB1 Battle Mode", 0.5f),
        new("Sb2 - Battle1", 0.5f),
        new("Sb2 - Battle2", 0.5f, "Sb2 - Battle2 Loop", 0.5f),
    };

    public static GameMusicController Instance;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    public AudioClip defaultMusic;
    public AudioClip defaultMusicLoop;
    public AudioClip deathMusic;

    [Range(0f, 1f)]
    public float defaultMusicVolume = 1f;

    [Range(0f, 1f)]
    public float defaultMusicLoopVolume = 1f;

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

        PlayDefaultMusic(true);
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

        PlayDefaultMusic(true);
    }

    void ApplySceneMusicSettingsFrom(GameMusicController sceneMusicController)
    {
        if (sceneMusicController == null)
            return;

        defaultMusic = sceneMusicController.defaultMusic;
        defaultMusicLoop = sceneMusicController.defaultMusicLoop;
        defaultMusicVolume = sceneMusicController.defaultMusicVolume;
        defaultMusicLoopVolume = sceneMusicController.defaultMusicLoopVolume;

        if (sceneMusicController.deathMusic != null)
            deathMusic = sceneMusicController.deathMusic;
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true, float pitch = 1f, bool restart = true)
    {
        if (clip == null || musicSource == null)
            return;

        StopAllCoroutines();

        bool sameClip = musicSource.clip == clip;

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.pitch = pitch;

        if (restart || !sameClip || !musicSource.isPlaying)
            musicSource.time = 0f;

        musicSource.Play();
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

        StopAllCoroutines();

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
        StartCoroutine(PlayLoopAfterIntroRoutine(introClip, loopClip, loopVolume, pitch));
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
        StopAllCoroutines();
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
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (musicSource == null || musicSource.clip == null)
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
        while (musicSource != null &&
               musicSource.clip == introClip &&
               musicSource.isPlaying &&
               musicSource.time < introClip.length)
        {
            yield return null;
        }

        if (musicSource == null || musicSource.clip != introClip)
            yield break;

        PlayMusic(loopClip, loopVolume, true, pitch, true);
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

        BattleModeMusicConfig selectedConfig = availableConfigs[Random.Range(0, availableConfigs.Length)];
        AudioClip selectedIntroClip = FindClipByName(battleModeClips, selectedConfig.IntroClipName);
        if (selectedIntroClip == null)
            return false;

        defaultMusic = selectedIntroClip;
        defaultMusicVolume = selectedConfig.IntroVolume;
        defaultMusicLoop = null;
        defaultMusicLoopVolume = selectedConfig.LoopVolume;

        if (!string.IsNullOrWhiteSpace(selectedConfig.LoopClipName))
            defaultMusicLoop = FindClipByName(battleModeClips, selectedConfig.LoopClipName);

        return true;
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
        public readonly string IntroClipName;
        public readonly float IntroVolume;
        public readonly string LoopClipName;
        public readonly float LoopVolume;

        public BattleModeMusicConfig(string introClipName, float introVolume)
            : this(introClipName, introVolume, null, introVolume)
        {
        }

        public BattleModeMusicConfig(string introClipName, float introVolume, string loopClipName, float loopVolume)
        {
            IntroClipName = introClipName;
            IntroVolume = Mathf.Clamp01(introVolume);
            LoopClipName = loopClipName;
            LoopVolume = Mathf.Clamp01(loopVolume);
        }
    }
}
