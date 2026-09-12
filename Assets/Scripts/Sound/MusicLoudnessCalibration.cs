using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Super Bomberman 6/Audio/Music Loudness Calibration")]
public sealed class MusicLoudnessCalibration : ScriptableObject
{
    public const string ResourcesPath = "Audio/MusicLoudnessCalibration";

    [Serializable]
    public struct ClipVolume
    {
        public string clipName;
        [Range(0f, 1f)] public float volume;
    }

    [SerializeField] List<ClipVolume> battleModeClipVolumes = new();

    public float GetVolume(string clipName, float fallback)
    {
        if (!string.IsNullOrWhiteSpace(clipName))
        {
            for (int i = 0; i < battleModeClipVolumes.Count; i++)
            {
                if (string.Equals(battleModeClipVolumes[i].clipName, clipName, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Clamp01(battleModeClipVolumes[i].volume);
            }
        }

        return Mathf.Clamp01(fallback);
    }

#if UNITY_EDITOR
    public void SetBattleModeClipVolumes(List<ClipVolume> volumes)
    {
        battleModeClipVolumes = volumes ?? new List<ClipVolume>();
    }
#endif
}
