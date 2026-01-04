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

    Coroutine fadeOutCoroutine;

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

    MovementController[] movementControllers = new MovementController[0];
    BombController[] bombControllers = new BombController[0];

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
            titleScreen.ForceHide();

        if (hudsonLogoIntro != null)
            hudsonLogoIntro.ForceHide();
    }

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        IntroRunning = true;
        EndingRunning = false;

        if (stageLabel != null)
            stageLabel.gameObject.SetActive(false);

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 0f;

        if (gameplayRoot != null)
            gameplayRoot.SetActive(false);

        RefreshControllers(includeInactive: true);

        DisableGameplayControllersAndHideSprites();

        if (fadeImage != null)
        {
            var c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }

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
            StartCoroutine(FullIntroSequence());
        else
            StartCoroutine(StageIntroOnlySequence());
    }

    void RefreshControllers(bool includeInactive)
    {
        var inactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        movementControllers = FindObjectsByType<MovementController>(inactive, FindObjectsSortMode.None);
        bombControllers = FindObjectsByType<BombController>(inactive, FindObjectsSortMode.None);
    }

    void DisableGameplayControllersAndHideSprites()
    {
        for (int i = 0; i < movementControllers.Length; i++)
        {
            var m = movementControllers[i];
            if (m == null) continue;

            m.enabled = false;
            m.SetAllSpritesVisible(false);
        }

        for (int i = 0; i < bombControllers.Length; i++)
        {
            var b = bombControllers[i];
            if (b == null) continue;

            b.enabled = false;
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

        var spawner = FindAnyObjectByType<PlayersSpawner>();
        if (spawner != null)
            spawner.SpawnNow();

        yield return null;

        RefreshControllers(includeInactive: false);

        ResyncSpawnedPlayersFromIdentity();
        ApplyPersistentPlayerSkin();

        for (int i = 0; i < movementControllers.Length; i++)
        {
            var m = movementControllers[i];
            if (m == null)
                continue;

            m.SyncMountedFromPersistent();

            if (IsStage17() && m.CompareTag("Player"))
            {
                m.SetAllSpritesVisible(false);

                if (m.TryGetComponent<PlayerLouieCompanion>(out var comp) && comp != null)
                    comp.SetMountedLouieVisible(false);

                continue;
            }

            m.EnableExclusiveFromState();
        }

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
        RefreshControllers(includeInactive: false);

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

        if (EndingScreenController.Instance != null)
            yield return EndingScreenController.Instance.Play(null);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        PlayerPersistentStats.ResetToDefaultsAll();

        hasPlayedLogoIntro = true;
        skipTitleNextRound = false;

        EndingRunning = false;

        if (titleScreen != null)
            titleScreen.SetIgnoreStartKeyUntilRelease();

        EndingRunning = false;
        yield break;
    }

    void ApplyPersistentPlayerSkin()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (id == null) continue;

            int playerId = Mathf.Clamp(id.playerId, 1, 4);
            var state = PlayerPersistentStats.Get(playerId);

            var skins = id.GetComponentsInChildren<PlayerBomberSkinController>(true);
            for (int s = 0; s < skins.Length; s++)
                if (skins[s] != null)
                    skins[s].Apply(state.Skin);
        }
    }

    void ResyncSpawnedPlayersFromIdentity()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (id == null) continue;

            int playerId = Mathf.Clamp(id.playerId, 1, 4);

            if (!id.TryGetComponent<MovementController>(out var move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!id.TryGetComponent<BombController>(out var bomb))
                bomb = id.GetComponentInChildren<BombController>(true);

            if (move != null)
                move.SetPlayerId(playerId);

            if (bomb != null)
                bomb.SetPlayerId(playerId);
        }
    }

    public void StartFadeOut(float fadeDuration)
    {
        StartFadeOut(fadeDuration, true);
    }

    public void StartFadeOut(float fadeDuration, bool stopOtherCoroutines)
    {
        if (StageMechaIntroController.Instance != null)
        {
            StageMechaIntroController.Instance.StartFadeOut(fadeDuration);
            return;
        }

        if (!fadeImage)
            return;

        fadeImage.gameObject.SetActive(true);
        fadeImage.transform.SetAsLastSibling();

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        if (stopOtherCoroutines)
            StopAllCoroutines();
        else if (fadeOutCoroutine != null)
            StopCoroutine(fadeOutCoroutine);

        fadeOutCoroutine = StartCoroutine(FadeOutRoutine(fadeDuration));
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

    public void ReturnToTitleFromPause()
    {
        StopAllCoroutines();

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        PlayerPersistentStats.ResetToDefaultsAll();

        if (titleScreen != null)
            titleScreen.SetIgnoreStartKeyUntilRelease();

        SceneManager.LoadScene(firstStageSceneName, LoadSceneMode.Single);
    }
}
