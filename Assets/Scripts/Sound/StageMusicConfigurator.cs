using UnityEngine;
using UnityEngine.SceneManagement;

public class StageMusicConfigurator : MonoBehaviour
{
    const string BattleModeMusicResourcesPath = "Sounds/BattleModeMusics";

    static readonly BattleModeMusicConfig[] BattleModeMusicConfigs =
    {
        new("SB1 Battle Mode", 0.5f),
        new("Sb2 - Battle1", 0.5f),
        new("Sb2 - Battle2", 0.5f, "Sb2 - Battle2 Loop", 0.5f),
    };

    [Header("Music for this stage")]
    public AudioClip stageDefaultMusic;

    [Range(0f, 1f)]
    public float musicVolume = 1f;

    AudioClip selectedMusic;
    float selectedMusicVolume;
    AudioClip selectedMusicLoop;
    float selectedMusicLoopVolume;

    private void Awake()
    {
        SelectMusicForScene();

        if (GameMusicController.Instance == null || selectedMusic == null)
            return;

        GameMusicController.Instance.defaultMusic = selectedMusic;
        GameMusicController.Instance.defaultMusicVolume = selectedMusicVolume;
        GameMusicController.Instance.defaultMusicLoop = selectedMusicLoop;
        GameMusicController.Instance.defaultMusicLoopVolume = selectedMusicLoopVolume;
    }

    private void Start()
    {
        if (GameMusicController.Instance == null || selectedMusic == null)
            return;

        GameMusicController.Instance.defaultMusic = selectedMusic;
        GameMusicController.Instance.defaultMusicVolume = selectedMusicVolume;
        GameMusicController.Instance.defaultMusicLoop = selectedMusicLoop;
        GameMusicController.Instance.defaultMusicLoopVolume = selectedMusicLoopVolume;

        if (ShouldPlayMusicOnSceneStart())
            GameMusicController.Instance.PlayDefaultMusic(true);
    }

    void SelectMusicForScene()
    {
        selectedMusic = stageDefaultMusic;
        selectedMusicVolume = musicVolume;
        selectedMusicLoop = null;
        selectedMusicLoopVolume = musicVolume;

        if (!IsBattleModeScene())
            return;

        AudioClip[] battleModeClips = Resources.LoadAll<AudioClip>(BattleModeMusicResourcesPath);
        if (battleModeClips == null || battleModeClips.Length <= 0)
            return;

        BattleModeMusicConfig[] availableConfigs = GetAvailableBattleModeMusicConfigs(battleModeClips);
        if (availableConfigs.Length <= 0)
            return;

        BattleModeMusicConfig selectedConfig = availableConfigs[Random.Range(0, availableConfigs.Length)];

        selectedMusic = FindClipByName(battleModeClips, selectedConfig.IntroClipName);
        selectedMusicVolume = selectedConfig.IntroVolume;

        if (!string.IsNullOrWhiteSpace(selectedConfig.LoopClipName))
        {
            selectedMusicLoop = FindClipByName(battleModeClips, selectedConfig.LoopClipName);
            selectedMusicLoopVolume = selectedConfig.LoopVolume;
        }
    }

    private static bool ShouldPlayMusicOnSceneStart()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() &&
               scene.name.StartsWith("BattleMode_", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsBattleModeScene()
    {
        Scene scene = SceneManager.GetActiveScene();
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
