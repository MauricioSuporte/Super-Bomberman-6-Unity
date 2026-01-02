using UnityEngine;

public static class PlayerInputBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureInputManager()
    {
        if (PlayerInputManager.Instance != null)
            return;

        var go = new GameObject("PlayerInputManager");
        go.AddComponent<PlayerInputManager>();
    }
}
