using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ControlsMenuBootstrap : MonoBehaviour
{
    const string LogPrefix = "[ControlsMenuBootstrap.Flow]";

    [Header("Controls")]
    [SerializeField] ControlsConfigMenu controlsMenu;

    [Header("Flow")]
    [SerializeField] string returnSceneName = "TitleScreen";
    [SerializeField, Range(1, 6)] int openerPlayerId = 1;

    void Start()
    {
        LogFlow("Start.Enter");
        PlayerPersistentStats.EnsureSessionBooted();
        LogFlow("After EnsureSessionBooted");
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        LogFlow("Before LoadControlsIntoInputManager");
        SaveSystem.LoadControlsIntoInputManager();
        LogFlow("After LoadControlsIntoInputManager");

        StartCoroutine(RunFlow());
        LogFlow("Start.Exit");
    }

    IEnumerator RunFlow()
    {
        LogFlow($"RunFlow.Enter controlsMenu={(controlsMenu != null ? controlsMenu.name : "null")}");
        if (controlsMenu == null)
            yield break;

        LogFlow("Before controlsMenu.OpenRoutine");
        yield return controlsMenu.OpenRoutine(openerPlayerId, null, 0f);
        LogFlow("After controlsMenu.OpenRoutine");

        if (!string.IsNullOrEmpty(returnSceneName))
        {
            TitleScreenSkip.SkipNextIntro = true;
            LogFlow($"Before LoadScene({returnSceneName})");
            SceneManager.LoadScene(returnSceneName);
        }
    }

    void LogFlow(string message)
    {
        Debug.Log(
            $"{LogPrefix} {message} | scene={SceneManager.GetActiveScene().name} frame={Time.frameCount} " +
            $"rt={Time.realtimeSinceStartup:0.000} unscaled={Time.unscaledTime:0.000} dt={Time.unscaledDeltaTime:0.0000}",
            this);
    }
}
