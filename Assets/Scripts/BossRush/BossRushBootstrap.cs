using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossRushBootstrap : MonoBehaviour
{
    [Header("Boss Rush")]
    [SerializeField] BossRushMenu bossRushMenu;

    [Header("Flow")]
    [SerializeField] string returnSceneName = "SkinSelect";
    [SerializeField] string nextSceneName = "BossRushStage01";

    [Header("Boss Rush Scene Identity")]
    [SerializeField] string bossRushSceneName = "BossRush";

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

        if (bossRushMenu.ReturnRequested)
        {
            if (!string.IsNullOrEmpty(returnSceneName))
            {
                string targetBossRushScene = !string.IsNullOrEmpty(bossRushSceneName)
                    ? bossRushSceneName
                    : SceneManager.GetActiveScene().name;

                SkinSelectFlowRouter.SetReturnToBossRush(targetBossRushScene);
                SceneManager.LoadScene(returnSceneName);
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