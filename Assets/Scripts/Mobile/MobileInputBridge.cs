using System.Collections.Generic;
using UnityEngine;

public class MobileInputBridge : MonoBehaviour
{
    public static MobileInputBridge Instance { get; private set; }

    private readonly Dictionary<PlayerAction, bool> held = new();
    private readonly Dictionary<PlayerAction, bool> down = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (PlayerAction action in System.Enum.GetValues(typeof(PlayerAction)))
        {
            held[action] = false;
            down[action] = false;
        }
    }

    void LateUpdate()
    {
        var keys = new List<PlayerAction>(down.Keys);
        for (int i = 0; i < keys.Count; i++)
            down[keys[i]] = false;
    }

    public void Press(PlayerAction action)
    {
        if (!held[action])
            down[action] = true;

        held[action] = true;
    }

    public void Release(PlayerAction action)
    {
        held[action] = false;
    }

    public bool Get(PlayerAction action)
    {
        return held.TryGetValue(action, out bool value) && value;
    }

    public bool GetDown(PlayerAction action)
    {
        return down.TryGetValue(action, out bool value) && value;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}