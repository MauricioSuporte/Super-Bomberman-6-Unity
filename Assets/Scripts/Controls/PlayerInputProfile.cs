using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerInputProfile
{
    public int playerId;

    [Header("Active Joystick (Legacy)")]
    [Range(1, 11)] public int joyIndex = 1;

    [Header("Legacy Input - DPad (Hat) Axes per Joystick")]
    [Range(0.05f, 0.95f)] public float axisDeadzone = 0.25f;

    [Tooltip("Invert DPad Y axis if your controller reports Up as -1.")]
    public bool invertDpadY;

    public Dictionary<PlayerAction, Binding> bindings = new();

    readonly Dictionary<PlayerAction, Binding> defaultBindings = new();

    [Serializable]
    class BindingWrapper { public Binding binding; }

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
        LoadFromPrefs();
    }

    public Binding GetBinding(PlayerAction action) => bindings[action];

    public void SetBinding(PlayerAction action, Binding binding) => bindings[action] = binding;

    public void ResetToDefault()
    {
        bindings.Clear();
        foreach (var kv in defaultBindings)
            bindings[kv.Key] = kv.Value;
    }

    public Dictionary<PlayerAction, Binding> CloneBindings()
    {
        var copy = new Dictionary<PlayerAction, Binding>(bindings.Count);
        foreach (var kv in bindings)
            copy[kv.Key] = kv.Value;
        return copy;
    }

    public void ApplyBindings(Dictionary<PlayerAction, Binding> source)
    {
        if (source == null) return;

        bindings.Clear();
        foreach (var kv in source)
            bindings[kv.Key] = kv.Value;
    }

    public void SaveToPrefs()
    {
        foreach (var kv in bindings)
        {
            var w = new BindingWrapper { binding = kv.Value };
            PlayerPrefs.SetString(PrefKey(kv.Key), JsonUtility.ToJson(w));
        }

        PlayerPrefs.SetInt(PrefHasAny(), 1);
        PlayerPrefs.Save();
    }

    public void LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(PrefHasAny()))
            return;

        foreach (var action in Enum.GetValues(typeof(PlayerAction)))
        {
            var a = (PlayerAction)action;
            var key = PrefKey(a);

            if (!PlayerPrefs.HasKey(key))
                continue;

            var json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json))
                continue;

            BindingWrapper w;
            try { w = JsonUtility.FromJson<BindingWrapper>(json); }
            catch { continue; }

            SetBinding(a, w.binding);
        }

        EnsureAllActionsExist();
    }

    void EnsureAllActionsExist()
    {
        foreach (var kv in defaultBindings)
        {
            if (!bindings.ContainsKey(kv.Key))
                bindings[kv.Key] = kv.Value;
        }
    }

    public static void ClearPrefs(int playerId)
    {
        PlayerPrefs.DeleteKey(PrefHasAny(playerId));

        foreach (var action in Enum.GetValues(typeof(PlayerAction)))
            PlayerPrefs.DeleteKey(PrefKey(playerId, (PlayerAction)action));

        PlayerPrefs.Save();
    }

    string PrefHasAny() => PrefHasAny(playerId);
    static string PrefHasAny(int playerId) => $"P{playerId}_BIND_HAS_ANY";

    string PrefKey(PlayerAction a) => PrefKey(playerId, a);
    static string PrefKey(int playerId, PlayerAction a) => $"P{playerId}_BIND_{a}";
}
