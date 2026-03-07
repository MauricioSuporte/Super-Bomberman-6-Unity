using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenBootstrap : MonoBehaviour
{
    public static TitleScreenBootstrap Instance;

    [Header("Fade")]
    [SerializeField] Image fadeImage;

    [Header("Hudson Logo")]
    [SerializeField] HudsonLogoIntro hudsonLogoIntro;

    [Header("Hudson Background")]
    [SerializeField] Image hudsonBackgroundImage;
    [SerializeField] bool showHudsonBackground = true;

    [Header("Title Screen")]
    [SerializeField] TitleScreenController titleScreen;

    [Header("Skin Select")]
    [SerializeField] BomberSkinSelectMenu skinSelectMenu;

    [Header("Flow")]
    [SerializeField] bool useWorldMapAfterSkinSelect = true;
    [SerializeField] string worldMapSceneName = "WorldMap";
    [SerializeField] string firstStageSceneName = "Stage_1-1";

    [Header("Hudson Fade")]
    [SerializeField, Min(0f)] float fadeOpenBeforeHudsonSeconds = 0.20f;
    [SerializeField, Min(0f)] float fadeCloseAfterHudsonSeconds = 0.12f;

    static bool hasPlayedLogoIntro;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (titleScreen != null)
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();

        SetHudsonBackgroundVisible(false);
    }

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        if (titleScreen != null)
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();

        SetHudsonBackgroundVisible(false);

        if (!hasPlayedLogoIntro && hudsonLogoIntro != null)
            StartCoroutine(FullIntroSequence());
        else
            StartCoroutine(ShowTitleScreen());
    }

    void SetHudsonBackgroundVisible(bool visible)
    {
        if (!showHudsonBackground)
            visible = false;

        if (hudsonBackgroundImage == null)
            return;

        hudsonBackgroundImage.gameObject.SetActive(visible);

        if (visible)
        {
            hudsonBackgroundImage.transform.SetAsFirstSibling();
            var c = hudsonBackgroundImage.color;
            c.a = 1f;
            hudsonBackgroundImage.color = c;
        }
    }

    IEnumerator FullIntroSequence()
    {
        hasPlayedLogoIntro = true;

        SetHudsonBackgroundVisible(true);

        if (fadeImage != null)
            yield return FadeAlphaRoutine(1f, 0f, fadeOpenBeforeHudsonSeconds);

        if (hudsonLogoIntro != null)
        {
            hudsonLogoIntro.gameObject.SetActive(true);
            hudsonLogoIntro.transform.SetAsLastSibling();
            yield return hudsonLogoIntro.Play();
        }

        if (titleScreen != null && hudsonLogoIntro != null && hudsonLogoIntro.Skipped)
            titleScreen.SetIgnoreStartKeyUntilRelease();

        if (fadeImage != null)
            yield return FadeAlphaRoutine(0f, 1f, fadeCloseAfterHudsonSeconds);

        SetHudsonBackgroundVisible(false);

        yield return ShowTitleScreen();
    }

    IEnumerator ShowTitleScreen()
    {
        if (titleScreen == null)
            yield break;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            SetFadeAlpha(1f);
        }

        SetHudsonBackgroundVisible(false);

        yield return titleScreen.Play(fadeImage);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (skinSelectMenu != null)
        {
            yield return skinSelectMenu.SelectSkinRoutine();

            if (skinSelectMenu.ReturnToTitleRequested)
            {
                if (titleScreen != null)
                    titleScreen.SetIgnoreStartKeyUntilRelease();

                yield return ShowTitleScreen();
                yield break;
            }

            int count = 1;
            if (GameSession.Instance != null)
                count = GameSession.Instance.ActivePlayerCount;

            for (int p = 1; p <= count; p++)
            {
                var chosen = skinSelectMenu.GetSelectedSkin(p);
                PlayerPersistentStats.Get(p).Skin = chosen;

                if (chosen != BomberSkin.Golden)
                    PlayerPersistentStats.SaveSelectedSkin(p);
            }

            PlayerPrefs.Save();

            if (useWorldMapAfterSkinSelect && !string.IsNullOrEmpty(worldMapSceneName))
            {
                SceneManager.LoadScene(worldMapSceneName);
                yield break;
            }

            if (!string.IsNullOrEmpty(firstStageSceneName))
            {
                SceneManager.LoadScene(firstStageSceneName);
                yield break;
            }
        }
    }

    void SetFadeAlpha(float a)
    {
        if (fadeImage == null)
            return;

        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    IEnumerator FadeAlphaRoutine(float from, float to, float seconds)
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);
        fadeImage.transform.SetAsLastSibling();

        float t = 0f;
        Color baseColor = fadeImage.color;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = seconds <= 0.0001f ? 1f : Mathf.Clamp01(t / seconds);
            float a = Mathf.Lerp(from, to, k);
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, to);
    }

    public static void ResetLogoState()
    {
        hasPlayedLogoIntro = false;
    }
}