using UnityEngine;

public class StageMusicConfigurator : MonoBehaviour
{
    [Header("Music for this stage")]
    public AudioClip stageDefaultMusic;

    [Range(0f, 1f)]
    public float musicVolume = 1f;

    private void Start()
    {
        if (GameMusicController.Instance == null)
            return;

        if (stageDefaultMusic != null)
        {
            GameMusicController.Instance.defaultMusic = stageDefaultMusic;
            GameMusicController.Instance.defaultMusicVolume = musicVolume;
        }
    }
}
