using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageIntroTransition : MonoBehaviour
{
    public static StageIntroTransition Instance;

    public Image fadeImage;
    public Image introLogoImage;

    public AudioClip introMusic;
    public AudioClip hudsonFx;

    public bool IntroRunning { get; private set; }

    static bool hasPlayedLogoIntro;

    MovementController[] movementControllers;
    BombController[] bombControllers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (GameMusicController.Instance != null)
        {
            GameMusicController.Instance.StopMusic();
        }

        IntroRunning = true;

        movementControllers = FindObjectsOfType<MovementController>();
        bombControllers = FindObjectsOfType<BombController>();

        foreach (var m in movementControllers) m.enabled = false;
        foreach (var b in bombControllers) b.enabled = false;

        Time.timeScale = 0f;

        if (fadeImage != null)
        {
            var c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }

        if (!hasPlayedLogoIntro && introLogoImage != null)
        {
            introLogoImage.enabled = true;
            introLogoImage.color = new Color(1f, 1f, 1f, 0f);
            StartCoroutine(FullIntroSequence());
        }
        else
        {
            if (introLogoImage != null)
                introLogoImage.enabled = false;

            StartCoroutine(StageIntroOnlySequence());
        }
    }

    private IEnumerator FullIntroSequence()
    {
        hasPlayedLogoIntro = true;

        yield return FadeLogo(2f, 0f, 1f);
        yield return LogoWithHudsonFx();
        yield return FadeLogo(2f, 1f, 0f);

        introLogoImage.enabled = false;

        yield return FadeInToGame();
    }

    private IEnumerator StageIntroOnlySequence()
    {
        yield return FadeInToGame();
    }

    private IEnumerator FadeLogo(float time, float startA, float endA)
    {
        if (!introLogoImage) yield break;

        float t = 0f;
        Color baseColor = introLogoImage.color;

        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startA, endA, t / time);
            introLogoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        introLogoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, endA);
    }

    private IEnumerator LogoWithHudsonFx()
    {
        float timer = 0f;

        if (hudsonFx != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(hudsonFx, 1f);

        while (timer < 2f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeInToGame()
    {
        if (introMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(introMusic, 1f);

        if (fadeImage == null)
        {
            Time.timeScale = 1f;
            EnableGameplay();
            yield break;
        }

        float duration = 1f;
        float t = 0f;
        Color baseColor = fadeImage.color;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        fadeImage.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(2f);

        Time.timeScale = 1f;
        EnableGameplay();

        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.defaultMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic, 1f, true);
        }
    }

    private void EnableGameplay()
    {
        foreach (var m in movementControllers) if (m) m.enabled = true;
        foreach (var b in bombControllers) if (b) b.enabled = true;
        IntroRunning = false;
    }

    public void StartFadeOut(float fadeDuration)
    {
        if (!fadeImage) return;

        fadeImage.gameObject.SetActive(true);

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    private IEnumerator FadeOutRoutine(float fadeDuration)
    {
        if (!fadeImage) yield break;

        float t = 0f;
        Color baseColor = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }
    }
}
