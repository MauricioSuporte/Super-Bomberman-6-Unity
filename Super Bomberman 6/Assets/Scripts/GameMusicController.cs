using UnityEngine;

public class GameMusicController : MonoBehaviour
{
    public static GameMusicController Instance;

    private AudioSource audioSource;

    [Header("Music Tracks")]
    public AudioClip defaultMusic;
    public AudioClip deathMusic;

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

        if (defaultMusic != null)
            PlayMusic(defaultMusic);
    }

    public void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;

        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public void PauseMusic()
    {
        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    public void ResumeMusic()
    {
        if (!audioSource.isPlaying && audioSource.clip != null)
            audioSource.UnPause();
    }
}
