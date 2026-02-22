using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Animated Tile", menuName = "Tiles/Animated Tile")]
public class AnimatedTile : TileBase
{
    public Sprite[] m_AnimatedSprites;

    public float m_MinSpeed = 1f;
    public float m_MaxSpeed = 1f;

    public float m_AnimationStartTime;

    public bool UseSpecifiedTime = false;
    [Min(0.01f)]
    public float SpecifiedTime = 0.25f;

    public Tile.ColliderType m_TileColliderType = Tile.ColliderType.Sprite;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = (m_AnimatedSprites != null && m_AnimatedSprites.Length > 0)
            ? m_AnimatedSprites[0]
            : null;

        tileData.colliderType = m_TileColliderType;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (m_AnimatedSprites == null || m_AnimatedSprites.Length == 0)
            return false;

        tileAnimationData.animatedSprites = m_AnimatedSprites;

        if (UseSpecifiedTime && SpecifiedTime > 0f)
        {
            float speed = m_AnimatedSprites.Length / SpecifiedTime;
            tileAnimationData.animationSpeed = speed;
        }
        else
        {
            tileAnimationData.animationSpeed = Random.Range(m_MinSpeed, m_MaxSpeed);
        }

        tileAnimationData.animationStartTime = m_AnimationStartTime;

        return true;
    }
}