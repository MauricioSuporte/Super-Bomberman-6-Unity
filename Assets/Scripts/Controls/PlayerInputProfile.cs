using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class PlayerInputProfile
{
    public int playerId;

    [Header("Active Joystick (Legacy Display)")]
    [Range(1, 11)] public int joyIndex = 1;

    [Header("Active Gamepad (Stable Identity)")]
    public int gamepadDeviceId = -1;
    public string gamepadProduct = "";

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

        BuildDefaultBindingsForPlayer(id);

        ResetToDefault();
        LoadFromPrefs();
    }

    void BuildDefaultBindingsForPlayer(int id)
    {
        defaultBindings.Clear();

        if (id == 1)
        {
            defaultBindings[PlayerAction.MoveUp] = Binding.FromKey(KeyCode.W);
            defaultBindings[PlayerAction.MoveDown] = Binding.FromKey(KeyCode.S);
            defaultBindings[PlayerAction.MoveLeft] = Binding.FromKey(KeyCode.A);
            defaultBindings[PlayerAction.MoveRight] = Binding.FromKey(KeyCode.D);

            defaultBindings[PlayerAction.Start] = Binding.FromKey(KeyCode.Return);
            defaultBindings[PlayerAction.ActionA] = Binding.FromKey(KeyCode.F);
            defaultBindings[PlayerAction.ActionB] = Binding.FromKey(KeyCode.G);
            defaultBindings[PlayerAction.ActionC] = Binding.FromKey(KeyCode.H);

            // NEW: L / R (rare actions)
            defaultBindings[PlayerAction.ActionL] = Binding.FromKey(KeyCode.R);
            defaultBindings[PlayerAction.ActionR] = Binding.FromKey(KeyCode.T);

            return;
        }

        if (id == 2)
        {
            defaultBindings[PlayerAction.MoveUp] = Binding.FromKey(KeyCode.I);
            defaultBindings[PlayerAction.MoveDown] = Binding.FromKey(KeyCode.K);
            defaultBindings[PlayerAction.MoveLeft] = Binding.FromKey(KeyCode.J);
            defaultBindings[PlayerAction.MoveRight] = Binding.FromKey(KeyCode.L);

            defaultBindings[PlayerAction.Start] = Binding.FromKey(KeyCode.Y);
            defaultBindings[PlayerAction.ActionA] = Binding.FromKey(KeyCode.U);
            defaultBindings[PlayerAction.ActionB] = Binding.FromKey(KeyCode.O);
            defaultBindings[PlayerAction.ActionC] = Binding.FromKey(KeyCode.P);

            // NEW: L / R
            defaultBindings[PlayerAction.ActionL] = Binding.FromKey(KeyCode.LeftBracket);
            defaultBindings[PlayerAction.ActionR] = Binding.FromKey(KeyCode.RightBracket);

            return;
        }

        if (id == 3)
        {
            defaultBindings[PlayerAction.MoveUp] = Binding.FromKey(KeyCode.UpArrow);
            defaultBindings[PlayerAction.MoveDown] = Binding.FromKey(KeyCode.DownArrow);
            defaultBindings[PlayerAction.MoveLeft] = Binding.FromKey(KeyCode.LeftArrow);
            defaultBindings[PlayerAction.MoveRight] = Binding.FromKey(KeyCode.RightArrow);

            defaultBindings[PlayerAction.Start] = Binding.FromKey(KeyCode.RightShift);
            defaultBindings[PlayerAction.ActionA] = Binding.FromKey(KeyCode.Comma);
            defaultBindings[PlayerAction.ActionB] = Binding.FromKey(KeyCode.Period);
            defaultBindings[PlayerAction.ActionC] = Binding.FromKey(KeyCode.Slash);

            // NEW: L / R
            defaultBindings[PlayerAction.ActionL] = Binding.FromKey(KeyCode.RightControl);
            defaultBindings[PlayerAction.ActionR] = Binding.FromKey(KeyCode.RightAlt);

            return;
        }

        // id == 4
        defaultBindings[PlayerAction.MoveUp] = Binding.FromKey(KeyCode.Keypad8);
        defaultBindings[PlayerAction.MoveDown] = Binding.FromKey(KeyCode.Keypad2);
        defaultBindings[PlayerAction.MoveLeft] = Binding.FromKey(KeyCode.Keypad4);
        defaultBindings[PlayerAction.MoveRight] = Binding.FromKey(KeyCode.Keypad6);

        defaultBindings[PlayerAction.Start] = Binding.FromKey(KeyCode.KeypadEnter);
        defaultBindings[PlayerAction.ActionA] = Binding.FromKey(KeyCode.Keypad1);
        defaultBindings[PlayerAction.ActionB] = Binding.FromKey(KeyCode.Keypad2);
        defaultBindings[PlayerAction.ActionC] = Binding.FromKey(KeyCode.Keypad3);

        // NEW: L / R
        defaultBindings[PlayerAction.ActionL] = Binding.FromKey(KeyCode.Keypad0);
        defaultBindings[PlayerAction.ActionR] = Binding.FromKey(KeyCode.KeypadPeriod);
    }

    public Binding GetBinding(PlayerAction action) => bindings[action];

    public void SetBinding(PlayerAction action, Binding binding) => bindings[action] = binding;

    public void ResetToDefault()
    {
        bindings.Clear();
        foreach (var kv in defaultBindings)
            bindings[kv.Key] = kv.Value;

        EnsureAllActionsExist();
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

        EnsureAllActionsExist();
    }

    public void BindToGamepad(Gamepad pad, int fallbackJoyIndexForDisplay = -1)
    {
        if (pad == null)
            return;

        gamepadDeviceId = pad.deviceId;
        gamepadProduct = pad.description.product ?? "";

        if (fallbackJoyIndexForDisplay > 0)
            joyIndex = fallbackJoyIndexForDisplay;
        else
            joyIndex = Mathf.Clamp(joyIndex, 1, 11);
    }

    public void ClearGamepadBinding()
    {
        gamepadDeviceId = -1;
        gamepadProduct = "";
    }

    public void SaveToPrefs()
    {
        foreach (var kv in bindings)
        {
            var w = new BindingWrapper { binding = kv.Value };
            PlayerPrefs.SetString(PrefKey(kv.Key), JsonUtility.ToJson(w));
        }

        PlayerPrefs.SetInt(PrefHasAny(), 1);

        PlayerPrefs.SetInt(PrefJoyIndex(), joyIndex);
        PlayerPrefs.SetInt(PrefGamepadDeviceId(), gamepadDeviceId);
        PlayerPrefs.SetString(PrefGamepadProduct(), gamepadProduct ?? "");

        PlayerPrefs.Save();
    }

    public void LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(PrefHasAny()))
            return;

        if (PlayerPrefs.HasKey(PrefJoyIndex()))
            joyIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefJoyIndex(), joyIndex), 1, 11);

        if (PlayerPrefs.HasKey(PrefGamepadDeviceId()))
            gamepadDeviceId = PlayerPrefs.GetInt(PrefGamepadDeviceId(), -1);

        if (PlayerPrefs.HasKey(PrefGamepadProduct()))
            gamepadProduct = PlayerPrefs.GetString(PrefGamepadProduct(), "");

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

        PlayerPrefs.DeleteKey(PrefJoyIndex(playerId));
        PlayerPrefs.DeleteKey(PrefGamepadDeviceId(playerId));
        PlayerPrefs.DeleteKey(PrefGamepadProduct(playerId));

        PlayerPrefs.Save();
    }

    string PrefHasAny() => PrefHasAny(playerId);
    static string PrefHasAny(int playerId) => $"P{playerId}_BIND_HAS_ANY";

    string PrefKey(PlayerAction a) => PrefKey(playerId, a);
    static string PrefKey(int playerId, PlayerAction a) => $"P{playerId}_BIND_{a}";

    string PrefJoyIndex() => PrefJoyIndex(playerId);
    static string PrefJoyIndex(int playerId) => $"P{playerId}_JOY_INDEX";

    string PrefGamepadDeviceId() => PrefGamepadDeviceId(playerId);
    static string PrefGamepadDeviceId(int playerId) => $"P{playerId}_GAMEPAD_DEVICE_ID";

    string PrefGamepadProduct() => PrefGamepadProduct(playerId);
    static string PrefGamepadProduct(int playerId) => $"P{playerId}_GAMEPAD_PRODUCT";
}
