using TMPro;
using UnityEngine;

[ExecuteAlways]
public sealed class HudPushStartInGridLayout : MonoBehaviour
{
    [Header("Text References Per Player")]
    [SerializeField] private TextMeshProUGUI[] pushStartTexts = new TextMeshProUGUI[4];

    [Header("Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Text Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 defaultTextSize = new(24f, 7f);

    [SerializeField]
    private Vector2[] textSizes = new Vector2[4]
    {
        new(24f, 7f),
        new(24f, 7f),
        new(24f, 7f),
        new(16f, 7f)
    };

    [Header("Text Offset Inside Each Grid (SNES pixels)")]
    [SerializeField]
    private Vector2[] textOffsets = new Vector2[4]
    {
        new(18f, 6f),
        new(18f, 6f),
        new(18f, 6f),
        new(2f, 6f)
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

    [Header("Text Content")]
    [SerializeField] private string pushStartLabel = "PUSH START";

    [Header("Text Style")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private int baseFontSize = 7;
    [SerializeField] private FontStyles fontStyle = FontStyles.Bold;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private bool richText = true;
    [SerializeField] private bool extraPadding = true;

    [Header("Dynamic Scale")]
    [SerializeField] private bool dynamicScale = true;
    [SerializeField] private int referenceWidth = 256;
    [SerializeField] private int referenceHeight = 224;
    [SerializeField, Min(1)] private int designUpscale = 1;
    [SerializeField, Min(0.01f)] private float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 10f;

    [Header("Blink")]
    [SerializeField] private float blinkInterval = 1f;

    private bool blinkVisible = true;
    private float lastBlinkRealtime;
    private RectTransform cachedRootRect;
    private float currentUiScale = 1f;

    void LateUpdate()
    {
        UpdateUiScale();
        UpdateBlink();
        UpdateTexts();
        UpdateLayout();
    }

    void UpdateUiScale()
    {
        cachedRootRect = ResolveReferenceRect();
        currentUiScale = ComputeUiScale();
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

    void UpdateTexts()
    {
        int activePlayerCount = GetActivePlayerCount();
        int scaledFontSize = ScaledFont(baseFontSize);

        for (int i = 0; i < pushStartTexts.Length; i++)
        {
            TextMeshProUGUI text = pushStartTexts[i];
            if (text == null)
                continue;

            bool playerAtivo = i < activePlayerCount;
            bool shouldShow = !playerAtivo && blinkVisible;

            if (fontAsset != null)
                text.font = fontAsset;

            text.text = pushStartLabel;
            text.fontSize = scaledFontSize;
            text.fontStyle = fontStyle;
            text.color = textColor;
            text.richText = richText;
            text.extraPadding = extraPadding;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;

            text.enabled = shouldShow;
        }
    }

    void UpdateLayout()
    {
        for (int i = 0; i < pushStartTexts.Length; i++)
        {
            TextMeshProUGUI text = pushStartTexts[i];
            if (text == null)
                continue;

            RectTransform textRect = text.rectTransform;
            RectTransform parentGrid = textRect.parent as RectTransform;

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

            textRect.anchorMin = new Vector2(minX, minY);
            textRect.anchorMax = new Vector2(maxX, maxY);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;
        }
    }

    RectTransform ResolveReferenceRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        Transform safeFrame = canvas.transform.Find("SafeFrame4x3");
        if (safeFrame is RectTransform safeFrameRect)
            return safeFrameRect;

        return canvas.transform as RectTransform;
    }

    float ComputeUiScale()
    {
        if (!dynamicScale)
            return 1f;

        RectTransform root = cachedRootRect;
        if (root == null)
            return 1f;

        Rect r = root.rect;
        float usedW = Mathf.Max(1f, r.width);
        float usedH = Mathf.Max(1f, r.height);

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);
        float baseScaleRaw = Mathf.Min(sx, sy);

        float ui = (baseScaleRaw / Mathf.Max(1f, designUpscale)) * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);

        return ui;
    }

    int ScaledFont(int baseSize)
    {
        return Mathf.Clamp(Mathf.RoundToInt(baseSize * currentUiScale), 1, 500);
    }

    int GetActivePlayerCount()
    {
        if (Application.isPlaying && GameSession.Instance != null)
            return Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return 4;
#endif

        return 1;
    }

    float GetGridWidth(int index)
    {
        if (gridWidths != null && index >= 0 && index < gridWidths.Length && gridWidths[index] > 0f)
            return gridWidths[index];

        return 46f;
    }

    Vector2 GetOffset(int index)
    {
        if (textOffsets != null && index >= 0 && index < textOffsets.Length)
            return textOffsets[index];

        return new Vector2(18f, 6f);
    }

    Vector2 GetSize(int index)
    {
        if (textSizes != null && index >= 0 && index < textSizes.Length)
            return textSizes[index];

        return defaultTextSize;
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
        {
            float[] newGridWidths = new float[4] { 46f, 46f, 46f, 20f };

            if (gridWidths != null)
            {
                for (int i = 0; i < Mathf.Min(gridWidths.Length, newGridWidths.Length); i++)
                    newGridWidths[i] = gridWidths[i];
            }

            gridWidths = newGridWidths;
        }

        if (textOffsets == null || textOffsets.Length != 4)
        {
            Vector2[] newOffsets = new Vector2[4]
            {
                new(18f, 6f),
                new(18f, 6f),
                new(18f, 6f),
                new(2f, 6f)
            };

            if (textOffsets != null)
            {
                for (int i = 0; i < Mathf.Min(textOffsets.Length, newOffsets.Length); i++)
                    newOffsets[i] = textOffsets[i];
            }

            textOffsets = newOffsets;
        }

        if (textSizes == null || textSizes.Length != 4)
        {
            Vector2[] newSizes = new Vector2[4]
            {
                new(24f, 7f),
                new(24f, 7f),
                new(24f, 7f),
                new(16f, 7f)
            };

            if (textSizes != null)
            {
                for (int i = 0; i < Mathf.Min(textSizes.Length, newSizes.Length); i++)
                    newSizes[i] = textSizes[i];
            }

            textSizes = newSizes;
        }

        if (playerOffsetAdjustments == null || playerOffsetAdjustments.Length != 4)
        {
            Vector2[] newAdjustments = new Vector2[4];

            if (playerOffsetAdjustments != null)
            {
                for (int i = 0; i < Mathf.Min(playerOffsetAdjustments.Length, newAdjustments.Length); i++)
                    newAdjustments[i] = playerOffsetAdjustments[i];
            }

            playerOffsetAdjustments = newAdjustments;
        }

        if (pushStartTexts == null || pushStartTexts.Length != 4)
        {
            TextMeshProUGUI[] newTexts = new TextMeshProUGUI[4];

            if (pushStartTexts != null)
            {
                for (int i = 0; i < Mathf.Min(pushStartTexts.Length, newTexts.Length); i++)
                    newTexts[i] = pushStartTexts[i];
            }

            pushStartTexts = newTexts;
        }
    }
}