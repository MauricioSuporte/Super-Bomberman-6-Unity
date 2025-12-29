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

    [Header("Boss / Mecha")]
    public MechaBossSequence mechaBossSequence;

    [Header("Spotlight (Boss Intro)")]
    public Image spotlightImage;

    [Header("Spotlight Offset")]
    public float spotlightYOffsetWorld = -0.8f;

    [Header("Spotlight Ellipse")]
    public float spotlightEllipseX = 0.3f;
    public float spotlightEllipseY = 0.3f;

    [Header("Spotlight Fade")]
    public float spotlightFadeInDuration = 0.6f;

    [Header("Only Stage_1-7")]
    public string stage17SceneName = "Stage_1-7";

    Material spotlightMatInstance;

    public bool IntroRunning { get; private set; }
    public bool EndingRunning { get; private set; }
    public bool FlashRunning { get; private set; }

    static bool hasPlayedLogoIntro;
    static bool skipTitleNextRound;

    MovementController[] movementControllers;
    BombController[] bombControllers;

    bool defaultMusicStarted;

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

        if (mechaBossSequence == null)
            mechaBossSequence = FindFirstObjectByType<MechaBossSequence>();

        if (spotlightImage != null && spotlightImage.material != null)
        {
            spotlightMatInstance = Instantiate(spotlightImage.material);
            spotlightImage.material = spotlightMatInstance;

            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            spotlightImage.gameObject.SetActive(false);
        }

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
        defaultMusicStarted = false;

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

        if (skipTitleNextRound)
        {
            skipTitleNextRound = false;

            if (hudsonLogoIntro != null)
                hudsonLogoIntro.ForceHide();

            if (titleScreen != null)
                titleScreen.ForceHide();

            StartCoroutine(FadeInToGame());
            return;
        }

        if (!hasPlayedLogoIntro && hudsonLogoIntro != null)
        {
            StartCoroutine(FullIntroSequence());
        }
        else
        {
            if (hudsonLogoIntro != null)
                hudsonLogoIntro.ForceHide();

            StartCoroutine(StageIntroOnlySequence());
        }
    }

    bool IsStage17()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == stage17SceneName;
    }

    public void StartDefaultMusicOnce()
    {
        if (!IsStage17())
            return;

        if (defaultMusicStarted)
            return;

        defaultMusicStarted = true;

        if (GameMusicController.Instance != null && GameMusicController.Instance.defaultMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic,
                GameMusicController.Instance.defaultMusicVolume,
                true
            );
        }
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

    public IEnumerator Flash(float halfDuration, int cycles)
    {
        if (fadeImage == null)
            yield break;

        FlashRunning = true;

        try
        {
            fadeImage.gameObject.SetActive(true);

            Color baseColor = fadeImage.color;
            float blackHold = 0.5f;

            for (int i = 0; i < cycles; i++)
            {
                float t = 0f;
                while (t < halfDuration)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / halfDuration);
                    fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }

                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

                if (mechaBossSequence == null)
                    mechaBossSequence = FindFirstObjectByType<MechaBossSequence>();

                if (mechaBossSequence != null)
                {
                    bool empty = (i == cycles - 1) || (i % 2 == 0);
                    mechaBossSequence.SetStandsEmpty(empty);
                }

                float hold = 0f;
                while (hold < blackHold)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    hold += Time.deltaTime;
                    yield return null;
                }

                t = 0f;
                while (t < halfDuration)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    t += Time.deltaTime;
                    float a = 1f - Mathf.Clamp01(t / halfDuration);
                    fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }

                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                yield return null;
            }

            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            fadeImage.gameObject.SetActive(false);
        }
        finally
        {
            FlashRunning = false;
        }
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

    public void SetFullDarkness(float alpha)
    {
        if (spotlightImage == null || spotlightMatInstance == null) return;

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
        spotlightMatInstance.SetVector("_Center", new Vector4(-10f, -10f, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", 0.001f);
        spotlightMatInstance.SetFloat("_Softness", 0.001f);

        spotlightImage.gameObject.SetActive(true);
    }

    public void SetSpotlightWorld(Vector3 worldCenter, float radiusWorld, float darknessAlpha, float softnessWorld)
    {
        if (spotlightImage == null || spotlightMatInstance == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        worldCenter.y += spotlightYOffsetWorld;

        Vector3 vp = cam.WorldToViewportPoint(worldCenter);

        float radiusVp = WorldRadiusToViewportRadius(cam, worldCenter, Mathf.Max(0.01f, radiusWorld));
        float softVp = WorldRadiusToViewportRadius(cam, worldCenter, Mathf.Max(0.001f, softnessWorld));

        softVp = Mathf.Min(softVp, radiusVp * 0.25f);

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(darknessAlpha)));
        spotlightMatInstance.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", Mathf.Clamp(radiusVp, 0.001f, 2f));
        spotlightMatInstance.SetFloat("_Softness", Mathf.Clamp(softVp, 0.001f, 2f));

        spotlightImage.gameObject.SetActive(true);
    }

    public void DisableSpotlight()
    {
        if (spotlightImage != null)
            spotlightImage.gameObject.SetActive(false);
    }

    float WorldRadiusToViewportRadius(Camera cam, Vector3 worldCenter, float radiusWorld)
    {
        Vector3 a = worldCenter;
        Vector3 b = worldCenter + Vector3.right * radiusWorld;

        Vector3 av = cam.WorldToViewportPoint(a);
        Vector3 bv = cam.WorldToViewportPoint(b);

        return Mathf.Abs(bv.x - av.x);
    }

    public IEnumerator FadeToFullDarknessAndWait(float targetAlpha, float duration)
    {
        if (spotlightImage == null || spotlightMatInstance == null)
            yield break;

        float endA = Mathf.Clamp01(targetAlpha);
        float d = Mathf.Max(0.001f, duration);

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetVector("_Center", new Vector4(-10f, -10f, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", 0.001f);
        spotlightMatInstance.SetFloat("_Softness", 0.001f);

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
        spotlightImage.gameObject.SetActive(true);

        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, endA, Mathf.Clamp01(t / d));
            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, a));
            yield return null;
        }

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, endA));
    }

    public IEnumerator FadeSpotlightAlphaAndWait(float targetAlpha, float duration)
    {
        if (spotlightImage == null || spotlightMatInstance == null)
            yield break;

        float endA = Mathf.Clamp01(targetAlpha);
        float d = Mathf.Max(0.001f, duration);

        spotlightImage.gameObject.SetActive(true);

        float startA = spotlightMatInstance.GetColor("_Color").a;
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp01(t / d));
            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, a));
            yield return null;
        }

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, endA));
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
}
