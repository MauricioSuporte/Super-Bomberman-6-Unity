using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossRushBootstrap : MonoBehaviour
{
    const string LOG = "[BossRushBootstrap]";

    [Header("Debug (Surgical Logs)")]
    [SerializeField] bool enableSurgicalLogs = true;

    [Header("Boss Rush")]
    [SerializeField] BossRushMenu bossRushMenu;

    [Header("Flow")]
    [SerializeField] string returnSceneName = "SkinSelect";

    [Header("Boss Rush Scene Identity")]
    [SerializeField] string bossRushSceneName = "BossRush";

    void Start()
    {
        SLog($"Start | scene={SceneManager.GetActiveScene().name}");

        PlayerPersistentStats.EnsureSessionBooted();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        SLog($"Start | timer overlay ensured | BossRushActive={BossRushSession.IsActive}");

        StartCoroutine(RunFlow());
    }

    IEnumerator RunFlow()
    {
        if (bossRushMenu == null)
        {
            SLog("RunFlow | bossRushMenu=NULL");
            yield break;
        }

        SLog("RunFlow | opening boss rush difficulty menu");
        yield return bossRushMenu.SelectDifficultyRoutine();

        SLog(
            $"RunFlow | menu finished | ReturnRequested={bossRushMenu.ReturnRequested} " +
            $"BossRushActive={BossRushSession.IsActive} " +
            $"CurrentStage={BossRushSession.GetCurrentStageSceneName()} " +
            $"Elapsed={BossRushSession.GetFormattedElapsed()}"
        );

        if (bossRushMenu.ReturnRequested)
        {
            if (!string.IsNullOrEmpty(returnSceneName))
            {
                string targetBossRushScene = !string.IsNullOrEmpty(bossRushSceneName)
                    ? bossRushSceneName
                    : SceneManager.GetActiveScene().name;

                SLog($"RunFlow | returning to skin select | targetBossRushScene={targetBossRushScene}");

                if (GameMusicController.Instance != null)
                    GameMusicController.Instance.StopMusic();

                SkinSelectFlowRouter.SetReturnToBossRush(targetBossRushScene);
                SceneManager.LoadScene(returnSceneName);
                yield break;
            }
        }

        SLog("RunFlow | finished");
    }

    void SLog(string message)
    {
        if (!enableSurgicalLogs)
            return;

        Debug.Log($"{LOG} {message}", this);
    }
}