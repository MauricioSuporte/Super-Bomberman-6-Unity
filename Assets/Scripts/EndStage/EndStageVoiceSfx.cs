using UnityEngine;

public static class EndStageVoiceSfx
{
    const float Good1Volume = 0.5f;
    const float Good2Volume = 0.5f;
    const float Good3Volume = 1f;

    static AudioClip[] goodClips;
    static AudioClip skullClip;

    static void EnsureGoodClipsLoaded()
    {
        if (goodClips != null)
            return;

        goodClips = new AudioClip[3];
        goodClips[0] = Resources.Load<AudioClip>("Sounds/good1");
        goodClips[1] = Resources.Load<AudioClip>("Sounds/good2");
        goodClips[2] = Resources.Load<AudioClip>("Sounds/good3");
    }

    static void EnsureSkullClipLoaded()
    {
        if (skullClip != null)
            return;

        skullClip = Resources.Load<AudioClip>("Sounds/skull");
    }

    static float GetGoodClipVolume(int clipIndex)
    {
        return clipIndex switch
        {
            0 => Good1Volume,
            1 => Good2Volume,
            2 => Good3Volume,
            _ => 1f,
        };
    }

    public static void PlayRandomGood(AudioSource audio)
    {
        if (audio == null)
            return;

        EnsureGoodClipsLoaded();

        int count = 0;
        for (int i = 0; i < goodClips.Length; i++)
        {
            if (goodClips[i] != null)
                count++;
        }

        if (count <= 0)
            return;

        int pick = Random.Range(0, goodClips.Length);
        for (int tries = 0; tries < goodClips.Length && goodClips[pick] == null; tries++)
            pick = (pick + 1) % goodClips.Length;

        AudioClip clip = goodClips[pick];
        if (clip == null)
            return;

        float volume = GetGoodClipVolume(pick);
        audio.PlayOneShot(clip, volume);
    }

    public static bool TryPlaySkull(AudioSource audio, float volume)
    {
        if (audio == null)
            return false;

        EnsureSkullClipLoaded();

        if (skullClip == null)
            return false;

        audio.PlayOneShot(skullClip, volume);
        return true;
    }
}