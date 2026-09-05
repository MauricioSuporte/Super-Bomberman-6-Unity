using System;
using UnityEngine;

/// <summary>Prefab-owned colors that remain visible through a world blackout.</summary>
[DisallowMultipleComponent]
public sealed class BlackoutColorPalette : MonoBehaviour
{
    [Tooltip("Sprite colors allowed through darkness (up to 8). Empty means no colors are revealed. Shared by the prefab's BlackoutVisibleParts entries.")]
    [SerializeField] private Color[] visibleColors = Array.Empty<Color>();
    [SerializeField, Range(0f, 0.25f)] private float colorTolerance = 0.001f;

    public Color[] VisibleColors => visibleColors ?? Array.Empty<Color>();
    public float ColorTolerance => colorTolerance;
    public bool CanReveal => isActiveAndEnabled && VisibleColors.Length > 0;
}
