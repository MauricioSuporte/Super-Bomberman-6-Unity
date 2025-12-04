using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class StageIntroTransition : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitForSecondsRealtime2 = new(2f);
    public static StageIntroTransition Instance;

    [Header("Fade / Logo")]
    public Image fadeImage;
    public Image introLogoImage;

    [Header("Audio")]
    public AudioClip introMusic;
    public AudioClip hudsonFx;

    [Header("Title Screen (Video)")]
    public RawImage titleScreenRawImage;
    public VideoPlayer titleVideoPlayer;
    public AudioClip titleMusic;
    public KeyCode startKey = KeyCode.Return;

    [Header("Stage Intro")]
    public StageLabel stageLabel;
    public int world = 1;
    public int stageNumber = 1;

    [Header("First Stage")]
    public string firstStageSceneName = "Stage_1-1";

    [Header("Ending Screen")]
    public Image endingScreenImage;
    public AudioClip endingScreenMusic;
    public KeyCode restartKey = KeyCode.Return;

    [Header("Gameplay Root")]
    public GameObject gameplayRoot;

    public bool IntroRunning { get; private set; }

    static bool hasPlayedLogoIntro;
    static bool skipTitleNextRound;

    MovementController[] movementControllers;
    BombController[] bombControllers;

    public static void SkipTitleScreenOnNextLoad()
    {
        skipTitleNextRound = true;
        hasPlayedLogoIntro = true;
    }

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
            GameMusicController.Instance.StopMusic();

        IntroRunning = true;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        movementControllers = Object.FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        bombControllers = Object.FindObjectsByType<BombController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (var m in movementControllers) if (m) m.enabled = false;
        foreach (var b in bombControllers) if (b) b.enabled = false;

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 0f;

        if (gameplayRoot != null)
            gameplayRoot.SetActive(false);

        if (fadeImage != null)
        {
            var c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }

        if (endingScreenImage != null)
            endingScreenImage.gameObject.SetActive(false);

        if (skipTitleNextRound)
        {
            skipTitleNextRound = false;
            if (introLogoImage != null)
                introLogoImage.enabled = false;
            StartCoroutine(FadeInToGame());
            return;
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

        yield return FadeLogo(2f, 0f, 1f);
        yield return LogoWithHudsonFx();
        yield return FadeLogo(2f, 1f, 0f);

        introLogoImage.enabled = false;

        yield return ShowTitleScreen();
    }

    IEnumerator StageIntroOnlySequence()
    {
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

    IEnumerator ShowTitleScreen()
    {
        if (titleScreenRawImage == null || titleVideoPlayer == null)
        {
            yield return FadeInToGame();
            yield break;
        }

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        titleScreenRawImage.gameObject.SetActive(true);

        if (titleMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(titleMusic, 1f, true);

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

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        titleVideoPlayer.Stop();
        titleScreenRawImage.gameObject.SetActive(false);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
        }

        yield return FadeInToGame();
    }

    IEnumerator FadeInToGame()
    {
        if (introMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(introMusic, 1f);

        if (fadeImage == null)
        {
            if (gameplayRoot != null)
                gameplayRoot.SetActive(true);

            if (stageLabel != null)
            {
                stageLabel.gameObject.SetActive(true);
                stageLabel.SetStage(world, stageNumber);
            }

            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            EnableGameplay();
            yield break;
        }

        if (gameplayRoot != null)
            gameplayRoot.SetActive(true);

        float duration = 1f;
        float t = 0f;
        Color baseColor = fadeImage.color;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);

            if (stageLabel != null && !stageLabel.gameObject.activeSelf && t >= 0.5f)
            {
                stageLabel.gameObject.SetActive(true);
                stageLabel.SetStage(world, stageNumber);
            }

            yield return null;
        }

        fadeImage.gameObject.SetActive(false);

        yield return _waitForSecondsRealtime2;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        EnableGameplay();

        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.defaultMusic != null)
        {
            float volume = GameMusicController.Instance.defaultMusicVolume;

            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic,
                volume,
                true);
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

    public void StartEndingScreenSequence()
    {
        StopAllCoroutines();
        StartCoroutine(EndingScreenSequence());
    }

    IEnumerator EndingScreenSequence()
    {
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 0f;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        if (gameplayRoot != null)
            gameplayRoot.SetActive(false);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            var fc = fadeImage.color;
            fc.a = 1f;
            fadeImage.color = fc;
        }

        if (titleScreenRawImage != null)
            titleScreenRawImage.gameObject.SetActive(false);

        if (introLogoImage != null)
            introLogoImage.enabled = false;

        if (endingScreenImage != null)
        {
            endingScreenImage.enabled = true;
            endingScreenImage.gameObject.SetActive(true);

            Color c = endingScreenImage.color;
            c.a = 0f;
            endingScreenImage.color = c;

            if (endingScreenMusic != null && GameMusicController.Instance != null)
                GameMusicController.Instance.PlayMusic(endingScreenMusic, 1f, true);

            float duration = 2f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);

                c.a = progress;
                endingScreenImage.color = c;

                if (fadeImage != null)
                {
                    var fc = fadeImage.color;
                    fc.a = 1f - progress;
                    fadeImage.color = fc;
                }

                yield return null;
            }

            if (fadeImage != null)
                fadeImage.gameObject.SetActive(false);
        }
        else
        {
            if (endingScreenMusic != null && GameMusicController.Instance != null)
                GameMusicController.Instance.PlayMusic(endingScreenMusic, 1f, true);
        }

        bool pressed = false;

        while (!pressed)
        {
            if (Input.GetKeyDown(restartKey))
                pressed = true;

            yield return null;
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        if (endingScreenImage != null)
            endingScreenImage.gameObject.SetActive(false);

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        PlayerPersistentStats.ResetToDefaults();

        skipTitleNextRound = true;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        if (!string.IsNullOrEmpty(firstStageSceneName))
        {
            SceneManager.LoadScene(firstStageSceneName);
        }
        else
        {
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }
    }

}
