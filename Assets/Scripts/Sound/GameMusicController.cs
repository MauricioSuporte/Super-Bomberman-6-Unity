using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class GameMusicController : MonoBehaviour
{
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
}
