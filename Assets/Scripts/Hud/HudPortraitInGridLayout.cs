using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class HudPortraitInGridLayout : MonoBehaviour
{
    [Header("Portrait References")]
    [SerializeField] private Image[] portraitImages = new Image[4];

    [Header("Resources")]
    [SerializeField] private string portraitsResourcesPath = "HUD/PortraitBombersLive";

    [Header("Portrait Logical Size (SNES pixels)")]
    [SerializeField] private float portraitWidth = 16f;
    [SerializeField] private float portraitHeight = 16f;

    [Header("Parent Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Portrait Offset Inside Each Grid (SNES pixels)")]
    [SerializeField]
    private Vector2[] portraitOffsets = new Vector2[4]
    {
        new Vector2(2f, 2f),
        new Vector2(2f, 2f),
        new Vector2(2f, 2f),
        new Vector2(2f, 2f)
    };

    [Header("Optional Per Portrait Size Override")]
    [SerializeField] private bool useIndividualSizes = false;

    [SerializeField]
    private Vector2[] portraitSizes = new Vector2[4]
    {
        new Vector2(16f, 16f),
        new Vector2(16f, 16f),
        new Vector2(16f, 16f),
        new Vector2(16f, 16f)
    };

    readonly Dictionary<int, Sprite> portraitByIndex = new();
    bool loaded;

    void LateUpdate()
    {
        EnsureSpritesLoaded();
        UpdatePortraitSprites();
        UpdatePortraitLayout();
    }

    void EnsureSpritesLoaded()
    {
        if (loaded)
            return;

        portraitByIndex.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(portraitsResourcesPath);
        if (sprites == null || sprites.Length == 0)
            return;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            string spriteName = sprite.name;
            int underscoreIndex = spriteName.LastIndexOf('_');

            if (underscoreIndex < 0 || underscoreIndex >= spriteName.Length - 1)
                continue;

            if (int.TryParse(spriteName.Substring(underscoreIndex + 1), out int index))
            {
                if (!portraitByIndex.ContainsKey(index))
                    portraitByIndex.Add(index, sprite);
            }
        }

        loaded = true;
    }

    void UpdatePortraitSprites()
    {
        for (int i = 0; i < portraitImages.Length; i++)
        {
            Image portraitImage = portraitImages[i];
            if (portraitImage == null)
                continue;

            int playerId = i + 1;
            BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
            int portraitIndex = GetPortraitIndex(skin);

            if (portraitByIndex.TryGetValue(portraitIndex, out Sprite portraitSprite))
            {
                if (portraitImage.sprite != portraitSprite)
                    portraitImage.sprite = portraitSprite;

                portraitImage.enabled = true;
                portraitImage.preserveAspect = false;
            }
            else
            {
                portraitImage.enabled = false;
            }
        }
    }

    void UpdatePortraitLayout()
    {
        for (int i = 0; i < portraitImages.Length; i++)
        {
            Image portraitImage = portraitImages[i];
            if (portraitImage == null)
                continue;

            RectTransform portraitRect = portraitImage.rectTransform;
            RectTransform parentGrid = portraitRect.parent as RectTransform;

            if (parentGrid == null)
                continue;

            float logicalGridWidth = GetGridWidth(i);
            float logicalGridHeight = gridHeight;

            if (logicalGridWidth <= 0f || logicalGridHeight <= 0f)
                continue;

            Vector2 offset = GetOffset(i);
            Vector2 size = GetSize(i);

            float left = offset.x;
            float bottom = offset.y;
            float right = left + size.x;
            float top = bottom + size.y;

            float minX = left / logicalGridWidth;
            float maxX = right / logicalGridWidth;
            float minY = bottom / logicalGridHeight;
            float maxY = top / logicalGridHeight;

            portraitRect.anchorMin = new Vector2(minX, minY);
            portraitRect.anchorMax = new Vector2(maxX, maxY);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            portraitRect.localScale = Vector3.one;
        }
    }

    float GetGridWidth(int index)
    {
        if (gridWidths != null && index < gridWidths.Length && gridWidths[index] > 0f)
            return gridWidths[index];

        return 46f;
    }

    Vector2 GetOffset(int index)
    {
        if (portraitOffsets != null && index < portraitOffsets.Length)
            return portraitOffsets[index];

        return new Vector2(2f, 2f);
    }

    Vector2 GetSize(int index)
    {
        if (useIndividualSizes && portraitSizes != null && index < portraitSizes.Length)
            return portraitSizes[index];

        return new Vector2(portraitWidth, portraitHeight);
    }

    int GetPortraitIndex(BomberSkin skin)
    {
        switch (skin)
        {
            case BomberSkin.White: return 0;
            case BomberSkin.Black: return 1;
            case BomberSkin.Red: return 2;
            case BomberSkin.Blue: return 3;
            case BomberSkin.Green: return 4;
            case BomberSkin.Yellow: return 5;
            case BomberSkin.Pink: return 6;
            case BomberSkin.Aqua: return 7;
            case BomberSkin.Orange: return 8;
            case BomberSkin.Purple: return 9;
            case BomberSkin.Gray: return 10;
            case BomberSkin.Olive: return 11;
            case BomberSkin.DarkGreen: return 12;
            case BomberSkin.Cyan: return 13;
            case BomberSkin.DarkBlue: return 14;
            case BomberSkin.Brown: return 15;
            case BomberSkin.Magenta: return 16;
            case BomberSkin.Nightmare: return 17;
            case BomberSkin.Gold: return 18;
            case BomberSkin.Golden: return 19;
            default: return 0;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        loaded = false;
        EnsureArraySizes();
    }
#endif

    void Reset()
    {
        EnsureArraySizes();
    }

    void EnsureArraySizes()
    {
        if (portraitOffsets == null || portraitOffsets.Length != 4)
        {
            Vector2[] newOffsets = new Vector2[4];

            if (portraitOffsets != null)
            {
                for (int i = 0; i < Mathf.Min(portraitOffsets.Length, newOffsets.Length); i++)
                    newOffsets[i] = portraitOffsets[i];
            }
            else
            {
                for (int i = 0; i < newOffsets.Length; i++)
                    newOffsets[i] = new Vector2(2f, 2f);
            }

            portraitOffsets = newOffsets;
        }

        if (portraitSizes == null || portraitSizes.Length != 4)
        {
            Vector2[] newSizes = new Vector2[4];

            if (portraitSizes != null)
            {
                for (int i = 0; i < Mathf.Min(portraitSizes.Length, newSizes.Length); i++)
                    newSizes[i] = portraitSizes[i];
            }
            else
            {
                for (int i = 0; i < newSizes.Length; i++)
                    newSizes[i] = new Vector2(16f, 16f);
            }

            portraitSizes = newSizes;
        }

        if (gridWidths == null || gridWidths.Length != 4)
        {
            float[] newGridWidths = new float[4] { 46f, 46f, 46f, 20f };

            if (gridWidths != null)
            {
                for (int i = 0; i < Mathf.Min(gridWidths.Length, newGridWidths.Length); i++)
                    newGridWidths[i] = gridWidths[i];
            }

            gridWidths = newGridWidths;
        }
    }
}