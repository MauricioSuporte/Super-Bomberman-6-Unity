using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }

    [Header("Players")]
    [Tooltip("How many player profiles to create (1-4).")]
    [Range(1, 4)]
    [SerializeField] int maxPlayers = 4;

    readonly Dictionary<int, PlayerInputProfile> players = new();

    readonly Dictionary<int, bool> prevUp = new();
    readonly Dictionary<int, bool> prevDown = new();
    readonly Dictionary<int, bool> prevLeft = new();
    readonly Dictionary<int, bool> prevRight = new();

    readonly Dictionary<int, bool> curUp = new();
    readonly Dictionary<int, bool> curDown = new();
    readonly Dictionary<int, bool> curLeft = new();
    readonly Dictionary<int, bool> curRight = new();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        int count = Mathf.Clamp(maxPlayers, 1, 4);

        for (int id = 1; id <= count; id++)
        {
            players[id] = new PlayerInputProfile(id);

            prevUp[id] = prevDown[id] = prevLeft[id] = prevRight[id] = false;
            curUp[id] = curDown[id] = curLeft[id] = curRight[id] = false;
        }
    }

    void Update()
    {
        foreach (var kv in players)
        {
            int id = kv.Key;
            var p = kv.Value;

            ReadDpadDigital(p, out bool up, out bool down, out bool left, out bool right);

            curUp[id] = up;
            curDown[id] = down;
            curLeft[id] = left;
            curRight[id] = right;
        }
    }

    void LateUpdate()
    {
        foreach (var kv in players)
        {
            int id = kv.Key;

            prevUp[id] = curUp[id];
            prevDown[id] = curDown[id];
            prevLeft[id] = curLeft[id];
            prevRight[id] = curRight[id];
        }
    }

    public PlayerInputProfile GetPlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);

        if (!players.TryGetValue(playerId, out var p) || p == null)
        {
            p = new PlayerInputProfile(playerId);
            players[playerId] = p;

            prevUp[playerId] = prevDown[playerId] = prevLeft[playerId] = prevRight[playerId] = false;
            curUp[playerId] = curDown[playerId] = curLeft[playerId] = curRight[playerId] = false;
        }

        return p;
    }

    public bool Get(int playerId, PlayerAction action) => Get(action, playerId);

    public bool GetDown(int playerId, PlayerAction action) => GetDown(action, playerId);

    public bool Get(PlayerAction action, int playerId = 1)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);

        var p = GetPlayer(playerId);
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return ReadKeyHeld(b.key);

        if (b.kind == BindKind.DPad)
        {
            return b.dpadDir switch
            {
                0 => curUp[playerId],
                1 => curDown[playerId],
                2 => curLeft[playerId],
                3 => curRight[playerId],
                _ => false
            };
        }

        if (b.kind == BindKind.JoyButton)
            return ReadGamepadButtonHeld(p, b.joyButton);

        return false;
    }

    public bool GetDown(PlayerAction action, int playerId = 1)
    {
        playerId = Mathf.Clamp(playerId, 1, 4);

        var p = GetPlayer(playerId);
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return ReadKeyDown(b.key);

        if (b.kind == BindKind.DPad)
        {
            bool now = b.dpadDir switch
            {
                0 => curUp[playerId],
                1 => curDown[playerId],
                2 => curLeft[playerId],
                3 => curRight[playerId],
                _ => false
            };

            bool was = b.dpadDir switch
            {
                0 => prevUp[playerId],
                1 => prevDown[playerId],
                2 => prevLeft[playerId],
                3 => prevRight[playerId],
                _ => false
            };

            return now && !was;
        }

        if (b.kind == BindKind.JoyButton)
            return ReadGamepadButtonDown(p, b.joyButton);

        return false;
    }

    static void ReadDpadDigital(PlayerInputProfile p, out bool up, out bool down, out bool left, out bool right)
    {
        var pad = ResolvePlayerGamepad(p);

        if (pad == null)
        {
            up = down = left = right = false;
            return;
        }

        up = pad.dpad.up.isPressed;
        down = pad.dpad.down.isPressed;
        left = pad.dpad.left.isPressed;
        right = pad.dpad.right.isPressed;
    }

    static Gamepad ResolvePlayerGamepad(PlayerInputProfile p)
    {
        if (p == null)
            return null;

        var all = Gamepad.all;
        if (all.Count == 0)
            return null;

        int idx = Mathf.Clamp(p.joyIndex, 1, 11) - 1;
        if (idx < 0 || idx >= all.Count)
            return null;

        return all[idx];
    }

    static bool ReadKeyHeld(KeyCode k)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        return TryGetKeyControl(k, kb, out var key) && key.isPressed;
    }

    static bool ReadKeyDown(KeyCode k)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        return TryGetKeyControl(k, kb, out var key) && key.wasPressedThisFrame;
    }

    static bool TryGetKeyControl(KeyCode desiredKeyCode, Keyboard kb, out KeyControl key)
    {
        key = null;
        if (kb == null) return false;

        foreach (var k in kb.allKeys)
        {
            if (k == null) continue;

            if (TryMapInputSystemKeyToUnityKeyCode(k.keyCode, out var kc) && kc == desiredKeyCode)
            {
                key = k;
                return true;
            }
        }

        return false;
    }

    static bool TryMapInputSystemKeyToUnityKeyCode(Key key, out KeyCode kc)
    {
        kc = KeyCode.None;
        string name = key.ToString();

        switch (key)
        {
            case Key.Enter: kc = KeyCode.Return; return true;
            case Key.Escape: kc = KeyCode.Escape; return true;
            case Key.Backspace: kc = KeyCode.Backspace; return true;
            case Key.Space: kc = KeyCode.Space; return true;
            case Key.Tab: kc = KeyCode.Tab; return true;
        }

        if (name.StartsWith("Digit", StringComparison.OrdinalIgnoreCase) && name.Length == 6)
        {
            char d = name[5];
            if (d >= '0' && d <= '9')
            {
                kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + d);
                return true;
            }
        }

        if (name.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Length == 7)
            {
                char d = name[6];
                if (d >= '0' && d <= '9')
                {
                    kc = (KeyCode)Enum.Parse(typeof(KeyCode), "Keypad" + d);
                    return true;
                }
            }

            if (name.Equals("NumpadEnter", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadEnter; return true; }
            if (name.Equals("NumpadPlus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPlus; return true; }
            if (name.Equals("NumpadMinus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMinus; return true; }
            if (name.Equals("NumpadMultiply", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadMultiply; return true; }
            if (name.Equals("NumpadDivide", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadDivide; return true; }
            if (name.Equals("NumpadPeriod", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.KeypadPeriod; return true; }
        }

        if (name.Equals("LeftCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftControl; return true; }
        if (name.Equals("RightCtrl", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightControl; return true; }
        if (name.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftAlt; return true; }
        if (name.Equals("RightAlt", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightAlt; return true; }
        if (name.Equals("Backquote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.BackQuote; return true; }

        if (name.Equals("Minus", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Minus; return true; }
        if (name.Equals("Equals", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Equals; return true; }
        if (name.Equals("LeftBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.LeftBracket; return true; }
        if (name.Equals("RightBracket", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.RightBracket; return true; }
        if (name.Equals("Semicolon", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Semicolon; return true; }
        if (name.Equals("Quote", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Quote; return true; }
        if (name.Equals("Backslash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Backslash; return true; }
        if (name.Equals("Slash", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Slash; return true; }
        if (name.Equals("Comma", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Comma; return true; }
        if (name.Equals("Period", StringComparison.OrdinalIgnoreCase)) { kc = KeyCode.Period; return true; }

        if (Enum.TryParse(name, true, out KeyCode parsed))
        {
            kc = parsed;
            return kc != KeyCode.None;
        }

        return false;
    }

    static bool ReadGamepadButtonHeld(PlayerInputProfile p, int btn)
    {
        var pad = ResolvePlayerGamepad(p);
        if (pad == null) return false;

        var c = MapLegacyButtonIndex(pad, btn);
        return c != null && c.isPressed;
    }

    static bool ReadGamepadButtonDown(PlayerInputProfile p, int btn)
    {
        var pad = ResolvePlayerGamepad(p);
        if (pad == null) return false;

        var c = MapLegacyButtonIndex(pad, btn);
        return c != null && c.wasPressedThisFrame;
    }

    static ButtonControl MapLegacyButtonIndex(Gamepad pad, int btn)
    {
        return btn switch
        {
            0 => pad.buttonSouth,
            1 => pad.buttonEast,
            2 => pad.buttonWest,
            3 => pad.buttonNorth,
            4 => pad.leftShoulder,
            5 => pad.rightShoulder,
            6 => pad.leftTrigger,
            7 => pad.rightTrigger,
            8 => pad.startButton,
            9 => pad.selectButton,
            _ => null
        };
    }
}
