using System.Collections;
using Assets.Scripts.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ControlsMenuBootstrap : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] ControlsConfigMenu controlsMenu;

    [Header("Flow")]
    [SerializeField] string returnSceneName = "TitleScreen";
    [SerializeField, Range(1, 4)] int openerPlayerId = 1;

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        SaveSystem.LoadControlsIntoInputManager();

        StartCoroutine(RunFlow());
    }

    IEnumerator RunFlow()
    {
        if (controlsMenu == null)
            yield break;

        yield return controlsMenu.OpenRoutine(openerPlayerId, null, 0f);

        if (!string.IsNullOrEmpty(returnSceneName))
        {
            TitleScreenSkip.SkipNextIntro = true;
            SceneManager.LoadScene(returnSceneName);
        }
    }
}