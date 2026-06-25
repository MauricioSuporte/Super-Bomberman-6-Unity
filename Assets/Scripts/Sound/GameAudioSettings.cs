using UnityEngine;

public static class GameAudioSettings
{
    const string MusicVolumeKey = "Audio.MusicVolume";
    const string SfxVolumeKey = "Audio.SfxVolume";
    const string VoicesEnabledKey = "Audio.VoicesEnabled";

    const float DefaultMusicVolume = 1f;
    const float DefaultSfxVolume = 1f;
    const int DefaultVoicesEnabled = 1;

    public static event System.Action Changed;

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
    public static bool VoicesEnabled => PlayerPrefs.GetInt(VoicesEnabledKey, DefaultVoicesEnabled) != 0;

    public static void SetMusicVolume(float value)
    {
        SetFloat(MusicVolumeKey, value);
    }

    public static void SetSfxVolume(float value)
    {
        SetFloat(SfxVolumeKey, value);
    }

    public static void SetVoicesEnabled(bool enabled)
    {
        int value = enabled ? 1 : 0;
        if (PlayerPrefs.GetInt(VoicesEnabledKey, DefaultVoicesEnabled) == value)
            return;

        PlayerPrefs.SetInt(VoicesEnabledKey, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static float ApplyMusicVolume(float volume)
    {
        return Mathf.Clamp01(volume) * Mathf.Clamp01(MusicVolume);
    }

    public static float ApplySfxVolume(float volume)
    {
        return Mathf.Clamp01(volume) * Mathf.Clamp01(SfxVolume);
    }

    public static float ApplyVoiceSfxVolume(float volume)
    {
        if (!VoicesEnabled)
            return 0f;

        return ApplySfxVolume(volume);
    }

    public static void PlaySfx(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (source == null || clip == null)
            return;

        source.PlayOneShot(clip, ApplySfxVolume(volume));
    }

    public static void PlayVoiceSfx(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (source == null || clip == null || !VoicesEnabled)
            return;

        source.PlayOneShot(clip, ApplyVoiceSfxVolume(volume));
    }

    public static void PlaySfxAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, position, ApplySfxVolume(volume));
    }

    public static void PlaySfxClip(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (source == null || clip == null)
            return;

        source.clip = clip;
        source.volume = ApplySfxVolume(volume);
        source.Play();
    }

    public static void PlayVoiceSfxAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || !VoicesEnabled)
            return;

        AudioSource.PlayClipAtPoint(clip, position, ApplyVoiceSfxVolume(volume));
    }

    public static int VolumePercent(float volume)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
    }

    static void SetFloat(string key, float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(PlayerPrefs.GetFloat(key, -1f), clamped))
            return;

        PlayerPrefs.SetFloat(key, clamped);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
