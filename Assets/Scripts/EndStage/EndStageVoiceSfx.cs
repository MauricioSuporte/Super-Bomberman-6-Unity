using System.Collections.Generic;
using UnityEngine;

public static class EndStageVoiceSfx
{
    const bool DebugVictoryVoice = false;
    const float Good1Volume = 0.5f;
    const float Good2Volume = 0.5f;
    const float Good3Volume = 1f;
    static readonly string[] SkullClipResourcePaths =
    {
        "Sounds/skull voice",
        "Sounds/skull",
        "Sounds/skull collect"
    };

    static AudioClip[] goodClips;
    static AudioClip skullClip;
    static bool voicePlayedThisStage;

    public static void ResetPlaybackState()
    {
        voicePlayedThisStage = false;
    }

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

        for (int i = 0; i < SkullClipResourcePaths.Length; i++)
        {
            skullClip = Resources.Load<AudioClip>(SkullClipResourcePaths[i]);
            if (skullClip != null)
                return;
        }
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
        TryPlayRandomGood(audio);
    }

    public static bool TryPlayRandomGood(AudioSource audio)
    {
        if (audio == null)
            return false;

        EnsureGoodClipsLoaded();

        int count = 0;
        for (int i = 0; i < goodClips.Length; i++)
        {
            if (goodClips[i] != null)
                count++;
        }

        if (count <= 0)
            return false;

        int pick = Random.Range(0, goodClips.Length);
        for (int tries = 0; tries < goodClips.Length && goodClips[pick] == null; tries++)
            pick = (pick + 1) % goodClips.Length;

        AudioClip clip = goodClips[pick];
        if (clip == null)
            return false;

        float volume = GetGoodClipVolume(pick);
        audio.PlayOneShot(clip, volume);
        return true;
    }

    public static bool TryPlaySkull(AudioSource audio, float volume)
    {
        return TryPlaySkull(audio, volume, null);
    }

    static bool TryPlaySkull(AudioSource audio, float volume, string context)
    {
        if (audio == null)
        {
            SLog(context, "TryPlaySkull | audio=NULL");
            return false;
        }

        EnsureSkullClipLoaded();

        if (skullClip == null)
        {
            SLog(context, "TryPlaySkull | skullClip=NULL paths=Sounds/skull voice,Sounds/skull,Sounds/skull collect");
            return false;
        }

        audio.PlayOneShot(skullClip, volume);
        SLog(context, $"TryPlaySkull | clip={skullClip.name} volume={volume}");
        return true;
    }

    public static bool TryPlayVictoryVoice(
        AudioSource audio,
        bool hasNightmareBomber,
        bool playVoice = true,
        bool playSkullForNightmareBomber = true,
        float skullVolume = 1f,
        string context = null)
    {
        SLog(context,
            $"TryPlayVictoryVoice | audio={(audio != null ? audio.name : "NULL")} " +
            $"playVoice={playVoice} voicePlayedThisStage={voicePlayedThisStage} " +
            $"hasNightmareBomber={hasNightmareBomber} playSkullForNightmareBomber={playSkullForNightmareBomber}");

        if (!playVoice)
            return false;

        if (voicePlayedThisStage)
            return false;

        if (audio == null)
            return false;

        if (playSkullForNightmareBomber && hasNightmareBomber && TryPlaySkull(audio, skullVolume, context))
        {
            voicePlayedThisStage = true;
            SLog(context, "TryPlayVictoryVoice | played=skull");
            return true;
        }

        if (TryPlayRandomGood(audio))
        {
            voicePlayedThisStage = true;
            SLog(context, "TryPlayVictoryVoice | played=good");
            return true;
        }

        SLog(context, "TryPlayVictoryVoice | played=none");
        return false;
    }

    public static bool HasAnyActiveNightmareBomber(IEnumerable<MovementController> players, string context = null)
    {
        if (players == null)
        {
            SLog(context, "HasAnyActiveNightmareBomber | players=NULL");
            return false;
        }

        PlayerPersistentStats.EnsureSessionBooted();

        int index = 0;
        bool found = false;
        foreach (MovementController movement in players)
        {
            SLog(context, $"Candidate[{index}] | {DescribePlayer(movement)}");

            if (IsActiveNightmareBomber(movement))
                found = true;

            index++;
        }

        SLog(context, $"HasAnyActiveNightmareBomber | count={index} found={found}");
        return found;
    }

    public static bool IsActiveNightmareBomber(MovementController movement)
    {
        if (movement == null)
            return false;

        if (!movement.CompareTag("Player"))
            return false;

        if (!movement.gameObject.activeInHierarchy)
            return false;

        if (movement.isDead)
            return false;

        int playerId = 1;

        if (movement.TryGetComponent<PlayerIdentity>(out var identity) && identity != null)
            playerId = Mathf.Clamp(identity.playerId, 1, 6);

        var state = PlayerPersistentStats.GetRuntime(playerId);
        if (state == null)
            state = PlayerPersistentStats.Get(playerId);

        return state != null && state.Skin == BomberSkin.Nightmare;
    }

    static string DescribePlayer(MovementController movement)
    {
        if (movement == null)
            return "movement=NULL";

        int identityPlayerId = -1;
        if (movement.TryGetComponent<PlayerIdentity>(out var identity) && identity != null)
            identityPlayerId = identity.playerId;

        int playerId = identityPlayerId > 0
            ? Mathf.Clamp(identityPlayerId, 1, 6)
            : Mathf.Clamp(movement.PlayerId, 1, 6);

        var runtime = PlayerPersistentStats.GetRuntime(playerId);
        var persistent = PlayerPersistentStats.Get(playerId);

        return
            $"name={movement.name} playerId={playerId} identityPlayerId={identityPlayerId} " +
            $"movementPlayerId={movement.PlayerId} active={movement.gameObject.activeInHierarchy} " +
            $"tag={movement.tag} isDead={movement.isDead} isEndingStage={movement.IsEndingStage} " +
            $"runtimeSkin={(runtime != null ? runtime.Skin.ToString() : "NULL")} " +
            $"persistentSkin={(persistent != null ? persistent.Skin.ToString() : "NULL")} " +
            $"isNightmare={IsActiveNightmareBomber(movement)}";
    }

    static void SLog(string context, string message)
    {
        if (!DebugVictoryVoice || string.IsNullOrEmpty(context))
            return;

        Debug.Log($"[EndStageVoiceSfx][{context}] {message}");
    }
}
