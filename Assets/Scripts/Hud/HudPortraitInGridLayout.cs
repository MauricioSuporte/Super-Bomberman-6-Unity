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

    const string LivePortraitsResourcesPath = "HUD/PortraitBombersLive";
    const string DeadPortraitsResourcesPath = "HUD/PortraitBombersDead";

    [Header("Portrait Logical Size (SNES pixels)")]
    [SerializeField] private float portraitWidth = 32f;
    [SerializeField] private float portraitHeight = 32f;

    [Header("Parent Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Portrait Offset Inside Each Grid (SNES pixels)")]
    [SerializeField]
    private Vector2[] portraitOffsets = new Vector2[4]
    {
        new(2f, 2f),
        new(2f, 2f),
        new(2f, 2f),
        new(2f, 2f)
    };

    [Header("Optional Per Portrait Size Override")]
    [SerializeField] private bool useIndividualSizes = false;

    [SerializeField]
    private Vector2[] portraitSizes = new Vector2[4]
    {
        new(32f, 32f),
        new(32f, 32f),
        new(32f, 32f),
        new(32f, 32f)
    };

    readonly Dictionary<int, Sprite> portraitByIndex = new();
    readonly Dictionary<int, Sprite> livePortraitsByIndex = new();
    readonly Dictionary<int, Sprite> deadPortraitsByIndex = new();
    private bool[] playerDead = new bool[4];
    private bool[] playerCornered = new bool[4];
    private bool[] playerInactive = new bool[4];
    private bool[] playerTimeUp = new bool[4];
    private bool[] playerVictory = new bool[4];
    private bool[] portraitTintActive = new bool[4];
    private bool[] portraitOriginalColorCaptured = new bool[4];
    private Color[] portraitTintColors = new Color[4];
    private Color[] portraitOriginalColors = new Color[4];
    readonly bool[] portraitCacheValid = new bool[4];
    readonly BomberCharacter[] portraitCacheCharacters = new BomberCharacter[4];
    readonly BomberSkin[] portraitCacheSkins = new BomberSkin[4];
    readonly int[] portraitCacheExpressions = new int[4];
    readonly int[] portraitCacheLegacyIndices = new int[4];
    readonly Sprite[] portraitCacheSprites = new Sprite[4];
    readonly bool[] portraitCacheEnabled = new bool[4];
    bool loaded;
    bool runtimeLayoutApplied;

    void OnEnable()
    {
        runtimeLayoutApplied = false;
    }

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.HudPortraitLateUpdate.Auto();

        EnsureSpritesLoaded();
        UpdatePortraitSprites();
        if (!Application.isPlaying || !runtimeLayoutApplied)
        {
            UpdatePortraitLayout();
            if (Application.isPlaying)
                runtimeLayoutApplied = true;
        }
    }

    void EnsureSpritesLoaded()
    {
        if (loaded)
            return;

        portraitByIndex.Clear();

        LoadPortraitDictionary(portraitsResourcesPath, portraitByIndex);
        LoadPortraitDictionary(LivePortraitsResourcesPath, livePortraitsByIndex);
        LoadPortraitDictionary(DeadPortraitsResourcesPath, deadPortraitsByIndex);

        loaded = true;
    }

    static void LoadPortraitDictionary(string resourcePath, Dictionary<int, Sprite> dictionary)
    {
        if (dictionary == null)
            return;

        dictionary.Clear();

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
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

            if (int.TryParse(spriteName.Substring(underscoreIndex + 1), out int index) &&
                !dictionary.ContainsKey(index))
            {
                dictionary.Add(index, sprite);
            }
        }
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

            if (Application.isPlaying &&
                GameSession.Instance != null &&
                GameSession.Instance.IsHardcorePlayerEliminated(playerId))
            {
                playerDead[i] = true;
            }

            int expressionIndex = GetExpressionIndex(i);

            TrySetPortrait(portraitImage, i, playerId, portraitIndex, expressionIndex);
            ApplyPortraitTint(i);
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

            SetRect(portraitRect, new Vector2(minX, minY), new Vector2(maxX, maxY));
        }
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect.anchorMin != anchorMin)
            rect.anchorMin = anchorMin;
        if (rect.anchorMax != anchorMax)
            rect.anchorMax = anchorMax;
        if (rect.offsetMin != Vector2.zero)
            rect.offsetMin = Vector2.zero;
        if (rect.offsetMax != Vector2.zero)
            rect.offsetMax = Vector2.zero;
        if (rect.localScale != Vector3.one)
            rect.localScale = Vector3.one;
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
        return Mathf.Max(0, BomberSkinResourceCatalog.GetPaletteNumber(skin) - 1);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        loaded = false;
        InvalidatePortraitCache();
        runtimeLayoutApplied = false;
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

    int GetExpressionIndex(int index)
    {
        if (playerDead[index])
            return HudCharacterPortraitCatalog.DeadExpression;
        if (playerVictory[index])
            return HudCharacterPortraitCatalog.VictoryExpression;
        if (playerTimeUp[index])
            return HudCharacterPortraitCatalog.TimeUpExpression;
        if (playerCornered[index])
            return HudCharacterPortraitCatalog.CorneredExpression;
        if (playerInactive[index])
            return HudCharacterPortraitCatalog.InactivityExpression;

        return HudCharacterPortraitCatalog.DefaultExpression;
    }

    void TrySetPortrait(Image portraitImage, int slotIndex, int playerId, int legacyIndex, int expressionIndex)
    {
        PlayerPersistentStats.PlayerState stats = PlayerPersistentStats.Get(playerId);
        if (slotIndex >= 0 &&
            slotIndex < portraitCacheValid.Length &&
            portraitCacheValid[slotIndex] &&
            portraitCacheCharacters[slotIndex] == stats.Character &&
            portraitCacheSkins[slotIndex] == stats.Skin &&
            portraitCacheExpressions[slotIndex] == expressionIndex &&
            portraitCacheLegacyIndices[slotIndex] == legacyIndex)
        {
            ApplyCachedPortrait(portraitImage, portraitCacheSprites[slotIndex], portraitCacheEnabled[slotIndex]);
            return;
        }

        Sprite sprite = HudCharacterPortraitCatalog.Load(stats.Character, stats.Skin, expressionIndex);
        bool enabled = sprite != null;

        if (sprite == null)
        {
            Dictionary<int, Sprite> legacyPortraits = expressionIndex == HudCharacterPortraitCatalog.DeadExpression
                ? deadPortraitsByIndex
                : livePortraitsByIndex;

            enabled = legacyPortraits.TryGetValue(legacyIndex, out sprite) && sprite != null;
        }

        if (slotIndex >= 0 && slotIndex < portraitCacheValid.Length)
        {
            portraitCacheValid[slotIndex] = true;
            portraitCacheCharacters[slotIndex] = stats.Character;
            portraitCacheSkins[slotIndex] = stats.Skin;
            portraitCacheExpressions[slotIndex] = expressionIndex;
            portraitCacheLegacyIndices[slotIndex] = legacyIndex;
            portraitCacheSprites[slotIndex] = sprite;
            portraitCacheEnabled[slotIndex] = enabled;
        }

        ApplyCachedPortrait(portraitImage, sprite, enabled);
    }

    static void ApplyCachedPortrait(Image portraitImage, Sprite sprite, bool enabled)
    {
        if (portraitImage.sprite != sprite)
            portraitImage.sprite = sprite;
        if (portraitImage.enabled != enabled)
            portraitImage.enabled = enabled;
        if (portraitImage.preserveAspect)
            portraitImage.preserveAspect = false;
    }

    void InvalidatePortraitCache()
    {
        for (int i = 0; i < portraitCacheValid.Length; i++)
        {
            portraitCacheValid[i] = false;
            portraitCacheSprites[i] = null;
            portraitCacheEnabled[i] = false;
        }
    }

    public void OnPlayerDied(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length)
            return;

        playerDead[index] = true;
        portraitCacheValid[index] = false;
    }

    public void OnPlayerRespawn(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length)
            return;

        playerDead[index] = false;
        playerCornered[index] = false;
        playerInactive[index] = false;
        playerTimeUp[index] = false;
        playerVictory[index] = false;
        portraitCacheValid[index] = false;
    }

    public void SetPlayerPortraitState(int playerId, HudPortraitState state, bool active)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length || playerDead[index])
            return;

        switch (state)
        {
            case HudPortraitState.Cornered: playerCornered[index] = active; break;
            case HudPortraitState.Inactive: playerInactive[index] = active; break;
            case HudPortraitState.TimeUp: playerTimeUp[index] = active; break;
            case HudPortraitState.Victory: playerVictory[index] = active; break;
        }

        portraitCacheValid[index] = false;
    }

    public void SetPlayerPortraitTint(int playerId, Color color)
    {
        int index = playerId - 1;
        if (index < 0 || index >= portraitImages.Length)
            return;

        Image portraitImage = portraitImages[index];
        if (portraitImage != null && !portraitOriginalColorCaptured[index])
        {
            portraitOriginalColors[index] = portraitImage.color;
            portraitOriginalColorCaptured[index] = true;
        }

        portraitTintColors[index] = color;
        portraitTintActive[index] = true;
        ApplyPortraitTint(index);
    }

    public void ClearPlayerPortraitTint(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= portraitImages.Length)
            return;

        portraitTintActive[index] = false;

        Image portraitImage = portraitImages[index];
        if (portraitImage != null && portraitOriginalColorCaptured[index])
            portraitImage.color = portraitOriginalColors[index];

        portraitOriginalColorCaptured[index] = false;
    }

    void ApplyPortraitTint(int index)
    {
        if (index < 0 || index >= portraitImages.Length)
            return;

        Image portraitImage = portraitImages[index];
        if (portraitImage == null || !portraitTintActive[index])
            return;

        portraitImage.color = portraitTintColors[index];
    }
}
