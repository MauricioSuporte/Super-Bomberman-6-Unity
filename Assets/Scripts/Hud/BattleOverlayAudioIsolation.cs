using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleOverlayAudioIsolation : MonoBehaviour
{
    static readonly bool EnableAudioIsolationLogs = true;
    static int activeIsolationCount;
    static bool listenerWasPaused;

    AudioSource overlayAudioSource;
    bool isolationActive;

    public static BattleOverlayAudioIsolation Begin(GameObject owner)
    {
        if (owner == null)
            return null;

        BattleOverlayAudioIsolation isolation = owner.GetComponent<BattleOverlayAudioIsolation>();
        if (isolation == null)
            isolation = owner.AddComponent<BattleOverlayAudioIsolation>();

        isolation.Activate();
        return isolation;
    }

    public void Play(AudioClip clip, float volume = 1f, bool loop = false)
    {
        if (clip == null)
            return;

        EnsureAudioSource();
        overlayAudioSource.Stop();
        overlayAudioSource.clip = clip;
        overlayAudioSource.volume = Mathf.Clamp01(volume);
        overlayAudioSource.loop = loop;
        overlayAudioSource.Play();
    }

    public void Stop()
    {
        if (overlayAudioSource == null)
            return;

        overlayAudioSource.Stop();
        overlayAudioSource.clip = null;
    }

    void Activate()
    {
        if (isolationActive)
            return;

        EnsureAudioSource();

        if (activeIsolationCount == 0)
            listenerWasPaused = AudioListener.pause;

        activeIsolationCount++;
        isolationActive = true;

        AudioIsolationLog($"Activate | owner='{gameObject.name}' | activeCount={activeIsolationCount} | listenerWasPaused={listenerWasPaused}");
        StopGameplayAudio();
        AudioListener.pause = true;
        AudioIsolationLog($"Activate | AudioListener.pause set true | owner='{gameObject.name}'");
    }

    void EnsureAudioSource()
    {
        if (overlayAudioSource != null)
            return;

        overlayAudioSource = gameObject.AddComponent<AudioSource>();
        overlayAudioSource.playOnAwake = false;
        overlayAudioSource.loop = false;
        overlayAudioSource.spatialBlend = 0f;
        overlayAudioSource.ignoreListenerPause = true;

        AudioSource musicSource = GameMusicController.Instance != null
            ? GameMusicController.Instance.GetMusicSource()
            : null;

        if (musicSource != null)
            overlayAudioSource.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
    }

    void StopGameplayAudio()
    {
        if (GameMusicController.Instance != null)
        {
            AudioIsolationLog("StopGameplayAudio | stopping GameMusicController music/sfx");
            GameMusicController.Instance.StopMusic();
            GameMusicController.Instance.StopSfx();
        }

        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null || source == overlayAudioSource)
                continue;

            string sourceName = source.gameObject != null ? source.gameObject.name : string.Empty;
            string clipName = source.clip != null ? source.clip.name : "(none)";
            bool isUnlockToastSfx = sourceName == UnlockToastPresenter.UnlockSfxSourceName;

            if (isUnlockToastSfx)
            {
                AudioIsolationLog($"StopGameplayAudio | keeping unlock toast sfx | source='{sourceName}' | clip='{clipName}' | ignorePause={source.ignoreListenerPause} | isPlaying={source.isPlaying}");
                continue;
            }

            if (source.isPlaying)
                AudioIsolationLog($"StopGameplayAudio | stopping source='{sourceName}' | clip='{clipName}' | ignorePause={source.ignoreListenerPause}");

            source.Stop();
        }
    }

    static void AudioIsolationLog(string message)
    {
        if (!EnableAudioIsolationLogs)
            return;

        Debug.Log($"[BattleOverlayAudioIsolation] {message}");
    }

    void OnDestroy()
    {
        if (!isolationActive)
            return;

        isolationActive = false;
        activeIsolationCount = Mathf.Max(0, activeIsolationCount - 1);

        if (activeIsolationCount == 0)
        {
            AudioListener.pause = listenerWasPaused;
            AudioIsolationLog($"OnDestroy | restored AudioListener.pause={listenerWasPaused}");
        }
    }
}
