using UnityEngine;

/// <summary>Prefab settings for a room's torch light.</summary>
[DisallowMultipleComponent]
public sealed class BlackoutTorch : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float lightRadius = 1f;
    [SerializeField, Min(0.001f)] private float lightSoftness = 0.2f;

    public float LightRadius => lightRadius;
    public float LightSoftness => lightSoftness;
}
