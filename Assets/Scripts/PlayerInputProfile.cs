using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerInputProfile
{
    public int playerId;

    public Dictionary<PlayerAction, KeyCode> bindings = new();

    public PlayerInputProfile(int id)
    {
        playerId = id;

        bindings[PlayerAction.MoveUp] = KeyCode.W;
        bindings[PlayerAction.MoveDown] = KeyCode.S;
        bindings[PlayerAction.MoveLeft] = KeyCode.A;
        bindings[PlayerAction.MoveRight] = KeyCode.D;

        bindings[PlayerAction.Start] = KeyCode.Return;
        bindings[PlayerAction.ActionA] = KeyCode.M;
        bindings[PlayerAction.ActionB] = KeyCode.N;
        bindings[PlayerAction.ActionC] = KeyCode.B;
    }

    public KeyCode GetKey(PlayerAction action)
    {
        return bindings[action];
    }
}
