using UnityEngine;

public class GameMusicController : MonoBehaviour
{
    public static GameMusicController Instance;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource.clip != null)
            audioSource.Play();   // Toca direto, sem PlayMusic
    }

    public void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        if (audioSource.clip == clip) return;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }
}
