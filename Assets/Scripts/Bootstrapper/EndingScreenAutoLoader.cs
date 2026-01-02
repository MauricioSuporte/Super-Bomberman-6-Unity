using UnityEngine;

public static class EndingScreenAutoLoader
{
    const string PrefabPath = "Systems/EndingScreenSystem";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureEndingScreen()
    {
        if (EndingScreenController.Instance != null)
            return;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"EndingScreenSystem prefab not found at Resources/{PrefabPath}.prefab");
            return;
        }

        Object.Instantiate(prefab);
    }
}
