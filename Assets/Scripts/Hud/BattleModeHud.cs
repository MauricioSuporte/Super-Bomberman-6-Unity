using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class BattleModeHud : MonoBehaviour
{
    const int MaxPlayers = 6;

    const float HudWidth = 256f;
    const float HudHeight = 27f;
    const float ReferenceScreenWidth = 256f;
    const float ReferenceScreenHeight = 224f;

    const float UsableAreaLeft = 5f;
    const float UsableAreaBottom = 3f;
    const float UsableAreaWidth = 247f;
    const float UsableAreaHeight = 22f;
    const float HudSidePadding = (HudWidth - (UsableAreaLeft + UsableAreaWidth));
    const float TeamBackgroundPartitionInset = 1f;
    const float TeamBackgroundFirstLeftBleed = 1f;
    const float TeamBackgroundInnerLeftBleed = 2f;
    const float TeamBackgroundLastLeftBleed = 3f;
    const float TeamBackgroundNonLastRightBleed = 3f;
    const float TeamBackgroundLastRightBleed = 1f;
    const float ThreePlayerTeamBackgroundMiddleExtraLeftBleed = 2f;
    const float ThreePlayerTeamBackgroundLastExtraLeftBleed = 1f;
    const float ThreePlayerTeamBackgroundNonLastExtraRightBleed = 1f;
    const float TeamBackgroundBottomBleed = 3f;
    const float TeamBackgroundTopBleed = 1f;

    const float PartitionWidth = 5f;
    const float PartitionHeight = 23f;
    const float PartitionBottom = 3f;
    const string DefaultBackgroundAssetPath = "Assets/Resources/HUD/BattleMode/DefaultBackground.png";
    const string FrameAssetPath = "Assets/Resources/HUD/BattleMode/Frame.png";
    const string PartitionAssetPath = "Assets/Resources/HUD/BattleMode/Partition.png";
    const string BombBlastSpeedAssetPath = "Assets/Resources/HUD/BattleMode/PrincipalItens 1.png";
    const string DefaultBackgroundResourcesPath = "HUD/BattleMode/DefaultBackground";
    const string FrameResourcesPath = "HUD/BattleMode/Frame";
    const string PartitionResourcesPath = "HUD/BattleMode/Partition";
    const string BombBlastSpeedResourcesPath = "HUD/BattleMode/PrincipalItens 1";
    const string Team1BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team1.png";
    const string Team2BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team2.png";
    const string Team3BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team3.png";
    const string VictoryNumbersAssetPath = "Assets/Sprites/HUD/NormalGame/HudNumbers.png";
    const string TimerBackgroundResourcesPath = "HUD/BattleMode/TimerPanel";
    const string TimerDigitsResourcesPath = "HUD/BattleMode/TimerDigits";

    const float PortraitSize = 16f;
    const float PortraitY = 2f;
    const float OuterHudEdgePadding = 1f;
    const float PortraitToPowerupGap = 2f;
    const float FirstPlayerPortraitExtraOffset = 1f;
    const float SubsequentPlayerPortraitOffsetX = -1f;
    const float NonLastPlayerItemsOffsetX = 1f;
    const float VictoryCounterSize = 7f;
    const float VictoryCounterOffsetX = 9f;
    const float VictoryCounterOffsetY = 0f;
    const float TimerDigitSize = 7f;
    const float TimerColonWidth = 3f;
    const float TimerSpacing = 1f;
    const float TimerBottom = -8f;
    const float TimerPanelWidth = 41f;
    const float TimerPanelHeight = 12f;
    const float TimerPanelBottom = -10f;
    const float TimerPanelInnerPaddingLeft = 4f;
    const float TimerPanelInnerPaddingRight = 4f;
    const float TimerPanelInnerPaddingBottom = 2f;

    const float NumberSize = 6f;
    const float AbilityIconSize = 6f;
    const float LifeIconSize = 6f;

    const float StatsPanelY = 0f;
    const float StatsPanelWidth = 23f;
    const float StatsPanelHeight = 7f;

    const float StatsNumberY = 1f;
    static readonly float[] StatsNumberOffsets = { 2f, 9f, 16f };

    const string RuntimeRootName = "__BattleModeRuntime";
    const string BackgroundName = "Background";
    const string BorderName = "Border";
    const string PartitionsRootName = "Partitions";
    const string PushStartLabel = "PUSH START";
    static readonly int[] displayedVictorySnapshot = new int[MaxPlayers];
    static bool useDisplayedVictorySnapshot;

    static readonly string[] LegacyHudChildNames =
    {
        "HudBackground",
        "Grid1",
        "Grid2",
        "Grid3",
        "Grid4",
        "AltGrid1",
        "AltGrid2",
        "AltGrid3",
        "AltGrid4"
    };

    static readonly float[] PowerupRowYs = { 15f, 8f };

    [Header("Battle HUD Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite partitionSprite;
    [SerializeField] private Sprite bombBlastSpeedSprite;
    [SerializeField] private Sprite team1BackgroundSprite;
    [SerializeField] private Sprite team2BackgroundSprite;
    [SerializeField] private Sprite team3BackgroundSprite;

    [Header("Digits 0-9")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];
    [SerializeField] private Sprite[] victoryDigitSprites = new Sprite[10];

    [Header("Battle Timer")]
    [SerializeField] private Sprite timerBackgroundSprite;
    [SerializeField] private Sprite[] timerDigitSprites = new Sprite[10];
    [SerializeField] private Sprite timerInfinitySprite;
    [SerializeField] private Sprite timerColonSprite;

    [Header("Mini Powerups")]
    [SerializeField] private Sprite kickSprite;
    [SerializeField] private Sprite bombPassSprite;
    [SerializeField] private Sprite punchSprite;
    [SerializeField] private Sprite powerGloveSprite;
    [SerializeField] private Sprite extraLifeSprite;
    [SerializeField] private Sprite destructiblePassSprite;
    [SerializeField] private Sprite fullFireSprite;

    [Header("Special Bomb Types")]
    [SerializeField] private Sprite powerBombSprite;
    [SerializeField] private Sprite rubberBombSprite;
    [SerializeField] private Sprite magnetBombSprite;
    [SerializeField] private Sprite remoteBombSprite;
    [SerializeField] private Sprite pierceBombSprite;

    [Header("Push Start")]
    [SerializeField] private TMP_FontAsset pushStartFont;
    [SerializeField] private int pushStartFontSize = 4;
    [SerializeField] private Color pushStartColor = Color.white;

    [Header("Diagnostics")]
    [SerializeField] private bool logLayoutDiagnostics = true;

    readonly bool[] playerDead = new bool[MaxPlayers];
    readonly CharacterHealth[] playerHealthCache = new CharacterHealth[MaxPlayers];
    readonly List<Sprite> activePowerupBuffer = new List<Sprite>(6);
    readonly List<int> activePlayerIdsBuffer = new List<int>(MaxPlayers);
    readonly SlotUi[] slots = new SlotUi[MaxPlayers];
    readonly Dictionary<int, Sprite> livePortraits = new Dictionary<int, Sprite>();
    readonly Dictionary<int, Sprite> deadPortraits = new Dictionary<int, Sprite>();
    readonly Image[] partitionImages = new Image[MaxPlayers - 1];
    readonly bool[] portraitTintActive = new bool[MaxPlayers];
    readonly Color[] portraitTintColors = new Color[MaxPlayers];

    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectTransform partitionsRoot;
    RectTransform timerRoot;
    Image backgroundImage;
    Image borderImage;
    Image timerBackgroundImage;
    Image timerMinuteDigitImage;
    Image timerSecondTensDigitImage;
    Image timerSecondOnesDigitImage;
    Image timerColonImage;
    bool portraitsLoaded;
    bool legacyHudSuppressed;
    string lastLayoutDiagnosticsSignature;

    sealed class SlotUi
    {
        public RectTransform Root;
        public Image TeamBackground;
        public Image Portrait;
        public Image PlayerNumber;
        public Image LifeIcon;
        public Image LifeNumber;
        public Image[] AbilityIcons;
        public Image StatsPanel;
        public Image BombNumber;
        public Image FireNumber;
        public Image SpeedNumber;
        public TextMeshProUGUI PushStart;
    }

    void LateUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.BattleHudLateUpdate.Auto();

        if (rootRect == null)
            rootRect = (RectTransform)transform;

        ApplyTopHudRootRect();
        SuppressLegacyHudAtRuntime();
        LoadHudSpritesIfNeeded();
        EnsurePortraitsLoaded();
        if (runtimeRoot == null)
            EnsureRuntimeUi();
        PopulateActivePlayerIds(activePlayerIdsBuffer);

        int visiblePlayerCount = GetVisiblePlayerCount();
        UpdateRootImages(visiblePlayerCount);
        UpdateLayout(visiblePlayerCount);
        UpdateContent(visiblePlayerCount);
        UpdateTimer();
        LogLayoutDiagnosticsIfNeeded(visiblePlayerCount);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        portraitsLoaded = false;

        if (backgroundSprite == null)
            backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultBackgroundAssetPath);

        if (borderSprite == null)
            borderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameAssetPath);

        if (partitionSprite == null)
            partitionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartitionAssetPath);

        if (bombBlastSpeedSprite == null)
            bombBlastSpeedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BombBlastSpeedAssetPath);

        if (team1BackgroundSprite == null)
            team1BackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Team1BackgroundAssetPath);

        if (team2BackgroundSprite == null)
            team2BackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Team2BackgroundAssetPath);

        if (team3BackgroundSprite == null)
            team3BackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Team3BackgroundAssetPath);

        if (victoryDigitSprites == null || victoryDigitSprites.Length != 10 || victoryDigitSprites[0] == null)
            victoryDigitSprites = LoadOrderedSpritesFromSheet(VictoryNumbersAssetPath, "HudNumbers_", 10);

        LoadTimerSpritesIfNeeded();
    }
#endif

    void LoadHudSpritesIfNeeded()
    {
        backgroundSprite = LoadPreferredResourceSprite(backgroundSprite, DefaultBackgroundResourcesPath);
        borderSprite = LoadPreferredResourceSprite(borderSprite, FrameResourcesPath);
        partitionSprite = LoadPreferredResourceSprite(partitionSprite, PartitionResourcesPath);
        bombBlastSpeedSprite = LoadPreferredResourceSprite(bombBlastSpeedSprite, BombBlastSpeedResourcesPath);
    }

    static Sprite LoadPreferredResourceSprite(Sprite current, string resourcePath)
    {
        Sprite loaded = Resources.Load<Sprite>(resourcePath);
        if (loaded != null)
            return loaded;

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath);
        return loadedSprites != null && loadedSprites.Length > 0 && loadedSprites[0] != null
            ? loadedSprites[0]
            : current;
    }

    void ApplyTopHudRootRect()
    {
        if (rootRect == null)
            return;

        rootRect.anchorMin = new Vector2(0f, (ReferenceScreenHeight - HudHeight) / ReferenceScreenHeight);
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.localScale = Vector3.one;
    }

    void OnDisable()
    {
        CleanupRuntimeUi();
    }

    void OnDestroy()
    {
        CleanupRuntimeUi();
    }

    public void OnPlayerDied(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length)
            return;

        playerDead[index] = true;
    }

    public void OnPlayerRespawn(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length)
            return;

        playerDead[index] = false;
    }

    public void SetPlayerPortraitTint(int playerId, Color color)
    {
        int index = playerId - 1;
        if (index < 0 || index >= MaxPlayers)
            return;

        portraitTintActive[index] = true;
        portraitTintColors[index] = color;
    }

    public void ClearPlayerPortraitTint(int playerId)
    {
        int index = playerId - 1;
        if (index < 0 || index >= MaxPlayers)
            return;

        portraitTintActive[index] = false;
        portraitTintColors[index] = Color.white;
    }

    public static void CaptureDisplayedVictorySnapshot()
    {
        useDisplayedVictorySnapshot = true;

        for (int playerId = 1; playerId <= MaxPlayers; playerId++)
        {
            displayedVictorySnapshot[playerId - 1] = GameSession.Instance != null
                ? GameSession.Instance.GetBattleMatchWins(playerId)
                : 0;
        }
    }

    public static void ReleaseDisplayedVictorySnapshot()
    {
        useDisplayedVictorySnapshot = false;
    }

    void EnsurePortraitsLoaded()
    {
        if (portraitsLoaded)
            return;

        portraitsLoaded = true;
        livePortraits.Clear();
        deadPortraits.Clear();

        LoadPortraitDictionary("HUD/PortraitBombersLive", livePortraits);
        LoadPortraitDictionary("HUD/PortraitBombersDead", deadPortraits);
    }

    void LoadPortraitDictionary(string resourcePath, Dictionary<int, Sprite> dictionary)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null)
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

            int parsedIndex;
            if (!int.TryParse(spriteName.Substring(underscoreIndex + 1), out parsedIndex))
                continue;

            if (!dictionary.ContainsKey(parsedIndex))
                dictionary.Add(parsedIndex, sprite);
        }
    }

    void EnsureRuntimeUi()
    {
        runtimeRoot = GetOrCreateRect(rootRect, RuntimeRootName);
        runtimeRoot.SetAsLastSibling();

        backgroundImage = GetOrCreateImage(runtimeRoot, BackgroundName);
        borderImage = GetOrCreateImage(runtimeRoot, BorderName);

        partitionsRoot = GetOrCreateRect(runtimeRoot, PartitionsRootName);
        timerRoot = GetOrCreateRect(runtimeRoot, "Timer");

        for (int i = 0; i < partitionImages.Length; i++)
            partitionImages[i] = GetOrCreateImage(partitionsRoot, "Partition" + (i + 1));

        timerBackgroundImage = GetOrCreateImage(timerRoot, "Background");
        timerMinuteDigitImage = GetOrCreateImage(timerRoot, "MinuteDigit");
        timerSecondTensDigitImage = GetOrCreateImage(timerRoot, "SecondTensDigit");
        timerSecondOnesDigitImage = GetOrCreateImage(timerRoot, "SecondOnesDigit");
        timerColonImage = GetOrCreateImage(timerRoot, "Colon");

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slots[i] == null)
                slots[i] = new SlotUi();

            EnsureSlot(slots[i], i);
        }
    }

    void EnsureSlot(SlotUi slot, int index)
    {
        string slotName = "Slot" + (index + 1);

        slot.Root = GetOrCreateRect(runtimeRoot, slotName);
        slot.Root.SetAsLastSibling();

        slot.TeamBackground = GetOrCreateImage(slot.Root, "TeamBackground");
        slot.Portrait = GetOrCreateImage(slot.Root, "Portrait");
        slot.PlayerNumber = GetOrCreateImage(slot.Root, "PlayerNumber");
        slot.LifeIcon = GetOrCreateImage(slot.Root, "LifeIcon");
        slot.LifeNumber = GetOrCreateImage(slot.Root, "LifeNumber");
        slot.StatsPanel = GetOrCreateImage(slot.Root, "BombBlastSpeed");
        slot.BombNumber = GetOrCreateImage(slot.Root, "BombNumber");
        slot.FireNumber = GetOrCreateImage(slot.Root, "FireNumber");
        slot.SpeedNumber = GetOrCreateImage(slot.Root, "SpeedNumber");
        slot.PushStart = GetOrCreateText(slot.Root, "PushStart");

        const int abilitySlotCount = 5;

        if (slot.AbilityIcons == null || slot.AbilityIcons.Length != abilitySlotCount)
            slot.AbilityIcons = new Image[abilitySlotCount];

        for (int i = 0; i < abilitySlotCount; i++)
            slot.AbilityIcons[i] = GetOrCreateImage(slot.Root, "Ability" + (i + 1));
    }

    void UpdateRootImages(int visiblePlayerCount)
    {
        SetImageSprite(backgroundImage, ShouldShowDefaultBackground() ? backgroundSprite : null, false);
        SetImageSprite(borderImage, borderSprite, false);

        ApplyLogicalRect(runtimeRoot, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect(partitionsRoot, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect(timerRoot, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect(
            (RectTransform)backgroundImage.transform,
            UsableAreaLeft,
            UsableAreaBottom,
            UsableAreaWidth,
            UsableAreaHeight,
            HudWidth,
            HudHeight);
        ApplyLogicalRect((RectTransform)borderImage.transform, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);

        float slotWidth = GetSlotWidth(visiblePlayerCount);

        for (int i = 0; i < partitionImages.Length; i++)
        {
            Image partition = partitionImages[i];
            bool shouldShowPartition = i < visiblePlayerCount - 1;
            SetImageSprite(partition, shouldShowPartition ? partitionSprite : null, true);

            if (!shouldShowPartition)
                continue;

            float slotRight = UsableAreaLeft + ((i + 1) * slotWidth);
            float partitionLeft = slotRight - (PartitionWidth * 0.5f);

            ApplyLogicalRect(
                partition.rectTransform,
                partitionLeft,
                PartitionBottom,
                PartitionWidth,
                PartitionHeight,
                HudWidth,
                HudHeight);
        }

        UpdateVisualLayerOrder();
    }

    void UpdateLayout(int visiblePlayerCount)
    {
        float slotWidth = GetSlotWidth(visiblePlayerCount);

        for (int i = 0; i < MaxPlayers; i++)
        {
            SlotUi slot = slots[i];
            if (slot == null || slot.Root == null)
                continue;

            float slotLeft = UsableAreaLeft + i * slotWidth;
            float statsPanelLeft = GetStatsPanelLeft(i, visiblePlayerCount, slotWidth);
            float itemsOffsetX = GetItemsOffsetX(i, visiblePlayerCount);
            float statsPanelDisplayLeft = statsPanelLeft + itemsOffsetX;
            float column0X = statsPanelLeft + StatsNumberOffsets[0] + itemsOffsetX;
            float column1X = statsPanelLeft + StatsNumberOffsets[1] + itemsOffsetX;
            float column2X = statsPanelLeft + StatsNumberOffsets[2] + itemsOffsetX;
            float portraitLeft = GetPortraitLeft(i, visiblePlayerCount, slotWidth, column0X);
            float teamBackgroundLeft = GetTeamBackgroundVisibleLeft(i, visiblePlayerCount);
            float teamBackgroundRight = GetTeamBackgroundVisibleRight(i, visiblePlayerCount, slotWidth);

            ApplyLogicalRect(slot.Root, slotLeft, UsableAreaBottom, slotWidth, UsableAreaHeight, HudWidth, HudHeight);
            ApplyLogicalRect(
                slot.TeamBackground.rectTransform,
                teamBackgroundLeft,
                -TeamBackgroundBottomBleed,
                teamBackgroundRight - teamBackgroundLeft,
                UsableAreaHeight + TeamBackgroundBottomBleed + TeamBackgroundTopBleed,
                slotWidth,
                UsableAreaHeight);
            ApplyLogicalRect(slot.Portrait.rectTransform, portraitLeft, PortraitY, PortraitSize, PortraitSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(
                slot.PlayerNumber.rectTransform,
                portraitLeft + VictoryCounterOffsetX,
                PortraitY + VictoryCounterOffsetY,
                VictoryCounterSize,
                VictoryCounterSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(slot.AbilityIcons[0].rectTransform, column0X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[1].rectTransform, column1X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[2].rectTransform, column2X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[3].rectTransform, column0X, PowerupRowYs[1], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[4].rectTransform, column1X, PowerupRowYs[1], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(slot.LifeIcon.rectTransform, column2X, PowerupRowYs[1], LifeIconSize, LifeIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.LifeNumber.rectTransform, column2X, PowerupRowYs[1], NumberSize, NumberSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(
                slot.StatsPanel.rectTransform,
                statsPanelDisplayLeft,
                StatsPanelY,
                StatsPanelWidth,
                StatsPanelHeight,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.BombNumber.rectTransform,
                column0X,
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.FireNumber.rectTransform,
                column1X,
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.SpeedNumber.rectTransform,
                column2X,
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(slot.PushStart.rectTransform, 17f, 7f, 21f, 9f, slotWidth, UsableAreaHeight);
        }
    }

    void UpdateContent(int visiblePlayerCount)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            SlotUi slot = slots[i];
            if (slot == null)
                continue;

            if (i >= visiblePlayerCount)
            {
                HideSlot(slot);
                continue;
            }

            int playerId = GetVisiblePlayerIdAtVisualIndex(i);
            SetSlotActive(slot, true);
            ShowActiveSlot(slot, playerId);
        }
    }

    void UpdateTimer()
    {
        LoadTimerSpritesIfNeeded();

        bool shouldShowTimer = ShouldShowBattleTimerDisplay();
        SetTimerActive(shouldShowTimer);

        if (!shouldShowTimer)
            return;

        bool showFiniteTimer = ShouldShowRoundTimer();
        float timerPanelLeft = (HudWidth - TimerPanelWidth) * 0.5f;
        float digitsAreaWidth = TimerPanelWidth - TimerPanelInnerPaddingLeft - TimerPanelInnerPaddingRight;
        float timerContentLeft = timerPanelLeft + TimerPanelInnerPaddingLeft;
        float timerContentBottom = TimerPanelBottom + TimerPanelInnerPaddingBottom;

        SetImageSprite(timerBackgroundImage, timerBackgroundSprite, false);
        ApplyLogicalRect(timerBackgroundImage.rectTransform, timerPanelLeft, TimerPanelBottom, TimerPanelWidth, TimerPanelHeight, HudWidth, HudHeight);

        if (!showFiniteTimer)
        {
            SetImageSprite(timerMinuteDigitImage, timerInfinitySprite, false);
            SetImageSprite(timerSecondTensDigitImage, null, false);
            SetImageSprite(timerSecondOnesDigitImage, null, false);
            SetImageSprite(timerColonImage, null, false);

            float infinityWidth = GetSpriteLogicalWidth(timerInfinitySprite, TimerDigitSize, digitsAreaWidth);

            ApplyLogicalRect(
                timerMinuteDigitImage.rectTransform,
                timerContentLeft + ((digitsAreaWidth - infinityWidth) * 0.5f),
                timerContentBottom,
                infinityWidth,
                TimerDigitSize,
                HudWidth,
                HudHeight);

            timerMinuteDigitImage.color = Color.white;
            return;
        }

        int totalSeconds = GetDisplayedBattleTimerSeconds();
        int minutes = Mathf.Clamp(totalSeconds / 60, 0, 9);
        int seconds = Mathf.Clamp(totalSeconds % 60, 0, 59);
        int secondsTens = seconds / 10;
        int secondsOnes = seconds % 10;

        SetImageSprite(timerMinuteDigitImage, GetTimerDigitSprite(minutes), false);
        SetImageSprite(timerSecondTensDigitImage, GetTimerDigitSprite(secondsTens), false);
        SetImageSprite(timerSecondOnesDigitImage, GetTimerDigitSprite(secondsOnes), false);
        SetImageSprite(timerColonImage, timerColonSprite, false);

        float minuteWidth = GetSpriteLogicalWidth(timerMinuteDigitImage.sprite, TimerDigitSize, TimerDigitSize);
        float colonWidth = GetSpriteLogicalWidth(timerColonImage.sprite, TimerDigitSize, TimerColonWidth);
        float secondTensWidth = GetSpriteLogicalWidth(timerSecondTensDigitImage.sprite, TimerDigitSize, TimerDigitSize);
        float secondOnesWidth = GetSpriteLogicalWidth(timerSecondOnesDigitImage.sprite, TimerDigitSize, TimerDigitSize);
        float timerWidth = minuteWidth + colonWidth + secondTensWidth + secondOnesWidth + (TimerSpacing * 3f);
        float timerLeft = timerPanelLeft + ((TimerPanelWidth - timerWidth) * 0.5f);

        ApplyLogicalRect(timerMinuteDigitImage.rectTransform, timerLeft, TimerBottom, minuteWidth, TimerDigitSize, HudWidth, HudHeight);
        ApplyLogicalRect(
            timerColonImage.rectTransform,
            timerLeft + minuteWidth + TimerSpacing,
            TimerBottom,
            colonWidth,
            TimerDigitSize,
            HudWidth,
            HudHeight);
        ApplyLogicalRect(
            timerSecondTensDigitImage.rectTransform,
            timerLeft + minuteWidth + colonWidth + (TimerSpacing * 2f),
            TimerBottom,
            secondTensWidth,
            TimerDigitSize,
            HudWidth,
            HudHeight);
        ApplyLogicalRect(
            timerSecondOnesDigitImage.rectTransform,
            timerLeft + minuteWidth + colonWidth + secondTensWidth + (TimerSpacing * 3f),
            TimerBottom,
            secondOnesWidth,
            TimerDigitSize,
            HudWidth,
            HudHeight);

        timerMinuteDigitImage.color = Color.white;
        timerSecondTensDigitImage.color = Color.white;
        timerSecondOnesDigitImage.color = Color.white;
        timerColonImage.color = GetBlinkingTimerColonColor(totalSeconds);
    }

    void HideSlot(SlotUi slot)
    {
        if (slot == null)
            return;

        SetSlotActive(slot, false);
    }

    void ShowActiveSlot(SlotUi slot, int playerId)
    {
        PlayerPersistentStats.PlayerState state = PlayerPersistentStats.GetRuntime(playerId);
        CharacterHealth health = GetPlayerHealth(playerId);
        bool isDead = IsPlayerDead(playerId, health);
        int currentLife = health != null ? Mathf.Max(0, health.life) : Mathf.Max(0, state.Life);

        SetImageSprite(slot.TeamBackground, GetTeamBackgroundSprite(playerId), false);
        SetImageSprite(slot.PlayerNumber, GetVictoryDigitSprite(GetDisplayedVictoryCount(playerId)), false);
        SetImageSprite(slot.Portrait, GetPortraitSprite(playerId, isDead), false);

        SetImageSprite(slot.StatsPanel, isDead ? null : bombBlastSpeedSprite, false);
        SetImageSprite(slot.BombNumber, isDead ? null : GetDigitSprite(state.BombAmount), false);
        SetImageSprite(slot.FireNumber, isDead ? null : state.HasFullFire ? fullFireSprite : GetDigitSprite(state.ExplosionRadius), false);
        SetImageSprite(slot.SpeedNumber, isDead ? null : GetDigitSprite(GetSpeedStepCount(state.SpeedInternal)), false);

        Color overlayColor = isDead
            ? new Color(1f, 1f, 1f, 0.45f)
            : Color.white;
        Color victoryCounterColor = isDead
            ? new Color(1f, 0.55f, 0.55f, 1f)
            : Color.white;

        slot.Portrait.color = GetPortraitColor(playerId);
        slot.TeamBackground.color = Color.white;
        slot.PlayerNumber.color = victoryCounterColor;
        slot.StatsPanel.color = Color.white;
        slot.BombNumber.color = Color.white;
        slot.FireNumber.color = Color.white;
        slot.SpeedNumber.color = Color.white;

        PopulateActivePowerups(state);

        for (int i = 0; i < slot.AbilityIcons.Length; i++)
        {
            Sprite sprite = i < activePowerupBuffer.Count ? activePowerupBuffer[i] : null;
            SetImageSprite(slot.AbilityIcons[i], sprite, false);
            slot.AbilityIcons[i].color = overlayColor;
        }

        UpdateLifeDisplay(slot, currentLife, overlayColor);
        ConfigurePushStart(slot.PushStart, false);
    }

    Color GetPortraitColor(int playerId)
    {
        int index = playerId - 1;
        if (index >= 0 && index < portraitTintActive.Length && portraitTintActive[index])
            return portraitTintColors[index];

        return Color.white;
    }

    float GetSlotContentLeft(int visualIndex)
    {
        return visualIndex <= 0
            ? OuterHudEdgePadding
            : PartitionWidth * 0.5f;
    }

    float GetSlotContentRight(int visualIndex, int visiblePlayerCount, float slotWidth)
    {
        return visualIndex >= visiblePlayerCount - 1
            ? slotWidth - OuterHudEdgePadding
            : slotWidth - (PartitionWidth * 0.5f);
    }

    float GetStatsPanelLeft(int visualIndex, int visiblePlayerCount, float slotWidth)
    {
        return GetSlotContentRight(visualIndex, visiblePlayerCount, slotWidth) - StatsPanelWidth;
    }

    float GetItemsOffsetX(int visualIndex, int visiblePlayerCount)
    {
        return visualIndex < visiblePlayerCount - 1 ? NonLastPlayerItemsOffsetX : 0f;
    }

    float GetPortraitLeft(int visualIndex, int visiblePlayerCount, float slotWidth, float firstPowerupColumnX)
    {
        float contentLeft = GetSlotContentLeft(visualIndex);
        float portraitAreaRight = Mathf.Max(contentLeft + PortraitSize, firstPowerupColumnX - PortraitToPowerupGap);
        float portraitAreaWidth = portraitAreaRight - contentLeft;
        float portraitLeft = contentLeft + Mathf.Max(0f, (portraitAreaWidth - PortraitSize) * 0.5f);

        if (visualIndex <= 0)
            portraitLeft += FirstPlayerPortraitExtraOffset;
        else
            portraitLeft += SubsequentPlayerPortraitOffsetX;

        return portraitLeft;
    }

    float GetTeamBackgroundVisibleLeft(int visualIndex, int visiblePlayerCount)
    {
        float left = visualIndex <= 0
            ? -UsableAreaLeft
            : 0f;

        return left - GetTeamBackgroundLeftBleed(visualIndex, visiblePlayerCount);
    }

    float GetTeamBackgroundVisibleRight(int visualIndex, int visiblePlayerCount, float slotWidth)
    {
        float right = visualIndex >= visiblePlayerCount - 1
            ? slotWidth + HudSidePadding
            : slotWidth - TeamBackgroundPartitionInset;

        return right + GetTeamBackgroundRightBleed(visualIndex, visiblePlayerCount);
    }

    float GetTeamBackgroundLeftBleed(int visualIndex, int visiblePlayerCount)
    {
        if (visualIndex >= visiblePlayerCount - 1)
        {
            float extraLastBleed = visiblePlayerCount == 3
                ? ThreePlayerTeamBackgroundLastExtraLeftBleed
                : 0f;
            return TeamBackgroundLastLeftBleed + extraLastBleed;
        }

        float bleed = visualIndex <= 0
            ? TeamBackgroundFirstLeftBleed
            : TeamBackgroundInnerLeftBleed;

        if (visiblePlayerCount == 3 && visualIndex > 0)
            bleed += ThreePlayerTeamBackgroundMiddleExtraLeftBleed;

        return bleed;
    }

    float GetTeamBackgroundRightBleed(int visualIndex, int visiblePlayerCount)
    {
        float bleed = visualIndex >= visiblePlayerCount - 1
            ? TeamBackgroundLastRightBleed
            : TeamBackgroundNonLastRightBleed;

        if (visiblePlayerCount == 3 && visualIndex < visiblePlayerCount - 1)
            bleed += ThreePlayerTeamBackgroundNonLastExtraRightBleed;

        return bleed;
    }

    void PopulateActivePlayerIds(List<int> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (!Application.isPlaying)
        {
            for (int playerId = 1; playerId <= MaxPlayers; playerId++)
                results.Add(playerId);
        }
        else
        {
            if (GameSession.Instance != null)
                GameSession.Instance.GetActivePlayerIds(results);

            if (results.Count <= 0)
                results.Add(1);
        }

        if (ShouldGroupPlayersByTeam())
            SortVisiblePlayersByTeam(results);
    }

    int GetVisiblePlayerCount()
    {
        return Mathf.Clamp(activePlayerIdsBuffer.Count, 1, MaxPlayers);
    }

    int GetVisiblePlayerIdAtVisualIndex(int visualIndex)
    {
        if (activePlayerIdsBuffer.Count <= 0)
            return 1;

        int clampedIndex = Mathf.Clamp(visualIndex, 0, activePlayerIdsBuffer.Count - 1);
        return activePlayerIdsBuffer[clampedIndex];
    }

    bool ShouldGroupPlayersByTeam()
    {
        return BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams;
    }

    void SortVisiblePlayersByTeam(List<int> playerIds)
    {
        if (playerIds == null || playerIds.Count <= 1)
            return;

        for (int i = 1; i < playerIds.Count; i++)
        {
            int currentPlayerId = playerIds[i];
            int insertIndex = i - 1;

            while (insertIndex >= 0 && ComparePlayersForHud(currentPlayerId, playerIds[insertIndex]) < 0)
            {
                playerIds[insertIndex + 1] = playerIds[insertIndex];
                insertIndex--;
            }

            playerIds[insertIndex + 1] = currentPlayerId;
        }
    }

    int ComparePlayersForHud(int leftPlayerId, int rightPlayerId)
    {
        int leftTeam = GetHudTeamSortKey(leftPlayerId);
        int rightTeam = GetHudTeamSortKey(rightPlayerId);

        if (leftTeam != rightTeam)
            return leftTeam.CompareTo(rightTeam);

        return leftPlayerId.CompareTo(rightPlayerId);
    }

    int GetHudTeamSortKey(int playerId)
    {
        if (BattleModeRules.Instance == null)
            return (int)BattleModeRules.TeamId.Blue;

        return (int)BattleModeRules.Instance.GetTeamForPlayer(playerId);
    }

    void UpdateLifeDisplay(SlotUi slot, int currentLife, Color overlayColor)
    {
        bool showLifeIcon = currentLife > 1;
        SetImageSprite(slot.LifeIcon, showLifeIcon ? extraLifeSprite : null, false);
        slot.LifeIcon.color = overlayColor;

        if (currentLife > 2)
        {
            SetImageSprite(slot.LifeNumber, GetDigitSprite(currentLife - 1), false);
            slot.LifeNumber.color = Color.white;
            return;
        }

        SetImageSprite(slot.LifeNumber, null, false);
    }

    void PopulateActivePowerups(PlayerPersistentStats.PlayerState state)
    {
        activePowerupBuffer.Clear();
        for (int i = 0; i < 5; i++)
            activePowerupBuffer.Add(null);

        activePowerupBuffer[0] = GetKickOrBombPassSprite(state);
        activePowerupBuffer[1] = state != null && state.CanPunchBombs ? punchSprite : null;
        activePowerupBuffer[2] = state != null && state.HasPowerGlove ? powerGloveSprite : null;
        activePowerupBuffer[3] = GetCurrentBombTypeSprite(state);
        activePowerupBuffer[4] = state != null && state.CanPassDestructibles ? destructiblePassSprite : null;
    }

    Sprite GetKickOrBombPassSprite(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return null;

        if (state.CanKickBombs)
            return kickSprite;

        if (state.CanPassBombs)
            return bombPassSprite;

        return null;
    }

    Sprite GetCurrentBombTypeSprite(PlayerPersistentStats.PlayerState state)
    {
        if (state == null)
            return null;

        if (state.HasControlBombs)
            return remoteBombSprite;

        if (state.HasPierceBombs)
            return pierceBombSprite;

        if (state.HasMagnetBomb)
            return magnetBombSprite;

        if (state.HasPowerBomb)
            return powerBombSprite;

        if (state.HasRubberBombs)
            return rubberBombSprite;

        return null;
    }

    Sprite GetPortraitSprite(int playerId, bool useDeadPortrait)
    {
        BomberSkin skin = PlayerPersistentStats.Get(playerId).Skin;
        int portraitIndex = GetPortraitIndex(skin);

        Dictionary<int, Sprite> source = useDeadPortrait ? deadPortraits : livePortraits;
        Sprite sprite;
        if (source.TryGetValue(portraitIndex, out sprite))
            return sprite;

        return null;
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

    Sprite GetDigitSprite(int value)
    {
        int digit = Mathf.Clamp(value, 0, 9);

        if (digitSprites == null || digitSprites.Length == 0)
            return null;

        // BattleMode/Numbers.png is arranged visually as 1,2,3,4,5,6,7,8,9,0.
        int spriteIndex = digit == 0 ? digitSprites.Length - 1 : digit - 1;
        if (spriteIndex < 0 || spriteIndex >= digitSprites.Length)
            return null;

        return digitSprites[spriteIndex];
    }

    Sprite GetVictoryDigitSprite(int value)
    {
        int digit = Mathf.Clamp(value, 0, 9);

        if (victoryDigitSprites == null || digit < 0 || digit >= victoryDigitSprites.Length)
            return null;

        return victoryDigitSprites[digit];
    }

    Sprite GetTimerDigitSprite(int value)
    {
        int digit = Mathf.Clamp(value, 0, 9);

        if (timerDigitSprites == null || digit < 0 || digit >= timerDigitSprites.Length)
            return null;

        return timerDigitSprites[digit];
    }

    int GetDisplayedVictoryCount(int playerId)
    {
        int index = playerId - 1;
        if (useDisplayedVictorySnapshot && index >= 0 && index < displayedVictorySnapshot.Length)
            return displayedVictorySnapshot[index];

        if (GameSession.Instance == null)
            return 0;

        return GameSession.Instance.GetBattleMatchWins(playerId);
    }

    int GetDisplayedBattleTimerSeconds()
    {
        if (GameManager.Instance == null || !GameManager.Instance.HasBattleTimeLimit)
            return 0;

        return Mathf.CeilToInt(GameManager.Instance.BattleTimeRemainingSeconds);
    }

    int GetSpeedStepCount(int speedInternal)
    {
        int clampedSpeed = PlayerPersistentStats.ClampSpeedInternal(speedInternal);
        int diff = clampedSpeed - PlayerPersistentStats.MinSpeedInternal;
        int steps = diff / PlayerPersistentStats.SpeedStep;
        return Mathf.Clamp(steps + 1, 1, 9);
    }

    bool IsPlayerDead(int playerId, CharacterHealth health)
    {
        int index = playerId - 1;
        if (index < 0 || index >= playerDead.Length)
            return false;

        if (health != null && health.life > 0)
        {
            playerDead[index] = false;
            return false;
        }

        if (health != null && health.life <= 0)
            playerDead[index] = true;

        return playerDead[index];
    }

    CharacterHealth GetPlayerHealth(int playerId)
    {
        if (!Application.isPlaying)
            return null;

        int index = playerId - 1;
        if (index < 0 || index >= playerHealthCache.Length)
            return null;

        CharacterHealth cached = playerHealthCache[index];
        if (cached != null)
            return cached;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            MovementController movement = players[i].GetComponent<MovementController>();
            if (movement == null || movement.PlayerId != playerId)
                continue;

            cached = players[i].GetComponent<CharacterHealth>();
            playerHealthCache[index] = cached;
            return cached;
        }

        return null;
    }

    Sprite GetTeamBackgroundSprite(int playerId)
    {
        if (!ShouldShowTeamBackgrounds())
            return null;

        BattleModeRules.TeamId teamId = BattleModeRules.TeamId.Blue;

        if (BattleModeTeams.Instance != null)
            teamId = BattleModeTeams.Instance.GetTeamForPlayer(playerId);

        switch (teamId)
        {
            case BattleModeRules.TeamId.Red:
                return team2BackgroundSprite;
            case BattleModeRules.TeamId.Green:
                return team3BackgroundSprite;
            default:
                return team1BackgroundSprite;
        }
    }

    bool ShouldShowDefaultBackground()
    {
        return !ShouldShowTeamBackgrounds();
    }

    bool ShouldShowTeamBackgrounds()
    {
        return BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams;
    }

    bool ShouldShowRoundTimer()
    {
        if (!Application.isPlaying)
            return BattleModeRules.Instance != null && BattleModeRules.Instance.UsesRoundTimer;

        return GameManager.Instance != null && GameManager.Instance.HasBattleTimeLimit;
    }

    bool ShouldShowBattleTimerDisplay()
    {
        if (!Application.isPlaying)
            return BattleModeRules.Instance != null;

        return SceneManager.GetActiveScene().name.StartsWith("BattleMode_");
    }

    float GetSlotWidth(int visiblePlayerCount)
    {
        return UsableAreaWidth / Mathf.Clamp(visiblePlayerCount, 1, MaxPlayers);
    }

    void SetSlotActive(SlotUi slot, bool active)
    {
        if (slot?.Root == null)
            return;

        if (slot.Root.gameObject.activeSelf != active)
            slot.Root.gameObject.SetActive(active);
    }

    void SetImageSprite(Image image, Sprite sprite, bool preserveAspect)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
    }

    void ConfigurePushStart(TextMeshProUGUI text, bool visible)
    {
        if (text == null)
            return;

        text.enabled = visible;
        text.text = PushStartLabel;

        if (pushStartFont != null)
            text.font = pushStartFont;

        text.fontSize = pushStartFontSize;
        text.color = pushStartColor;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.extraPadding = true;
        text.richText = true;
    }

    void LoadTimerSpritesIfNeeded()
    {
        if (timerBackgroundSprite != null
            && timerInfinitySprite != null
            && timerColonSprite != null
            && timerDigitSprites != null
            && timerDigitSprites.Length == 10)
        {
            bool hasAllDigits = true;
            for (int i = 0; i < timerDigitSprites.Length; i++)
            {
                if (timerDigitSprites[i] != null)
                    continue;

                hasAllDigits = false;
                break;
            }

            if (hasAllDigits)
                return;
        }

        if (timerBackgroundSprite == null)
            timerBackgroundSprite = Resources.Load<Sprite>(TimerBackgroundResourcesPath);

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(TimerDigitsResourcesPath);
        if (loadedSprites == null || loadedSprites.Length <= 0)
            return;

        if (timerDigitSprites == null || timerDigitSprites.Length != 10)
            timerDigitSprites = new Sprite[10];

        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite == null)
                continue;

            if (TryGetTimerDigitIndex(sprite.name, out int digitIndex))
            {
                timerDigitSprites[digitIndex] = sprite;
                continue;
            }

            if (sprite.name == "TimerDigits_Infinity")
            {
                timerInfinitySprite = sprite;
                continue;
            }

            if (sprite.name == "TimerDigits_Colon")
                timerColonSprite = sprite;
        }
    }

    static bool TryGetTimerDigitIndex(string spriteName, out int digitIndex)
    {
        digitIndex = -1;
        const string prefix = "TimerDigits_";

        if (string.IsNullOrEmpty(spriteName) || !spriteName.StartsWith(prefix))
            return false;

        return int.TryParse(spriteName.Substring(prefix.Length), out digitIndex)
            && digitIndex >= 0
            && digitIndex <= 9;
    }

    static float GetSpriteLogicalWidth(Sprite sprite, float targetHeight, float fallbackWidth)
    {
        if (sprite == null || sprite.rect.height <= 0f)
            return fallbackWidth;

        return targetHeight * (sprite.rect.width / sprite.rect.height);
    }

    static Color GetBlinkingTimerColonColor(int displayedTotalSeconds)
    {
        if (!Application.isPlaying)
            return Color.white;

        Color color = Color.white;
        color.a = displayedTotalSeconds % 2 == 0 ? 1f : 0f;
        return color;
    }

    void LogLayoutDiagnosticsIfNeeded(int visiblePlayerCount)
    {
        if (!logLayoutDiagnostics)
            return;

        string signature = BuildLayoutDiagnosticsSignature(visiblePlayerCount);
        if (signature == lastLayoutDiagnosticsSignature)
            return;

        lastLayoutDiagnosticsSignature = signature;
        Debug.Log(signature, this);
    }

    string BuildLayoutDiagnosticsSignature(int visiblePlayerCount)
    {
        RectTransform parent = rootRect != null ? rootRect.parent as RectTransform : null;
        RectTransform backgroundRect = backgroundImage != null ? backgroundImage.rectTransform : null;
        RectTransform borderRect = borderImage != null ? borderImage.rectTransform : null;
        RectTransform firstPartitionRect = partitionImages.Length > 0 && partitionImages[0] != null
            ? partitionImages[0].rectTransform
            : null;
        RectTransform firstSlotRect = slots.Length > 0 && slots[0] != null ? slots[0].Root : null;
        RectTransform firstStatsRect = slots.Length > 0 && slots[0] != null && slots[0].StatsPanel != null
            ? slots[0].StatsPanel.rectTransform
            : null;

        return "[BattleModeHud Layout] "
            + $"expectedHud={HudWidth:0.###}x{HudHeight:0.###} reference={ReferenceScreenWidth:0.###}x{ReferenceScreenHeight:0.###} "
            + $"visiblePlayers={visiblePlayerCount} "
            + $"parent={FormatRect(parent)} "
            + $"root={FormatRect(rootRect)} "
            + $"runtime={FormatRect(runtimeRoot)} "
            + $"background={FormatRect(backgroundRect)} expected=247x22 "
            + $"frame={FormatRect(borderRect)} expected=256x27 "
            + $"partition1={FormatRect(firstPartitionRect)} expected=5x23 "
            + $"slot1={FormatRect(firstSlotRect)} "
            + BuildTeamBackgroundDiagnostics(visiblePlayerCount)
            + $"slot1Items={FormatRect(firstStatsRect)} expected=23x7";
    }

    string BuildTeamBackgroundDiagnostics(int visiblePlayerCount)
    {
        float slotWidth = GetSlotWidth(visiblePlayerCount);
        string result = string.Empty;

        for (int i = 0; i < visiblePlayerCount && i < slots.Length; i++)
        {
            SlotUi slot = slots[i];
            RectTransform backgroundRect = slot != null && slot.TeamBackground != null
                ? slot.TeamBackground.rectTransform
                : null;
            float left = GetTeamBackgroundVisibleLeft(i, visiblePlayerCount);
            float right = GetTeamBackgroundVisibleRight(i, visiblePlayerCount, slotWidth);
            float slotGlobalLeft = UsableAreaLeft + i * slotWidth;
            float globalLeft = slotGlobalLeft + left;
            float globalRight = slotGlobalLeft + right;

            result += $"teamBg{i + 1}={FormatRect(backgroundRect)} "
                + $"logicalLocal=({left:0.###},{-TeamBackgroundBottomBleed:0.###})-({right:0.###},{UsableAreaHeight + TeamBackgroundTopBleed:0.###}) "
                + $"logicalGlobalX={globalLeft:0.###}-{globalRight:0.###} expectedHeight=26 ";
        }

        return result;
    }

    static string FormatRect(RectTransform rect)
    {
        if (rect == null)
            return "<null>";

        Rect currentRect = rect.rect;
        return $"{rect.name} size={currentRect.width:0.###}x{currentRect.height:0.###} "
            + $"anchors=({rect.anchorMin.x:0.####},{rect.anchorMin.y:0.####})-({rect.anchorMax.x:0.####},{rect.anchorMax.y:0.####}) "
            + $"offsetMin=({rect.offsetMin.x:0.###},{rect.offsetMin.y:0.###}) "
            + $"offsetMax=({rect.offsetMax.x:0.###},{rect.offsetMax.y:0.###})";
    }

    void SetTimerActive(bool active)
    {
        if (timerRoot == null)
            return;

        if (timerRoot.gameObject.activeSelf != active)
            timerRoot.gameObject.SetActive(active);
    }

    void SuppressLegacyHudAtRuntime()
    {
        if (!Application.isPlaying || legacyHudSuppressed)
            return;

        DisableLegacyBehaviour(GetComponent<HudGridLayout>());
        DisableLegacyBehaviour(GetComponent<HudPortraitInGridLayout>());
        DisableLegacyBehaviour(GetComponent<HudStatIconsInGridLayout>());
        DisableLegacyBehaviour(GetComponent<HudPushStartInGridLayout>());

        for (int i = 0; i < LegacyHudChildNames.Length; i++)
        {
            Transform legacyChild = transform.Find(LegacyHudChildNames[i]);
            if (legacyChild != null)
                legacyChild.gameObject.SetActive(false);
        }

        legacyHudSuppressed = true;
    }

    void UpdateVisualLayerOrder()
    {
        if (backgroundImage != null)
            backgroundImage.rectTransform.SetAsFirstSibling();

        for (int i = 0; i < slots.Length; i++)
        {
            SlotUi slot = slots[i];
            if (slot != null && slot.Root != null)
            {
                slot.Root.SetAsLastSibling();

                if (slot.TeamBackground != null)
                    slot.TeamBackground.rectTransform.SetAsFirstSibling();
            }
        }

        if (timerRoot != null)
            timerRoot.SetAsLastSibling();

        if (borderImage != null)
            borderImage.rectTransform.SetAsLastSibling();

        if (partitionsRoot != null)
            partitionsRoot.SetAsLastSibling();
    }

    static void DisableLegacyBehaviour(Behaviour behaviour)
    {
        if (behaviour != null)
            behaviour.enabled = false;
    }

    static void ApplyLogicalRect(
        RectTransform rect,
        float left,
        float bottom,
        float width,
        float height,
        float logicalParentWidth,
        float logicalParentHeight)
    {
        if (rect == null || logicalParentWidth <= 0f || logicalParentHeight <= 0f)
            return;

        rect.anchorMin = new Vector2(left / logicalParentWidth, bottom / logicalParentHeight);
        rect.anchorMax = new Vector2((left + width) / logicalParentWidth, (bottom + height) / logicalParentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static RectTransform GetOrCreateRect(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        RectTransform existingRect = existing as RectTransform;
        if (existingRect != null)
            return existingRect;

        GameObject go = new GameObject(childName, typeof(RectTransform));
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    static Image GetOrCreateImage(Transform parent, string childName)
    {
        RectTransform rect = GetOrCreateRect(parent, childName);
        Image image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();

        image.raycastTarget = false;
        return image;
    }

#if UNITY_EDITOR
    static Sprite[] LoadOrderedSpritesFromSheet(string assetPath, string spriteNamePrefix, int expectedCount)
    {
        Sprite[] orderedSprites = new Sprite[expectedCount];
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (!(assets[i] is Sprite sprite) || sprite == null)
                continue;

            string spriteName = sprite.name;
            if (!spriteName.StartsWith(spriteNamePrefix))
                continue;

            string suffix = spriteName.Substring(spriteNamePrefix.Length);
            if (!int.TryParse(suffix, out int index))
                continue;

            if (index < 0 || index >= orderedSprites.Length)
                continue;

            orderedSprites[index] = sprite;
        }

        return orderedSprites;
    }
#endif

    static TextMeshProUGUI GetOrCreateText(Transform parent, string childName)
    {
        RectTransform rect = GetOrCreateRect(parent, childName);
        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        text.raycastTarget = false;
        return text;
    }

    void CleanupRuntimeUi()
    {
        if (runtimeRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeRoot.gameObject);
        else
            DestroyImmediate(runtimeRoot.gameObject);

        runtimeRoot = null;
        partitionsRoot = null;
        timerRoot = null;
        backgroundImage = null;
        borderImage = null;
        timerBackgroundImage = null;
        timerMinuteDigitImage = null;
        timerSecondTensDigitImage = null;
        timerSecondOnesDigitImage = null;
        timerColonImage = null;
        rootRect = null;

        for (int i = 0; i < partitionImages.Length; i++)
            partitionImages[i] = null;

        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;

        for (int i = 0; i < playerHealthCache.Length; i++)
            playerHealthCache[i] = null;

        legacyHudSuppressed = false;
    }
}
