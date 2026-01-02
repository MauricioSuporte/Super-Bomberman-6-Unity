using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerInputProfile
{
    public int playerId;

    [Header("Active Joystick (Legacy)")]
    [Range(1, 11)] public int joyIndex = 1;

    [Header("Legacy Input - DPad (Hat) Axes per Joystick")]
    [Range(0.05f, 0.95f)] public float axisDeadzone = 0.25f;

    [Tooltip("Marque se no seu controle o DPad Up vier como -1 no eixo Y.")]
    public bool invertDpadY;

    public Dictionary<PlayerAction, Binding> bindings = new();
    readonly Dictionary<PlayerAction, Binding> defaultBindings = new();

    public PlayerInputProfile(int id)
    {
        playerId = id;

        defaultBindings[PlayerAction.MoveUp] = Binding.FromKey(KeyCode.W);
        defaultBindings[PlayerAction.MoveDown] = Binding.FromKey(KeyCode.S);
        defaultBindings[PlayerAction.MoveLeft] = Binding.FromKey(KeyCode.A);
        defaultBindings[PlayerAction.MoveRight] = Binding.FromKey(KeyCode.D);

        defaultBindings[PlayerAction.Start] = Binding.FromKey(KeyCode.Return);
        defaultBindings[PlayerAction.ActionA] = Binding.FromKey(KeyCode.M);
        defaultBindings[PlayerAction.ActionB] = Binding.FromKey(KeyCode.N);
        defaultBindings[PlayerAction.ActionC] = Binding.FromKey(KeyCode.B);

        ResetToDefault();
    }

    public Binding GetBinding(PlayerAction action) => bindings[action];

    public void SetBinding(PlayerAction action, Binding binding) => bindings[action] = binding;

    public void ResetToDefault()
    {
        bindings.Clear();
        foreach (var kv in defaultBindings)
            bindings[kv.Key] = kv.Value;
    }
}
