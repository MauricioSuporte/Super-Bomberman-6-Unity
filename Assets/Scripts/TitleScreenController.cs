using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class TitleScreenController : MonoBehaviour
{
    [Header("Menu SFX")]
    public AudioClip moveOptionSfx;
    [Range(0f, 1f)] public float moveOptionVolume = 1f;

    public AudioClip selectOptionSfx;
    [Range(0f, 1f)] public float selectOptionVolume = 1f;

    [Header("Back SFX")]
    [SerializeField] AudioClip backOptionSfx;
    [SerializeField, Range(0f, 1f)] float backOptionVolume = 1f;

    [Header("UI / Title")]
    public RawImage titleScreenRawImage;

    [Tooltip("Sprite 256x224 para a tela de título (pixel perfect).")]
    [SerializeField] Sprite titleScreenSprite;

    [Header("Menu Text (TMP)")]
    public TMP_Text menuText;

    [Header("Menu Layout (BASE @ designUpscale)")]
    [SerializeField] Vector2 menuAnchoredPos = new(-70f, 75f);
    [SerializeField] int menuFontSize = 46;

    [Header("Dynamic Scale (Pixel Perfect friendly)")]
    [SerializeField] bool dynamicScale = true;

    [SerializeField] int referenceWidth = 256;
    [SerializeField] int referenceHeight = 224;

    [Tooltip("If true, uses integer upscale steps like PixelPerfectCamera.")]
    [SerializeField] bool useIntegerUpscale = true;

    [Tooltip("The upscale you tuned your BASE sizes for. Example: FullHD looks like ~4x, set 4.")]
    [SerializeField, Min(1)] int designUpscale = 4;

    [Tooltip("Optional extra multiplier after normalization.")]
    [SerializeField, Min(0.01f)] float extraScaleMultiplier = 1f;

    [SerializeField, Min(0.01f)] float minScale = 0.5f;
    [SerializeField, Min(0.01f)] float maxScale = 10f;

    [Header("Text Style (SB5-like)")]
    [SerializeField] bool forceBold = true;

    [Header("Outline (TMP SDF)")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.42f;
    [SerializeField, Range(0f, 1f)] float outlineSoftness = 0.0f;

    [Header("Face Thickness (TMP SDF)")]
    [SerializeField, Range(-1f, 1f)] float faceDilate = 0.38f;
    [SerializeField, Range(0f, 1f)] float faceSoftness = 0.0f;

    [Header("Underlay (Shadow)")]
    [SerializeField] bool enableUnderlay = true;
    [SerializeField] Color underlayColor = new(0f, 0f, 0f, 1f);
    [SerializeField, Range(-1f, 1f)] float underlayDilate = 0.18f;
    [SerializeField, Range(0f, 1f)] float underlaySoftness = 0.0f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetX = 0.35f;
    [SerializeField, Range(-2f, 2f)] float underlayOffsetY = -0.35f;

    [Header("Audio")]
    public AudioClip titleMusic;
    [Range(0f, 1f)] public float titleMusicVolume = 1f;

    [Header("Exit")]
    public float exitDelayRealtime = 1f;

    [Header("Start Game Timing")]
    [SerializeField] float startGameFadeOutDuration = 0.25f;

    [Header("Cursor (AnimatedSpriteRenderer)")]
    public AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Vector2 cursorOffset = new(-30f, 0f);
    [SerializeField] bool cursorAsChildOfMenuText = true;

    [Header("Cursor Scaling")]
    [SerializeField] bool scaleCursorWithUi = true;

    [Header("Push Start (TMP)")]
    [SerializeField] TextMeshProUGUI pushStartText;
    [SerializeField] string pushStartLabel = "PUSH START BUTTON";
    [SerializeField] string pushStartHex = "#FFA621";
    [SerializeField] int pushStartFontSize = 46;
    [SerializeField] float pushStartBlinkInterval = 1f;
    [SerializeField] float pushStartYOffset = 18f;

    [Header("Controls Menu")]
    [SerializeField] ControlsConfigMenu controlsMenu;

    public bool ControlsRequested { get; private set; }

    RectTransform cursorRect;

    public bool Running { get; private set; }
    public bool NormalGameRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    enum MenuMode
    {
        Main = 0,
        PlayerCount = 1
    }

    MenuMode menuMode = MenuMode.Main;

    int menuIndex;
    bool locked;
    bool ignoreStartKeyUntilRelease;
    bool bootedSession;

    Material runtimeMenuMat;
    RectTransform menuRect;

    Coroutine pushStartRoutine;
    bool pushStartVisible = true;
    RectTransform pushStartRect;

    Rect _lastCamRect;
    int _lastBaseScaleInt = -999;
    float _lastUiScale = -999f;

    Vector3 _cursorBaseLocalScale = Vector3.one;
    bool _cursorBaseScaleCaptured;

    float UiScale
    {
        get
        {
            if (!dynamicScale)
            {
                if (menuText == null) return 1f;
                var c = menuText.canvas;
                if (c == null) return 1f;
                return Mathf.Max(0.01f, c.scaleFactor);
            }

            var cam = Camera.main;

            float usedW = (cam != null) ? cam.pixelRect.width : Screen.width;
            float usedH = (cam != null) ? cam.pixelRect.height : Screen.height;

            float sx = usedW / Mathf.Max(1f, referenceWidth);
            float sy = usedH / Mathf.Max(1f, referenceHeight);
            float baseScaleRaw = Mathf.Min(sx, sy);

            float baseScaleForUi = useIntegerUpscale ? Mathf.Round(baseScaleRaw) : baseScaleRaw;
            if (baseScaleForUi < 1f) baseScaleForUi = 1f;

            int baseScaleInt = Mathf.Max(1, Mathf.RoundToInt(baseScaleForUi));

            float normalized = baseScaleInt / Mathf.Max(1f, designUpscale);

            float ui = normalized * Mathf.Max(0.01f, extraScaleMultiplier);
            ui = Mathf.Clamp(ui, minScale, maxScale);

            _lastBaseScaleInt = baseScaleInt;
            _lastUiScale = ui;

            return ui;
        }
    }

    int ScaledFont(int baseSize) => Mathf.Clamp(Mathf.RoundToInt(baseSize * UiScale), 10, 500);
    float ScaledFloat(float baseValue) => baseValue * UiScale;
    Vector2 ScaledVec(Vector2 v) => v * UiScale;

    int MenuFontSizeScaled => ScaledFont(menuFontSize);
    int PushStartFontSizeScaled => ScaledFont(pushStartFontSize);
    float PushStartYOffsetScaled => ScaledFloat(pushStartYOffset);
    Vector2 MenuAnchoredPosScaled => ScaledVec(menuAnchoredPos);
    Vector2 CursorOffsetScaled => ScaledVec(cursorOffset);

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (menuText != null)
            menuRect = menuText.rectTransform;

        if (cursorRenderer != null)
        {
            cursorRect = cursorRenderer.GetComponent<RectTransform>();

            if (cursorAsChildOfMenuText && menuText != null)
                cursorRenderer.transform.SetParent(menuText.transform, false);

            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
        }

        EnsurePushStartText();
        ForceHide();
    }

    void OnEnable()
    {
        _lastCamRect = default;
        _lastBaseScaleInt = -999;
        _lastUiScale = -999f;
        ApplyDynamicLayoutIfNeeded(true);
    }

    void Update()
    {
        ApplyDynamicLayoutIfNeeded(false);
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);
    }

    void ApplyDynamicLayoutIfNeeded(bool force)
    {
        var cam = Camera.main;
        Rect r = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

        float ui = UiScale;

        bool rectChanged = r != _lastCamRect;
        bool scaleChanged = Mathf.Abs(ui - _lastUiScale) > 0.0001f;

        _lastCamRect = r;

        if (!force && !rectChanged && !scaleChanged)
            return;

        ApplyMenuAnchoredPosition();
        ApplyCursorScale();
        EnsurePushStartText();

        RefreshMenuText();
        UpdateCursorPosition();
        UpdatePushStartPosition();
    }

    void ApplyCursorScale()
    {
        if (!scaleCursorWithUi || cursorRenderer == null)
            return;

        if (!_cursorBaseScaleCaptured)
        {
            _cursorBaseLocalScale = cursorRenderer.transform.localScale;
            _cursorBaseScaleCaptured = true;
        }

        float s = UiScale;
        var baseScale = _cursorBaseLocalScale;
        cursorRenderer.transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
    }

    void EnsureBootSession()
    {
        if (bootedSession)
            return;

        PlayerPersistentStats.EnsureSessionBooted();
        bootedSession = true;
    }

    public void SetIgnoreStartKeyUntilRelease()
    {
        ignoreStartKeyUntilRelease = true;
    }

    void SetupMenuTextMaterial()
    {
        if (menuText == null)
            return;

        menuText.textWrappingMode = TextWrappingModes.NoWrap;
        menuText.overflowMode = TextOverflowModes.Overflow;
        menuText.extraPadding = true;

        if (forceBold)
            menuText.fontStyle |= FontStyles.Bold;

        ApplyMenuAnchoredPosition();

        Material baseMat = menuText.fontMaterial;
        if (baseMat == null)
            baseMat = menuText.fontSharedMaterial;

        if (baseMat == null && menuText.font != null)
            baseMat = menuText.font.material;

        if (baseMat == null)
            return;

        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);

        runtimeMenuMat = new Material(baseMat);

        TrySetColor(runtimeMenuMat, "_OutlineColor", outlineColor);
        TrySetFloat(runtimeMenuMat, "_OutlineWidth", outlineWidth);
        TrySetFloat(runtimeMenuMat, "_OutlineSoftness", outlineSoftness);

        TrySetFloat(runtimeMenuMat, "_FaceDilate", faceDilate);
        TrySetFloat(runtimeMenuMat, "_FaceSoftness", faceSoftness);

        if (enableUnderlay)
        {
            TrySetFloat(runtimeMenuMat, "_UnderlayDilate", underlayDilate);
            TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", underlaySoftness);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", underlayOffsetX);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", underlayOffsetY);
            TrySetColor(runtimeMenuMat, "_UnderlayColor", underlayColor);
        }
        else
        {
            TrySetFloat(runtimeMenuMat, "_UnderlayDilate", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlaySoftness", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetX", 0f);
            TrySetFloat(runtimeMenuMat, "_UnderlayOffsetY", 0f);
        }

        menuText.fontMaterial = runtimeMenuMat;
        menuText.UpdateMeshPadding();
        menuText.SetVerticesDirty();

        if (pushStartText != null)
            pushStartText.fontMaterial = runtimeMenuMat;
    }

    void ApplyMenuAnchoredPosition()
    {
        if (menuRect == null)
            menuRect = menuText != null ? menuText.rectTransform : null;

        if (menuRect == null)
            return;

        menuRect.anchoredPosition = MenuAnchoredPosScaled;
    }

    static void TrySetFloat(Material m, string prop, float value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetFloat(prop, value);
    }

    static void TrySetColor(Material m, string prop, Color value)
    {
        if (m != null && m.HasProperty(prop))
            m.SetColor(prop, value);
    }

    void EnsurePushStartText()
    {
        if (menuText == null)
            return;

        if (pushStartText == null)
        {
            var go = new GameObject("PushStartText", typeof(RectTransform));
            go.transform.SetParent(menuText.transform, false);

            pushStartText = go.AddComponent<TextMeshProUGUI>();
            pushStartText.raycastTarget = false;
        }

        pushStartRect = pushStartText.rectTransform;

        pushStartText.font = menuText.font;
        pushStartText.fontSize = PushStartFontSizeScaled;
        pushStartText.fontStyle = menuText.fontStyle;
        pushStartText.textWrappingMode = TextWrappingModes.NoWrap;
        pushStartText.overflowMode = TextOverflowModes.Overflow;
        pushStartText.extraPadding = true;

        pushStartText.fontMaterial = runtimeMenuMat != null ? runtimeMenuMat : menuText.fontMaterial;

        pushStartText.alignment = TextAlignmentOptions.Center;
        pushStartText.color = Color.white;
        pushStartText.text = $"<color={pushStartHex}>{pushStartLabel}</color>";
        pushStartText.gameObject.SetActive(false);
    }

    public void ForceHide()
    {
        Running = false;
        locked = false;

        NormalGameRequested = false;
        ExitRequested = false;
        ControlsRequested = false;

        menuMode = MenuMode.Main;

        StopPushStartBlink();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);
    }

    void ApplyTitleVisualNow()
    {
        if (titleScreenRawImage == null)
            return;

        titleScreenRawImage.gameObject.SetActive(true);

        if (titleScreenSprite != null)
        {
            var tex = titleScreenSprite.texture;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }

            titleScreenRawImage.texture = tex;

            var r = titleScreenSprite.textureRect;
            var tr = tex != null ? new Rect(0f, 0f, tex.width, tex.height) : r;
            var uv = tex != null && tr.width > 0f && tr.height > 0f
                ? new Rect(r.x / tr.width, r.y / tr.height, r.width / tr.width, r.height / tr.height)
                : new Rect(0f, 0f, 1f, 1f);

            titleScreenRawImage.uvRect = uv;
        }
    }

    void ShowTitleScreenNow()
    {
        ApplyTitleVisualNow();

        if (menuText != null)
        {
            menuText.gameObject.SetActive(true);
            SetupMenuTextMaterial();
        }

        EnsurePushStartText();

        if (cursorRenderer != null)
        {
            cursorRenderer.gameObject.SetActive(true);
            cursorRenderer.RefreshFrame();
        }

        ApplyDynamicLayoutIfNeeded(true);
    }

    bool TryGetAnyPlayerDown(PlayerAction action, out int pid)
    {
        pid = 1;

        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.GetDown(p, action))
            {
                pid = p;
                return true;
            }
        }

        return false;
    }

    bool TryGetAnyPlayerDownEither(PlayerAction a, PlayerAction b, out int pid)
    {
        if (TryGetAnyPlayerDown(a, out pid)) return true;
        if (TryGetAnyPlayerDown(b, out pid)) return true;
        pid = 1;
        return false;
    }

    bool AnyPlayerHeld(PlayerAction action)
    {
        var input = PlayerInputManager.Instance;
        if (input == null) return false;

        for (int p = 1; p <= 4; p++)
        {
            if (input.Get(p, action))
                return true;
        }

        return false;
    }

    bool AnyPlayerHeldAnyMenuKey()
    {
        return AnyPlayerHeld(PlayerAction.ActionA) ||
               AnyPlayerHeld(PlayerAction.ActionB) ||
               AnyPlayerHeld(PlayerAction.ActionC) ||
               AnyPlayerHeld(PlayerAction.Start) ||
               AnyPlayerHeld(PlayerAction.MoveUp) ||
               AnyPlayerHeld(PlayerAction.MoveDown) ||
               AnyPlayerHeld(PlayerAction.MoveLeft) ||
               AnyPlayerHeld(PlayerAction.MoveRight);
    }

    public IEnumerator Play(Image fadeToHideOptional)
    {
        EnsureBootSession();

        Running = true;
        locked = false;

        menuMode = MenuMode.Main;
        menuIndex = 0;

        NormalGameRequested = false;
        ExitRequested = false;
        ControlsRequested = false;

        if (fadeToHideOptional != null)
            fadeToHideOptional.gameObject.SetActive(false);

        ShowTitleScreenNow();

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, titleMusicVolume, true);

        if (ignoreStartKeyUntilRelease ||
            AnyPlayerHeld(PlayerAction.Start) ||
            AnyPlayerHeld(PlayerAction.ActionA))
        {
            ignoreStartKeyUntilRelease = false;

            while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                yield return null;

            yield return null;
        }

        RefreshMenuText();
        StartPushStartBlink();

        while (Running && !locked)
        {
            int itemCount = GetMenuItemCount();

            if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out _))
            {
                menuIndex = Wrap(menuIndex - 1, itemCount);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out _))
            {
                menuIndex = Wrap(menuIndex + 1, itemCount);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (menuMode == MenuMode.PlayerCount && TryGetAnyPlayerDown(PlayerAction.ActionB, out _))
            {
                PlayBackSfx();
                menuMode = MenuMode.Main;
                menuIndex = 0;
                RefreshMenuText();

                while (AnyPlayerHeld(PlayerAction.ActionB))
                    yield return null;

                yield return null;
                continue;
            }

            if (TryGetAnyPlayerDownEither(PlayerAction.Start, PlayerAction.ActionA, out int pidConfirm))
            {
                if (menuMode == MenuMode.Main)
                {
                    if (menuIndex == 0)
                    {
                        locked = true;
                        PlaySelectSfx();

                        if (cursorRenderer != null)
                            yield return cursorRenderer.PlayCycles(2);

                        menuMode = MenuMode.PlayerCount;
                        menuIndex = 0;
                        locked = false;

                        RefreshMenuText();

                        while (AnyPlayerHeld(PlayerAction.Start) || AnyPlayerHeld(PlayerAction.ActionA))
                            yield return null;

                        yield return null;
                        continue;
                    }

                    locked = true;
                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    if (menuIndex == 1)
                    {
                        ControlsRequested = true;

                        HideTitleScreenCompletely();

                        if (controlsMenu != null)
                            yield return controlsMenu.OpenRoutine(pidConfirm, titleMusic, titleMusicVolume);

                        RestoreTitleScreenAfterControls();

                        ControlsRequested = false;

                        locked = false;
                        menuMode = MenuMode.Main;
                        menuIndex = 0;

                        RefreshMenuText();
                        StartPushStartBlink();

                        while (AnyPlayerHeldAnyMenuKey())
                            yield return null;

                        yield return null;
                        continue;
                    }

                    ExitRequested = true;
                    yield return ExitGame();
                    yield break;
                }

                if (menuMode == MenuMode.PlayerCount)
                {
                    int chosenCount = menuIndex + 1;

                    if (GameSession.Instance != null)
                        GameSession.Instance.SetActivePlayerCount(chosenCount);

                    locked = true;
                    NormalGameRequested = true;

                    PlaySelectSfx();

                    if (cursorRenderer != null)
                        yield return cursorRenderer.PlayCycles(2);

                    yield return StartNormalGame();
                    yield break;
                }
            }

            yield return null;
        }
    }

    int GetMenuItemCount()
    {
        if (menuMode == MenuMode.PlayerCount)
            return 4;

        return 3;
    }

    IEnumerator StartNormalGame()
    {
        float d = Mathf.Max(0.01f, startGameFadeOutDuration);

        var transition = StageIntroTransition.Instance;
        if (transition != null)
            transition.StartFadeOut(d, false);

        yield return new WaitForSecondsRealtime(d);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);

        StopPushStartBlink();
        Running = false;
    }

    IEnumerator ExitGame()
    {
        float wait = Mathf.Max(0f, exitDelayRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        StopPushStartBlink();
        Running = false;
    }

    void RefreshMenuText()
    {
        if (menuText == null)
            return;

        const string color = "#FFFFE7";
        int size = MenuFontSizeScaled;

        if (menuMode == MenuMode.Main)
        {
            string normal = $"<color={color}>NORMAL GAME</color>";
            string controls = $"<color={color}>CONTROLS</color>";
            string exit = $"<color={color}>EXIT</color>";

            menuText.text =
                "<align=left>" +
                $"<size={size}>{normal}</size>\n" +
                $"<size={size}>{controls}</size>\n" +
                $"<size={size}>{exit}</size>" +
                "</align>";

            UpdateCursorPosition();
            UpdatePushStartPosition();
            return;
        }

        string p1 = $"<color={color}>1 PLAYER</color>";
        string p2 = $"<color={color}>2 PLAYERS</color>";
        string p3 = $"<color={color}>3 PLAYERS</color>";
        string p4 = $"<color={color}>4 PLAYERS</color>";

        menuText.text =
            "<align=left>" +
            $"<size={size}>{p1}</size>\n" +
            $"<size={size}>{p2}</size>\n" +
            $"<size={size}>{p3}</size>\n" +
            $"<size={size}>{p4}</size>" +
            "</align>";

        UpdateCursorPosition();
        UpdatePushStartPosition();
    }

    void StartPushStartBlink()
    {
        EnsurePushStartText();

        if (pushStartText == null)
            return;

        StopPushStartBlink();

        pushStartVisible = true;
        pushStartText.gameObject.SetActive(true);
        UpdatePushStartPosition();

        pushStartRoutine = StartCoroutine(PushStartBlinkRoutine());
    }

    void StopPushStartBlink()
    {
        if (pushStartRoutine != null)
        {
            StopCoroutine(pushStartRoutine);
            pushStartRoutine = null;
        }

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);
    }

    IEnumerator PushStartBlinkRoutine()
    {
        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, pushStartBlinkInterval));

        while (Running)
        {
            pushStartVisible = !pushStartVisible;

            if (pushStartText != null)
                pushStartText.gameObject.SetActive(pushStartVisible);

            yield return wait;
        }
    }

    void UpdatePushStartPosition()
    {
        if (menuText == null || pushStartRect == null)
            return;

        menuText.ForceMeshUpdate();
        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < ti.lineCount; i++)
        {
            var li = ti.lineInfo[i];
            minX = Mathf.Min(minX, li.lineExtents.min.x);
            maxX = Mathf.Max(maxX, li.lineExtents.max.x);
        }

        float centerX = (minX + maxX) * 0.5f;

        var first = ti.lineInfo[0];
        float y = first.ascender + PushStartYOffsetScaled;

        pushStartRect.localPosition = new Vector3(centerX, y, 0f);
    }

    void PlayMoveSfx()
    {
        if (moveOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(moveOptionSfx, moveOptionVolume);
    }

    void PlaySelectSfx()
    {
        if (selectOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(selectOptionSfx, selectOptionVolume);
    }

    void PlayBackSfx()
    {
        if (backOptionSfx == null || GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.PlaySfx(backOptionSfx, backOptionVolume);
    }

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
    }

    void UpdateCursorPosition()
    {
        if (menuText == null || cursorRenderer == null)
            return;

        menuText.ForceMeshUpdate();

        var ti = menuText.textInfo;
        if (ti == null || ti.lineCount <= 0)
            return;

        int line = Mathf.Clamp(menuIndex, 0, ti.lineCount - 1);
        var li = ti.lineInfo[line];

        float y = (li.ascender + li.descender) * 0.5f;
        float x = li.lineExtents.min.x;

        Vector2 offs = CursorOffsetScaled;

        Vector3 localPos = new(x + offs.x, y + offs.y, 0f);
        cursorRenderer.SetExternalBaseLocalPosition(localPos);
    }

    void HideTitleScreenCompletely()
    {
        StopPushStartBlink();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);

        if (pushStartText != null)
            pushStartText.gameObject.SetActive(false);
    }

    void RestoreTitleScreenAfterControls()
    {
        ShowTitleScreenNow();
    }
}