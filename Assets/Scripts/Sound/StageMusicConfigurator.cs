using UnityEngine;
using UnityEngine.SceneManagement;

public class StageMusicConfigurator : MonoBehaviour
{
    [Header("Music for this stage")]
    public AudioClip stageDefaultMusic;

    [Range(0f, 1f)]
    public float musicVolume = 1f;

    private void Awake()
    {
        if (GameMusicController.Instance == null || stageDefaultMusic == null)
            return;

        GameMusicController.Instance.defaultMusic = stageDefaultMusic;
        GameMusicController.Instance.defaultMusicVolume = musicVolume;
    }

    private void Start()
    {
        if (GameMusicController.Instance == null || stageDefaultMusic == null)
            return;

        GameMusicController.Instance.defaultMusic = stageDefaultMusic;
        GameMusicController.Instance.defaultMusicVolume = musicVolume;

        if (ShouldPlayMusicOnSceneStart())
            GameMusicController.Instance.PlayDefaultMusic(true);
    }

    private static bool ShouldPlayMusicOnSceneStart()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() &&
               scene.name.StartsWith("BattleMode_", System.StringComparison.OrdinalIgnoreCase);
    }
}
