using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerInputProfile
{
    public int playerId;

    public Dictionary<PlayerAction, KeyCode> bindings = new();

    readonly Dictionary<PlayerAction, KeyCode> defaultBindings = new();

    public PlayerInputProfile(int id)
    {
        playerId = id;

        defaultBindings[PlayerAction.MoveUp] = KeyCode.W;
        defaultBindings[PlayerAction.MoveDown] = KeyCode.S;
        defaultBindings[PlayerAction.MoveLeft] = KeyCode.A;
        defaultBindings[PlayerAction.MoveRight] = KeyCode.D;

        defaultBindings[PlayerAction.Start] = KeyCode.Return;
        defaultBindings[PlayerAction.ActionA] = KeyCode.M;
        defaultBindings[PlayerAction.ActionB] = KeyCode.N;
        defaultBindings[PlayerAction.ActionC] = KeyCode.B;

        ResetToDefault();
    }

    public KeyCode GetKey(PlayerAction action)
    {
        return bindings[action];
    }

    public void SetKey(PlayerAction action, KeyCode key)
    {
        bindings[action] = key;
    }

    public void ResetToDefault()
    {
        bindings.Clear();
        foreach (var kv in defaultBindings)
            bindings[kv.Key] = kv.Value;
    }
}
