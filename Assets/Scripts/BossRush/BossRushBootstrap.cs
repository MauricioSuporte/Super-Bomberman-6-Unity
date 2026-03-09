using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossRushBootstrap : MonoBehaviour
{
    [Header("Boss Rush")]
    [SerializeField] BossRushMenu bossRushMenu;

    [Header("Flow")]
    [SerializeField] string titleSceneName = "TitleScreen";
    [SerializeField] string nextSceneName = "BossRushStage01";

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        StartCoroutine(RunFlow());
    }

    IEnumerator RunFlow()
    {
        if (bossRushMenu == null)
            yield break;

        yield return bossRushMenu.SelectDifficultyRoutine();

        if (bossRushMenu.ReturnToTitleRequested)
        {
            if (!string.IsNullOrEmpty(titleSceneName))
            {
                SceneManager.LoadScene(titleSceneName);
                yield break;
            }
        }

        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }
    }
}