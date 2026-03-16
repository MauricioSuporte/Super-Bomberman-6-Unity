using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkinSelectBootstrap : MonoBehaviour
{
    [Header("Skin Select")]
    [SerializeField] BomberSkinSelectMenu skinSelectMenu;

    [Header("Default Flow")]
    [SerializeField] string saveFileMenuSceneName = "SaveFileMenu";
    [SerializeField] string firstStageSceneName = "Stage_1-1";
    [SerializeField] string titleSceneName = "TitleScreen";

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        StartCoroutine(RunFlow());
    }

    public void SetNextDestinationToWorldMap()
    {
        SkinSelectFlowRouter.SetReturnToCustomScene(saveFileMenuSceneName);
    }

    public void SetNextDestinationToBossRush(string sceneName)
    {
        SkinSelectFlowRouter.SetReturnToBossRush(sceneName);
    }

    public void SetNextDestinationToFirstStage()
    {
        SkinSelectFlowRouter.SetReturnToFirstStage();
    }

    public void SetNextDestinationToCustomScene(string sceneName)
    {
        SkinSelectFlowRouter.SetReturnToCustomScene(sceneName);
    }

    IEnumerator RunFlow()
    {
        if (skinSelectMenu == null)
            yield break;

        yield return skinSelectMenu.SelectSkinRoutine();

        if (skinSelectMenu.ReturnToTitleRequested)
        {
            SkinSelectFlowRouter.Clear();

            if (!string.IsNullOrEmpty(titleSceneName))
            {
                TitleScreenSkip.SkipNextIntro = true;

                SceneManager.LoadScene(titleSceneName);
                yield break;
            }
        }

        int count = 1;
        if (GameSession.Instance != null)
            count = GameSession.Instance.ActivePlayerCount;

        for (int p = 1; p <= count; p++)
        {
            var chosen = skinSelectMenu.GetSelectedSkin(p);
            PlayerPersistentStats.Get(p).Skin = chosen;
            PlayerPersistentStats.SaveSelectedSkin(p);
        }

        switch (SkinSelectFlowRouter.NextDestination)
        {
            case SkinSelectFlowRouter.Destination.BossRush:
                {
                    string bossRushScene = SkinSelectFlowRouter.BossRushSceneName;
                    SkinSelectFlowRouter.Clear();

                    if (!string.IsNullOrEmpty(bossRushScene))
                    {
                        SceneManager.LoadScene(bossRushScene);
                        yield break;
                    }

                    break;
                }

            case SkinSelectFlowRouter.Destination.CustomScene:
                {
                    string customScene = SkinSelectFlowRouter.CustomSceneName;
                    SkinSelectFlowRouter.Clear();

                    if (!string.IsNullOrEmpty(customScene))
                    {
                        SceneManager.LoadScene(customScene);
                        yield break;
                    }

                    break;
                }

            case SkinSelectFlowRouter.Destination.FirstStage:
                {
                    SkinSelectFlowRouter.Clear();

                    if (!string.IsNullOrEmpty(firstStageSceneName))
                    {
                        SceneManager.LoadScene(firstStageSceneName);
                        yield break;
                    }

                    break;
                }

            case SkinSelectFlowRouter.Destination.SaveFileMenu:
            default:
                {
                    SkinSelectFlowRouter.Clear();

                    if (!string.IsNullOrEmpty(saveFileMenuSceneName))
                    {
                        SceneManager.LoadScene(saveFileMenuSceneName);
                        yield break;
                    }

                    if (!string.IsNullOrEmpty(firstStageSceneName))
                    {
                        SceneManager.LoadScene(firstStageSceneName);
                        yield break;
                    }

                    break;
                }
        }
    }
}