using System.Collections;
using UnityEngine;

public class GamePauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Keys")]
    public KeyCode pauseKey = KeyCode.Return;
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode selectKey = KeyCode.Return;
    public KeyCode backKey = KeyCode.Escape;

    [Header("SFX (Pause toggle)")]
    public AudioClip pauseSfx;
    public AudioSource sfxSource;

    [Header("Pause Menu SFX")]
    public AudioClip moveOptionSfx;
    [Range(0f, 1f)] public float moveOptionVolume = 1f;

    public AudioClip selectOptionSfx;
    [Range(0f, 1f)] public float selectOptionVolume = 1f;

    [Header("Return To Title")]
    [SerializeField] float returnToTitleDelayRealtime = 1f;

    int menuIndex;
    bool confirmReturn;
    int confirmIndex;

    bool exitingToTitle;
    Coroutine exitRoutine;

    void Update()
    {
        if (exitingToTitle)
            return;

        if (!IsPaused)
        {
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
                return;
            }

            return;
        }

        if (StageIntroTransition.Instance != null)
        {
            if (StageIntroTransition.Instance.IntroRunning ||
                StageIntroTransition.Instance.EndingRunning)
                return;
        }

        TickPauseMenu();
    }

    void TogglePause()
    {
        if (exitingToTitle)
            return;

        if (StageIntroTransition.Instance != null)
        {
            if (StageIntroTransition.Instance.IntroRunning ||
                StageIntroTransition.Instance.EndingRunning)
                return;
        }

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
    }

    void TickPauseMenu()
    {
        if (exitingToTitle)
            return;

        if (!confirmReturn)
        {
            if (Input.GetKeyDown(upKey))
            {
                menuIndex = Wrap(menuIndex - 1, 2);
                PlayMoveSfx();
                RefreshPauseUI();
                return;
            }

            if (Input.GetKeyDown(downKey))
            {
                menuIndex = Wrap(menuIndex + 1, 2);
                PlayMoveSfx();
                RefreshPauseUI();
                return;
            }

            if (Input.GetKeyDown(backKey))
            {
                TogglePause();
                return;
            }

            if (Input.GetKeyDown(selectKey))
            {
                if (menuIndex == 0)
                {
                    TogglePause();
                    return;
                }

                confirmReturn = true;
                confirmIndex = 0;

                PlaySelectSfx();
                RefreshPauseUI();
                return;
            }

            return;
        }

        if (Input.GetKeyDown(upKey))
        {
            confirmIndex = Wrap(confirmIndex - 1, 2);
            PlayMoveSfx();
            RefreshPauseUI();
            return;
        }

        if (Input.GetKeyDown(downKey))
        {
            confirmIndex = Wrap(confirmIndex + 1, 2);
            PlayMoveSfx();
            RefreshPauseUI();
            return;
        }

        if (Input.GetKeyDown(backKey))
        {
            confirmReturn = false;
            menuIndex = 1;
            RefreshPauseUI();
            return;
        }

        if (Input.GetKeyDown(selectKey))
        {
            if (confirmIndex == 0)
            {
                confirmReturn = false;
                menuIndex = 1;
                PlaySelectSfx();
                RefreshPauseUI();
                return;
            }

            BeginExitToTitle();
        }
    }

    void BeginExitToTitle()
    {
        if (exitingToTitle)
            return;

        exitingToTitle = true;

        IsPaused = true;
        Time.timeScale = 0f;

        PlaySelectSfx();

        if (exitRoutine != null)
            StopCoroutine(exitRoutine);

        exitRoutine = StartCoroutine(ExitToTitleRoutine());
    }

    IEnumerator ExitToTitleRoutine()
    {
        float wait = Mathf.Max(0f, returnToTitleDelayRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        var transition = StageIntroTransition.Instance;
        if (transition != null)
        {
            transition.ReturnToTitleFromPause();
            yield break;
        }

        // fallback: só destrava (não deveria acontecer no seu fluxo)
        exitingToTitle = false;
        ForceUnpause();
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
            label.SetPauseMenu(w, s, menuIndex);
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
        IsPaused = false;
        Time.timeScale = 1f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.ResumeMusic();

        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.stageLabel != null)
            StageIntroTransition.Instance.stageLabel.gameObject.SetActive(false);
    }
}
