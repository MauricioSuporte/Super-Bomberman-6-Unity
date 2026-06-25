using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class HudPushStartInGridLayout : MonoBehaviour
{
    [Header("Sprite References Per Player")]
    [SerializeField] private Image[] pushStartImages = new Image[4];

    [Header("Sprite")]
    [SerializeField] private Sprite pushStartSprite;
    [SerializeField] private Color spriteColor = Color.white;
    [SerializeField] private bool preserveAspect = true;

    [Header("Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Sprite Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 defaultSpriteSize = new Vector2(23f, 19f);

    private Vector2[] spriteSizes = new Vector2[4]
    {
        new Vector2(23f, 19f),
        new Vector2(23f, 19f),
        new Vector2(23f, 19f),
        new Vector2(23f, 19f)
    };

    [Header("Sprite Offset Inside Each Grid (SNES pixels)")]
    [SerializeField]
    private Vector2[] spriteOffsets = new Vector2[4]
    {
        new Vector2(11.5f, 0f),
        new Vector2(11.5f, 0f),
        new Vector2(11.5f, 0f),
        new Vector2(2f, 0f)
    };

    [Header("Optional Per Player Global Offset (SNES pixels)")]
    [SerializeField]
    private Vector2[] playerOffsetAdjustments = new Vector2[4]
    {
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
        Vector2.zero
    };

    [Header("Blink")]
    [SerializeField] private float blinkInterval = 1f;

    private bool blinkVisible = true;
    private float lastBlinkRealtime;

    void LateUpdate()
    {
        UpdateBlink();
        UpdateImages();
        UpdateLayout();
    }

    void UpdateBlink()
    {
        if (!Application.isPlaying)
        {
            blinkVisible = true;
            return;
        }

        if (blinkInterval <= 0f)
        {
            blinkVisible = true;
            return;
        }

        float now = Time.unscaledTime;

        if (lastBlinkRealtime <= 0f)
            lastBlinkRealtime = now;

        if (now - lastBlinkRealtime >= blinkInterval)
        {
            blinkVisible = !blinkVisible;
            lastBlinkRealtime = now;
        }
    }

    void UpdateImages()
    {
        for (int i = 0; i < pushStartImages.Length; i++)
        {
            Image image = pushStartImages[i];
            if (image == null)
                continue;

            bool playerAtivo = IsPlayerAtivo(i + 1);
            bool shouldShow = !playerAtivo && blinkVisible;

            if (pushStartSprite != null)
                image.sprite = pushStartSprite;

            image.color = spriteColor;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            image.enabled = shouldShow;
        }
    }

    void UpdateLayout()
    {
        for (int i = 0; i < pushStartImages.Length; i++)
        {
            Image image = pushStartImages[i];
            if (image == null)
                continue;

            RectTransform imageRect = image.rectTransform;
            RectTransform parentGrid = imageRect.parent as RectTransform;

            if (parentGrid == null)
                continue;

            float logicalGridWidth = GetGridWidth(i);
            float logicalGridHeight = gridHeight;

            if (logicalGridWidth <= 0f || logicalGridHeight <= 0f)
                continue;

            Vector2 playerAdjust = GetPlayerAdjustment(i);
            Vector2 offset = GetOffset(i) + playerAdjust;
            Vector2 size = GetSize(i);

            float left = offset.x;
            float bottom = offset.y;
            float right = left + size.x;
            float top = bottom + size.y;

            float minX = left / logicalGridWidth;
            float maxX = right / logicalGridWidth;
            float minY = bottom / logicalGridHeight;
            float maxY = top / logicalGridHeight;

            imageRect.anchorMin = new Vector2(minX, minY);
            imageRect.anchorMax = new Vector2(maxX, maxY);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.localScale = Vector3.one;
        }
    }

    bool IsPlayerAtivo(int playerId)
    {
        if (Application.isPlaying && GameSession.Instance != null)
            return GameSession.Instance.IsPlayerActive(playerId);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return playerId >= 1 && playerId <= 4;
#endif

        return playerId == 1;
    }

    float GetGridWidth(int index)
    {
        if (gridWidths != null && index >= 0 && index < gridWidths.Length && gridWidths[index] > 0f)
            return gridWidths[index];

        return 46f;
    }

    Vector2 GetOffset(int index)
    {
        if (spriteOffsets != null && index >= 0 && index < spriteOffsets.Length)
            return spriteOffsets[index];

        return new Vector2(11.5f, 0f);
    }

    Vector2 GetSize(int index)
    {
        if (spriteSizes != null && index >= 0 && index < spriteSizes.Length)
            return spriteSizes[index];

        return defaultSpriteSize;
    }

    Vector2 GetPlayerAdjustment(int index)
    {
        if (playerOffsetAdjustments != null && index >= 0 && index < playerOffsetAdjustments.Length)
            return playerOffsetAdjustments[index];

        return Vector2.zero;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureArraySizes();
    }
#endif

    void Reset()
    {
        EnsureArraySizes();
    }

    void EnsureArraySizes()
    {
        if (gridWidths == null || gridWidths.Length != 4)
            gridWidths = new float[4] { 46f, 46f, 46f, 20f };

        if (spriteSizes == null || spriteSizes.Length != 4)
        {
            spriteSizes = new Vector2[4]
            {
                new Vector2(23f, 19f),
                new Vector2(23f, 19f),
                new Vector2(23f, 19f),
                new Vector2(23f, 19f)
            };
        }

        if (spriteOffsets == null || spriteOffsets.Length != 4)
        {
            spriteOffsets = new Vector2[4]
            {
                new Vector2(11.5f, 0f),
                new Vector2(11.5f, 0f),
                new Vector2(11.5f, 0f),
                new Vector2(2f, 0f)
            };
        }

        if (playerOffsetAdjustments == null || playerOffsetAdjustments.Length != 4)
            playerOffsetAdjustments = new Vector2[4];

        if (pushStartImages == null || pushStartImages.Length != 4)
            pushStartImages = new Image[4];
    }
}