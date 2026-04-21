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
            musicSource.pitch = 1f;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.clip = null;
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true, float pitch = 1f, bool restart = true)
    {
        if (clip == null || musicSource == null)
            return;

        bool sameClip = musicSource.clip == clip;

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.pitch = pitch;

        if (restart || !sameClip || !musicSource.isPlaying)
            musicSource.time = 0f;

        musicSource.Play();
    }

    public void PlayDefaultMusic(bool restart = true)
    {
        if (defaultMusic == null || musicSource == null)
            return;

        PlayMusic(defaultMusic, defaultMusicVolume, true, 1f, restart);
    }

    public void PlayDefaultMusicWithPitch(float pitch, bool restart = true)
    {
        if (defaultMusic == null || musicSource == null)
            return;

        PlayMusic(defaultMusic, defaultMusicVolume, true, pitch, restart);
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
}