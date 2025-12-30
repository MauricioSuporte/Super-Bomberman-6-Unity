using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StageIntroTransition : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitForSecondsRealtime2 = new(2f);
    public static StageIntroTransition Instance;

    [Header("Fade")]
    public Image fadeImage;

    [Header("Hudson Logo")]
    public HudsonLogoIntro hudsonLogoIntro;

    [Header("Title Screen")]
    public TitleScreenController titleScreen;

    [Header("Audio")]
    public AudioClip introMusic;

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

    [Header("Only Stage_1-7")]
    public string stage17SceneName = "Stage_1-7";

    [Header("Skin Select")]
    public BomberSkinSelectMenu skinSelectMenu;

    public bool IntroRunning { get; private set; }
    public bool EndingRunning { get; private set; }

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

        if (titleScreen != null)
            titleScreen.startKey = (hudsonLogoIntro != null ? hudsonLogoIntro.skipKey : titleScreen.startKey);

        if (hudsonLogoIntro != null && titleScreen != null)
            hudsonLogoIntro.skipKey = titleScreen.startKey;

        if (titleScreen != null)
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();
    }

    void Start()
    {
        PlayerPersistentStats.LoadSelectedSkin();

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        IntroRunning = true;
        EndingRunning = false;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        movementControllers = Object.FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bombControllers = Object.FindObjectsByType<BombController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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

        if (titleScreen != null)
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();

        if (skipTitleNextRound)
        {
            skipTitleNextRound = false;
            StartCoroutine(FadeInToGame());
            return;
        }

        if (!hasPlayedLogoIntro && hudsonLogoIntro != null)
        {
            StartCoroutine(FullIntroSequence());
        }
        else
        {
            StartCoroutine(StageIntroOnlySequence());
        }
    }

    bool IsStage17()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == stage17SceneName;
    }

    IEnumerator FullIntroSequence()
    {
        hasPlayedLogoIntro = true;

        if (hudsonLogoIntro != null)
            yield return hudsonLogoIntro.Play();

        if (titleScreen != null && hudsonLogoIntro != null && hudsonLogoIntro.Skipped)
            titleScreen.SetIgnoreStartKeyUntilRelease();

        yield return ShowTitleScreen();
    }

    IEnumerator StageIntroOnlySequence()
    {
        yield return ShowTitleScreen();
    }

    IEnumerator ShowTitleScreen()
    {
        if (titleScreen == null)
        {
            yield return FadeInToGame();
            yield break;
        }

        yield return titleScreen.Play(fadeImage);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (skinSelectMenu != null)
        {
            yield return skinSelectMenu.SelectSkinRoutine();

            var chosen = skinSelectMenu.GetSelectedSkin();
            PlayerPersistentStats.Skin = chosen;
            PlayerPersistentStats.SaveSelectedSkin();

            SkipTitleScreenOnNextLoad();

            if (!string.IsNullOrEmpty(firstStageSceneName))
            {
                SceneManager.LoadScene(firstStageSceneName);
                yield break;
            }
        }

        yield return FadeInToGame();
    }

    IEnumerator FadeInToGame()
    {
        if (introMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(introMusic, 1f);

        if (gameplayRoot != null)
            gameplayRoot.SetActive(true);

        ApplyPersistentPlayerSkin();

        if (fadeImage == null)
        {
            if (stageLabel != null)
            {
                stageLabel.gameObject.SetActive(true);
                stageLabel.SetStage(world, stageNumber);
            }

            GamePauseController.ClearPauseFlag();
            Time.timeScale = 1f;
            EnableGameplay();

            if (!IsStage17())
                TryStartDefaultMusicNormalFlow();

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

        if (!IsStage17())
            TryStartDefaultMusicNormalFlow();
    }

    void TryStartDefaultMusicNormalFlow()
    {
        if (GameMusicController.Instance != null && GameMusicController.Instance.defaultMusic != null)
        {
            float volume = GameMusicController.Instance.defaultMusicVolume;

            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic,
                volume,
                true
            );
        }
    }

    void EnableGameplay()
    {
        foreach (var m in movementControllers) if (m) m.enabled = true;
        foreach (var b in bombControllers) if (b) b.enabled = true;
        IntroRunning = false;
    }

    public void StartEndingScreenSequence()
    {
        EndingRunning = true;

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

        if (titleScreen != null)
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();

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

        EndingRunning = false;

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

    void ApplyPersistentPlayerSkin()
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo == null)
            return;

        if (!playerGo.TryGetComponent<PlayerBomberSkinController>(out var skin))
            skin = playerGo.GetComponentInChildren<PlayerBomberSkinController>(true);

        if (skin != null)
            skin.ApplyCurrentSkin();
    }

    public void StartFadeOut(float fadeDuration)
    {
        if (StageMechaIntroController.Instance != null)
        {
            StageMechaIntroController.Instance.StartFadeOut(fadeDuration);
            return;
        }

        if (!fadeImage)
            return;

        fadeImage.gameObject.SetActive(true);

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    IEnumerator FadeOutRoutine(float fadeDuration)
    {
        if (!fadeImage)
            yield break;

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
