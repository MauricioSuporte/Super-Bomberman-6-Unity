using UnityEngine;

public enum BindKind { Key, DPad, JoyButton }

[System.Serializable]
public struct Binding
{
    public BindKind kind;

    public KeyCode key;

    // 0=Up,1=Down,2=Left,3=Right
    public int dpadDir;

    // Legacy: qual joystick (1..11)
    public int joyIndex;

    // Legacy: Joystick#Button#
    public int joyButton;  // 0..19+

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
