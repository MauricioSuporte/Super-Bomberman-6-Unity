using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AchievementsMenu : MonoBehaviour
{
    [System.Serializable]
    private sealed class BackgroundSet
    {
        public Sprite[] sprites = new Sprite[2];
    }

    private sealed class RowVisual
    {
        public RectTransform Root;
        public Image Background;
        public Image CheckImage;
        public Image Icon;
        public TextMeshProUGUI Label;
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform referenceRect;
    [SerializeField] private Image fadeImage;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

    [Header("Flow")]
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField, Min(0.01f)] private float musicFadeOutDuration = 0.5f;

    [Header("Input Timing")]
    [SerializeField, Min(0.01f)] private float directionalRepeatInitialDelay = 0.22f;
    [SerializeField, Min(0.01f)] private float directionalRepeatInterval = 0.12f;

    [Header("Animated Backgrounds")]
    [SerializeField] private BackgroundSet matchModeBackgrounds = new();
    [SerializeField, Min(0.01f)] private float backgroundSwapInterval = 2f;
    [SerializeField] private bool backgroundSwapLoop = true;

    [Header("Music")]
    [SerializeField] private AudioClip selectMusic;
    [SerializeField, Range(0f, 1f)] private float selectMusicVolume = 1f;
    [SerializeField] private bool loopSelectMusic = true;

    [Header("SFX")]
    [SerializeField] private AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] private float moveCursorSfxVolume = 1f;
    [SerializeField] private AudioClip returnSfx;
    [SerializeField, Range(0f, 1f)] private float returnSfxVolume = 1f;

    [Header("Cursor")]
    [SerializeField] private Sprite[] itemSelectCursorSprites = new Sprite[3];
    [SerializeField, Min(0.01f)] private float itemSelectCursorFrameSeconds = 0.08f;
    [SerializeField] private int[] itemSelectCursorFrameSequence = { 0, 1, 2, 1 };

    [Header("Layout")]
    [SerializeField] private bool dynamicScale = true;
    [SerializeField] private int referenceWidth = 256;
    [SerializeField] private int referenceHeight = 224;
    [SerializeField] private bool useIntegerUpscale = true;
    [SerializeField, Min(1)] private int designUpscale = 4;
    [SerializeField, Min(0.01f)] private float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 10f;
    [SerializeField, Min(1)] private int visibleRowCount = 9;
    [SerializeField] private Vector2 listOffset = new(0f, 18f);
    [SerializeField] private Vector2 listSize = new(880f, 440f);
    [SerializeField] private float rowHeight = 46f;
    [SerializeField] private float rowCheckX = -398f;
    [SerializeField] private float rowIconX = -342f;
    [SerializeField] private float rowTextX = 80f;
    [SerializeField] private float rowTextWidth = 650f;
    [SerializeField] private float rowCursorLeftX = -430f;
    [SerializeField] private float rowCursorRightX = 418f;
    [SerializeField] private Vector2 rowCursorSize = new(24f, 48f);
    [SerializeField] private Vector2 listScrollbarOffset = new(432f, 0f);
    [SerializeField] private Vector2 listScrollbarSize = new(12f, 410f);
    [SerializeField] private Vector2 detailOffset = new(0f, -275f);
    [SerializeField] private Vector2 detailSize = new(880f, 130f);
#pragma warning disable CS0414
    [SerializeField] private int rowFontSize = 24;
#pragma warning restore CS0414
    [SerializeField] private int rowDescriptionFontSize = 20;
    [SerializeField] private int detailFontSize = 22;
    [SerializeField] private Vector2 progressTextOffset = new(0f, 304f);
    [SerializeField] private Vector2 progressBarOffset = new(0f, 268f);
    [SerializeField] private Vector2 progressBarSize = new(560f, 20f);

    [Header("Checkbox")]
    [SerializeField] private Sprite checkboxUncheckedSprite;
    [SerializeField] private Sprite checkboxCheckedSprite;

    [Header("Text Style")]
    [SerializeField] private TMP_FontAsset optionFontAsset;
    [SerializeField] private Material optionFontMaterialPreset;
    [SerializeField] private bool forceBold = true;
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.28f;
    [SerializeField] private bool enableUnderlay = true;
    [SerializeField] private Color underlayColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float underlayDilate = 0.12f;
    [SerializeField, Range(0f, 1f)] private float underlaySoftness = 0f;
    [SerializeField] private float underlayOffsetX = 0.35f;
    [SerializeField] private float underlayOffsetY = -0.35f;

    private readonly List<RowVisual> rows = new();
    private readonly List<AchievementCatalog.AchievementInfo> entries = new();
    private readonly bool[] previousMenuHeld = new bool[System.Enum.GetValues(typeof(PlayerAction)).Length];
    private readonly bool[] menuHoldConsumed = new bool[System.Enum.GetValues(typeof(PlayerAction)).Length];
    private readonly float[] nextMenuRepeatTime = new float[System.Enum.GetValues(typeof(PlayerAction)).Length];

    private RectTransform contentRoot;
    private RectTransform cursorRect;
    private Image cursorImage;
    private RectTransform rightCursorRect;
    private Image rightCursorImage;
    private RectTransform listScrollbarThumbRect;
    private Image listScrollbarThumbImage;
    private Image detailIcon;
    private TextMeshProUGUI detailTitleText;
    private TextMeshProUGUI detailDescriptionText;
    private TextMeshProUGUI detailUnlocksText;
    private TextMeshProUGUI progressText;
    private Image progressFillImage;
    private TextMeshProUGUI progressPercentText;
    private Image runtimeBackgroundImage;
    private Sprite runtimeCheckboxUncheckedSprite;
    private Sprite runtimeCheckboxCheckedSprite;
    private int selectedIndex;
    private int firstVisibleIndex;
    private int backgroundSpriteIndex;
    private float backgroundSwapTimer;
    private float cursorFrameTimer;
    private int cursorFrameIndex;
    private AudioSource localMusicSource;
    private AudioSource localSfxSource;
    private bool exiting;
    private int lastScreenW = -1;
    private int lastScreenH = -1;
    private Rect lastReferencePixelRect;
    private float currentUiScale = 1f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
    private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
    private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
    private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
    private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");

    private void Awake()
    {
        if (root != null)
            root.SetActive(true);

        EnsureReferences();
        ApplyDynamicScaleIfNeeded(true);
        HideDuplicatedBattleModeChildren();
        DisableDuplicatedGameplayControllers();
        UnlockProgress.ReloadFromDisk();
        EnsureCheckboxSprites();
        BuildEntries();
        BuildUi();
        ApplyCurrentBackgroundSprite(true);
        Refresh();
        StartSelectMusic();
        StartFadeIn();
    }

    private void Update()
    {
        HideDuplicatedBattleModeChildren();
        ApplyDynamicScaleIfNeeded(false);
        TickBackgroundSpriteSwap();
        TickCursorAnimation();
        HandleInput();
    }

    private void BuildEntries()
    {
        entries.Clear();
        entries.AddRange(AchievementCatalog.All);
    }

    private void BuildUi()
    {
        EnsureReferences();

        RectTransform parent = referenceRect != null ? referenceRect : transform as RectTransform;
        if (parent == null)
            parent = GetComponentInParent<Canvas>()?.transform as RectTransform;

        contentRoot = CreateRect("AchievementsContent", parent, Vector2.zero, new Vector2(1024f, 720f));
        contentRoot.localScale = Vector3.one * currentUiScale;

        progressText = CreateText("ProgressText", contentRoot, "", progressTextOffset, new Vector2(560f, 34f), 22, TextAlignmentOptions.Center);
        progressText.color = new Color(1f, 0.9f, 0.28f);
        BuildProgressBar();

        RectTransform listRoot = CreatePanel("AchievementsList", contentRoot, listOffset, listSize, new Color(0.06f, 0.055f, 0.015f, 0.72f), new Color(0.95f, 0.72f, 0.05f, 1f));
        rows.Clear();

        for (int i = 0; i < visibleRowCount; i++)
        {
            float y = (listSize.y * 0.5f) - (rowHeight * 0.5f) - (i * rowHeight);
            RowVisual row = CreateRow(listRoot, i, y);
            rows.Add(row);
        }

        BuildListScrollbar(listRoot);

        cursorRect = CreateRect("CursorLeft", listRoot, new Vector2(rowCursorLeftX, 0f), rowCursorSize);
        cursorImage = cursorRect.gameObject.AddComponent<Image>();
        cursorImage.preserveAspect = true;
        cursorImage.raycastTarget = false;

        rightCursorRect = CreateRect("CursorRight", listRoot, new Vector2(rowCursorRightX, 0f), rowCursorSize);
        rightCursorImage = rightCursorRect.gameObject.AddComponent<Image>();
        rightCursorImage.preserveAspect = true;
        rightCursorImage.raycastTarget = false;
        rightCursorRect.localScale = new Vector3(-1f, 1f, 1f);

        RectTransform detailRoot = CreatePanel("AchievementDetails", contentRoot, detailOffset, detailSize, new Color(0.20f, 0.18f, 0.11f, 0.92f), new Color(1f, 0.76f, 0.08f, 1f));
        detailIcon = CreateImage("DetailIcon", detailRoot, new Vector2(-360f, 0f), new Vector2(96f, 96f));
        detailTitleText = CreateText("DetailTitle", detailRoot, "", new Vector2(75f, 35f), new Vector2(700f, 38f), detailFontSize + 6, TextAlignmentOptions.Left);
        detailDescriptionText = CreateText("DetailDescription", detailRoot, "", new Vector2(75f, 2f), new Vector2(700f, 42f), detailFontSize - 2, TextAlignmentOptions.Left);
        detailUnlocksText = CreateText("DetailUnlocks", detailRoot, "", new Vector2(75f, -38f), new Vector2(700f, 34f), detailFontSize - 2, TextAlignmentOptions.Left);
    }

    private void BuildListScrollbar(RectTransform listRoot)
    {
        RectTransform track = CreateRect("ListScrollbar", listRoot, listScrollbarOffset, listScrollbarSize);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.03f, 0.025f, 0.01f, 0.88f);
        trackImage.raycastTarget = false;

        Outline trackOutline = track.gameObject.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.95f, 0.72f, 0.05f, 1f);
        trackOutline.effectDistance = new Vector2(2f, -2f);

        listScrollbarThumbRect = CreateRect("Thumb", track, Vector2.zero, Vector2.zero);
        listScrollbarThumbRect.anchorMin = new Vector2(0f, 1f);
        listScrollbarThumbRect.anchorMax = new Vector2(1f, 1f);
        listScrollbarThumbRect.pivot = new Vector2(0.5f, 1f);
        listScrollbarThumbImage = listScrollbarThumbRect.gameObject.AddComponent<Image>();
        listScrollbarThumbImage.color = new Color(1f, 0.78f, 0.08f, 0.95f);
        listScrollbarThumbImage.raycastTarget = false;
    }

    private void BuildProgressBar()
    {
        RectTransform shell = CreatePanel("ProgressBar", contentRoot, progressBarOffset, progressBarSize, new Color(0.06f, 0.055f, 0.015f, 0.82f), new Color(0.95f, 0.72f, 0.05f, 1f));

        RectTransform fillRect = CreateRect("Fill", shell, Vector2.zero, Vector2.zero);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        progressFillImage = fillRect.gameObject.AddComponent<Image>();
        progressFillImage.color = new Color(1f, 0.78f, 0.08f, 0.95f);
        progressFillImage.raycastTarget = false;

        progressPercentText = CreateText("Percent", shell, "", Vector2.zero, progressBarSize, 14, TextAlignmentOptions.Center);
        progressPercentText.color = new Color(1f, 0.97f, 0.72f);
    }

    private RowVisual CreateRow(RectTransform parent, int rowIndex, float y)
    {
        RectTransform rowRoot = CreateRect($"AchievementRow{rowIndex + 1}", parent, new Vector2(0f, y), new Vector2(listSize.x - 24f, rowHeight));
        Image bg = rowRoot.gameObject.AddComponent<Image>();
        bg.color = rowIndex % 2 == 0 ? new Color(0.19f, 0.16f, 0.03f, 0.72f) : new Color(0.11f, 0.09f, 0.02f, 0.72f);

        Image check = CreateImage("Check", rowRoot, new Vector2(rowCheckX, 0f), new Vector2(32f, 32f));
        check.preserveAspect = true;
        Image icon = CreateImage("Icon", rowRoot, new Vector2(rowIconX, 0f), new Vector2(38f, 38f));
        TextMeshProUGUI label = CreateText("Label", rowRoot, "", new Vector2(rowTextX, 0f), new Vector2(rowTextWidth, rowHeight - 4f), rowDescriptionFontSize, TextAlignmentOptions.Left);

        return new RowVisual
        {
            Root = rowRoot,
            Background = bg,
            CheckImage = check,
            Icon = icon,
            Label = label
        };
    }

    private void HandleInput()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        if (MenuDirectionalPressed(input, PlayerAction.MoveUp))
            MoveSelection(-1);
        else if (MenuDirectionalPressed(input, PlayerAction.MoveDown))
            MoveSelection(1);
        else if (MenuButtonPressed(input, PlayerAction.ActionB))
            ReturnToTitle();

        CapturePreviousHeldInputs(input);
    }

    private void MoveSelection(int direction)
    {
        if (entries.Count <= 0)
            return;

        int previous = selectedIndex;
        selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, entries.Count - 1);
        if (selectedIndex == previous)
            return;

        if (selectedIndex < firstVisibleIndex)
            firstVisibleIndex = selectedIndex;
        else if (selectedIndex >= firstVisibleIndex + visibleRowCount)
            firstVisibleIndex = selectedIndex - visibleRowCount + 1;

        PlaySfx(moveCursorSfx, moveCursorSfxVolume);
        Refresh();
    }

    private void Refresh()
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int entryIndex = firstVisibleIndex + rowIndex;
            RowVisual row = rows[rowIndex];
            bool hasEntry = entryIndex >= 0 && entryIndex < entries.Count;
            row.Root.gameObject.SetActive(hasEntry);

            if (!hasEntry)
                continue;

            AchievementCatalog.AchievementInfo entry = entries[entryIndex];
            bool unlocked = entry.IsUnlocked != null && entry.IsUnlocked();
            bool selected = entryIndex == selectedIndex;

            row.CheckImage.sprite = unlocked ? checkboxCheckedSprite : checkboxUncheckedSprite;
            row.CheckImage.color = Color.white;
            row.Icon.sprite = entry.LoadIcon != null ? entry.LoadIcon() : null;
            row.Icon.color = unlocked ? Color.white : new Color(0.52f, 0.52f, 0.52f, 0.9f);
            LocalizedTmpFontFallback.Apply(row.Label);
            row.Label.text = entry.Hint;
            row.Label.color = selected ? new Color(1f, 0.95f, 0.32f) : new Color(0.96f, 0.91f, 0.72f);
            row.Background.color = selected ? new Color(0.44f, 0.34f, 0.04f, 0.84f) : (rowIndex % 2 == 0 ? new Color(0.19f, 0.16f, 0.03f, 0.72f) : new Color(0.11f, 0.09f, 0.02f, 0.72f));
        }

        int visibleRow = selectedIndex - firstVisibleIndex;
        if (cursorRect != null && visibleRow >= 0 && visibleRow < rows.Count)
            cursorRect.anchoredPosition = new Vector2(rowCursorLeftX, rows[visibleRow].Root.anchoredPosition.y);

        if (rightCursorRect != null && visibleRow >= 0 && visibleRow < rows.Count)
            rightCursorRect.anchoredPosition = new Vector2(rowCursorRightX, rows[visibleRow].Root.anchoredPosition.y);

        RefreshDetails();
        RefreshProgress();
        RefreshListScrollbar();
    }

    private void RefreshListScrollbar()
    {
        if (listScrollbarThumbRect == null || listScrollbarThumbImage == null)
            return;

        int total = entries.Count;
        int visible = Mathf.Clamp(visibleRowCount, 1, Mathf.Max(1, total));
        if (total <= visibleRowCount)
        {
            listScrollbarThumbImage.enabled = false;
            return;
        }

        listScrollbarThumbImage.enabled = true;

        float trackHeight = Mathf.Max(1f, listScrollbarSize.y - 8f);
        float thumbHeight = Mathf.Max(rowHeight, trackHeight * (visible / (float)total));
        int maxFirst = Mathf.Max(1, total - visibleRowCount);
        float scrollRatio = Mathf.Clamp01(firstVisibleIndex / (float)maxFirst);
        float travel = Mathf.Max(0f, trackHeight - thumbHeight);

        listScrollbarThumbRect.sizeDelta = new Vector2(-4f, thumbHeight);
        listScrollbarThumbRect.anchoredPosition = new Vector2(0f, -4f - (travel * scrollRatio));
    }

    private void RefreshProgress()
    {
        int unlocked = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsUnlocked != null && entries[i].IsUnlocked())
                unlocked++;
        }

        int total = Mathf.Max(1, entries.Count);
        float ratio = Mathf.Clamp01(unlocked / (float)total);
        int percent = Mathf.RoundToInt(ratio * 100f);

        if (progressText != null)
        {
            LocalizedTmpFontFallback.Apply(progressText);
            progressText.text = string.Format(GameTextDatabase.AchievementsMenu.Progress, unlocked, entries.Count);
        }

        if (progressFillImage != null)
        {
            RectTransform fillRect = progressFillImage.rectTransform;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMax = new Vector2(ratio <= 0f ? 4f : -4f, -4f);
            progressFillImage.enabled = ratio > 0f;
        }

        if (progressPercentText != null)
            progressPercentText.text = $"{percent}%";
    }

    private void RefreshDetails()
    {
        if (entries.Count <= 0 || selectedIndex < 0 || selectedIndex >= entries.Count)
            return;

        AchievementCatalog.AchievementInfo entry = entries[selectedIndex];
        bool unlocked = entry.IsUnlocked != null && entry.IsUnlocked();

        if (detailIcon != null)
        {
            detailIcon.sprite = entry.LoadIcon != null ? entry.LoadIcon() : null;
            detailIcon.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.95f);
        }

        if (detailTitleText != null)
        {
            LocalizedTmpFontFallback.Apply(detailTitleText);
            string state = unlocked ? GameTextDatabase.Common.Obtained : GameTextDatabase.Common.Locked;
            detailTitleText.text = string.Format(GameTextDatabase.AchievementsMenu.DetailState, entry.Name, state);
            detailTitleText.color = unlocked ? new Color(0.45f, 1f, 0.42f) : new Color(1f, 0.88f, 0.24f);
        }

        if (detailDescriptionText != null)
        {
            LocalizedTmpFontFallback.Apply(detailDescriptionText);
            detailDescriptionText.text = entry.Hint;
        }

        if (detailUnlocksText != null)
        {
            LocalizedTmpFontFallback.Apply(detailUnlocksText);
            string label = unlocked ? GameTextDatabase.Common.Unlocked : GameTextDatabase.Common.Unlocks;
            detailUnlocksText.text = string.Format(GameTextDatabase.AchievementsMenu.RewardLine, label, entry.RewardText);
        }
    }

    private void EnsureCheckboxSprites()
    {
        if (checkboxUncheckedSprite == null)
        {
            runtimeCheckboxUncheckedSprite ??= CreateCheckboxSprite(false);
            checkboxUncheckedSprite = runtimeCheckboxUncheckedSprite;
        }

        if (checkboxCheckedSprite == null)
        {
            runtimeCheckboxCheckedSprite ??= CreateCheckboxSprite(true);
            checkboxCheckedSprite = runtimeCheckboxCheckedSprite;
        }
    }

    private static Sprite CreateCheckboxSprite(bool checkedState)
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new(0f, 0f, 0f, 0f);
        Color shadow = new(0.03f, 0.025f, 0.01f, 0.92f);
        Color dark = new(0.07f, 0.055f, 0.015f, 0.96f);
        Color gold = new(1f, 0.78f, 0.08f, 1f);
        Color bright = new(1f, 0.95f, 0.34f, 1f);
        Color check = new(0.42f, 1f, 0.36f, 1f);
        Color checkShadow = new(0.02f, 0.12f, 0.02f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, clear);
        }

        FillRect(texture, 3, 2, 27, 26, shadow);
        FillRect(texture, 2, 4, 26, 26, gold);
        FillRect(texture, 5, 7, 20, 20, dark);
        DrawRect(texture, 4, 6, 22, 22, bright);

        if (checkedState)
        {
            DrawPixelLine(texture, 9, 15, 13, 11, checkShadow, 3);
            DrawPixelLine(texture, 13, 11, 23, 22, checkShadow, 3);
            DrawPixelLine(texture, 8, 16, 12, 12, check, 3);
            DrawPixelLine(texture, 12, 12, 22, 23, check, 3);
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = checkedState ? "RuntimeCheckboxChecked" : "RuntimeCheckboxUnchecked";
        return sprite;
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && yy >= 0 && xx < texture.width && yy < texture.height)
                    texture.SetPixel(xx, yy, color);
            }
        }
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        FillRect(texture, x, y, width, 1, color);
        FillRect(texture, x, y + height - 1, width, 1, color);
        FillRect(texture, x, y, 1, height, color);
        FillRect(texture, x + width - 1, y, 1, height, color);
    }

    private static void DrawPixelLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            FillRect(texture, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Vector2 position, Vector2 size, Color fill, Color border)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = fill;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(3f, -3f);

        return rect;
    }

    private RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : gameObject.layer;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (optionFontAsset != null)
            tmp.font = optionFontAsset;
        if (optionFontMaterialPreset != null)
            tmp.fontSharedMaterial = optionFontMaterialPreset;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = false;
        tmp.alignment = alignment;
        tmp.color = new Color(0.96f, 0.91f, 0.72f);
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.fontStyle = forceBold ? FontStyles.Bold : FontStyles.Normal;
        ApplyBattleModeTextMaterial(tmp);
        return tmp;
    }

    private void ApplyBattleModeTextMaterial(TextMeshProUGUI tmp)
    {
        if (tmp == null || optionFontMaterialPreset != null)
            return;

        Material material = tmp.fontMaterial;
        if (material == null)
            return;

        if (material.HasProperty(OutlineColorId))
            material.SetColor(OutlineColorId, useOutline ? outlineColor : Color.clear);
        if (material.HasProperty(OutlineWidthId))
            material.SetFloat(OutlineWidthId, useOutline ? outlineWidth : 0f);
        if (material.HasProperty(UnderlayColorId))
            material.SetColor(UnderlayColorId, enableUnderlay ? underlayColor : Color.clear);
        if (material.HasProperty(UnderlayDilateId))
            material.SetFloat(UnderlayDilateId, enableUnderlay ? underlayDilate : 0f);
        if (material.HasProperty(UnderlaySoftnessId))
            material.SetFloat(UnderlaySoftnessId, enableUnderlay ? underlaySoftness : 0f);
        if (material.HasProperty(UnderlayOffsetXId))
            material.SetFloat(UnderlayOffsetXId, enableUnderlay ? underlayOffsetX : 0f);
        if (material.HasProperty(UnderlayOffsetYId))
            material.SetFloat(UnderlayOffsetYId, enableUnderlay ? underlayOffsetY : 0f);

        tmp.fontMaterial = material;
    }

    private void EnsureReferences()
    {
        if (root == null)
            root = gameObject;

        if (referenceRect == null)
            referenceRect = root != null ? root.GetComponent<RectTransform>() : transform as RectTransform;

        if (backgroundImage == null || !IsOwnedBackground(backgroundImage))
            backgroundImage = FindOwnedBackgroundImage();

        if (backgroundImage == null)
            backgroundImage = CreateRuntimeBackgroundImage();

        if (backgroundImage != null)
        {
            backgroundImage.gameObject.SetActive(true);
            backgroundImage.enabled = true;
            backgroundImage.transform.SetAsFirstSibling();
            SetBackgroundVisible(true);
        }
    }

    private Image FindOwnedBackgroundImage()
    {
        if (root != null)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (IsOwnedBackground(image))
                {
                    return image;
                }
            }
        }

        if (referenceRect != null)
        {
            Image[] images = referenceRect.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (IsOwnedBackground(image))
                {
                    return image;
                }
            }
        }

        return null;
    }

    private bool IsOwnedBackground(Image image)
    {
        if (image == null)
            return false;

        if (runtimeBackgroundImage != null && image == runtimeBackgroundImage)
            return true;

        return string.Equals(image.gameObject.name, "AchievementsBackground", System.StringComparison.Ordinal);
    }

    private Image CreateRuntimeBackgroundImage()
    {
        RectTransform parent = referenceRect != null ? referenceRect : root != null ? root.GetComponent<RectTransform>() : GetComponentInParent<Canvas>()?.transform as RectTransform;
        if (parent == null)
            parent = transform as RectTransform;
        if (parent == null)
            return null;

        RectTransform rect = CreateRect("AchievementsBackground", parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        rect.SetAsFirstSibling();

        Image image = rect.gameObject.AddComponent<Image>();
        image.preserveAspect = false;
        image.raycastTarget = false;
        runtimeBackgroundImage = image;
        SetBackgroundVisible(true);
        return image;
    }

    private void SetBackgroundVisible(bool visible)
    {
        if (backgroundImage == null)
            return;

        Color color = backgroundImage.color;
        color.a = visible ? 1f : 0f;
        backgroundImage.color = color;
    }

    private void HideDuplicatedBattleModeChildren()
    {
        if (root == null)
            return;

        SaveFileMenuOptions leftPanel = root.GetComponentInChildren<SaveFileMenuOptions>(true);
        if (leftPanel != null)
            leftPanel.gameObject.SetActive(false);

        BomberSkinSelectMenu skinSelect = root.GetComponentInChildren<BomberSkinSelectMenu>(true);
        if (skinSelect != null && skinSelect.gameObject != gameObject)
            skinSelect.gameObject.SetActive(false);
    }

    private void DisableDuplicatedGameplayControllers()
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.enabled = false;

        GameObject stageSettings = GameObject.Find("StageSettings");
        if (stageSettings != null)
            stageSettings.SetActive(false);
    }

    private void ReturnToTitle()
    {
        if (exiting)
            return;

        exiting = true;
        PlaySfx(returnSfx, returnSfxVolume);
        StartCoroutine(ReturnToTitleRoutine());
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float musicDuration = Mathf.Max(0.01f, musicFadeOutDuration);
        float totalDuration = Mathf.Max(duration, musicDuration);

        AudioSource musicSource = GetActiveMusicSource();
        float startVolume = musicSource != null ? musicSource.volume : 0f;

        PrepareFade(0f);

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (fadeImage != null)
            {
                BringFadeImageToFront();
                SetFadeAlpha(Mathf.Clamp01(elapsed / duration));
            }

            if (musicSource != null)
                musicSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / musicDuration));

            yield return null;
        }

        if (musicSource != null)
            musicSource.volume = 0f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();
        else if (localMusicSource != null)
            localMusicSource.Stop();

        TitleScreenBootstrap.SkipNextIntroSequence();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
    }

    private void StartFadeIn()
    {
        if (fadeImage == null)
            return;

        PrepareFade(1f);
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadeImage == null)
            yield break;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            BringFadeImageToFront();
            SetFadeAlpha(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    private void PrepareFade(float alpha)
    {
        if (fadeImage == null)
            return;

        fadeImage.gameObject.SetActive(true);
        BringFadeImageToFront();
        SetFadeAlpha(alpha);
    }

    private void BringFadeImageToFront()
    {
        if (fadeImage == null)
            return;

        Canvas canvas = fadeImage.canvas != null ? fadeImage.canvas : GetComponentInParent<Canvas>();
        if (canvas != null && fadeImage.transform.parent != canvas.transform)
            fadeImage.rectTransform.SetParent(canvas.transform, false);

        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        fadeImage.transform.SetAsLastSibling();
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }

    private AudioSource GetActiveMusicSource()
    {
        if (GameMusicController.Instance != null)
            return GameMusicController.Instance.GetMusicSource();

        return localMusicSource;
    }

    private bool MenuButtonPressed(PlayerInputManager input, PlayerAction action)
    {
        int actionIndex = (int)action;
        if (input == null || actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
            return false;

        bool held = AnyPlayerGet(input, action);
        bool pressed = held && !previousMenuHeld[actionIndex];

        if (pressed)
        {
            menuHoldConsumed[actionIndex] = true;
            return true;
        }

        if (held && !menuHoldConsumed[actionIndex])
        {
            menuHoldConsumed[actionIndex] = true;
            return true;
        }

        return false;
    }

    private bool MenuDirectionalPressed(PlayerInputManager input, PlayerAction action)
    {
        int actionIndex = (int)action;
        if (input == null || actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
            return false;

        bool held = AnyPlayerGet(input, action);
        if (!held)
            return false;

        float now = Time.unscaledTime;
        if (!previousMenuHeld[actionIndex])
        {
            nextMenuRepeatTime[actionIndex] = now + Mathf.Max(0.01f, directionalRepeatInitialDelay);
            return true;
        }

        if (now >= nextMenuRepeatTime[actionIndex])
        {
            nextMenuRepeatTime[actionIndex] = now + Mathf.Max(0.01f, directionalRepeatInterval);
            return true;
        }

        return false;
    }

    private void CapturePreviousHeldInputs(PlayerInputManager input)
    {
        PlayerAction[] actions = { PlayerAction.ActionB, PlayerAction.Start, PlayerAction.MoveUp, PlayerAction.MoveDown };

        for (int i = 0; i < actions.Length; i++)
        {
            PlayerAction action = actions[i];
            int actionIndex = (int)action;
            if (actionIndex < 0 || actionIndex >= previousMenuHeld.Length)
                continue;

            bool held = AnyPlayerGet(input, action);
            previousMenuHeld[actionIndex] = held;

            if (!held)
            {
                menuHoldConsumed[actionIndex] = false;
                nextMenuRepeatTime[actionIndex] = 0f;
            }
        }
    }

    private static bool AnyPlayerGet(PlayerInputManager input, PlayerAction action)
    {
        if (input == null)
            return false;

        for (int playerId = 1; playerId <= 6; playerId++)
        {
            if (input.Get(playerId, action))
                return true;
        }

        return false;
    }

    private void StartSelectMusic()
    {
        GameMusicController music = GameMusicController.Instance;
        if (selectMusic == null)
            return;

        if (selectMusic.loadState == AudioDataLoadState.Unloaded)
            selectMusic.LoadAudioData();

        if (music != null)
        {
            music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
            return;
        }

        EnsureLocalAudioSources();
        if (localMusicSource == null)
            return;

        localMusicSource.clip = selectMusic;
        localMusicSource.volume = selectMusicVolume;
        localMusicSource.loop = loopSelectMusic;
        localMusicSource.Play();
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlaySfx(clip, volume);
            return;
        }

        EnsureLocalAudioSources();
        if (localSfxSource != null)
            GameAudioSettings.PlaySfx(localSfxSource, clip, volume);
    }

    private void EnsureLocalAudioSources()
    {
        if (localMusicSource != null && localSfxSource != null)
            return;

        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length > 0)
            localMusicSource = sources[0];
        else
            localMusicSource = gameObject.AddComponent<AudioSource>();

        if (sources.Length > 1)
            localSfxSource = sources[1];
        else
            localSfxSource = gameObject.AddComponent<AudioSource>();

        localMusicSource.playOnAwake = false;
        localMusicSource.loop = true;
        localSfxSource.playOnAwake = false;
        localSfxSource.loop = false;
    }

    private void TickCursorAnimation()
    {
        if ((cursorImage == null && rightCursorImage == null) || itemSelectCursorSprites == null || itemSelectCursorSprites.Length == 0)
            return;

        cursorFrameTimer += Time.unscaledDeltaTime;
        if (cursorFrameTimer >= itemSelectCursorFrameSeconds)
        {
            cursorFrameTimer = 0f;
            int sequenceLength = itemSelectCursorFrameSequence != null && itemSelectCursorFrameSequence.Length > 0
                ? itemSelectCursorFrameSequence.Length
                : itemSelectCursorSprites.Length;
            cursorFrameIndex = (cursorFrameIndex + 1) % sequenceLength;
        }

        int spriteIndex = cursorFrameIndex;
        if (itemSelectCursorFrameSequence != null && itemSelectCursorFrameSequence.Length > 0)
            spriteIndex = itemSelectCursorFrameSequence[Mathf.Clamp(cursorFrameIndex, 0, itemSelectCursorFrameSequence.Length - 1)];

        Sprite sprite = itemSelectCursorSprites[Mathf.Clamp(spriteIndex, 0, itemSelectCursorSprites.Length - 1)];
        if (sprite == null)
            return;

        if (cursorImage != null)
            cursorImage.sprite = sprite;
        if (rightCursorImage != null)
            rightCursorImage.sprite = sprite;
    }

    private void TickBackgroundSpriteSwap()
    {
        if (backgroundImage == null)
        {
            return;
        }

        Sprite[] sprites = matchModeBackgrounds?.sprites;
        int validCount = GetValidSpriteCount(sprites);
        if (validCount <= 0)
        {
            return;
        }

        backgroundSwapTimer += Time.unscaledDeltaTime;
        float interval = Mathf.Max(0.01f, backgroundSwapInterval);
        while (backgroundSwapTimer >= interval)
        {
            backgroundSwapTimer -= interval;
            backgroundSpriteIndex++;

            if (backgroundSpriteIndex >= validCount)
                backgroundSpriteIndex = backgroundSwapLoop ? 0 : validCount - 1;
        }

        ApplyCurrentBackgroundSprite(false);
    }

    private void ApplyCurrentBackgroundSprite(bool force)
    {
        if (backgroundImage == null)
        {
            return;
        }

        Sprite[] sprites = matchModeBackgrounds?.sprites;
        int validCount = GetValidSpriteCount(sprites);
        if (validCount <= 0)
        {
            return;
        }

        Sprite sprite = GetSpriteFromSet(sprites, Mathf.Clamp(backgroundSpriteIndex, 0, validCount - 1));
        if (sprite != null && (force || backgroundImage.sprite != sprite))
        {
            backgroundImage.sprite = sprite;
            backgroundImage.color = Color.white;
            backgroundImage.enabled = true;
            backgroundImage.gameObject.SetActive(true);
            backgroundImage.transform.SetAsFirstSibling();
        }
    }

    private static int GetValidSpriteCount(Sprite[] sprites)
    {
        if (sprites == null)
            return 0;

        int count = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                count++;
        }

        return count;
    }

    private static Sprite GetSpriteFromSet(Sprite[] sprites, int index)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        List<Sprite> validSprites = null;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            validSprites ??= new List<Sprite>(sprites.Length);
            validSprites.Add(sprites[i]);
        }

        if (validSprites == null || validSprites.Count == 0)
            return null;

        return validSprites[Mathf.Clamp(index, 0, validSprites.Count - 1)];
    }

    private void ApplyDynamicScaleIfNeeded(bool force)
    {
        if (contentRoot == null && !force)
            return;

        int sw = Screen.width;
        int sh = Screen.height;
        Rect refPx = GetReferencePixelRect();

        bool changed =
            force ||
            sw != lastScreenW ||
            sh != lastScreenH ||
            !ApproximatelyRect(refPx, lastReferencePixelRect);

        if (!changed)
            return;

        lastScreenW = sw;
        lastScreenH = sh;
        lastReferencePixelRect = refPx;
        currentUiScale = ComputeUiScaleForRect(refPx.width, refPx.height);

        if (contentRoot != null)
            contentRoot.localScale = Vector3.one * currentUiScale;

    }

    private float ComputeUiScaleForRect(float usedW, float usedH)
    {
        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);
        float baseScaleRaw = Mathf.Min(sx, sy);
        float baseScaleForUi = useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f)
            baseScaleForUi = 1f;

        int baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));
        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        return Mathf.Clamp(normalized * Mathf.Max(0.01f, extraScaleMultiplier), minScale, maxScale);
    }

    private Rect GetReferencePixelRect()
    {
        if (referenceRect == null)
            return new Rect(0f, 0f, Screen.width, Screen.height);

        Canvas canvas = referenceRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        Vector3[] corners = new Vector3[4];
        referenceRect.GetWorldCorners(corners);
        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float xMin = Mathf.Min(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y);
        float width = Mathf.Abs(tr.x - bl.x);
        float height = Mathf.Abs(tr.y - bl.y);

        if (width <= 1f || height <= 1f)
            return new Rect(0f, 0f, Screen.width, Screen.height);

        return new Rect(xMin, yMin, width, height);
    }

    private static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    private static string DescribeRect(RectTransform rect)
    {
        if (rect == null)
            return "NULL";

        return $"anchorMin={rect.anchorMin} anchorMax={rect.anchorMax} anchored={rect.anchoredPosition} size={rect.sizeDelta} scale={rect.localScale}";
    }

}
