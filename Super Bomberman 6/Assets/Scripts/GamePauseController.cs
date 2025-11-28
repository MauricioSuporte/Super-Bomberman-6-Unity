using UnityEngine;

public class GamePauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    public KeyCode pauseKey = KeyCode.Return;

    [Header("Pause SFX")]
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
        if (StageIntroTransition.Instance != null &&
            StageIntroTransition.Instance.IntroRunning)
            return;

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
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.ResumeMusic();
    }

    private void PlayPauseSfx()
    {
        if (pauseSfx == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(pauseSfx);
    }
}
