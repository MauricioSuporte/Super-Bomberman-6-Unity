using UnityEngine;

/// <summary>
/// Provides the normal bomb fuse animation from the shared 16x16 item sheet.
/// Sheet coordinates use a top-left origin: (x, y).
/// </summary>
public static class BombSheetSpriteSet
{
    private const string SheetResourcesPath = "Sprites/BombItems/Itens";
    private const int CellSize = 16;

    private static Sprite[] normalBombFrames;

    public static void ApplyNormalBombFuse(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Sprite[] frames = GetNormalBombFrames();
        if (frames == null || frames.Length == 0)
            return;

        renderer.idleSprite = frames[0];
        renderer.animationSprite = frames;
        renderer.idle = false;
        renderer.loop = true;
        renderer.RefreshFrame();
    }

    private static Sprite[] GetNormalBombFrames()
    {
        if (normalBombFrames != null)
            return normalBombFrames;

        Texture2D sheet = Resources.Load<Texture2D>(SheetResourcesPath);
        if (sheet == null)
        {
            Debug.LogWarning($"[BombSheetSpriteSet] Sprite sheet not found at Resources/{SheetResourcesPath}.");
            return null;
        }

        sheet.filterMode = FilterMode.Point;
        sheet.wrapMode = TextureWrapMode.Clamp;

        Sprite large = CreateSprite(sheet, 19, 0);
        Sprite medium = CreateSprite(sheet, 20, 0);
        Sprite small = CreateSprite(sheet, 21, 0);

        if (large == null || medium == null || small == null)
            return null;

        normalBombFrames = new[] { medium, large, medium, small };
        return normalBombFrames;
    }

    private static Sprite CreateSprite(Texture2D sheet, int column, int row)
    {
        if (column < 0 || row < 0 ||
            (column + 1) * CellSize > sheet.width ||
            (row + 1) * CellSize > sheet.height)
        {
            Debug.LogWarning($"[BombSheetSpriteSet] Cell ({column}, {row}) is outside {sheet.width}x{sheet.height} sheet.");
            return null;
        }

        float y = sheet.height - ((row + 1) * CellSize);
        Sprite sprite = Sprite.Create(
            sheet,
            new Rect(column * CellSize, y, CellSize, CellSize),
            new Vector2(0.5f, 0.5f),
            CellSize,
            0,
            SpriteMeshType.FullRect);
        sprite.name = $"Itens_{column}_{row}";
        return sprite;
    }
}
