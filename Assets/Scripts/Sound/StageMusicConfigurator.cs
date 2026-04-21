using UnityEngine;

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

        GameMusicController.Instance.PlayDefaultMusic(true);
    }
}