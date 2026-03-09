using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossRushMenu : MonoBehaviour
{
    const string LOG = "[BossRushMenu]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;
    [SerializeField] bool dumpDifficultyLayoutEveryUpdate = false;

    [Header("UI Root")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

    [Header("Reference Frame")]
    [SerializeField] RectTransform referenceRect;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float fadeOutOnConfirmDuration = 1.5f;

    [Header("Difficulty List")]
    [SerializeField] RectTransform difficultyListRoot;
    [SerializeField] Text difficultyItemPrefab;
    [SerializeField] Color difficultyNormalColor = Color.white;
    [SerializeField] Color difficultySelectedColor = Color.yellow;
    [SerializeField] Color difficultyConfirmedColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] int fontSize = 18;
    [SerializeField] float difficultyItemHeight = 32f;
    [SerializeField] Vector2 difficultySpacing = new Vector2(0f, 10f);

    [Header("Best Times")]
    [SerializeField] Text bestTimesTitleText;
    [SerializeField] Text bestTimesBodyText;
    [SerializeField] string noTimesText = "No clear times recorded yet for this difficulty.";
    [SerializeField] int bestTimesTitleFontSize = 18;
    [SerializeField] int bestTimesBodyFontSize = 16;

    [Header("Top Items")]
    [SerializeField] Image bombAmountIcon;
    [SerializeField] Text bombAmountText;
    [SerializeField] Image fireBlastIcon;
    [SerializeField] Text fireBlastText;
    [SerializeField] Image speedIcon;
    [SerializeField] Text speedText;
    [SerializeField] Image heartIcon;
    [SerializeField] Text heartText;
    [SerializeField] int topItemsFontSize = 16;

    [Header("Background Sprite")]
    [SerializeField] Sprite[] backgroundSprites = new Sprite[2];
    [SerializeField] float backgroundSwapInterval = 2f;
    [SerializeField] bool backgroundSwapLoop = true;

    [Header("Music")]
    [SerializeField] AudioClip selectMusic;
    [SerializeField, Range(0f, 1f)] float selectMusicVolume = 1f;
    [SerializeField] bool loopSelectMusic = true;

    [Header("SFX")]
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorSfxVolume = 1f;
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmSfxVolume = 1f;
    [SerializeField] AudioClip returnSfx;
    [SerializeField, Range(0f, 1f)] float returnSfxVolume = 1f;

    [Header("Dynamic Scale (Pixel Perfect SNES)")]
    [SerializeField] bool dynamicScale = true;
    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;
    [SerializeField] bool useIntegerUpscale = true;
    [SerializeField, Min(1)] int designUpscale = 4;
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;
    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    readonly List<Text> difficultyTexts = new();
    readonly List<BossRushDifficulty> difficulties = new()
    {
        BossRushDifficulty.Easy,
        BossRushDifficulty.Normal,
        BossRushDifficulty.Hard,
        BossRushDifficulty.Nightmare
    };

    int selectedIndex;
    bool confirmed;
    bool menuActive;

    int backgroundSpriteIndex;
    float backgroundSwapTimer;

    Coroutine fadeInCoroutine;

    AudioClip previousClip;
    float previousVolume;
    bool previousLoop;
    bool capturedPreviousMusic;

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;
    int _lastScreenW = -1;
    int _lastScreenH = -1;
    Rect _lastCameraRect;
    Rect _lastRefPixelRect;

    public bool ReturnToTitleRequested { get; private set; }
    public BossRushDifficulty SelectedDifficulty => difficulties[Mathf.Clamp(selectedIndex, 0, difficulties.Count - 1)];

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);
    float ScaledFloat(float baseValue) => baseValue * _currentUiScale;

    void Awake()
    {
        if (root == null)
            root = gameObject;

        SLog($"Awake | root={(root != null ? root.name : "NULL")} difficultyListRoot={(difficultyListRoot != null ? difficultyListRoot.name : "NULL")} difficultyItemPrefab={(difficultyItemPrefab != null ? difficultyItemPrefab.name : "NULL")}");

        ApplyDynamicScaleIfNeeded(true);
        BuildDifficultyList();
        ApplyCurrentBackgroundSprite();

        if (root != null)
            root.SetActive(false);
    }

    void Update()
    {
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);
        TickBackgroundSpriteSwap();
        UpdateDifficultyVisuals();

        if (dumpDifficultyLayoutEveryUpdate)
            DumpDifficultyState("Update");
    }

    public void Hide()
    {
        menuActive = false;
        SLog("Hide | menuActive=false");

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public IEnumerator SelectDifficultyRoutine()
    {
        if (root == null)
            root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        SLog($"SelectDifficultyRoutine | root activated={root.activeInHierarchy}");

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
            SLog($"SelectDifficultyRoutine | fadeImage active={fadeImage.gameObject.activeInHierarchy}");
        }

        ReturnToTitleRequested = false;
        confirmed = false;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();
        ApplyDynamicScaleIfNeeded(true);
        BuildDifficultyList();

        selectedIndex = Mathf.Clamp((int)BossRushProgress.GetSelectedDifficulty(), 0, difficulties.Count - 1);
        SLog($"SelectDifficultyRoutine | selectedIndex={selectedIndex} difficulty={SelectedDifficulty}");

        UpdateDifficultyVisuals();
        UpdateTopItems();
        UpdateBestTimes();

        DumpDifficultyState("After Build + UpdateVisuals");

        StartSelectMusic();

        var input = PlayerInputManager.Instance;

        while (input != null &&
               (input.Get(1, PlayerAction.ActionA) ||
                input.Get(1, PlayerAction.ActionB) ||
                input.Get(1, PlayerAction.Start) ||
                input.Get(1, PlayerAction.MoveUp) ||
                input.Get(1, PlayerAction.MoveDown)))
        {
            yield return null;
        }

        yield return null;

        menuActive = true;
        SLog("SelectDifficultyRoutine | menuActive=true");

        if (fadeInCoroutine != null)
            StopCoroutine(fadeInCoroutine);

        fadeInCoroutine = StartCoroutine(FadeInRoutine());

        while (!confirmed)
        {
            if (input == null)
            {
                yield return null;
                continue;
            }

            bool moved = false;

            if (input.GetDown(1, PlayerAction.MoveUp))
            {
                selectedIndex--;
                if (selectedIndex < 0)
                    selectedIndex = difficulties.Count - 1;
                moved = true;
            }
            else if (input.GetDown(1, PlayerAction.MoveDown))
            {
                selectedIndex++;
                if (selectedIndex >= difficulties.Count)
                    selectedIndex = 0;
                moved = true;
            }

            if (moved)
            {
                SLog($"Input Move | selectedIndex={selectedIndex} difficulty={SelectedDifficulty}");
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                UpdateDifficultyVisuals();
                UpdateTopItems();
                UpdateBestTimes();
                DumpDifficultyState("After Move");
            }

            if (input.GetDown(1, PlayerAction.ActionB))
            {
                ReturnToTitleRequested = true;
                confirmed = true;
                SLog("Input Back | ReturnToTitleRequested=true");
                PlaySfx(returnSfx, returnSfxVolume);
                break;
            }

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
            {
                BossRushProgress.SetSelectedDifficulty(SelectedDifficulty);
                confirmed = true;
                SLog($"Input Confirm | saved difficulty={SelectedDifficulty}");
                PlaySfx(confirmSfx, confirmSfxVolume);
                break;
            }

            yield return null;
        }

        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }

        fadeDuration = fadeOutOnConfirmDuration;
        yield return FadeOutRoutine();

        StopSelectMusicAndRestorePrevious(restorePrevious: ReturnToTitleRequested);
        Hide();

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);
    }

    void BuildDifficultyList()
    {
        difficultyTexts.Clear();

        if (difficultyListRoot == null || difficultyItemPrefab == null)
        {
            SLog($"BuildDifficultyList ABORT | difficultyListRoot={(difficultyListRoot == null ? "NULL" : difficultyListRoot.name)} difficultyItemPrefab={(difficultyItemPrefab == null ? "NULL" : difficultyItemPrefab.name)}");
            return;
        }

        SLog($"BuildDifficultyList START | root={difficultyListRoot.name} childCount(before)={difficultyListRoot.childCount} prefabActiveSelf={difficultyItemPrefab.gameObject.activeSelf}");

        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = ScaledFloat(difficultySpacing.y);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            SLog($"BuildDifficultyList | VerticalLayoutGroup found spacing={layout.spacing} childAlignment={layout.childAlignment}");
        }
        else
        {
            SLog("BuildDifficultyList | VerticalLayoutGroup NOT FOUND on difficultyListRoot");
        }

        for (int i = difficultyListRoot.childCount - 1; i >= 0; i--)
        {
            var child = difficultyListRoot.GetChild(i);
            if (child == null)
                continue;

            if (child == difficultyItemPrefab.transform)
                continue;

            SLog($"BuildDifficultyList | Destroy old child='{child.name}' index={i}");
            Destroy(child.gameObject);
        }

        difficultyItemPrefab.gameObject.SetActive(false);

        for (int i = 0; i < difficulties.Count; i++)
        {
            var txt = Instantiate(difficultyItemPrefab, difficultyListRoot);
            txt.gameObject.SetActive(true);
            txt.enabled = true;
            txt.text = GetDifficultyDisplayName(difficulties[i]);
            txt.fontSize = ScaledFont(fontSize);
            txt.color = difficultyNormalColor;
            txt.transform.SetAsLastSibling();
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.resizeTextForBestFit = false;

            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);

            var le = txt.GetComponent<LayoutElement>();
            if (le == null)
                le = txt.gameObject.AddComponent<LayoutElement>();

            le.ignoreLayout = false;
            le.minHeight = ScaledFloat(difficultyItemHeight);
            le.preferredHeight = ScaledFloat(difficultyItemHeight);
            le.flexibleHeight = 0f;
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;

            difficultyTexts.Add(txt);

            SLog(
                $"BuildDifficultyList | Created index={i} name='{txt.name}' text='{txt.text}' " +
                $"activeSelf={txt.gameObject.activeSelf} activeInHierarchy={txt.gameObject.activeInHierarchy} " +
                $"parent='{txt.transform.parent.name}' siblingIndex={txt.transform.GetSiblingIndex()} " +
                $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} rect={rt.rect} " +
                $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} " +
                $"color={txt.color} font={(txt.font != null ? txt.font.name : "NULL")} fontSize={txt.fontSize}"
            );
        }

        ApplyScaledFontsToStaticTexts();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(difficultyListRoot);
        DumpDifficultyState("BuildDifficultyList END");
    }

    void ApplyScaledFontsToStaticTexts()
    {
        if (difficultyItemPrefab != null)
            difficultyItemPrefab.fontSize = ScaledFont(fontSize);

        if (bestTimesTitleText != null)
            bestTimesTitleText.fontSize = ScaledFont(bestTimesTitleFontSize);

        if (bestTimesBodyText != null)
            bestTimesBodyText.fontSize = ScaledFont(bestTimesBodyFontSize);

        if (bombAmountText != null)
            bombAmountText.fontSize = ScaledFont(topItemsFontSize);

        if (fireBlastText != null)
            fireBlastText.fontSize = ScaledFont(topItemsFontSize);

        if (speedText != null)
            speedText.fontSize = ScaledFont(topItemsFontSize);

        if (heartText != null)
            heartText.fontSize = ScaledFont(topItemsFontSize);
    }

    void UpdateDifficultyVisuals()
    {
        for (int i = 0; i < difficultyTexts.Count; i++)
        {
            var txt = difficultyTexts[i];
            if (txt == null)
            {
                SLog($"UpdateDifficultyVisuals | index={i} text=NULL");
                continue;
            }

            txt.fontSize = ScaledFont(fontSize);

            bool isSelected = i == selectedIndex;

            txt.text = isSelected
                ? $"> {GetDifficultyDisplayName(difficulties[i])}"
                : $"  {GetDifficultyDisplayName(difficulties[i])}";

            txt.color = confirmed && isSelected
                ? difficultyConfirmedColor
                : isSelected
                    ? difficultySelectedColor
                    : difficultyNormalColor;
        }
    }

    void UpdateTopItems()
    {
        int amount = BossRushProgress.GetStartingItemAmount(SelectedDifficulty);

        ApplyScaledFontsToStaticTexts();

        if (bombAmountText != null) bombAmountText.text = $"x{amount}";
        if (fireBlastText != null) fireBlastText.text = $"x{amount}";
        if (speedText != null) speedText.text = $"x{amount}";
        if (heartText != null) heartText.text = $"x{amount}";

        SLog($"UpdateTopItems | difficulty={SelectedDifficulty} amount={amount}");
    }

    void UpdateBestTimes()
    {
        ApplyScaledFontsToStaticTexts();

        if (bestTimesTitleText != null)
            bestTimesTitleText.text = $"Best Times - {GetDifficultyDisplayName(SelectedDifficulty)}";

        if (bestTimesBodyText == null)
            return;

        List<float> times = BossRushProgress.GetTopTimes(SelectedDifficulty);

        if (times == null || times.Count == 0)
        {
            bestTimesBodyText.text = noTimesText;
            SLog($"UpdateBestTimes | difficulty={SelectedDifficulty} no times");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        int max = Mathf.Min(3, times.Count);
        for (int i = 0; i < max; i++)
            sb.AppendLine($"{i + 1}. {BossRushProgress.FormatTime(times[i])}");

        bestTimesBodyText.text = sb.ToString().TrimEnd();
        SLog($"UpdateBestTimes | difficulty={SelectedDifficulty} count={times.Count}");
    }

    string GetDifficultyDisplayName(BossRushDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BossRushDifficulty.Easy: return "Easy";
            case BossRushDifficulty.Normal: return "Normal";
            case BossRushDifficulty.Hard: return "Hard";
            case BossRushDifficulty.Nightmare: return "Nightmare";
            default: return difficulty.ToString();
        }
    }

    void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        var music = GameMusicController.Instance;
        if (music == null)
            return;

        music.PlaySfx(clip, volume);
    }

    void StartSelectMusic()
    {
        var music = GameMusicController.Instance;
        if (music == null || selectMusic == null)
            return;

        if (!capturedPreviousMusic)
        {
            var src = music.GetComponent<AudioSource>();
            if (src != null)
            {
                previousClip = src.clip;
                previousVolume = src.volume;
                previousLoop = src.loop;
                capturedPreviousMusic = true;
            }
        }

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    void StopSelectMusicAndRestorePrevious(bool restorePrevious)
    {
        var music = GameMusicController.Instance;
        if (music == null)
            return;

        if (!restorePrevious)
        {
            music.StopMusic();
            return;
        }

        if (!capturedPreviousMusic)
        {
            music.StopMusic();
            return;
        }

        if (previousClip != null)
            music.PlayMusic(previousClip, previousVolume, previousLoop);
        else
            music.StopMusic();
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    IEnumerator FadeInRoutine()
    {
        if (fadeImage == null)
            yield break;

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    IEnumerator FadeOutRoutine()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        fadeImage.transform.SetAsLastSibling();
        SetFadeAlpha(0f);

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            SetFadeAlpha(a);
            yield return null;
        }

        SetFadeAlpha(1f);
    }

    void ResetBackgroundSpriteSwap()
    {
        backgroundSpriteIndex = 0;
        backgroundSwapTimer = 0f;
    }

    void TickBackgroundSpriteSwap()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        if (backgroundSprites.Length == 1)
        {
            if (backgroundImage.sprite != backgroundSprites[0])
                backgroundImage.sprite = backgroundSprites[0];
            return;
        }

        backgroundSwapTimer += Time.unscaledDeltaTime;

        if (backgroundSwapTimer < Mathf.Max(0.01f, backgroundSwapInterval))
            return;

        backgroundSwapTimer = 0f;
        backgroundSpriteIndex++;

        if (backgroundSpriteIndex >= backgroundSprites.Length)
        {
            if (backgroundSwapLoop)
                backgroundSpriteIndex = 0;
            else
                backgroundSpriteIndex = backgroundSprites.Length - 1;
        }

        ApplyCurrentBackgroundSprite();
    }

    void ApplyCurrentBackgroundSprite()
    {
        if (backgroundImage == null)
            return;

        if (backgroundSprites == null || backgroundSprites.Length == 0)
            return;

        backgroundSpriteIndex = Mathf.Clamp(backgroundSpriteIndex, 0, backgroundSprites.Length - 1);

        if (backgroundSprites[backgroundSpriteIndex] != null)
            backgroundImage.sprite = backgroundSprites[backgroundSpriteIndex];
    }

    Canvas GetRootCanvas()
    {
        if (root != null)
            return root.GetComponentInParent<Canvas>();

        return GetComponentInParent<Canvas>();
    }

    Camera GetMainCameraSafe()
    {
        var cam = Camera.main;
        if (cam != null) return cam;
        return FindFirstObjectByType<Camera>();
    }

    Rect GetReferencePixelRect(out string source)
    {
        source = "NONE";

        var canvas = GetRootCanvas();
        if (canvas == null)
        {
            source = "NO_CANVAS";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        RectTransform rt = referenceRect;
        if (rt == null)
        {
            if (difficultyListRoot != null) rt = difficultyListRoot;
            else if (root != null) rt = root.GetComponent<RectTransform>();
        }

        if (rt == null)
        {
            source = "FALLBACK_SCREEN";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        source = rt.name;

        Rect px = RectTransformUtility.PixelAdjustRect(rt, canvas);
        if (px.width <= 1f || px.height <= 1f)
        {
            var r = rt.rect;
            px = new Rect(0, 0, r.width, r.height);
            source += "+rt.rect";
        }
        else
        {
            source += "+PixelAdjustRect";
        }

        return px;
    }

    float ComputeUiScaleForRect(float usedW, float usedH, out int baseScaleInt)
    {
        baseScaleInt = 1;

        if (!dynamicScale)
            return 1f;

        float sx = usedW / Mathf.Max(1f, referenceWidth);
        float sy = usedH / Mathf.Max(1f, referenceHeight);

        float baseScaleRaw = Mathf.Min(sx, sy);
        float baseScaleForUi = useIntegerUpscale ? Mathf.Floor(baseScaleRaw) : baseScaleRaw;
        if (baseScaleForUi < 1f) baseScaleForUi = 1f;

        baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

        float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);
        float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
        ui = Mathf.Clamp(ui, minScale, maxScale);
        return ui;
    }

    void ApplyDynamicScaleIfNeeded(bool force = false)
    {
        int sw = Screen.width;
        int sh = Screen.height;

        var cam = GetMainCameraSafe();
        Rect camRect = cam != null ? cam.rect : new Rect(0, 0, 1, 1);
        Rect refPx = GetReferencePixelRect(out _);

        bool changed =
            force ||
            sw != _lastScreenW ||
            sh != _lastScreenH ||
            camRect != _lastCameraRect ||
            !ApproximatelyRect(refPx, _lastRefPixelRect);

        if (!changed)
            return;

        _lastScreenW = sw;
        _lastScreenH = sh;
        _lastCameraRect = camRect;
        _lastRefPixelRect = refPx;

        _currentUiScale = ComputeUiScaleForRect(refPx.width, refPx.height, out _currentBaseScaleInt);

        SLog($"ApplyDynamicScaleIfNeeded | uiScale={_currentUiScale:0.###} baseScaleInt={_currentBaseScaleInt} refPx={refPx}");

        ApplyScaledFontsToStaticTexts();

        if (difficultyTexts.Count > 0)
        {
            for (int i = 0; i < difficultyTexts.Count; i++)
            {
                if (difficultyTexts[i] == null)
                    continue;

                difficultyTexts[i].fontSize = ScaledFont(fontSize);

                var rt = difficultyTexts[i].rectTransform;
                rt.sizeDelta = new Vector2(0f, ScaledFloat(difficultyItemHeight));
            }

            var layout = difficultyListRoot != null ? difficultyListRoot.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
                layout.spacing = ScaledFloat(difficultySpacing.y);

            Canvas.ForceUpdateCanvases();
            if (difficultyListRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(difficultyListRoot);
        }
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    void DumpDifficultyState(string context)
    {
        if (!enableSurgicalLogs)
            return;

        if (difficultyListRoot == null)
        {
            Debug.Log($"{LOG} {context} | difficultyListRoot=NULL", this);
            return;
        }

        var rootRt = difficultyListRoot;
        var rootRect = rootRt.rect;
        var layout = difficultyListRoot.GetComponent<VerticalLayoutGroup>();

        Debug.Log(
            $"{LOG} {context} | " +
            $"difficultyListRoot='{difficultyListRoot.name}' activeSelf={difficultyListRoot.gameObject.activeSelf} activeInHierarchy={difficultyListRoot.gameObject.activeInHierarchy} " +
            $"childCount={difficultyListRoot.childCount} difficultyTexts.Count={difficultyTexts.Count} " +
            $"rect={rootRect} sizeDelta={rootRt.sizeDelta} anchoredPos={rootRt.anchoredPosition} " +
            $"anchorMin={rootRt.anchorMin} anchorMax={rootRt.anchorMax} pivot={rootRt.pivot} " +
            $"hasVerticalLayout={(layout != null)} uiScale={_currentUiScale:0.###}",
            this
        );

        for (int i = 0; i < difficultyTexts.Count; i++)
        {
            var txt = difficultyTexts[i];
            if (txt == null)
            {
                Debug.Log($"{LOG} {context} | item[{i}] = NULL", this);
                continue;
            }

            var go = txt.gameObject;
            var rt = txt.rectTransform;
            var col = txt.color;

            Debug.Log(
                $"{LOG} {context} | item[{i}] name='{go.name}' text='{txt.text}' " +
                $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} enabled={txt.enabled} " +
                $"rect={rt.rect} sizeDelta={rt.sizeDelta} anchoredPos={rt.anchoredPosition} " +
                $"anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} siblingIndex={go.transform.GetSiblingIndex()} " +
                $"color=({col.r:0.###},{col.g:0.###},{col.b:0.###},{col.a:0.###}) " +
                $"font={(txt.font != null ? txt.font.name : "NULL")} fontSize={txt.fontSize} " +
                $"canvasRendererCull={txt.canvasRenderer.cull}",
                this
            );
        }
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}