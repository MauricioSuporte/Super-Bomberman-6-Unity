using UnityEngine;

[CreateAssetMenu(
    fileName = "New Resurrecting Animated Tile",
    menuName = "Tiles/Resurrecting Animated Destructible Tile")]
public class ResurrectingAnimatedTile : AnimatedTile
{
    [Header("Respawn")]
    [Min(0.1f)] public float respawnSeconds = 10f;
    [Min(0.02f)] public float retryCheckSeconds = 0.25f;

    [Header("Overlap Check")]
    [Min(0.05f)] public float overlapBoxSize = 0.6f;

    [Header("Pre-Respawn Warning (Tile)")]
    [Tooltip("Tile que aparece ANTES do ressurgimento (deve ser atravessável, sem collider).")]
    public AnimatedTile preRespawnWarningTile;

    [Tooltip("Quanto tempo o tile de aviso fica na célula antes do respawn.")]
    [Min(0.01f)] public float preRespawnWarningSeconds = 3f;

    [Header("Respawn Animation (Tile)")]
    [Tooltip("AnimatedTile tocado imediatamente antes do destructible reaparecer (opcional).")]
    public AnimatedTile respawnAnimationTile;

    [Tooltip("Duração da animação de respawn (opcional).")]
    [Min(0.01f)] public float respawnAnimationDuration = 0.4f;
}
