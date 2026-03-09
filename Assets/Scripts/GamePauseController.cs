using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GamePauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    enum PauseExitTarget
    {
        None = 0,
        WorldMap = 1,
        TitleScreen = 2
    }

    [Header("Pause Availability")]
    private readonly string[] blockedSceneNames = { "TitleScreen", "WorldMap", "SkinSelect", "ControlsMenu", "BossRush" };

    [Header("SFX (Pause toggle)")]
    public AudioClip pauseSfx;
    public AudioSource sfxSource;

    [Header("Pause Menu SFX")]
    public AudioClip moveOptionSfx;
    [Range(0f, 1f)] public float moveOptionVolume = 1f;

    public AudioClip selectOptionSfx;
    [Range(0f, 1f)] public float selectOptionVolume = 1f;

    [Header("Pause Confirm Back SFX")]
    [SerializeField] AudioClip backConfirmSfx;
    [SerializeField, Range(0f, 1f)] float backConfirmVolume = 1f;

    [Header("Return / Exit")]
    [SerializeField] float returnToSceneDelayRealtime = 1f;
    [SerializeField] string worldMapSceneName = "WorldMap";
    [SerializeField] string titleSceneName = "TitleScreen";

    int menuIndex;
    bool confirmReturn;
    int confirmIndex;
    PauseExitTarget confirmTarget = PauseExitTarget.None;

    bool exitingToScene;
    Coroutine exitRoutine;

    int lastScreenW;
    int lastScreenH;

    public static GamePauseController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        exitingToScene = false;
        confirmReturn = false;
        confirmTarget = PauseExitTarget.None;
        menuIndex = 0;
        confirmIndex = 0;

        ClearPauseFlag();
        Time.timeScale = 1f;

        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.stageLabel != null)
            StageIntroTransition.Instance.stageLabel.gameObject.SetActive(false);

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
    }

    void Update()
    {
        if (exitingToScene)
            return;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        if (!IsPauseAllowedInCurrentScene())
        {
            if (IsPaused)
                ForceUnpause(resumeMusic: true);

            return;
        }

        if (!IsPaused)
        {
            if (IsStartPressed())
            {
                TogglePause();
                return;
            }

            return;
        }

        if (IsPauseBlockedByGameplayState())
            return;

        if (Screen.width != lastScreenW || Screen.height != lastScreenH)
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
            RefreshPauseUI();
        }

        TickPauseMenu();
    }

    bool IsPauseAllowedInCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        string sceneName = scene.name;
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (blockedSceneNames != null)
        {
            for (int i = 0; i < blockedSceneNames.Length; i++)
            {
                string blocked = blockedSceneNames[i];
                if (!string.IsNullOrEmpty(blocked) &&
                    string.Equals(sceneName, blocked, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    bool IsPauseBlockedByGameplayState()
    {
        if (StageIntroTransition.Instance != null)
        {
            if (StageIntroTransition.Instance.IntroRunning ||
                StageIntroTransition.Instance.EndingRunning)
                return true;
        }

        var players = FindObjectsByType<MovementController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null)
                continue;

            bool isPlayer = p.CompareTag("Player") || p.GetComponent<PlayerIdentity>() != null;
            if (!isPlayer)
                continue;

            if (p.isDead || p.IsHoleDeathInProgress || p.IsEndingStage)
                return true;
        }

        return false;
    }

    void TogglePause()
    {
        if (exitingToScene)
            return;

        if (!IsPauseAllowedInCurrentScene())
            return;

        if (IsPauseBlockedByGameplayState())
            return;

        IsPaused = !IsPaused;

        if (IsPaused)
            PauseGame();
        else
            ResumeGame();

        PlayPauseSfx();
    }

    void PauseGame()
    {
        Time.timeScale = 0f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.PauseMusic();

        menuIndex = 0;
        confirmReturn = false;
        confirmIndex = 0;
        confirmTarget = PauseExitTarget.None;

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        RefreshPauseUI();
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.ResumeMusic();

        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.stageLabel != null)
            StageIntroTransition.Instance.stageLabel.gameObject.SetActive(false);

        confirmReturn = false;
        confirmTarget = PauseExitTarget.None;
    }

    void TickPauseMenu()
    {
        if (exitingToScene)
            return;

        bool confirmPressed = IsStartPressed();

        if (!confirmReturn)
        {
            if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out _))
            {
                menuIndex = Wrap(menuIndex - 1, 3);
                PlayMoveSfx();
                RefreshPauseUI();
                return;
            }

            if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out _))
            {
                menuIndex = Wrap(menuIndex + 1, 3);
                PlayMoveSfx();
                RefreshPauseUI();
                return;
            }

            if (confirmPressed)
            {
                if (menuIndex == 0)
                {
                    TogglePause();
                    return;
                }

                confirmReturn = true;
                confirmIndex = 0;
                confirmTarget = menuIndex == 1
                    ? PauseExitTarget.WorldMap
                    : PauseExitTarget.TitleScreen;

                PlaySelectSfx();
                RefreshPauseUI();
                return;
            }

            return;
        }

        if (TryGetAnyPlayerDown(PlayerAction.MoveUp, out _))
        {
            confirmIndex = Wrap(confirmIndex - 1, 2);
            PlayMoveSfx();
            RefreshPauseUI();
            return;
        }

        if (TryGetAnyPlayerDown(PlayerAction.MoveDown, out _))
        {
            confirmIndex = Wrap(confirmIndex + 1, 2);
            PlayMoveSfx();
            RefreshPauseUI();
            return;
        }

        if (confirmPressed)
        {
            if (confirmIndex == 0)
            {
                confirmReturn = false;
                menuIndex = confirmTarget == PauseExitTarget.WorldMap ? 1 : 2;

                PlayBackConfirmSfx();
                RefreshPauseUI();
                return;
            }

            if (confirmTarget == PauseExitTarget.WorldMap)
            {
                BeginExitToScene(worldMapSceneName, resetSessionForTitle: false);
                return;
            }

            BeginExitToScene(titleSceneName, resetSessionForTitle: true);
        }
    }

    void BeginExitToScene(string sceneName, bool resetSessionForTitle)
    {
        if (exitingToScene)
            return;

        exitingToScene = true;

        IsPaused = true;
        Time.timeScale = 0f;

        PlaySelectSfx();

        if (exitRoutine != null)
            StopCoroutine(exitRoutine);

        exitRoutine = StartCoroutine(ExitToSceneRoutine(sceneName, resetSessionForTitle));
    }

    IEnumerator ExitToSceneRoutine(string sceneName, bool resetSessionForTitle)
    {
        float wait = Mathf.Max(0f, returnToSceneDelayRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        ForceUnpause(resumeMusic: false);

        exitingToScene = false;
        confirmReturn = false;
        confirmTarget = PauseExitTarget.None;

        if (resetSessionForTitle)
            PlayerPersistentStats.ResetSessionForReturnToTitle();

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    void RefreshPauseUI()
    {
        if (StageIntroTransition.Instance == null || StageIntroTransition.Instance.stageLabel == null)
            return;

        var label = StageIntroTransition.Instance.stageLabel;
        label.gameObject.SetActive(true);

        int w = StageIntroTransition.Instance.world;
        int s = StageIntroTransition.Instance.stageNumber;

        if (!confirmReturn)
        {
            label.SetPauseMenu(w, s, menuIndex);
            return;
        }

        if (confirmTarget == PauseExitTarget.WorldMap)
            label.SetPauseConfirmReturnToWorldMap(w, s, confirmIndex);
        else
            label.SetPauseConfirmReturnToTitle(w, s, confirmIndex);
    }

    void PlayPauseSfx()
    {
        if (pauseSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(pauseSfx);
    }

    void PlayMoveSfx()
    {
        if (moveOptionSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(moveOptionSfx, moveOptionVolume);
    }

    void PlaySelectSfx()
    {
        if (selectOptionSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(selectOptionSfx, selectOptionVolume);
    }

    void PlayBackConfirmSfx()
    {
        if (backConfirmSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(backConfirmSfx, backConfirmVolume);
    }

    int Wrap(int v, int count)
    {
        if (count <= 0) return 0;
        v %= count;
        if (v < 0) v += count;
        return v;
    }

    public static void ClearPauseFlag()
    {
        IsPaused = false;
    }

    public static void ForceUnpause()
    {
        ForceUnpause(resumeMusic: true);
    }

    public static void ForceUnpause(bool resumeMusic)
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (resumeMusic && GameMusicController.Instance != null)
            GameMusicController.Instance.ResumeMusic();

        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.stageLabel != null)
            StageIntroTransition.Instance.stageLabel.gameObject.SetActive(false);
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

    bool IsStartPressed()
    {
        var input = PlayerInputManager.Instance;
        bool startPressed = input != null && input.AnyGetDown(PlayerAction.Start);

        if (startPressed)
            return true;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }
}