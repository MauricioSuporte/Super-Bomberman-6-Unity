using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class StageIntroTransition : MonoBehaviour
{
    public static StageIntroTransition Instance;

    [Header("Fade / Logo")]
    public Image fadeImage;
    public Image introLogoImage;

    [Header("Audio")]
    public AudioClip introMusic;
    public AudioClip hudsonFx;

    [Header("Title Screen (Video)")]
    public RawImage titleScreenRawImage;      // RawImage que mostra o vídeo
    public VideoPlayer titleVideoPlayer;      // VideoPlayer com o MP4
    public AudioClip titleMusic;              // música da tela de título (opcional)
    public KeyCode startKey = KeyCode.Return; // tecla para "PRESS ENTER"

    public bool IntroRunning { get; private set; }

    static bool hasPlayedLogoIntro;

    MovementController[] movementControllers;
    BombController[] bombControllers;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
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

    IEnumerator FullIntroSequence()
    {
        hasPlayedLogoIntro = true;

        // Logo UDISOM
        yield return FadeLogo(2f, 0f, 1f);
        yield return LogoWithHudsonFx();
        yield return FadeLogo(2f, 1f, 0f);

        introLogoImage.enabled = false;

        // Depois do logo, sempre mostra a tela de título
        yield return ShowTitleScreen();
    }

    IEnumerator StageIntroOnlySequence()
    {
        // Quando recarrega o round, pula o logo mas mostra a tela de título
        yield return ShowTitleScreen();
    }

    IEnumerator FadeLogo(float time, float startA, float endA)
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

    IEnumerator LogoWithHudsonFx()
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

    /// <summary>
    /// Tela de título com vídeo + música, esperando o jogador apertar ENTER.
    /// </summary>
    IEnumerator ShowTitleScreen()
    {
        if (titleScreenRawImage == null || titleVideoPlayer == null)
        {
            // Se não estiver configurado, cai pro comportamento antigo
            yield return FadeInToGame();
            yield break;
        }

        // Garante que o fade não fique tampando a tela
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        // Ativa RawImage da tela de título
        titleScreenRawImage.gameObject.SetActive(true);

        // Música da tela de título (se quiser usar uma faixa separada)
        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, 1f, true);

        // Prepara vídeo
        titleVideoPlayer.isLooping = true;
        titleVideoPlayer.playOnAwake = false;
        titleVideoPlayer.Stop();
        titleVideoPlayer.Play();

        bool pressed = false;

        while (!pressed)
        {
            if (Input.GetKeyDown(startKey))
                pressed = true;

            yield return null;
        }

        // Parar música da tela de título
        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        // Parar vídeo
        titleVideoPlayer.Stop();
        titleScreenRawImage.gameObject.SetActive(false);

        // Volta o fade pra fazer o efeito de entrada
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
        }

        // Agora faz o fade-in e libera o jogo
        yield return FadeInToGame();
    }

    IEnumerator FadeInToGame()
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

        // "Get Ready!"
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

    void EnableGameplay()
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

    IEnumerator FadeOutRoutine(float fadeDuration)
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
