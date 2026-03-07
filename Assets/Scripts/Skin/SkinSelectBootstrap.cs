using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkinSelectBootstrap : MonoBehaviour
{
    [Header("Skin Select")]
    [SerializeField] BomberSkinSelectMenu skinSelectMenu;

    [Header("Flow")]
    [SerializeField] bool useWorldMapAfterSkinSelect = true;
    [SerializeField] string worldMapSceneName = "WorldMap";
    [SerializeField] string firstStageSceneName = "Stage_1-1";
    [SerializeField] string titleSceneName = "TitleScreen";

    void Start()
    {
        PlayerPersistentStats.EnsureSessionBooted();
        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;

        StartCoroutine(RunFlow());
    }

    IEnumerator RunFlow()
    {
        if (skinSelectMenu == null)
            yield break;

        yield return skinSelectMenu.SelectSkinRoutine();

        if (skinSelectMenu.ReturnToTitleRequested)
        {
            if (!string.IsNullOrEmpty(titleSceneName))
            {
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

            if (chosen != BomberSkin.Golden)
                PlayerPersistentStats.SaveSelectedSkin(p);
        }

        PlayerPrefs.Save();

        if (useWorldMapAfterSkinSelect && !string.IsNullOrEmpty(worldMapSceneName))
        {
            SceneManager.LoadScene(worldMapSceneName);
            yield break;
        }

        if (!string.IsNullOrEmpty(firstStageSceneName))
        {
            SceneManager.LoadScene(firstStageSceneName);
            yield break;
        }
    }
}