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

    const float PartitionWidth = 3f;
    const float PartitionHeight = 21f;
    const string PartitionAssetPath = "Assets/Sprites/HUD/BattleMode/partition.png";
    const string BombBlastSpeedAssetPath = "Assets/Sprites/HUD/BattleMode/BombBlastSpeed.png";

    const float PortraitSize = 16f;
    const float PortraitX = 1f;
    const float PortraitY = 2f;

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

    static readonly Vector2[] AbilityIconPositions =
    {
        new Vector2(19f, 14f),
        new Vector2(26f, 14f),
        new Vector2(19f, 8f),
        new Vector2(26f, 8f),
        new Vector2(33f, 8f)
    };

    static readonly Vector2 LifeIconPosition = new Vector2(33f, 14f);

    [Header("Battle HUD Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite partitionSprite;
    [SerializeField] private Sprite bombBlastSpeedSprite;

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

    sealed class SlotUi
    {
        public RectTransform Root;
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
        UpdateRootImages();
        UpdateLayout();
        UpdateContent();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        portraitsLoaded = false;

        if (partitionSprite == null)
            partitionSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PartitionAssetPath);

        if (bombBlastSpeedSprite == null)
            bombBlastSpeedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BombBlastSpeedAssetPath);
    }
#endif

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

    void UpdateRootImages()
    {
        SetImageSprite(backgroundImage, backgroundSprite, false);
        SetImageSprite(borderImage, borderSprite, false);

        ApplyLogicalRect(runtimeRoot, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect(partitionsRoot, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect((RectTransform)backgroundImage.transform, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);
        ApplyLogicalRect((RectTransform)borderImage.transform, 0f, 0f, HudWidth, HudHeight, HudWidth, HudHeight);

        float slotWidth = GetSlotWidth();

        for (int i = 0; i < partitionImages.Length; i++)
        {
            Image partition = partitionImages[i];
            SetImageSprite(partition, partitionSprite, true);

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

    void UpdateLayout()
    {
        float slotWidth = GetSlotWidth();

        for (int i = 0; i < MaxPlayers; i++)
        {
            SlotUi slot = slots[i];
            if (slot == null || slot.Root == null)
                continue;

            float slotLeft = UsableAreaLeft + i * slotWidth;

            ApplyLogicalRect(slot.Root, slotLeft, UsableAreaBottom, slotWidth, UsableAreaHeight, HudWidth, HudHeight);
            ApplyLogicalRect(slot.Portrait.rectTransform, PortraitX, PortraitY, PortraitSize, PortraitSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.PlayerNumber.rectTransform, PortraitX + 5f, PortraitY + 5f, NumberSize, NumberSize, slotWidth, UsableAreaHeight);

            ApplyLogicalRect(slot.LifeIcon.rectTransform, LifeIconPosition.x, LifeIconPosition.y, LifeIconSize, LifeIconSize, slotWidth, UsableAreaHeight);
            ApplyLogicalRect(slot.LifeNumber.rectTransform, LifeIconPosition.x, LifeIconPosition.y, NumberSize, NumberSize, slotWidth, UsableAreaHeight);

            for (int abilityIndex = 0; abilityIndex < slot.AbilityIcons.Length && abilityIndex < AbilityIconPositions.Length; abilityIndex++)
            {
                Vector2 pos = AbilityIconPositions[abilityIndex];
                ApplyLogicalRect(
                    slot.AbilityIcons[abilityIndex].rectTransform,
                    pos.x,
                    pos.y,
                    AbilityIconSize,
                    AbilityIconSize,
                    slotWidth,
                    UsableAreaHeight);
            }

            float statsPanelLeft = slotWidth - StatsPanelWidth - 1f;

            if (i == MaxPlayers - 1)
                statsPanelLeft -= 2f;

            ApplyLogicalRect(
                slot.StatsPanel.rectTransform,
                statsPanelLeft,
                StatsPanelY,
                StatsPanelWidth,
                StatsPanelHeight,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.BombNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[0],
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.FireNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[1],
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(
                slot.SpeedNumber.rectTransform,
                statsPanelLeft + StatsNumberOffsets[2],
                StatsPanelY + StatsNumberY - 1f,
                NumberSize,
                NumberSize,
                slotWidth,
                UsableAreaHeight);

            ApplyLogicalRect(slot.PushStart.rectTransform, 17f, 7f, 21f, 9f, slotWidth, UsableAreaHeight);
        }
    }

    void UpdateContent()
    {
        int activePlayerCount = GetActivePlayerCount();

        for (int i = 0; i < MaxPlayers; i++)
        {
            SlotUi slot = slots[i];
            if (slot == null)
                continue;

            int playerId = i + 1;
            bool activePlayer = i < activePlayerCount;

            if (!activePlayer)
            {
                ShowInactiveSlot(slot, playerId);
                continue;
            }

            ShowActiveSlot(slot, playerId);
        }
    }

    void ShowInactiveSlot(SlotUi slot, int playerId)
    {
        SetImageSprite(slot.Portrait, null, false);
        SetImageSprite(slot.LifeIcon, null, false);
        SetImageSprite(slot.LifeNumber, null, false);
        SetImageSprite(slot.StatsPanel, null, false);
        SetImageSprite(slot.BombNumber, null, false);
        SetImageSprite(slot.FireNumber, null, false);
        SetImageSprite(slot.SpeedNumber, null, false);

        for (int i = 0; i < slot.AbilityIcons.Length; i++)
            SetImageSprite(slot.AbilityIcons[i], null, false);

        SetImageSprite(slot.PlayerNumber, GetDigitSprite(playerId), false);
        slot.PlayerNumber.color = Color.white;

        ConfigurePushStart(slot.PushStart, true);
    }

    void ShowActiveSlot(SlotUi slot, int playerId)
    {
        PlayerPersistentStats.PlayerState state = PlayerPersistentStats.GetRuntime(playerId);
        CharacterHealth health = GetPlayerHealth(playerId);
        bool isDead = IsPlayerDead(playerId, health);
        int currentLife = health != null ? Mathf.Max(0, health.life) : Mathf.Max(0, state.Life);

        SetImageSprite(slot.PlayerNumber, null, false);
        SetImageSprite(slot.Portrait, GetPortraitSprite(playerId, isDead), false);

        SetImageSprite(slot.StatsPanel, bombBlastSpeedSprite, false);
        SetImageSprite(slot.BombNumber, GetDigitSprite(state.BombAmount), false);
        SetImageSprite(slot.FireNumber, GetDigitSprite(state.ExplosionRadius), false);
        SetImageSprite(slot.SpeedNumber, GetDigitSprite(GetSpeedStepCount(state.SpeedInternal)), false);

        Color overlayColor = isDead
            ? new Color(1f, 1f, 1f, 0.45f)
            : Color.white;

        slot.Portrait.color = Color.white;
        slot.StatsPanel.color = overlayColor;
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

        AddPowerupIfAny(GetCurrentBombTypeSprite(state));
        AddPowerupIfAny(GetKickOrBombPassSprite(state));
        AddPowerupIfAny(state.HasPowerGlove ? powerGloveSprite : null);
        AddPowerupIfAny(state.CanPassDestructibles ? destructiblePassSprite : null);
        AddPowerupIfAny(state.HasFullFire ? fullFireSprite : null);
        AddPowerupIfAny(state.CanPunchBombs ? punchSprite : null);
    }

    void AddPowerupIfAny(Sprite sprite)
    {
        if (sprite == null)
            return;

        if (activePowerupBuffer.Count >= AbilityIconPositions.Length)
            return;

        activePowerupBuffer.Add(sprite);
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

    int GetActivePlayerCount()
    {
        if (Application.isPlaying && GameSession.Instance != null)
            return Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, MaxPlayers);

        return MaxPlayers;
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

    float GetSlotWidth()
    {
        return UsableAreaWidth / MaxPlayers;
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
                slot.Root.SetAsLastSibling();
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
