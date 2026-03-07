using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class GameMusicController : MonoBehaviour
{
    public static GameMusicController Instance;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    public AudioClip defaultMusic;
    public AudioClip deathMusic;

    [Range(0f, 1f)]
    public float defaultMusicVolume = 1f;

    private void Awake()
    {
        musicSource = GetComponent<AudioSource>();

        if (Instance != null && Instance != this)
        {
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
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.clip = null;
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || musicSource == null)
            return;

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.Play();
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
    }

    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying && musicSource.clip != null)
            musicSource.UnPause();
    }

    public AudioSource GetMusicSource()
    {
        return musicSource;
    }
}
