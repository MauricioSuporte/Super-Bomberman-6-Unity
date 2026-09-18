using UnityEngine;
using UnityEngine.UI;

// Four independent 8x8 corners around a 32x32 portrait, plus an 8x8 digit and P.
public sealed class BomberPortraitSelectionCursor : MonoBehaviour
{
    const string ResourcesRoot = "Sprites/CharacterSelect/";
    const float FrameSeconds = 0.5f;
    static readonly int[] ExpansionFrames = { 0, 1, 2, 1 };
    readonly Image[] corners = new Image[4];
    Image number;
    Image suffix;
    Texture2D palette;
    float animationTime;

    public void Initialize(int playerId)
    {
        string[] names = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
        for (int i = 0; i < corners.Length; i++)
            corners[i] = CreatePart(names[i]);
        number = CreatePart(Mathf.Clamp(playerId, 1, GameSession.MaxPlayerId).ToString());
        suffix = CreatePart("P");
        palette = Resources.Load<Texture2D>(ResourcesRoot + "SelectionPalette");
    }

    Image CreatePart(string name)
    {
        GameObject part = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        part.transform.SetParent(transform, false);
        Image image = part.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>(ResourcesRoot + "Cursor/" + name);
        image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return image;
    }

    void OnEnable() => animationTime = 0f;

    public void Refresh(BomberSkin skin, Vector2 portraitSize)
    {
        if (number == null) return;
        animationTime = (animationTime + Time.unscaledDeltaTime) % (FrameSeconds * ExpansionFrames.Length);
        int expansion = ExpansionFrames[Mathf.FloorToInt(animationTime / FrameSeconds)];
        Vector2 pixel = portraitSize / 32f;
        Color tint = Color.white;
        if (palette != null)
        {
            int column = BomberSkinResourceCatalog.GetPaletteNumber(skin);
            tint = column < palette.width ? palette.GetPixel(column, palette.height - 1) : Color.clear;
            if (tint.a == 0) tint = palette.GetPixel(0, palette.height - 1);
        }
        for (int i = 0; i < corners.Length; i++)
        {
            float x = i % 2 == 0 ? -1f : 1f;
            float y = i < 2 ? 1f : -1f;
            SetPart(corners[i], new Vector2(x * (16 + expansion), y * (16 + expansion)), pixel, tint);
        }
        SetPart(number, new Vector2(-4f, -16f), pixel, tint);
        SetPart(suffix, new Vector2(4f, -16f), pixel, tint);
    }

    static void SetPart(Image image, Vector2 position, Vector2 pixelSize, Color tint)
    {
        image.rectTransform.sizeDelta = pixelSize * 8f;
        image.rectTransform.anchoredPosition = Vector2.Scale(position, pixelSize);
        image.color = tint;
    }
}
