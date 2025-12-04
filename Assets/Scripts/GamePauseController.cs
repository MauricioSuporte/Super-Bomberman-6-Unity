using UnityEngine;

public class GamePauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    public KeyCode pauseKey = KeyCode.Return;

    public AudioClip pauseSfx;
    public AudioSource sfxSource;

    public static void ClearPauseFlag()
    {
        IsPaused = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
            TogglePause();
    }

    private void TogglePause()
    {
        if (StageIntroTransition.Instance != null)
        {
            if (StageIntroTransition.Instance.IntroRunning ||
                StageIntroTransition.Instance.EndingRunning)
            {
                return;
            }
        }

        IsPaused = !IsPaused;

        if (IsPaused)
            PauseGame();
        else
            ResumeGame();

        PlayPauseSfx();
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.PauseMusic();

        if (StageIntroTransition.Instance != null &&
            StageIntroTransition.Instance.stageLabel != null)
        {
            var label = StageIntroTransition.Instance.stageLabel;
            label.gameObject.SetActive(true);
            label.SetPauseText(
                StageIntroTransition.Instance.world,
                StageIntroTransition.Instance.stageNumber
            );
        }
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.ResumeMusic();

        if (StageIntroTransition.Instance != null &&
            StageIntroTransition.Instance.stageLabel != null)
        {
            var label = StageIntroTransition.Instance.stageLabel;
            label.SetStage(
                StageIntroTransition.Instance.world,
                StageIntroTransition.Instance.stageNumber
            );

            label.gameObject.SetActive(false);
        }
    }

    private void PlayPauseSfx()
    {
        if (pauseSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(pauseSfx);
    }
}
