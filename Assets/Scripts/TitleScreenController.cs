using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
public class TitleScreenController : MonoBehaviour
{
    [Header("Menu SFX")]
    public AudioClip moveOptionSfx;
    [Range(0f, 1f)] public float moveOptionVolume = 1f;

    public AudioClip selectOptionSfx;
    [Range(0f, 1f)] public float selectOptionVolume = 1f;

    [Header("UI / Video")]
    public RawImage titleScreenRawImage;
    public VideoPlayer titleVideoPlayer;

    [Header("Menu Text (TMP)")]
    public TMP_Text menuText;

    [Header("Menu Layout")]
    [SerializeField] Vector2 menuAnchoredPos = new(-70f, 75f);
    [SerializeField] int menuFontSize = 46;

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

    int menuIndex;
    bool locked;
    bool ignoreStartKeyUntilRelease;
    bool bootedSession;

    Material runtimeMenuMat;
    RectTransform menuRect;

    Coroutine pushStartRoutine;
    bool pushStartVisible = true;
    RectTransform pushStartRect;

    static readonly WaitForSecondsRealtime _wait1s = new(1f);

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (titleVideoPlayer == null)
            titleVideoPlayer = GetComponent<VideoPlayer>();

        if (menuText != null)
            menuRect = menuText.rectTransform;

        if (cursorRenderer != null)
        {
            cursorRect = cursorRenderer.GetComponent<RectTransform>();

            if (cursorAsChildOfMenuText && menuText != null)
                cursorRenderer.transform.SetParent(menuText.transform, false);
        }

        EnsurePushStartText();
        ForceHide();
    }

    void OnDestroy()
    {
        if (runtimeMenuMat != null)
            Destroy(runtimeMenuMat);
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
    }

    void ApplyMenuAnchoredPosition()
    {
        if (menuRect == null)
            menuRect = menuText != null ? menuText.rectTransform : null;

        if (menuRect == null)
            return;

        menuRect.anchoredPosition = menuAnchoredPos;
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
        pushStartText.fontSize = pushStartFontSize;
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

        StopPushStartBlink();

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(false);
    }

    public IEnumerator Play(Image fadeToHideOptional)
    {
        EnsureBootSession();

        Running = true;
        locked = false;
        menuIndex = 0;

        NormalGameRequested = false;
        ExitRequested = false;

        if (fadeToHideOptional != null)
            fadeToHideOptional.gameObject.SetActive(false);

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(true);

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

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, titleMusicVolume, true);

        if (titleVideoPlayer != null)
        {
            titleVideoPlayer.isLooping = true;
            titleVideoPlayer.Stop();
            titleVideoPlayer.Play();
        }

        var input = PlayerInputManager.Instance;

        if (ignoreStartKeyUntilRelease ||
            input.Get(PlayerAction.Start) ||
            input.Get(PlayerAction.ActionA))
        {
            ignoreStartKeyUntilRelease = false;

            while (input.Get(PlayerAction.Start) || input.Get(PlayerAction.ActionA))
                yield return null;

            yield return null;
        }

        RefreshMenuText();
        StartPushStartBlink();

        while (Running && !locked)
        {
            if (input.GetDown(PlayerAction.MoveUp))
            {
                menuIndex = Wrap(menuIndex - 1, 3);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (input.GetDown(PlayerAction.MoveDown))
            {
                menuIndex = Wrap(menuIndex + 1, 3);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (input.GetDown(PlayerAction.Start) || input.GetDown(PlayerAction.ActionA))
            {
                locked = true;
                StopPushStartBlink();

                PlaySelectSfx();

                if (cursorRenderer != null)
                    yield return cursorRenderer.PlayCycles(2);

                if (menuIndex == 0)
                {
                    NormalGameRequested = true;
                    yield return StartNormalGame();
                    yield break;
                }

                if (menuIndex == 1)
                {
                    ControlsRequested = true;

                    HideTitleScreenCompletely();

                    if (controlsMenu != null)
                        yield return controlsMenu.OpenRoutine(titleMusic, titleMusicVolume);

                    RestoreTitleScreenAfterControls();

                    ControlsRequested = false;

                    locked = false;
                    RefreshMenuText();
                    StartPushStartBlink();

                    while (input.Get(PlayerAction.Start) ||
                           input.Get(PlayerAction.ActionA) ||
                           input.Get(PlayerAction.ActionB) ||
                           input.Get(PlayerAction.ActionC))
                        yield return null;

                    yield return null;
                    continue;
                }

                ExitRequested = true;
                yield return ExitGame();
                yield break;
            }

            yield return null;
        }
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

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

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

        string normal = $"<color={color}>NORMAL GAME</color>";
        string controls = $"<color={color}>CONTROLS</color>";
        string exit = $"<color={color}>EXIT</color>";

        menuText.text =
            "<align=left>" +
            $"<size={menuFontSize}>{normal}</size>\n" +
            $"<size={menuFontSize}>{controls}</size>\n" +
            $"<size={menuFontSize}>{exit}</size>" +
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

        while (Running && !locked)
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
        float y = first.ascender + pushStartYOffset;

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

        Vector3 localPos = new(x + cursorOffset.x, y + cursorOffset.y, 0f);
        cursorRenderer.SetExternalBaseLocalPosition(localPos);
    }

    void HideTitleScreenCompletely()
    {
        StopPushStartBlink();

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

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
        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(true);

        if (menuText != null)
            menuText.gameObject.SetActive(true);

        if (cursorRenderer != null)
            cursorRenderer.gameObject.SetActive(true);

        if (titleVideoPlayer != null)
        {
            titleVideoPlayer.isLooping = true;
            titleVideoPlayer.Stop();
            titleVideoPlayer.Play();
        }
    }
}
