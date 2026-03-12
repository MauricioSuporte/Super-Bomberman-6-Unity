using UnityEngine;

public static class SkinSelectFlowRouter
{
    public enum Destination
    {
        SaveFileMenu = 0,
        BossRush = 1,
        FirstStage = 2,
        CustomScene = 3
    }

    static Destination nextDestination = Destination.SaveFileMenu;
    static string customSceneName;
    static string bossRushSceneName;

    public static Destination NextDestination => nextDestination;
    public static string CustomSceneName => customSceneName;
    public static string BossRushSceneName => bossRushSceneName;

    public static void SetReturnToSaveFileMenu()
    {
        nextDestination = Destination.SaveFileMenu;
        customSceneName = null;
        bossRushSceneName = null;
    }

    public static void SetReturnToBossRush(string sceneName)
    {
        nextDestination = Destination.BossRush;
        bossRushSceneName = sceneName;
        customSceneName = null;
    }

    public static void SetReturnToFirstStage()
    {
        nextDestination = Destination.FirstStage;
        customSceneName = null;
        bossRushSceneName = null;
    }

    public static void SetReturnToCustomScene(string sceneName)
    {
        nextDestination = Destination.CustomScene;
        customSceneName = sceneName;
        bossRushSceneName = null;
    }

    public static void Clear()
    {
        nextDestination = Destination.SaveFileMenu;
        customSceneName = null;
        bossRushSceneName = null;
    }
}