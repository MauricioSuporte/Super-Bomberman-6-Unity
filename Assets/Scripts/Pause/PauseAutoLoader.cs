using UnityEngine;

public static class PauseAutoLoader
{
    const string PrefabPath = "Systems/PauseSystem";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsurePauseSystem()
    {
        if (GamePauseController.Instance != null)
            return;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"PauseSystem prefab not found at Resources/{PrefabPath}.prefab");
            return;
        }

        Object.Instantiate(prefab);
    }
}
