using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class BattleModeHud : MonoBehaviour
{
    const int MaxPlayers = 6;

    const float HudWidth = 256f;
    const float HudHeight = 23f;

    const float UsableAreaLeft = 3f;
    const float UsableAreaBottom = 1f;
    const float UsableAreaWidth = 250f;
    const float UsableAreaHeight = 21f;
    const float HudSidePadding = (HudWidth - (UsableAreaLeft + UsableAreaWidth));
    const float TeamBackgroundVerticalBleed = 1f;
    const float TeamBackgroundPartitionInset = 1f;
    const int TeamBackgroundTrimLeft = 2;
    const int TeamBackgroundTrimRight = 2;
    const int TeamBackgroundTrimTop = 1;
    const int TeamBackgroundTrimBottom = 2;

    const float PartitionWidth = 3f;
    const float PartitionHeight = 21f;
    const string PartitionAssetPath = "Assets/Sprites/HUD/BattleMode/partition.png";
    const string BombBlastSpeedAssetPath = "Assets/Sprites/HUD/BattleMode/BombBlastSpeed.png";
    const string Team1BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team1.png";
    const string Team2BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team2.png";
    const string Team3BackgroundAssetPath = "Assets/Sprites/HUD/BattleMode/Background_Team3.png";

    const float PortraitSize = 16f;
    const float PortraitY = 2f;
    const float OuterHudEdgePadding = 1f;
    const float PortraitToPowerupGap = 2f;
    const float FirstPlayerPortraitExtraOffset = 1f;
    const float LastPlayerItemsExtraOffset = 2f;
    const float AbilityIconStep = 7f;
    const float AbilityGridWidth = 20f;
    const float BaseSlotContentWidth = (UsableAreaWidth / MaxPlayers) - OuterHudEdgePadding - (PartitionWidth * 0.5f);
    const float PowerupGridExtraWidthFactor = 0.1f;

    const float NumberSize = 6f;
    const float AbilityIconSize = 6f;
    const float LifeIconSize = 6f;

    const float StatsPanelX = 18f;
    const float StatsPanelY = 1f;
    const float StatsPanelWidth = 21f;
    const float StatsPanelHeight = 7f;

    // Painel 21x7 com 1px de margem lateral.
    // Cada número ocupa 6x6 e fica centralizado sobre cada ícone.
    const float StatsNumberY = 1f;
    static readonly float[] StatsNumberOffsets = { 1f, 8f, 15f };

    const string RuntimeRootName = "__BattleModeRuntime";
    const string BackgroundName = "Background";
    const string BorderName = "Border";
    const string PartitionsRootName = "Partitions";
    const string PushStartLabel = "PUSH START";

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

    readonly bool[] playerDead = new bool[MaxPlayers];
    readonly CharacterHealth[] playerHealthCache = new CharacterHealth[MaxPlayers];
    readonly List<Sprite> activePowerupBuffer = new List<Sprite>(6);
    readonly List<int> activePlayerIdsBuffer = new List<int>(MaxPlayers);
    readonly SlotUi[] slots = new SlotUi[MaxPlayers];
    readonly Dictionary<int, Sprite> livePortraits = new Dictionary<int, Sprite>();
    readonly Dictionary<int, Sprite> deadPortraits = new Dictionary<int, Sprite>();
    readonly Image[] partitionImages = new Image[MaxPlayers - 1];

    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectTransform partitionsRoot;
    Image backgroundImage;
    Image borderImage;
    bool portraitsLoaded;
    bool legacyHudSuppressed;
    Sprite trimmedTeam1BackgroundSprite;
    Sprite trimmedTeam2BackgroundSprite;
    Sprite trimmedTeam3BackgroundSprite;

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
        if (rootRect == null)
            rootRect = (RectTransform)transform;

        SuppressLegacyHudAtRuntime();
        EnsurePortraitsLoaded();
        EnsureRuntimeUi();
        PopulateActivePlayerIds(activePlayerIdsBuffer);

        int visiblePlayerCount = GetVisiblePlayerCount();
        UpdateRootImages(visiblePlayerCount);
        UpdateLayout(visiblePlayerCount);
        UpdateContent(visiblePlayerCount);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        portraitsLoaded = false;
        trimmedTeam1BackgroundSprite = null;
        trimmedTeam2BackgroundSprite = null;
        trimmedTeam3BackgroundSprite = null;

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
    }
#endif

    void OnDisable()
    {
        CleanupRuntimeUi();
        ReleaseTrimmedTeamBackgroundSprites();
    }

    void OnDestroy()
    {
        CleanupRuntimeUi();
        ReleaseTrimmedTeamBackgroundSprites();
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

        for (int i = 0; i < partitionImages.Length; i++)
            partitionImages[i] = GetOrCreateImage(partitionsRoot, "Partition" + (i + 1));

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
        ApplyLogicalRect((RectTransform)backgroundImage.transform, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
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
                UsableAreaBottom,
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
            float lastPlayerItemsOffsetX = i == visiblePlayerCount - 1 ? LastPlayerItemsExtraOffset : 0f;
            float lastPlayerStatsPanelOffsetX = i == visiblePlayerCount - 1 ? LastPlayerItemsExtraOffset : 0f;
            float statsPanelLeft = GetStatsPanelLeft(i, visiblePlayerCount, slotWidth);
            float column0X = statsPanelLeft + StatsNumberOffsets[0] + lastPlayerItemsOffsetX;
            float column1X = statsPanelLeft + StatsNumberOffsets[1] + lastPlayerItemsOffsetX;
            float column2X = statsPanelLeft + StatsNumberOffsets[2] + lastPlayerItemsOffsetX;
            float portraitLeft = GetPortraitLeft(i, visiblePlayerCount, slotWidth, column0X);
            float teamBackgroundLeft = GetTeamBackgroundVisibleLeft(i);
            float teamBackgroundRight = GetTeamBackgroundVisibleRight(i, visiblePlayerCount, slotWidth);

            ApplyLogicalRect(slot.Root, slotLeft, UsableAreaBottom, slotWidth, UsableAreaHeight, HudWidth, HudHeight);
            ApplyLogicalRect(
                slot.TeamBackground.rectTransform,
                teamBackgroundLeft,
                -TeamBackgroundVerticalBleed,
                teamBackgroundRight - teamBackgroundLeft,
                HudHeight,
                slotWidth,
                UsableAreaHeight);
            ApplyLogicalRect(slot.Portrait.rectTransform, portraitLeft, PortraitY, PortraitSize, PortraitSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.PlayerNumber.rectTransform, portraitLeft, PortraitY, NumberSize, NumberSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(slot.AbilityIcons[0].rectTransform, column0X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[1].rectTransform, column1X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[2].rectTransform, column2X, PowerupRowYs[0], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[3].rectTransform, column0X, PowerupRowYs[1], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.AbilityIcons[4].rectTransform, column1X, PowerupRowYs[1], AbilityIconSize, AbilityIconSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(slot.LifeIcon.rectTransform, column2X, PowerupRowYs[1], LifeIconSize, LifeIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.LifeNumber.rectTransform, column2X, PowerupRowYs[1], NumberSize, NumberSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(
                slot.StatsPanel.rectTransform,
                statsPanelLeft + lastPlayerStatsPanelOffsetX,
                StatsPanelY,
                StatsPanelWidth,
                StatsPanelHeight,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.BombNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[0] + lastPlayerItemsOffsetX,
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.FireNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[1] + lastPlayerItemsOffsetX,
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.SpeedNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[2] + lastPlayerItemsOffsetX,
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
        SetImageSprite(slot.PlayerNumber, null, false);
        SetImageSprite(slot.Portrait, GetPortraitSprite(playerId, isDead), false);

        SetImageSprite(slot.StatsPanel, isDead ? null : bombBlastSpeedSprite, false);
        SetImageSprite(slot.BombNumber, isDead ? null : GetDigitSprite(state.BombAmount), false);
        SetImageSprite(slot.FireNumber, isDead ? null : state.HasFullFire ? fullFireSprite : GetDigitSprite(state.ExplosionRadius), false);
        SetImageSprite(slot.SpeedNumber, isDead ? null : GetDigitSprite(GetSpeedStepCount(state.SpeedInternal)), false);

        Color overlayColor = isDead
            ? new Color(1f, 1f, 1f, 0.45f)
            : Color.white;

        slot.Portrait.color = Color.white;
        slot.TeamBackground.color = Color.white;
        slot.PlayerNumber.color = overlayColor;
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
        float statsPanelLeft = slotWidth - StatsPanelWidth - 1f;

        if (visualIndex == visiblePlayerCount - 1)
            statsPanelLeft -= 2f;

        return statsPanelLeft;
    }

    float GetPortraitLeft(int visualIndex, int visiblePlayerCount, float slotWidth, float firstPowerupColumnX)
    {
        float contentLeft = GetSlotContentLeft(visualIndex);
        float portraitAreaRight = Mathf.Max(contentLeft + PortraitSize, firstPowerupColumnX - PortraitToPowerupGap);
        float portraitAreaWidth = portraitAreaRight - contentLeft;
        float portraitLeft = contentLeft + Mathf.Max(0f, (portraitAreaWidth - PortraitSize) * 0.5f);

        if (visualIndex <= 0)
            portraitLeft += FirstPlayerPortraitExtraOffset;

        return portraitLeft;
    }

    float GetTeamBackgroundVisibleLeft(int visualIndex)
    {
        return visualIndex <= 0
            ? -UsableAreaLeft
            : 0f;
    }

    float GetTeamBackgroundVisibleRight(int visualIndex, int visiblePlayerCount, float slotWidth)
    {
        return visualIndex >= visiblePlayerCount - 1
            ? slotWidth + HudSidePadding
            : slotWidth - TeamBackgroundPartitionInset;
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
                return GetTrimmedTeamBackgroundSprite(team2BackgroundSprite, ref trimmedTeam2BackgroundSprite);
            case BattleModeRules.TeamId.Green:
                return GetTrimmedTeamBackgroundSprite(team3BackgroundSprite, ref trimmedTeam3BackgroundSprite);
            default:
                return GetTrimmedTeamBackgroundSprite(team1BackgroundSprite, ref trimmedTeam1BackgroundSprite);
        }
    }

    Sprite GetTrimmedTeamBackgroundSprite(Sprite source, ref Sprite cache)
    {
        if (source == null)
            return null;

        if (cache != null)
            return cache;

        Rect rect = source.rect;
        float trimmedX = rect.x + TeamBackgroundTrimLeft;
        float trimmedY = rect.y + TeamBackgroundTrimBottom;
        float trimmedWidth = rect.width - TeamBackgroundTrimLeft - TeamBackgroundTrimRight;
        float trimmedHeight = rect.height - TeamBackgroundTrimTop - TeamBackgroundTrimBottom;

        if (trimmedWidth <= 0f || trimmedHeight <= 0f)
            return source;

        cache = Sprite.Create(
            source.texture,
            new Rect(trimmedX, trimmedY, trimmedWidth, trimmedHeight),
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        cache.name = source.name + "_TrimmedRuntime";
        return cache;
    }

    void ReleaseTrimmedTeamBackgroundSprites()
    {
        ReleaseTrimmedSprite(ref trimmedTeam1BackgroundSprite);
        ReleaseTrimmedSprite(ref trimmedTeam2BackgroundSprite);
        ReleaseTrimmedSprite(ref trimmedTeam3BackgroundSprite);
    }

    void ReleaseTrimmedSprite(ref Sprite sprite)
    {
        if (sprite == null)
            return;

        if (Application.isPlaying)
            Destroy(sprite);
        else
            DestroyImmediate(sprite);

        sprite = null;
    }

    bool ShouldShowDefaultBackground()
    {
        return !ShouldShowTeamBackgrounds();
    }

    bool ShouldShowTeamBackgrounds()
    {
        return BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams;
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
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.Center;
        text.extraPadding = true;
        text.richText = true;
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

        if (partitionsRoot != null)
            partitionsRoot.SetAsLastSibling();

        if (borderImage != null)
            borderImage.rectTransform.SetAsLastSibling();
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
        backgroundImage = null;
        borderImage = null;
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
