using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Header("Boss Rush Loadouts")]
    [SerializeField] BossRushLoadoutPreset[] difficultyLoadouts;

    [Header("Left Panel")]
    [SerializeField] BossRushLeftPanel leftPanel;

    [Header("Right Panel")]
    [SerializeField] BossRushRightPanel rightPanel;

    [Header("Top Panel")]
    [SerializeField] BossRushTopPanel topPanel;

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

    [Header("Locked Difficulty")]
    [SerializeField] bool useSavedNightmareUnlock = true;
    [SerializeField] bool nightmareUnlocked = false;
    [SerializeField] AudioClip deniedOptionSfx;
    [SerializeField, Range(0f, 1f)] float deniedOptionVolume = 1f;
    [SerializeField] string nightmareLockedMessage = "UNLOCKED BY CLEARING HARD";

    [Header("Locked Difficulty Message UI")]
    [SerializeField] TextMeshProUGUI nightmareLockedText;
    [SerializeField] int nightmareLockedFontSize = 32;
    [SerializeField] string nightmareLockedMessageHex = "#E73F3F";
    [SerializeField] float nightmareLockedShowSeconds = 2f;
    [SerializeField] float nightmareLockedBottomMargin = 12f;

    [Header("Locked Difficulty Message TMP")]
    [SerializeField] TMP_FontAsset nightmareLockedFontAsset;
    [SerializeField] Material nightmareLockedFontMaterialPreset;
    [SerializeField] bool forceNightmareLockedBold = true;

    [Header("Locked Difficulty Message Colors")]
    [SerializeField] Color nightmareLockedFaceColor = new Color32(231, 63, 63, 255);
    [SerializeField] Color nightmareLockedOutlineColor = Color.black;

    [Header("Locked Difficulty Message TMP Outline")]
    [SerializeField] bool useNightmareLockedOutline = true;
    [SerializeField, Range(0f, 1f)] float nightmareLockedOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float nightmareLockedOutlineSoftness = 0f;

    [Header("Locked Difficulty Message TMP Face")]
    [SerializeField, Range(-1f, 1f)] float nightmareLockedFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float nightmareLockedFaceSoftness = 0f;

    [Header("Locked Difficulty Message TMP Underlay")]
    [SerializeField] bool enableNightmareLockedUnderlay = true;
    [SerializeField] Color nightmareLockedUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float nightmareLockedUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float nightmareLockedUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float nightmareLockedUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float nightmareLockedUnderlayOffsetY = -0.25f;

    [Header("Result Music - New Record")]
    [SerializeField] AudioClip newRecordIntroMusic;
    [SerializeField, Range(0f, 1f)] float newRecordIntroMusicVolume = 1f;
    [SerializeField] AudioClip newRecordLoopMusic;
    [SerializeField, Range(0f, 1f)] float newRecordLoopMusicVolume = 1f;

    [Header("Result Music - 2nd / 3rd Place")]
    [SerializeField] AudioClip top3IntroMusic;
    [SerializeField, Range(0f, 1f)] float top3IntroMusicVolume = 1f;
    [SerializeField] AudioClip top3LoopMusic;
    [SerializeField, Range(0f, 1f)] float top3LoopMusicVolume = 1f;

    [Header("Result Highlight")]
    [SerializeField] float newTopTimeBlinkSeconds = 5f;

    Coroutine resultMusicRoutine;

    bool hasMenuCelebrationResult;
    BossRushDifficulty menuCelebrationDifficulty;
    int menuCelebrationRank = -1;
    float menuCelebrationTime = -1f;

    int selectedIndex;
    bool confirmed;
    bool menuActive;

    int backgroundSpriteIndex;
    float backgroundSwapTimer;

    Coroutine fadeInCoroutine;
    Coroutine nightmareLockedMessageRoutine;

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

    BossRushDifficulty _lastTopDifficulty;
    bool _topDifficultyInitialized;

    RectTransform nightmareLockedRect;
    Material nightmareLockedRuntimeMaterial;

    public bool ReturnRequested { get; private set; }

    public BossRushDifficulty SelectedDifficulty
    {
        get
        {
            if (leftPanel == null || leftPanel.Count == 0)
                return BossRushDifficulty.NORMAL;

            return leftPanel.GetDifficultyAt(selectedIndex);
        }
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        SLog(
            $"Awake | root={(root != null ? root.name : "NULL")} " +
            $"leftPanel={(leftPanel != null ? leftPanel.name : "NULL")} " +
            $"rightPanel={(rightPanel != null ? rightPanel.name : "NULL")} " +
            $"topPanel={(topPanel != null ? topPanel.name : "NULL")}"
        );

        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.Initialize(_currentUiScale);

        if (rightPanel != null)
            rightPanel.Initialize(_currentUiScale);

        if (topPanel != null)
            topPanel.Initialize(_currentUiScale);

        ApplyCurrentBackgroundSprite();
        EnsureNightmareLockedText();

        if (root != null)
            root.SetActive(false);
    }

    void OnDestroy()
    {
        if (resultMusicRoutine != null)
            StopCoroutine(resultMusicRoutine);

        if (nightmareLockedRuntimeMaterial != null)
            Destroy(nightmareLockedRuntimeMaterial);
    }

    void Update()
    {
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);
        TickBackgroundSpriteSwap();
        RefreshDifficultyLocks();

        if (leftPanel != null)
            leftPanel.UpdateDifficultyVisuals(selectedIndex, confirmed);

        if (rightPanel != null)
            rightPanel.SetDifficulty(SelectedDifficulty);

        if (topPanel != null)
        {
            BossRushDifficulty current = SelectedDifficulty;
            if (!_topDifficultyInitialized || current != _lastTopDifficulty)
            {
                topPanel.SetDifficulty(current);
                _lastTopDifficulty = current;
                _topDifficultyInitialized = true;
            }
        }
    }

    public void Hide()
    {
        if (resultMusicRoutine != null)
        {
            StopCoroutine(resultMusicRoutine);
            resultMusicRoutine = null;
        }

        menuActive = false;
        SLog("Hide | menuActive=false");

        HideNightmareLockedMessageImmediate();

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

        EnsureNightmareLockedText();
        HideNightmareLockedMessageImmediate();

        ReturnRequested = false;
        confirmed = false;
        _topDifficultyInitialized = false;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.BuildDifficultyList();

        RefreshDifficultyLocks();

        selectedIndex = Mathf.Clamp(
            (int)BossRushProgress.GetSelectedDifficulty(),
            0,
            leftPanel != null ? Mathf.Max(0, leftPanel.Count - 1) : 0
        );

        SLog($"SelectDifficultyRoutine | selectedIndex={selectedIndex} difficulty={SelectedDifficulty}");

        if (leftPanel != null)
            leftPanel.ShowCursor();

        UpdateDifficultyVisuals();
        UpdateTopItems();
        UpdateBestTimes();

        if (leftPanel != null)
            leftPanel.DumpState("After Build + UpdateVisuals");

        ConsumeLastCompletedRunResult();
        ApplyCompletedRunVisualFeedback();
        StartMenuMusicFlow();

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

                HideNightmareLockedMessageImmediate();
                UpdateDifficultyVisuals();
                UpdateTopItems();
                UpdateBestTimes();

                if (leftPanel != null)
                    leftPanel.DumpState("After Move");
            }

            if (input.GetDown(1, PlayerAction.ActionB))
            {
                ReturnRequested = true;
                confirmed = true;
                BossRushSession.CancelRun();
                SLog("Input Back | ReturnToTitleRequested=true");
                PlaySfx(returnSfx, returnSfxVolume);
                break;
            }

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
            {
                if (!IsSelectedDifficultyUnlocked())
                {
                    SLog(
                        $"Input Confirm Denied | difficulty={SelectedDifficulty} locked " +
                        $"useSavedNightmareUnlock={useSavedNightmareUnlock} " +
                        $"nightmareUnlocked(serialized)={nightmareUnlocked} " +
                        $"nightmareUnlocked(resolved)={GetNightmareUnlocked()}"
                    );

                    PlayDeniedSfx();

                    if (!string.IsNullOrEmpty(nightmareLockedMessage))
                        ShowNightmareLockedMessage();
                    else
                        SLog("Input Confirm Denied | nightmareLockedMessage empty");

                    yield return null;
                    continue;
                }

                BossRushProgress.SetSelectedDifficulty(SelectedDifficulty);

                var preset = GetLoadoutPreset(SelectedDifficulty);
                BossRushSession.StartRun(SelectedDifficulty, preset);

                confirmed = true;
                SLog($"Input Confirm | saved difficulty={SelectedDifficulty} startScene={BossRushSession.GetCurrentStageSceneName()}");
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

        StopSelectMusicAndRestorePrevious(restorePrevious: ReturnRequested);
        Hide();

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (!ReturnRequested && BossRushSession.IsActive)
        {
            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            SceneManager.LoadScene(BossRushSession.GetCurrentStageSceneName());
        }
    }

    BossRushLoadoutPreset GetLoadoutPreset(BossRushDifficulty difficulty)
    {
        if (difficultyLoadouts == null || difficultyLoadouts.Length == 0)
            return null;

        for (int i = 0; i < difficultyLoadouts.Length; i++)
        {
            if (difficultyLoadouts[i] == null)
                continue;

            if (difficultyLoadouts[i].difficulty.Equals(difficulty))
                return difficultyLoadouts[i];
        }

        return null;
    }

    void UpdateDifficultyVisuals()
    {
        if (leftPanel != null)
            leftPanel.UpdateDifficultyVisuals(selectedIndex, confirmed);

        RefreshDifficultyLocks();
    }

    void UpdateTopItems()
    {
        if (topPanel == null)
            return;

        BossRushDifficulty current = SelectedDifficulty;
        topPanel.SetDifficulty(current);
        _lastTopDifficulty = current;
        _topDifficultyInitialized = true;
    }

    void UpdateBestTimes()
    {
        if (rightPanel != null)
            rightPanel.SetDifficulty(SelectedDifficulty);
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

        CapturePreviousMusicIfNeeded();
        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);

        SLog(
            $"StartSelectMusic | clip={(selectMusic != null ? selectMusic.name : "NULL")} " +
            $"volume={selectMusicVolume:0.##} loop={loopSelectMusic}"
        );
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
        if (cam != null)
            return cam;

        return FindFirstObjectByType<Camera>();
    }

    Rect GetReferencePixelRect(out string source)
    {
        source = "SCREEN";

        if (referenceRect == null)
            return new Rect(0, 0, Screen.width, Screen.height);

        Canvas canvas = GetRootCanvas();
        Camera cam = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : GetMainCameraSafe();

        Vector3[] corners = new Vector3[4];
        referenceRect.GetWorldCorners(corners);

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float xMin = Mathf.Min(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y);
        float xMax = Mathf.Max(bl.x, tr.x);
        float yMax = Mathf.Max(bl.y, tr.y);

        float width = xMax - xMin;
        float height = yMax - yMin;

        if (width <= 1f || height <= 1f)
        {
            source = "SCREEN_FALLBACK";
            return new Rect(0, 0, Screen.width, Screen.height);
        }

        source = "REFERENCE_WORLD_CORNERS";
        return new Rect(xMin, yMin, width, height);
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
        if (baseScaleForUi < 1f)
            baseScaleForUi = 1f;

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

        string refSource;
        Rect refPx = GetReferencePixelRect(out refSource);

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

        SLog(
            $"ApplyDynamicScaleIfNeeded | " +
            $"Screen=({Screen.width}x{Screen.height}) " +
            $"uiScale={_currentUiScale:0.###} " +
            $"baseScaleInt={_currentBaseScaleInt} " +
            $"refSource={refSource} refPx={refPx}"
        );

        if (leftPanel != null)
            leftPanel.SetUiScale(_currentUiScale);

        if (rightPanel != null)
            rightPanel.SetUiScale(_currentUiScale);

        if (topPanel != null)
            topPanel.SetUiScale(_currentUiScale);

        ApplyNightmareLockedTextVisualStyle();
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    bool IsSelectedDifficultyUnlocked()
    {
        if (SelectedDifficulty == BossRushDifficulty.NIGHTMARE)
            return GetNightmareUnlocked();

        return true;
    }

    void RefreshDifficultyLocks()
    {
        if (leftPanel != null)
            leftPanel.SetNightmareUnlocked(GetNightmareUnlocked());

        SLog(
            $"RefreshDifficultyLocks | " +
            $"useSavedNightmareUnlock={useSavedNightmareUnlock} " +
            $"nightmareUnlocked(serialized)={nightmareUnlocked} " +
            $"nightmareUnlocked(resolved)={GetNightmareUnlocked()}"
        );
    }

    void PlayDeniedSfx()
    {
        if (deniedOptionSfx == null)
        {
            SLog("PlayDeniedSfx | deniedOptionSfx=NULL");
            return;
        }

        var music = GameMusicController.Instance;
        if (music == null)
        {
            SLog("PlayDeniedSfx | GameMusicController.Instance=NULL");
            return;
        }

        music.PlaySfx(deniedOptionSfx, deniedOptionVolume);
        SLog($"PlayDeniedSfx | played volume={deniedOptionVolume:0.##}");
    }

    bool GetNightmareUnlocked()
    {
        if (useSavedNightmareUnlock)
            return BossRushDifficultyUnlocks.IsNightmareUnlocked();

        return nightmareUnlocked;
    }

    void EnsureNightmareLockedText()
    {
        if (root == null)
        {
            SLog("EnsureNightmareLockedText | root=NULL");
            return;
        }

        if (nightmareLockedText == null)
        {
            GameObject go = new GameObject("NightmareLockedText", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);

            nightmareLockedText = go.AddComponent<TextMeshProUGUI>();
            nightmareLockedText.raycastTarget = false;

            SLog("EnsureNightmareLockedText | created runtime TMP text");
        }

        nightmareLockedRect = nightmareLockedText.rectTransform;

        nightmareLockedRect.anchorMin = new Vector2(0f, 0f);
        nightmareLockedRect.anchorMax = new Vector2(1f, 0f);
        nightmareLockedRect.pivot = new Vector2(0.5f, 0f);
        nightmareLockedRect.anchoredPosition = new Vector2(0f, nightmareLockedBottomMargin * _currentUiScale);
        nightmareLockedRect.sizeDelta = new Vector2(0f, 0f);
        nightmareLockedRect.localScale = Vector3.one;

        if (nightmareLockedFontAsset == null)
            nightmareLockedFontAsset = Resources.Load<TMP_FontAsset>("Font/Retro Gaming SDF");

        ApplyNightmareLockedTextVisualStyle();

        nightmareLockedText.text = string.Empty;
        nightmareLockedText.gameObject.SetActive(false);

        SLog(
            $"EnsureNightmareLockedText | " +
            $"exists={(nightmareLockedText != null)} " +
            $"activeSelf={(nightmareLockedText != null && nightmareLockedText.gameObject.activeSelf)} " +
            $"anchoredPos={(nightmareLockedRect != null ? nightmareLockedRect.anchoredPosition.ToString() : "NULL")} " +
            $"fontSize={(nightmareLockedText != null ? nightmareLockedText.fontSize.ToString() : "NULL")} " +
            $"font={(nightmareLockedText != null && nightmareLockedText.font != null ? nightmareLockedText.font.name : "NULL")} " +
            $"rootActive={(root != null && root.activeInHierarchy)}"
        );
    }

    void ShowNightmareLockedMessage()
    {
        EnsureNightmareLockedText();

        if (nightmareLockedText == null)
        {
            SLog("ShowNightmareLockedMessage | nightmareLockedText=NULL");
            return;
        }

        if (nightmareLockedMessageRoutine != null)
        {
            StopCoroutine(nightmareLockedMessageRoutine);
            nightmareLockedMessageRoutine = null;
        }

        ApplyNightmareLockedTextVisualStyle();

        nightmareLockedText.text = nightmareLockedMessage;
        nightmareLockedText.gameObject.SetActive(true);
        nightmareLockedText.transform.SetAsLastSibling();

        SLog(
            $"ShowNightmareLockedMessage | " +
            $"text='{nightmareLockedMessage}' " +
            $"activeSelf={nightmareLockedText.gameObject.activeSelf} " +
            $"activeInHierarchy={nightmareLockedText.gameObject.activeInHierarchy} " +
            $"rootActive={(root != null && root.activeInHierarchy)} " +
            $"fontSize={nightmareLockedText.fontSize} " +
            $"font={(nightmareLockedText.font != null ? nightmareLockedText.font.name : "NULL")} " +
            $"anchoredPos={(nightmareLockedRect != null ? nightmareLockedRect.anchoredPosition.ToString() : "NULL")} " +
            $"sizeDelta={(nightmareLockedRect != null ? nightmareLockedRect.sizeDelta.ToString() : "NULL")}"
        );

        nightmareLockedMessageRoutine = StartCoroutine(HideNightmareLockedMessageRoutine());
    }

    void ApplyNightmareLockedTextVisualStyle()
    {
        if (nightmareLockedText == null)
            return;

        if (nightmareLockedFontAsset == null)
            nightmareLockedFontAsset = Resources.Load<TMP_FontAsset>("Font/Retro Gaming SDF");

        if (nightmareLockedFontAsset != null)
            nightmareLockedText.font = nightmareLockedFontAsset;

        nightmareLockedText.alignment = TextAlignmentOptions.Center;
        nightmareLockedText.textWrappingMode = TextWrappingModes.NoWrap;
        nightmareLockedText.overflowMode = TextOverflowModes.Overflow;
        nightmareLockedText.extraPadding = true;
        nightmareLockedText.fontSize = Mathf.Clamp(Mathf.RoundToInt(nightmareLockedFontSize * _currentUiScale), 8, 300);
        nightmareLockedText.color = nightmareLockedFaceColor;
        nightmareLockedText.margin = Vector4.zero;
        nightmareLockedText.raycastTarget = false;

        if (forceNightmareLockedBold)
            nightmareLockedText.fontStyle |= FontStyles.Bold;
        else
            nightmareLockedText.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateNightmareLockedRuntimeMaterial();
        ApplyNightmareLockedMaterialStyle(runtimeMat);

        if (runtimeMat != null)
            nightmareLockedText.fontMaterial = runtimeMat;

        nightmareLockedText.UpdateMeshPadding();
        nightmareLockedText.ForceMeshUpdate();
        nightmareLockedText.SetVerticesDirty();
    }

    Material GetOrCreateNightmareLockedRuntimeMaterial()
    {
        if (nightmareLockedText == null)
            return null;

        if (nightmareLockedRuntimeMaterial != null)
            return nightmareLockedRuntimeMaterial;

        Material baseMat = null;

        if (nightmareLockedFontMaterialPreset != null)
            baseMat = nightmareLockedFontMaterialPreset;
        else if (nightmareLockedText.fontSharedMaterial != null)
            baseMat = nightmareLockedText.fontSharedMaterial;
        else if (nightmareLockedText.font != null)
            baseMat = nightmareLockedText.font.material;

        if (baseMat == null)
            return null;

        nightmareLockedRuntimeMaterial = new Material(baseMat);
        nightmareLockedRuntimeMaterial.name = baseMat.name + "_BossRushNightmareLockedRuntime";
        return nightmareLockedRuntimeMaterial;
    }

    void ApplyNightmareLockedMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", nightmareLockedFaceColor);

        if (useNightmareLockedOutline)
        {
            TrySetColor(mat, "_OutlineColor", nightmareLockedOutlineColor);
            TrySetFloat(mat, "_OutlineWidth", nightmareLockedOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", nightmareLockedOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", nightmareLockedFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", nightmareLockedFaceSoftness);

        if (enableNightmareLockedUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", nightmareLockedUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", nightmareLockedUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", nightmareLockedUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", nightmareLockedUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", nightmareLockedUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
        }
    }

    IEnumerator HideNightmareLockedMessageRoutine()
    {
        float wait = Mathf.Max(0.05f, nightmareLockedShowSeconds);
        SLog($"HideNightmareLockedMessageRoutine | waiting {wait:0.##}s");

        yield return new WaitForSecondsRealtime(wait);

        HideNightmareLockedMessageImmediate();
    }

    void HideNightmareLockedMessageImmediate()
    {
        if (nightmareLockedMessageRoutine != null)
        {
            StopCoroutine(nightmareLockedMessageRoutine);
            nightmareLockedMessageRoutine = null;
        }

        if (nightmareLockedText != null)
        {
            nightmareLockedText.gameObject.SetActive(false);

            SLog(
                $"HideNightmareLockedMessageImmediate | hidden " +
                $"activeSelf={nightmareLockedText.gameObject.activeSelf}"
            );
        }
    }

    static void TrySetFloat(Material mat, string prop, float value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetFloat(prop, value);
    }

    static void TrySetColor(Material mat, string prop, Color value)
    {
        if (mat != null && mat.HasProperty(prop))
            mat.SetColor(prop, value);
    }

    void ConsumeLastCompletedRunResult()
    {
        hasMenuCelebrationResult = false;
        menuCelebrationRank = -1;
        menuCelebrationTime = -1f;
        menuCelebrationDifficulty = BossRushDifficulty.NORMAL;

        if (!BossRushSession.HasLastCompletedRun)
        {
            SLog("ConsumeLastCompletedRunResult | no completed run result");
            return;
        }

        hasMenuCelebrationResult = true;
        menuCelebrationDifficulty = BossRushSession.LastCompletedDifficulty;
        menuCelebrationRank = BossRushSession.LastCompletedRank;
        menuCelebrationTime = BossRushSession.LastCompletedTime;

        SLog(
            $"ConsumeLastCompletedRunResult | difficulty={menuCelebrationDifficulty} " +
            $"rank={menuCelebrationRank} time={BossRushProgress.FormatTime(menuCelebrationTime)}"
        );

        BossRushSession.ClearLastCompletedRun();
    }

    void ApplyCompletedRunVisualFeedback()
    {
        if (rightPanel == null)
            return;

        if (!hasMenuCelebrationResult || menuCelebrationRank < 0 || menuCelebrationRank > 2)
        {
            rightPanel.ClearNewTopTimeBlink();
            return;
        }

        rightPanel.StartNewTopTimeBlink(
            menuCelebrationDifficulty,
            menuCelebrationRank,
            Mathf.Max(0.1f, newTopTimeBlinkSeconds)
        );

        SLog(
            $"ApplyCompletedRunVisualFeedback | blink difficulty={menuCelebrationDifficulty} " +
            $"rank={menuCelebrationRank} duration={newTopTimeBlinkSeconds:0.##}"
        );
    }

    void StartMenuMusicFlow()
    {
        CapturePreviousMusicIfNeeded();

        if (resultMusicRoutine != null)
        {
            StopCoroutine(resultMusicRoutine);
            resultMusicRoutine = null;
        }

        if (!hasMenuCelebrationResult || menuCelebrationRank < 0 || menuCelebrationRank > 2)
        {
            StartSelectMusic();
            return;
        }

        if (menuCelebrationRank == 0)
        {
            resultMusicRoutine = StartCoroutine(
                PlayMenuMusicFlowRoutine(
                    newRecordIntroMusic,
                    newRecordIntroMusicVolume,
                    newRecordLoopMusic,
                    newRecordLoopMusicVolume
                )
            );

            SLog("StartMenuMusicFlow | using NEW RECORD music flow");
            return;
        }

        resultMusicRoutine = StartCoroutine(
            PlayMenuMusicFlowRoutine(
                top3IntroMusic,
                top3IntroMusicVolume,
                top3LoopMusic,
                top3LoopMusicVolume
            )
        );

        SLog("StartMenuMusicFlow | using TOP3 music flow");
    }

    IEnumerator PlayMenuMusicFlowRoutine(
        AudioClip introClip,
        float introVolume,
        AudioClip loopClip,
        float loopVolume)
    {
        var music = GameMusicController.Instance;
        if (music == null)
            yield break;

        if (introClip != null)
        {
            music.PlayMusic(introClip, introVolume, false);
            SLog($"PlayMenuMusicFlowRoutine | intro={introClip.name}");

            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, introClip.length));
        }

        if (loopClip != null)
        {
            music.PlayMusic(loopClip, loopVolume, true);
            SLog($"PlayMenuMusicFlowRoutine | loop={loopClip.name}");
            yield break;
        }

        if (introClip == null)
        {
            StartSelectMusic();
            yield break;
        }
    }

    void CapturePreviousMusicIfNeeded()
    {
        if (capturedPreviousMusic)
            return;

        var music = GameMusicController.Instance;
        if (music == null)
            return;

        var src = music.GetComponent<AudioSource>();
        if (src == null)
            return;

        previousClip = src.clip;
        previousVolume = src.volume;
        previousLoop = src.loop;
        capturedPreviousMusic = true;

        SLog(
            $"CapturePreviousMusicIfNeeded | " +
            $"clip={(previousClip != null ? previousClip.name : "NULL")} " +
            $"volume={previousVolume:0.##} loop={previousLoop}"
        );
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}