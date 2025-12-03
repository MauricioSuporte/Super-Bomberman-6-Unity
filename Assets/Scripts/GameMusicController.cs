using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameMusicController : MonoBehaviour
{
    public static GameMusicController Instance;

    private AudioSource audioSource;

    public AudioClip defaultMusic;
    public AudioClip deathMusic;

    [Range(0f, 1f)]
    public float defaultMusicVolume = 1f;

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

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.clip = null;
        }
    }

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.loop = loop;
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    public void StopMusic()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
    }

    public void PauseMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Pause();
    }

    public void ResumeMusic()
    {
        if (audioSource != null && !audioSource.isPlaying && audioSource.clip != null)
            audioSource.UnPause();
    }
}
