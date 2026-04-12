using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class HudStatIconsInGridLayout : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerStatIconRefs
    {
        public Image bombAmountIcon;
        public Image bombTypeIcon;
        public Image firePowerIcon;
        public Image speedIcon;

        public Image bombAmountNumber;
        public Image firePowerNumber;
        public Image speedNumber;
    }

    [Header("Icon References Per Player")]
    [SerializeField]
    private PlayerStatIconRefs[] playerIcons = new PlayerStatIconRefs[4]
    {
        new PlayerStatIconRefs(),
        new PlayerStatIconRefs(),
        new PlayerStatIconRefs(),
        new PlayerStatIconRefs()
    };

    [Header("Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Icon Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountIconSize = new(7f, 7f);
    [SerializeField] private Vector2 bombTypeIconSize = new(7f, 7f);
    [SerializeField] private Vector2 firePowerIconSize = new(7f, 7f);
    [SerializeField] private Vector2 speedIconSize = new(7f, 7f);

    [Header("Number Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountNumberSize = new(7f, 7f);
    [SerializeField] private Vector2 firePowerNumberSize = new(7f, 7f);
    [SerializeField] private Vector2 speedNumberSize = new(7f, 7f);

    [Header("2x2 Matrix Icon Offsets Inside Grid (SNES pixels)")]
    [Tooltip("Matrix cell 0,0")]
    [SerializeField] private Vector2 bombAmountIconOffset = new(20f, 10f);

    [Tooltip("Matrix cell 0,1")]
    [SerializeField] private Vector2 bombTypeIconOffset = new(28f, 10f);

    [Tooltip("Matrix cell 1,0")]
    [SerializeField] private Vector2 firePowerIconOffset = new(20f, 2f);

    [Tooltip("Matrix cell 1,1")]
    [SerializeField] private Vector2 speedIconOffset = new(28f, 2f);

    [Header("Number Offsets Inside Grid (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountNumberOffset = new(20f, 10f);
    [SerializeField] private Vector2 firePowerNumberOffset = new(20f, 2f);
    [SerializeField] private Vector2 speedNumberOffset = new(28f, 2f);

    [Header("Optional Per Player Global Offset")]
    [SerializeField]
    private Vector2[] playerOffsetAdjustments = new Vector2[4]
    {
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
        Vector2.zero
    };

    [Header("Static Sprites")]
    [SerializeField] private Sprite bombAmountSprite;
    [SerializeField] private Sprite firePowerSprite;
    [SerializeField] private Sprite speedSprite;

    [Header("Bomb Type Sprites")]
    [SerializeField] private Sprite normalBombSprite;
    [SerializeField] private Sprite pierceBombSprite;
    [SerializeField] private Sprite controlBombSprite;
    [SerializeField] private Sprite powerBombSprite;
    [SerializeField] private Sprite rubberBombSprite;

    [Header("Number Sprites 0-9")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    void LateUpdate()
    {
        UpdateSprites();
        UpdateLayout();
    }

    void UpdateSprites()
    {
        for (int i = 0; i < playerIcons.Length; i++)
        {
            int playerId = i + 1;
            var state = PlayerPersistentStats.GetRuntime(playerId);
            var refs = GetPlayerRefs(i);

            if (refs == null || state == null)
                continue;

            SetIconSprite(refs.bombAmountIcon, bombAmountSprite);
            SetIconSprite(refs.firePowerIcon, firePowerSprite);
            SetIconSprite(refs.speedIcon, speedSprite);
            SetIconSprite(refs.bombTypeIcon, GetCurrentBombTypeSprite(state));

            SetNumberSprite(refs.bombAmountNumber, state.BombAmount);
            SetNumberSprite(refs.firePowerNumber, state.ExplosionRadius);
            SetNumberSprite(refs.speedNumber, GetSpeedStepCount(state.SpeedInternal));
        }
    }

    void UpdateLayout()
    {
        for (int i = 0; i < playerIcons.Length; i++)
        {
            var refs = GetPlayerRefs(i);
            if (refs == null)
                continue;

            float logicalGridWidth = GetGridWidth(i);
            if (logicalGridWidth <= 0f || gridHeight <= 0f)
                continue;

            Vector2 playerAdjust = GetPlayerAdjustment(i);

            ApplyElementLayout(refs.bombAmountIcon, logicalGridWidth, gridHeight, bombAmountIconOffset + playerAdjust, bombAmountIconSize);
            ApplyElementLayout(refs.bombTypeIcon, logicalGridWidth, gridHeight, bombTypeIconOffset + playerAdjust, bombTypeIconSize);
            ApplyElementLayout(refs.firePowerIcon, logicalGridWidth, gridHeight, firePowerIconOffset + playerAdjust, firePowerIconSize);
            ApplyElementLayout(refs.speedIcon, logicalGridWidth, gridHeight, speedIconOffset + playerAdjust, speedIconSize);

            ApplyElementLayout(refs.bombAmountNumber, logicalGridWidth, gridHeight, bombAmountNumberOffset + playerAdjust, bombAmountNumberSize);
            ApplyElementLayout(refs.firePowerNumber, logicalGridWidth, gridHeight, firePowerNumberOffset + playerAdjust, firePowerNumberSize);
            ApplyElementLayout(refs.speedNumber, logicalGridWidth, gridHeight, speedNumberOffset + playerAdjust, speedNumberSize);
        }
    }

    void ApplyElementLayout(Image image, float logicalGridWidth, float logicalGridHeight, Vector2 offset, Vector2 size)
    {
        if (image == null)
            return;

        RectTransform rect = image.rectTransform;

        float left = offset.x;
        float bottom = offset.y;
        float right = left + size.x;
        float top = bottom + size.y;

        float minX = left / logicalGridWidth;
        float maxX = right / logicalGridWidth;
        float minY = bottom / logicalGridHeight;
        float maxY = top / logicalGridHeight;

        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    void SetIconSprite(Image iconImage, Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.preserveAspect = false;
    }

    void SetNumberSprite(Image numberImage, int value)
    {
        if (numberImage == null)
            return;

        int clamped = Mathf.Clamp(value, 0, 9);
        Sprite sprite = GetDigitSprite(clamped);

        numberImage.sprite = sprite;
        numberImage.enabled = sprite != null;
        numberImage.preserveAspect = false;
    }

    Sprite GetDigitSprite(int digit)
    {
        if (digitSprites == null || digit < 0 || digit >= digitSprites.Length)
            return null;

        return digitSprites[digit];
    }

    Sprite GetCurrentBombTypeSprite(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return normalBombSprite;

        if (state.HasControlBombs)
            return controlBombSprite;

        if (state.HasPierceBombs)
            return pierceBombSprite;

        if (state.HasPowerBomb)
            return powerBombSprite;

        if (state.HasRubberBombs)
            return rubberBombSprite;

        return normalBombSprite;
    }

    int GetSpeedStepCount(int speedInternal)
    {
        int diff = speedInternal - PlayerPersistentStats.BaseSpeedNormal;
        if (diff <= 0)
            return 0;

        return Mathf.Clamp(diff / PlayerPersistentStats.SpeedStep, 0, 9);
    }

    PlayerStatIconRefs GetPlayerRefs(int index)
    {
        if (playerIcons == null || index < 0 || index >= playerIcons.Length)
            return null;

        return playerIcons[index];
    }

    float GetGridWidth(int index)
    {
        if (gridWidths != null && index >= 0 && index < gridWidths.Length && gridWidths[index] > 0f)
            return gridWidths[index];

        return 46f;
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
        EnsureDigitArraySize();
    }
#endif

    void Reset()
    {
        EnsureArraySizes();
        EnsureDigitArraySize();
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

        if (playerIcons == null || playerIcons.Length != 4)
        {
            PlayerStatIconRefs[] newRefs = new PlayerStatIconRefs[4]
            {
                new(),
                new(),
                new(),
                new()
            };

            if (playerIcons != null)
            {
                for (int i = 0; i < Mathf.Min(playerIcons.Length, newRefs.Length); i++)
                    newRefs[i] = playerIcons[i] ?? new PlayerStatIconRefs();
            }

            playerIcons = newRefs;
        }
    }

    void EnsureDigitArraySize()
    {
        if (digitSprites == null || digitSprites.Length != 10)
        {
            Sprite[] newDigits = new Sprite[10];

            if (digitSprites != null)
            {
                for (int i = 0; i < Mathf.Min(digitSprites.Length, newDigits.Length); i++)
                    newDigits[i] = digitSprites[i];
            }

            digitSprites = newDigits;
        }
    }
}