using System.Collections.Generic;
using UnityEngine;

public class MobileInputBridge : MonoBehaviour
{
    public static MobileInputBridge Instance { get; private set; }

    private readonly Dictionary<PlayerAction, bool> held = new();
    private readonly Dictionary<PlayerAction, bool> down = new();

    private Vector2 moveVector;
    private bool moveVectorActive;

    public Vector2 MoveVector => moveVectorActive ? moveVector : Vector2.zero;
    public bool HasMoveVector => moveVectorActive && moveVector.sqrMagnitude > 0f;

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

    public void SetMoveVector(Vector2 value)
    {
        moveVector = Vector2.ClampMagnitude(value, 1f);
        moveVectorActive = moveVector.sqrMagnitude > 0f;
    }

    public void ClearMoveVector()
    {
        moveVector = Vector2.zero;
        moveVectorActive = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
