using UnityEngine;

public static class MobileControlsAutoBootstrap
{
    static bool? portMaster;
    public static bool IsPortMaster => portMaster ??= Application.platform == RuntimePlatform.Android &&
        (System.Environment.GetEnvironmentVariable("UA_PAD_LAYOUT") == "nintendo" ||
         System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "portmaster.flag")));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (!Application.isMobilePlatform)
            return;

        if (MobileControlsRoot.Instance != null)
            return;

        var prefab = Resources.Load<GameObject>("UI/MobileControlsPrefab");
        if (prefab == null)
        {
            Debug.LogWarning("MobileControlsAutoBootstrap: prefab não encontrado em Resources/UI/MobileControlsPrefab");
            return;
        }

        Object.Instantiate(prefab);
    }
}