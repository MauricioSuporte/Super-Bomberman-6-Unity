using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveFileMenu : MonoBehaviour
{
    [Header("UI Root")]
    [SerializeField] GameObject root;
    [SerializeField] Image backgroundImage;

    [Header("Reference Frame")]
    [SerializeField] RectTransform referenceRect;

    [Header("Fade")]
    [SerializeField] Image fadeImage;
    [SerializeField] float fadeDuration = 0.75f;

    [Header("Left Panel")]
    [SerializeField] SaveFileMenuOptions leftPanel;

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
    [SerializeField] AudioClip deniedOptionSfx;
    [SerializeField, Range(0f, 1f)] float deniedOptionVolume = 1f;

    [Header("State")]
    [SerializeField] bool hasAnySaveFile = false;

    [Header("Disabled Option Message")]
    [SerializeField] TextMeshProUGUI disabledOptionText;
    [SerializeField] string disabledContinueMessage = "NO SAVE DATA";
    [SerializeField] string disabledDeleteMessage = "NO SAVE DATA";
    [SerializeField] float disabledMessageShowSeconds = 1.5f;
    [SerializeField] int disabledMessageFontSize = 22;
    [SerializeField] Color disabledMessageFaceColor = new Color32(231, 63, 63, 255);
    [SerializeField] Color disabledMessageOutlineColor = Color.black;
    [SerializeField] TMP_FontAsset disabledMessageFontAsset;
    [SerializeField] Material disabledMessageFontMaterialPreset;
    [SerializeField] bool forceDisabledMessageBold = true;
    [SerializeField] bool useDisabledMessageOutline = true;
    [SerializeField, Range(0f, 1f)] float disabledMessageOutlineWidth = 0.35f;
    [SerializeField, Range(0f, 1f)] float disabledMessageOutlineSoftness = 0f;
    [SerializeField, Range(-1f, 1f)] float disabledMessageFaceDilate = 0.2f;
    [SerializeField, Range(0f, 1f)] float disabledMessageFaceSoftness = 0f;
    [SerializeField] bool enableDisabledMessageUnderlay = true;
    [SerializeField] Color disabledMessageUnderlayColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float disabledMessageUnderlayDilate = 0.1f;
    [SerializeField, Range(0f, 1f)] float disabledMessageUnderlaySoftness = 0f;
    [SerializeField, Range(-2f, 2f)] float disabledMessageUnderlayOffsetX = 0.25f;
    [SerializeField, Range(-2f, 2f)] float disabledMessageUnderlayOffsetY = -0.25f;
    [SerializeField] float disabledMessageBottomMargin = 12f;

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
    Coroutine disabledMessageRoutine;

    float _currentUiScale = 1f;
    int _currentBaseScaleInt = 1;
    int _lastScreenW = -1;
    int _lastScreenH = -1;
    Rect _lastCameraRect;
    Rect _lastRefPixelRect;

    RectTransform disabledMessageRect;
    Material disabledMessageRuntimeMaterial;

    public SaveFileOption SelectedOption
    {
        get
        {
            if (leftPanel == null || leftPanel.Count == 0)
                return SaveFileOption.NewGame;

            return leftPanel.GetOptionAt(selectedIndex);
        }
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.Initialize(_currentUiScale);

        ApplyCurrentBackgroundSprite();
        EnsureDisabledOptionText();

        if (root != null)
            root.SetActive(false);
    }

    void Start()
    {
        StartCoroutine(OpenMenuRoutine());
    }

    void OnDestroy()
    {
        if (disabledMessageRuntimeMaterial != null)
            Destroy(disabledMessageRuntimeMaterial);
    }

    void Update()
    {
        if (!menuActive)
            return;

        ApplyDynamicScaleIfNeeded(false);
        TickBackgroundSpriteSwap();

        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed, hasAnySaveFile);
    }

    IEnumerator OpenMenuRoutine()
    {
        if (root == null)
            root = gameObject;

        root.transform.SetAsLastSibling();
        root.SetActive(true);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        EnsureDisabledOptionText();
        HideDisabledMessageImmediate();

        confirmed = false;
        menuActive = false;

        ResetBackgroundSpriteSwap();
        ApplyCurrentBackgroundSprite();
        ApplyDynamicScaleIfNeeded(true);

        if (leftPanel != null)
            leftPanel.BuildOptionList();

        selectedIndex = 0;

        if (leftPanel != null)
            leftPanel.ShowCursor();

        UpdateOptionVisuals();
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
                PlaySfx(moveCursorSfx, moveCursorSfxVolume);
                HideDisabledMessageImmediate();
                UpdateOptionVisuals();
            }

            if (input.GetDown(1, PlayerAction.ActionB))
            {
                PlaySfx(returnSfx, returnSfxVolume);
                confirmed = true;
                yield return FadeOutRoutine();
                Hide();

                // Ajuste aqui se quiser voltar para outra cena
                // SceneManager.LoadScene("TitleScreen");
                yield break;
            }

            if (input.GetDown(1, PlayerAction.ActionA) || input.GetDown(1, PlayerAction.Start))
            {
                if (!IsSelectedOptionEnabled())
                {
                    PlayDeniedSfx();
                    ShowDisabledMessageForCurrentOption();
                    yield return null;
                    continue;
                }

                PlaySfx(confirmSfx, confirmSfxVolume);
                confirmed = true;

                yield return FadeOutRoutine();
                Hide();

                HandleConfirmedOption();
                yield break;
            }

            yield return null;
        }
    }

    void HandleConfirmedOption()
    {
        switch (SelectedOption)
        {
            case SaveFileOption.NewGame:
                Debug.Log("[SaveFileMenu] Confirmed: New Game");
                break;

            case SaveFileOption.Continue:
                Debug.Log("[SaveFileMenu] Confirmed: Continue");
                break;

            case SaveFileOption.DeleteFile:
                Debug.Log("[SaveFileMenu] Confirmed: Delete File");
                break;
        }
    }

    bool IsSelectedOptionEnabled()
    {
        switch (SelectedOption)
        {
            case SaveFileOption.Continue:
            case SaveFileOption.DeleteFile:
                return hasAnySaveFile;

            default:
                return true;
        }
    }

    void UpdateOptionVisuals()
    {
        if (leftPanel != null)
            leftPanel.UpdateOptionVisuals(selectedIndex, confirmed, hasAnySaveFile);
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

        music.PlayMusic(selectMusic, selectMusicVolume, loopSelectMusic);
    }

    void PlayDeniedSfx()
    {
        PlaySfx(deniedOptionSfx, deniedOptionVolume);
    }

    public void SetHasAnySaveFile(bool value)
    {
        hasAnySaveFile = value;

        if (menuActive)
            UpdateOptionVisuals();
    }

    public void Hide()
    {
        menuActive = false;
        HideDisabledMessageImmediate();

        if (leftPanel != null)
            leftPanel.HideCursor();

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
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

        if (leftPanel != null)
            leftPanel.SetUiScale(_currentUiScale);

        ApplyDisabledMessageVisualStyle();
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return
            Mathf.Abs(a.x - b.x) < 0.01f &&
            Mathf.Abs(a.y - b.y) < 0.01f &&
            Mathf.Abs(a.width - b.width) < 0.01f &&
            Mathf.Abs(a.height - b.height) < 0.01f;
    }

    void EnsureDisabledOptionText()
    {
        if (root == null)
            return;

        if (disabledOptionText == null)
        {
            GameObject go = new GameObject("DisabledOptionText", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);

            disabledOptionText = go.AddComponent<TextMeshProUGUI>();
            disabledOptionText.raycastTarget = false;
        }

        disabledMessageRect = disabledOptionText.rectTransform;

        disabledMessageRect.anchorMin = new Vector2(0f, 0f);
        disabledMessageRect.anchorMax = new Vector2(1f, 0f);
        disabledMessageRect.pivot = new Vector2(0.5f, 0f);
        disabledMessageRect.anchoredPosition = new Vector2(0f, disabledMessageBottomMargin * _currentUiScale);
        disabledMessageRect.sizeDelta = new Vector2(0f, 0f);
        disabledMessageRect.localScale = Vector3.one;

        ApplyDisabledMessageVisualStyle();

        disabledOptionText.text = string.Empty;
        disabledOptionText.gameObject.SetActive(false);
    }

    void ShowDisabledMessageForCurrentOption()
    {
        EnsureDisabledOptionText();

        if (disabledOptionText == null)
            return;

        if (disabledMessageRoutine != null)
        {
            StopCoroutine(disabledMessageRoutine);
            disabledMessageRoutine = null;
        }

        string message = disabledContinueMessage;

        switch (SelectedOption)
        {
            case SaveFileOption.Continue:
                message = disabledContinueMessage;
                break;

            case SaveFileOption.DeleteFile:
                message = disabledDeleteMessage;
                break;
        }

        ApplyDisabledMessageVisualStyle();

        disabledOptionText.text = message;
        disabledOptionText.gameObject.SetActive(true);
        disabledOptionText.transform.SetAsLastSibling();

        disabledMessageRoutine = StartCoroutine(HideDisabledMessageRoutine());
    }

    IEnumerator HideDisabledMessageRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, disabledMessageShowSeconds));
        HideDisabledMessageImmediate();
    }

    void HideDisabledMessageImmediate()
    {
        if (disabledMessageRoutine != null)
        {
            StopCoroutine(disabledMessageRoutine);
            disabledMessageRoutine = null;
        }

        if (disabledOptionText != null)
            disabledOptionText.gameObject.SetActive(false);
    }

    void ApplyDisabledMessageVisualStyle()
    {
        if (disabledOptionText == null)
            return;

        if (disabledMessageFontAsset != null)
            disabledOptionText.font = disabledMessageFontAsset;

        disabledOptionText.alignment = TextAlignmentOptions.Center;
        disabledOptionText.textWrappingMode = TextWrappingModes.NoWrap;
        disabledOptionText.overflowMode = TextOverflowModes.Overflow;
        disabledOptionText.extraPadding = true;
        disabledOptionText.fontSize = Mathf.Clamp(Mathf.RoundToInt(disabledMessageFontSize * _currentUiScale), 8, 300);
        disabledOptionText.color = disabledMessageFaceColor;
        disabledOptionText.margin = Vector4.zero;
        disabledOptionText.raycastTarget = false;

        if (forceDisabledMessageBold)
            disabledOptionText.fontStyle |= FontStyles.Bold;
        else
            disabledOptionText.fontStyle &= ~FontStyles.Bold;

        Material runtimeMat = GetOrCreateDisabledMessageRuntimeMaterial();
        ApplyDisabledMessageMaterialStyle(runtimeMat);

        if (runtimeMat != null)
            disabledOptionText.fontMaterial = runtimeMat;

        disabledOptionText.UpdateMeshPadding();
        disabledOptionText.ForceMeshUpdate();
        disabledOptionText.SetVerticesDirty();
    }

    Material GetOrCreateDisabledMessageRuntimeMaterial()
    {
        if (disabledOptionText == null)
            return null;

        if (disabledMessageRuntimeMaterial != null)
            return disabledMessageRuntimeMaterial;

        Material baseMat = null;

        if (disabledMessageFontMaterialPreset != null)
            baseMat = disabledMessageFontMaterialPreset;
        else if (disabledOptionText.fontSharedMaterial != null)
            baseMat = disabledOptionText.fontSharedMaterial;
        else if (disabledOptionText.font != null)
            baseMat = disabledOptionText.font.material;

        if (baseMat == null)
            return null;

        disabledMessageRuntimeMaterial = new Material(baseMat);
        disabledMessageRuntimeMaterial.name = baseMat.name + "_SaveFileDisabledRuntime";
        return disabledMessageRuntimeMaterial;
    }

    void ApplyDisabledMessageMaterialStyle(Material mat)
    {
        if (mat == null)
            return;

        TrySetColor(mat, "_FaceColor", disabledMessageFaceColor);

        if (useDisabledMessageOutline)
        {
            TrySetColor(mat, "_OutlineColor", disabledMessageOutlineColor);
            TrySetFloat(mat, "_OutlineWidth", disabledMessageOutlineWidth);
            TrySetFloat(mat, "_OutlineSoftness", disabledMessageOutlineSoftness);
        }
        else
        {
            TrySetFloat(mat, "_OutlineWidth", 0f);
            TrySetFloat(mat, "_OutlineSoftness", 0f);
        }

        TrySetFloat(mat, "_FaceDilate", disabledMessageFaceDilate);
        TrySetFloat(mat, "_FaceSoftness", disabledMessageFaceSoftness);

        if (enableDisabledMessageUnderlay)
        {
            TrySetColor(mat, "_UnderlayColor", disabledMessageUnderlayColor);
            TrySetFloat(mat, "_UnderlayDilate", disabledMessageUnderlayDilate);
            TrySetFloat(mat, "_UnderlaySoftness", disabledMessageUnderlaySoftness);
            TrySetFloat(mat, "_UnderlayOffsetX", disabledMessageUnderlayOffsetX);
            TrySetFloat(mat, "_UnderlayOffsetY", disabledMessageUnderlayOffsetY);
        }
        else
        {
            TrySetFloat(mat, "_UnderlayDilate", 0f);
            TrySetFloat(mat, "_UnderlaySoftness", 0f);
            TrySetFloat(mat, "_UnderlayOffsetX", 0f);
            TrySetFloat(mat, "_UnderlayOffsetY", 0f);
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
}