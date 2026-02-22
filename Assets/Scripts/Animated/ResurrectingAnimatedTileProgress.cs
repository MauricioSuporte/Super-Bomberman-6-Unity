using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "New Resurrecting Animated Tile (Progress)",
    menuName = "Tiles/Resurrecting Animated Destructible Tile (Progress)")]
public class ResurrectingAnimatedTileProgress : AnimatedTile
{
    [Header("Idle Visual (when alive on stage)")]
    public TileBase idleVisualTile;

    [Header("Timing (from destruction moment)")]
    [Min(0.01f)] public float destructionAnimationSeconds = 0.5f;
    [Min(0.1f)] public float respawnSeconds = 10f;
    [Min(0.02f)] public float retryCheckSeconds = 0.25f;

    [Header("Overlap Check")]
    [Min(0.05f)] public float overlapBoxSize = 0.6f;

    [Header("Optional - Pre-Respawn Warning (Tile)")]
    public AnimatedTile preRespawnWarningTile;
    [Min(0.0f)] public float preRespawnWarningSeconds = 0f;

    [Header("Resurrection Animation")]
    public AnimatedTile resurrectionAnimatedTile;
    public TileBase[] resurrectionFrames;

    [Header("Rendering")]
    [Tooltip("Se true, o handler desenha warning/ressurreição no Tilemap Ground e só coloca o destrutível no Tilemap Destructibles ao final.")]
    public bool renderResurrectionOnGround = true;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        Sprite idleSprite = null;

        TileBase pick = idleVisualTile;
        if (pick == null && resurrectionFrames != null && resurrectionFrames.Length > 0)
            pick = resurrectionFrames[^1];

        if (pick is Tile t)
            idleSprite = t.sprite;
        else if (pick is AnimatedTile at && at.m_AnimatedSprites != null && at.m_AnimatedSprites.Length > 0)
            idleSprite = at.m_AnimatedSprites[0];
        else if (m_AnimatedSprites != null && m_AnimatedSprites.Length > 0)
            idleSprite = m_AnimatedSprites[0];

        tileData.sprite = idleSprite;
        tileData.colliderType = m_TileColliderType;
    }
}