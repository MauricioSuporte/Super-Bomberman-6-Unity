using UnityEngine;

public enum BindKind { Key, DPad, JoyButton }

[System.Serializable]
public struct Binding
{
    public BindKind kind;

    public KeyCode key;

    public int dpadDir;

    public int joyIndex;

    public int joyButton;

    public static Binding FromKey(KeyCode k) => new()
    {
        kind = BindKind.Key,
        key = k,
        dpadDir = -1,
        joyIndex = 0,
        joyButton = -1
    };

    public static Binding FromDpad(int joy, int dir) => new()
    {
        kind = BindKind.DPad,
        key = KeyCode.None,
        dpadDir = dir,
        joyIndex = joy,
        joyButton = -1
    };

    public static Binding FromJoyButton(int joy, int btn) => new()
    {
        kind = BindKind.JoyButton,
        key = KeyCode.None,
        dpadDir = -1,
        joyIndex = joy,
        joyButton = btn
    };
}
