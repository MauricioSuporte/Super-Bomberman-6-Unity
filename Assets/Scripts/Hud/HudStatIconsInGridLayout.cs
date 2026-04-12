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
        public Image lifePowerupNumber;

        public Image kickOrBombPassPowerup;
        public Image punchPowerup;
        public Image powerGlovePowerup;
        public Image lifePowerup;
        public Image destructiblePassPowerup;
        public Image fullFirePowerup;
    }

    [Header("Icon References Per Player")]
    [SerializeField]
    private PlayerStatIconRefs[] playerIcons = new PlayerStatIconRefs[4]
    {
        new(),
        new(),
        new(),
        new()
    };

    [Header("Grid Logical Size (SNES pixels)")]
    [SerializeField] private float[] gridWidths = new float[4] { 46f, 46f, 46f, 20f };
    [SerializeField] private float gridHeight = 19f;

    [Header("Main 2x2 Icon Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountIconSize = new(7f, 7f);
    [SerializeField] private Vector2 bombTypeIconSize = new(7f, 7f);
    [SerializeField] private Vector2 firePowerIconSize = new(7f, 7f);
    [SerializeField] private Vector2 speedIconSize = new(7f, 7f);

    [Header("Number Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountNumberSize = new(7f, 7f);
    [SerializeField] private Vector2 firePowerNumberSize = new(7f, 7f);
    [SerializeField] private Vector2 speedNumberSize = new(7f, 7f);
    [SerializeField] private Vector2 lifePowerupNumberSize = new(7f, 7f);

    [Header("Main 2x2 Matrix Icon Offsets Inside Grid (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountIconOffset = new(2f, 11f);
    [SerializeField] private Vector2 bombTypeIconOffset = new(10f, 11f);
    [SerializeField] private Vector2 firePowerIconOffset = new(2f, 1f);
    [SerializeField] private Vector2 speedIconOffset = new(10f, 1f);

    [Header("Number Offsets Inside Grid (SNES pixels)")]
    [SerializeField] private Vector2 bombAmountNumberOffset = new(2f, 11f);
    [SerializeField] private Vector2 firePowerNumberOffset = new(2f, 1f);
    [SerializeField] private Vector2 speedNumberOffset = new(10f, 1f);
    [SerializeField] private Vector2 lifePowerupNumberOffset = new(19f, 1f);

    [Header("Powerup 2x3 Icon Logical Size (SNES pixels)")]
    [SerializeField] private Vector2 kickOrBombPassPowerupSize = new(7f, 7f);
    [SerializeField] private Vector2 punchPowerupSize = new(7f, 7f);
    [SerializeField] private Vector2 powerGlovePowerupSize = new(7f, 7f);
    [SerializeField] private Vector2 lifePowerupSize = new(7f, 7f);
    [SerializeField] private Vector2 destructiblePassPowerupSize = new(7f, 7f);
    [SerializeField] private Vector2 fullFirePowerupSize = new(7f, 7f);

    [Header("Powerup 2x3 Offsets Inside Grid (SNES pixels)")]
    [Tooltip("Cell 0,0 = Kick or BombPass")]
    [SerializeField] private Vector2 kickOrBombPassPowerupOffset = new(19f, 11f);

    [Tooltip("Cell 0,1 = Punch")]
    [SerializeField] private Vector2 punchPowerupOffset = new(27f, 11f);

    [Tooltip("Cell 0,2 = PowerGlove")]
    [SerializeField] private Vector2 powerGlovePowerupOffset = new(35f, 11f);

    [Tooltip("Cell 1,0 = Life")]
    [SerializeField] private Vector2 lifePowerupOffset = new(19f, 1f);

    [Tooltip("Cell 1,1 = DestructiblePass")]
    [SerializeField] private Vector2 destructiblePassPowerupOffset = new(27f, 1f);

    [Tooltip("Cell 1,2 = FullFire")]
    [SerializeField] private Vector2 fullFirePowerupOffset = new(35f, 1f);

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

    [Header("Powerup Sprites")]
    [SerializeField] private Sprite kickPowerupSprite;
    [SerializeField] private Sprite bombPassPowerupSprite;
    [SerializeField] private Sprite punchPowerupSprite;
    [SerializeField] private Sprite powerGlovePowerupSprite;
    [SerializeField] private Sprite lifePowerupSprite;
    [SerializeField] private Sprite destructiblePassPowerupSprite;
    [SerializeField] private Sprite fullFirePowerupSprite;

    private CharacterHealth[] playerHealthCache = new CharacterHealth[4];

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

            CharacterHealth health = GetPlayerHealth(playerId);
            int currentLife = health != null ? Mathf.Max(0, health.life) : 0;

            SetIconSprite(refs.bombAmountIcon, bombAmountSprite);
            SetIconSprite(refs.firePowerIcon, firePowerSprite);
            SetIconSprite(refs.speedIcon, speedSprite);
            SetIconSprite(refs.bombTypeIcon, GetCurrentBombTypeSprite(state));

            SetNumberSprite(refs.bombAmountNumber, state.BombAmount);
            SetNumberSprite(refs.firePowerNumber, state.ExplosionRadius);
            SetNumberSprite(refs.speedNumber, GetSpeedStepCount(state.SpeedInternal));

            SetOptionalPowerupSprite(
                refs.kickOrBombPassPowerup,
                GetKickOrBombPassSprite(state));

            SetOptionalPowerupSprite(
                refs.punchPowerup,
                state.CanPunchBombs ? punchPowerupSprite : null);

            SetOptionalPowerupSprite(
                refs.powerGlovePowerup,
                state.HasPowerGlove ? powerGlovePowerupSprite : null);

            SetOptionalPowerupSprite(
                refs.lifePowerup,
                currentLife > 1 ? lifePowerupSprite : null);

            SetConditionalNumberSprite(
                refs.lifePowerupNumber,
                currentLife > 2 ? currentLife - 1 : -1);

            SetOptionalPowerupSprite(
                refs.destructiblePassPowerup,
                state.CanPassDestructibles ? destructiblePassPowerupSprite : null);

            SetOptionalPowerupSprite(
                refs.fullFirePowerup,
                state.HasFullFire ? fullFirePowerupSprite : null);
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

            ApplyElementLayout(refs.bombAmountNumber, logicalGridWidth, gridHeight, bombAmountNumberOffset + playerAdjust, bombAmountNumberSize, true);
            ApplyElementLayout(refs.firePowerNumber, logicalGridWidth, gridHeight, firePowerNumberOffset + playerAdjust, firePowerNumberSize, true);
            ApplyElementLayout(refs.speedNumber, logicalGridWidth, gridHeight, speedNumberOffset + playerAdjust, speedNumberSize, true);
            ApplyElementLayout(refs.lifePowerupNumber, logicalGridWidth, gridHeight, lifePowerupNumberOffset + playerAdjust, lifePowerupNumberSize, true);

            ApplyElementLayout(refs.kickOrBombPassPowerup, logicalGridWidth, gridHeight, kickOrBombPassPowerupOffset + playerAdjust, kickOrBombPassPowerupSize);
            ApplyElementLayout(refs.punchPowerup, logicalGridWidth, gridHeight, punchPowerupOffset + playerAdjust, punchPowerupSize);
            ApplyElementLayout(refs.powerGlovePowerup, logicalGridWidth, gridHeight, powerGlovePowerupOffset + playerAdjust, powerGlovePowerupSize);
            ApplyElementLayout(refs.lifePowerup, logicalGridWidth, gridHeight, lifePowerupOffset + playerAdjust, lifePowerupSize);
            ApplyElementLayout(refs.destructiblePassPowerup, logicalGridWidth, gridHeight, destructiblePassPowerupOffset + playerAdjust, destructiblePassPowerupSize);
            ApplyElementLayout(refs.fullFirePowerup, logicalGridWidth, gridHeight, fullFirePowerupOffset + playerAdjust, fullFirePowerupSize);
        }
    }

    void ApplyElementLayout(Image image, float logicalGridWidth, float logicalGridHeight, Vector2 offset, Vector2 size, bool bringToFront = false)
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

        if (bringToFront)
            rect.SetAsLastSibling();
    }

    void SetIconSprite(Image iconImage, Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.preserveAspect = false;
    }

    void SetOptionalPowerupSprite(Image iconImage, Sprite sprite)
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

    void SetConditionalNumberSprite(Image numberImage, int value)
    {
        if (numberImage == null)
            return;

        if (value < 0)
        {
            numberImage.sprite = null;
            numberImage.enabled = false;
            return;
        }

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

    Sprite GetKickOrBombPassSprite(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return null;

        if (state.CanKickBombs)
            return kickPowerupSprite;

        if (state.CanPassBombs)
            return bombPassPowerupSprite;

        return null;
    }

    int GetSpeedStepCount(int speedInternal)
    {
        int clampedSpeed = PlayerPersistentStats.ClampSpeedInternal(speedInternal);
        int diff = clampedSpeed - PlayerPersistentStats.MinSpeedInternal;
        int steps = diff / PlayerPersistentStats.SpeedStep;

        return Mathf.Clamp(steps + 1, 1, 9);
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

    CharacterHealth GetPlayerHealth(int playerId)
    {
        int index = playerId - 1;

        if (index < 0 || index >= playerHealthCache.Length)
            return null;

        if (playerHealthCache[index] != null)
            return playerHealthCache[index];

        var players = GameObject.FindGameObjectsWithTag("Player");

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            var mv = players[i].GetComponent<MovementController>();
            if (mv != null && mv.PlayerId == playerId)
            {
                var health = players[i].GetComponent<CharacterHealth>();
                playerHealthCache[index] = health;
                return health;
            }
        }

        return null;
    }
}