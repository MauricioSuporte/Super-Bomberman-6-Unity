using UnityEngine;

public static class MobileControlsAutoBootstrap
{
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