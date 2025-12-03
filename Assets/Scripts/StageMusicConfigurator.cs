using UnityEngine;

public class StageMusicConfigurator : MonoBehaviour
{
    [Header("Music for this stage")]
    public AudioClip stageDefaultMusic;

    private void Start()
    {
        if (GameMusicController.Instance == null)
            return;

        if (stageDefaultMusic != null)
        {
            GameMusicController.Instance.defaultMusic = stageDefaultMusic;
        }
    }
}
