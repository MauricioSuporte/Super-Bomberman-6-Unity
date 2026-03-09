using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossRushMenu : MonoBehaviour
{
    const string LOG = "[BossRushMenu]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("UI Root")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

    [Header("Reference Frame")]
    [SerializeField] RectTransform referenceRect;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 1f;
    [SerializeField] float fadeOutOnConfirmDuration = 1.5f;

    [Header("Left Panel")]
    [SerializeField] BossRushLeftPanel leftPanel;

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

    public BossRushDifficulty SelectedDifficulty
    {
        get
        {
            if (leftPanel == null || leftPanel.Count == 0)
                return BossRushDifficulty.Normal;

            return leftPanel.GetDifficultyAt(selectedIndex);
        }
    }

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * _currentUiScale), 8, 300);

    void Awake()
    {
        if (root == null)
            root = gameObject;

        SLog($"Awake | root={(root != null ? root.name : "NULL")} leftPanel={(leftPanel != null ? leftPanel.name : "NULL")}");

        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.Initialize(_currentUiScale);

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

        if (leftPanel != null)
            leftPanel.UpdateDifficultyVisuals(selectedIndex, confirmed);
    }

    public void Hide()
    {
        menuActive = false;
        SLog("Hide | menuActive=false");

        if (leftPanel != null)
            leftPanel.HideCursor();

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

        if (leftPanel != null)
            leftPanel.BuildDifficultyList();

        selectedIndex = Mathf.Clamp((int)BossRushProgress.GetSelectedDifficulty(), 0, leftPanel != null ? Mathf.Max(0, leftPanel.Count - 1) : 0);
        SLog($"SelectDifficultyRoutine | selectedIndex={selectedIndex} difficulty={SelectedDifficulty}");

        if (leftPanel != null)
            leftPanel.ShowCursor();

        UpdateDifficultyVisuals();
        UpdateTopItems();
        UpdateBestTimes();

        if (leftPanel != null)
            leftPanel.DumpState("After Build + UpdateVisuals");

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
                    selectedIndex = leftPanel != null ? leftPanel.Count - 1 : 0;
                moved = true;
            }
            else if (input.GetDown(1, PlayerAction.MoveDown))
            {
                selectedIndex++;
                if (leftPanel != null && selectedIndex >= leftPanel.Count)
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

                if (leftPanel != null)
                    leftPanel.DumpState("After Move");
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

    void UpdateDifficultyVisuals()
    {
        if (leftPanel != null)
            leftPanel.UpdateDifficultyVisuals(selectedIndex, confirmed);
    }

    void ApplyScaledFontsToStaticTexts()
    {
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
            if (root != null) rt = root.GetComponent<RectTransform>();
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

        if (leftPanel != null)
            leftPanel.SetUiScale(_currentUiScale);
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}