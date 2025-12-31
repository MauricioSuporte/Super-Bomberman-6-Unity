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
    [Tooltip("Posição do RectTransform do menu (anchoredPosition).")]
    [SerializeField] Vector2 menuAnchoredPos = new(-70f, 75f);

    [Tooltip("Tamanho do menu (antes era 42).")]
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

    [Header("Input")]
    public KeyCode startKey = KeyCode.Return;
    public KeyCode startKeyAlt = KeyCode.M;
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;

    [Header("Exit")]
    public float exitDelayRealtime = 1f;

    public bool Running { get; private set; }
    public bool NormalGameRequested { get; private set; }
    public bool ExitRequested { get; private set; }

    int menuIndex;
    bool locked;
    bool ignoreStartKeyUntilRelease;
    bool bootedSession;

    Material runtimeMenuMat;
    RectTransform menuRect;

    static readonly WaitForSecondsRealtime _wait1s = new(1f);

    void Awake()
    {
        if (titleScreenRawImage == null)
            titleScreenRawImage = GetComponent<RawImage>();

        if (titleVideoPlayer == null)
            titleVideoPlayer = GetComponent<VideoPlayer>();

        if (menuText != null)
            menuRect = menuText.rectTransform;

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

    public void ForceHide()
    {
        Running = false;
        locked = false;

        NormalGameRequested = false;
        ExitRequested = false;

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);
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

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, 1f, true);

        if (titleVideoPlayer != null)
        {
            titleVideoPlayer.isLooping = true;
            titleVideoPlayer.Stop();
            titleVideoPlayer.Play();
        }

        if (ignoreStartKeyUntilRelease || Input.GetKey(startKey) || Input.GetKey(startKeyAlt))
        {
            ignoreStartKeyUntilRelease = false;
            while (Input.GetKey(startKey) || Input.GetKey(startKeyAlt))
                yield return null;
            yield return null;
        }

        RefreshMenuText();

        while (Running && !locked)
        {
            if (Input.GetKeyDown(upKey))
            {
                menuIndex = Wrap(menuIndex - 1, 2);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (Input.GetKeyDown(downKey))
            {
                menuIndex = Wrap(menuIndex + 1, 2);
                PlayMoveSfx();
                RefreshMenuText();
            }

            if (Input.GetKeyDown(startKey) || Input.GetKeyDown(startKeyAlt))
            {
                locked = true;
                PlaySelectSfx();

                if (menuIndex == 0)
                {
                    NormalGameRequested = true;
                    yield return StartNormalGame();
                    yield break;
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
        yield return _wait1s;

        var transition = StageIntroTransition.Instance;
        if (transition != null)
            transition.StartFadeOut(1f, false);

        yield return _wait1s;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (titleVideoPlayer != null)
            titleVideoPlayer.Stop();

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (menuText != null)
            menuText.gameObject.SetActive(false);

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

        Running = false;
    }

    void RefreshMenuText()
    {
        if (menuText == null)
            return;

        string normal =
            menuIndex == 0
                ? "<color=#FF6F31>NORMAL GAME</color>"
                : "<color=#E8E8E8>NORMAL GAME</color>";

        string exit =
            menuIndex == 1
                ? "<color=#FF6F31>EXIT</color>"
                : "<color=#E8E8E8>EXIT</color>";

        menuText.text =
            "<align=left>" +
            $"<size={menuFontSize}>{normal}</size>\n" +
            $"<size={menuFontSize}>{exit}</size>" +
            "</align>";
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
}
