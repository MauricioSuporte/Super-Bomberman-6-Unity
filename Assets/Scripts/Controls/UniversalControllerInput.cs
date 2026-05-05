using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class UniversalControllerInput
{
    public const int MaxControllerCount = 11;
    const float DefaultDirectionalThreshold = 0.35f;

    static readonly List<InputDevice> controllerDevices = new();
    static readonly List<ButtonControl> buttonScratch = new();
    static readonly Dictionary<int, int> previousDpadMaskByDevice = new();
    static readonly Dictionary<int, int> dpadMaskStampByDevice = new();

    static int controllerCacheFrame = -1;
    static int controllerCacheDeviceCount = -1;

    public struct DpadHit
    {
        public int dir;
        public int joyIndex;
        public int deviceId;
        public string product;
    }

    public struct ButtonHit
    {
        public int btn;
        public int joyIndex;
        public int deviceId;
        public string product;
    }

    public static void BindProfileToDevice(PlayerInputProfile profile, InputDevice device, int fallbackJoyIndexForDisplay = -1)
    {
        if (profile == null || device == null)
            return;

        profile.gamepadDeviceId = device.deviceId;
        profile.gamepadProduct = GetDeviceProduct(device);

        profile.joyIndex = fallbackJoyIndexForDisplay > 0
            ? Mathf.Clamp(fallbackJoyIndexForDisplay, 1, MaxControllerCount)
            : Mathf.Clamp(GetJoyIndex(device), 1, MaxControllerCount);
    }

    public static InputDevice ResolveProfileDevice(PlayerInputProfile profile)
    {
        if (profile == null)
            return null;

        RefreshControllerCache();

        if (profile.gamepadDeviceId >= 0)
        {
            for (int i = 0; i < controllerDevices.Count; i++)
            {
                var device = controllerDevices[i];
                if (device == null)
                    continue;

                if (device.deviceId == profile.gamepadDeviceId)
                {
                    profile.joyIndex = Mathf.Clamp(i + 1, 1, MaxControllerCount);
                    return device;
                }
            }
        }

        if (!string.IsNullOrEmpty(profile.gamepadProduct))
        {
            for (int i = 0; i < controllerDevices.Count; i++)
            {
                var device = controllerDevices[i];
                if (device == null)
                    continue;

                if (string.Equals(GetDeviceProduct(device), profile.gamepadProduct, StringComparison.OrdinalIgnoreCase))
                {
                    BindProfileToDevice(profile, device, i + 1);
                    return device;
                }
            }
        }

        int idx = Mathf.Clamp(profile.joyIndex, 1, MaxControllerCount) - 1;
        if (idx < 0 || idx >= controllerDevices.Count)
            return null;

        var resolved = controllerDevices[idx];
        if (resolved != null)
            BindProfileToDevice(profile, resolved, idx + 1);

        return resolved;
    }

    public static bool TryReadAnyDpadDownThisFrame(out DpadHit hit)
    {
        RefreshControllerCache();

        for (int i = 0; i < controllerDevices.Count; i++)
        {
            var device = controllerDevices[i];
            if (device == null)
                continue;

            if (TryReadDpadDownThisFrame(device, out int dir))
            {
                hit = new DpadHit
                {
                    dir = dir,
                    joyIndex = i + 1,
                    deviceId = device.deviceId,
                    product = GetDeviceProduct(device)
                };
                return true;
            }
        }

        hit = default;
        return false;
    }

    public static bool TryReadAnyButtonDownThisFrame(out ButtonHit hit)
    {
        RefreshControllerCache();

        for (int i = 0; i < controllerDevices.Count; i++)
        {
            var device = controllerDevices[i];
            if (device == null)
                continue;

            if (TryReadButtonDownThisFrame(device, out int button))
            {
                hit = new ButtonHit
                {
                    btn = button,
                    joyIndex = i + 1,
                    deviceId = device.deviceId,
                    product = GetDeviceProduct(device)
                };
                return true;
            }
        }

        hit = default;
        return false;
    }

    public static bool ReadDirectionalDigital(
        InputDevice device,
        float threshold,
        bool includeSecondaryStick,
        out bool up,
        out bool down,
        out bool left,
        out bool right)
    {
        up = down = left = right = false;

        if (device == null)
            return false;

        threshold = Mathf.Clamp01(threshold <= 0f ? DefaultDirectionalThreshold : threshold);

        if (device is Gamepad pad)
        {
            up |= pad.dpad.up.isPressed;
            down |= pad.dpad.down.isPressed;
            left |= pad.dpad.left.isPressed;
            right |= pad.dpad.right.isPressed;

            AddStickAsDigital(pad.leftStick, threshold, ref up, ref down, ref left, ref right);

            if (includeSecondaryStick)
                AddStickAsDigital(pad.rightStick, threshold, ref up, ref down, ref left, ref right);

            return up || down || left || right;
        }

        if (device is Joystick joystick)
        {
            AddVectorAsDigital(joystick.hatswitch, threshold, ref up, ref down, ref left, ref right);
            AddStickAsDigital(joystick.stick, threshold, ref up, ref down, ref left, ref right);
            return up || down || left || right;
        }

        AddGenericDirectionalControls(device, threshold, includeSecondaryStick, ref up, ref down, ref left, ref right);
        return up || down || left || right;
    }

    public static bool ReadButtonHeld(InputDevice device, int button)
    {
        var control = MapButton(device, button);
        return control != null && control.isPressed;
    }

    public static bool ReadButtonDown(InputDevice device, int button)
    {
        var control = MapButton(device, button);
        return control != null && control.wasPressedThisFrame;
    }

    public static string GetDeviceProduct(InputDevice device)
    {
        if (device == null)
            return "";

        string product = device.description.product;
        if (!string.IsNullOrEmpty(product))
            return product;

        if (!string.IsNullOrEmpty(device.displayName))
            return device.displayName;

        return device.name ?? "";
    }

    static int GetJoyIndex(InputDevice device)
    {
        if (device == null)
            return 1;

        RefreshControllerCache();

        for (int i = 0; i < controllerDevices.Count; i++)
        {
            if (controllerDevices[i] == device)
                return i + 1;
        }

        return 1;
    }

    static void RefreshControllerCache()
    {
        int frame = Time.frameCount;
        int deviceCount = InputSystem.devices.Count;

        if (controllerCacheFrame == frame && controllerCacheDeviceCount == deviceCount)
            return;

        controllerCacheFrame = frame;
        controllerCacheDeviceCount = deviceCount;
        controllerDevices.Clear();

        for (int i = 0; i < InputSystem.devices.Count && controllerDevices.Count < MaxControllerCount; i++)
        {
            var device = InputSystem.devices[i];
            if (IsControllerLikeDevice(device))
                controllerDevices.Add(device);
        }
    }

    static bool IsControllerLikeDevice(InputDevice device)
    {
        if (device == null || !device.added || !device.enabled)
            return false;

        if (device is Gamepad || device is Joystick)
            return true;

        if (device is Keyboard || device is Mouse || device is Touchscreen)
            return false;

        string layout = device.layout ?? "";
        if (Contains(layout, "Keyboard") ||
            Contains(layout, "Mouse") ||
            Contains(layout, "Touch") ||
            Contains(layout, "Pointer"))
        {
            return false;
        }

        bool looksLikeController =
            Contains(layout, "Gamepad") ||
            Contains(layout, "Joystick") ||
            Contains(layout, "Controller") ||
            Contains(device.description.interfaceName, "HID") ||
            Contains(device.description.product, "Controller") ||
            Contains(device.description.product, "Gamepad") ||
            Contains(device.description.product, "Joystick") ||
            Contains(device.description.product, "DualShock") ||
            Contains(device.description.product, "Wireless Controller") ||
            Contains(device.description.product, "8BitDo") ||
            Contains(device.displayName, "Controller") ||
            Contains(device.displayName, "Gamepad") ||
            Contains(device.displayName, "Joystick");

        if (!looksLikeController)
            return false;

        return CountCandidateButtons(device, 2) >= 2 && HasDirectionalControl(device);
    }

    static bool Contains(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasDirectionalControl(InputDevice device)
    {
        for (int i = 0; i < device.allControls.Count; i++)
        {
            var control = device.allControls[i];
            if (control is DpadControl)
                return true;

            if (control is StickControl)
                return true;

            if (control is Vector2Control vector && IsDirectionalVectorControl(vector))
                return true;
        }

        return CountCandidateAxes(device, 2) >= 2;
    }

    static int CountCandidateButtons(InputDevice device, int stopAt)
    {
        int count = 0;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is ButtonControl button && IsCandidateButton(button))
            {
                count++;
                if (count >= stopAt)
                    return count;
            }
        }

        return count;
    }

    static int CountCandidateAxes(InputDevice device, int stopAt)
    {
        int count = 0;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is AxisControl axis && IsCandidateAxis(axis))
            {
                count++;
                if (count >= stopAt)
                    return count;
            }
        }

        return count;
    }

    static bool TryReadDpadDownThisFrame(InputDevice device, out int dir)
    {
        dir = -1;

        if (device is Gamepad pad)
        {
            if (pad.dpad.up.wasPressedThisFrame) { dir = 0; return true; }
            if (pad.dpad.down.wasPressedThisFrame) { dir = 1; return true; }
            if (pad.dpad.left.wasPressedThisFrame) { dir = 2; return true; }
            if (pad.dpad.right.wasPressedThisFrame) { dir = 3; return true; }
            return false;
        }

        DpadControl dpad = FindDpadControl(device);
        if (dpad != null)
        {
            if (dpad.up.wasPressedThisFrame) { dir = 0; return true; }
            if (dpad.down.wasPressedThisFrame) { dir = 1; return true; }
            if (dpad.left.wasPressedThisFrame) { dir = 2; return true; }
            if (dpad.right.wasPressedThisFrame) { dir = 3; return true; }
            return false;
        }

        Vector2Control vectorDpad = FindDpadVectorControl(device);
        if (vectorDpad == null)
            return false;

        return TryReadVectorDpadDownThisFrame(device.deviceId, vectorDpad.ReadValue(), out dir);
    }

    static bool TryReadVectorDpadDownThisFrame(int deviceId, Vector2 value, out int dir)
    {
        dir = -1;

        int frame = Time.frameCount;
        int currentMask = VectorToDirectionMask(value, DefaultDirectionalThreshold);

        previousDpadMaskByDevice.TryGetValue(deviceId, out int previousMask);

        if (dpadMaskStampByDevice.TryGetValue(deviceId, out int stamp) && stamp == frame)
        {
            previousMask = previousDpadMaskByDevice.TryGetValue(deviceId, out int cachedMask) ? cachedMask : previousMask;
        }
        else
        {
            dpadMaskStampByDevice[deviceId] = frame;
            previousDpadMaskByDevice[deviceId] = currentMask;
        }

        int pressedMask = currentMask & ~previousMask;
        if ((pressedMask & 1) != 0) { dir = 0; return true; }
        if ((pressedMask & 2) != 0) { dir = 1; return true; }
        if ((pressedMask & 4) != 0) { dir = 2; return true; }
        if ((pressedMask & 8) != 0) { dir = 3; return true; }

        if (!dpadMaskStampByDevice.TryGetValue(deviceId, out stamp) || stamp != frame)
            previousDpadMaskByDevice[deviceId] = currentMask;

        return false;
    }

    static int VectorToDirectionMask(Vector2 value, float threshold)
    {
        int mask = 0;

        if (value.y >= threshold) mask |= 1;
        if (value.y <= -threshold) mask |= 2;
        if (value.x <= -threshold) mask |= 4;
        if (value.x >= threshold) mask |= 8;

        return mask;
    }

    static bool TryReadButtonDownThisFrame(InputDevice device, out int button)
    {
        button = -1;

        if (device is Gamepad)
        {
            for (int i = 0; i <= 9; i++)
            {
                var control = MapGamepadButton((Gamepad)device, i);
                if (control != null && control.wasPressedThisFrame)
                {
                    button = i;
                    return true;
                }
            }

            return false;
        }

        FillCandidateButtons(device);

        for (int i = 0; i < buttonScratch.Count; i++)
        {
            var control = buttonScratch[i];
            if (control != null && control.wasPressedThisFrame)
            {
                button = i;
                return true;
            }
        }

        return false;
    }

    static ButtonControl MapButton(InputDevice device, int button)
    {
        if (device == null || button < 0)
            return null;

        if (device is Gamepad pad)
            return MapGamepadButton(pad, button);

        FillCandidateButtons(device);
        return button >= 0 && button < buttonScratch.Count ? buttonScratch[button] : null;
    }

    static ButtonControl MapGamepadButton(Gamepad pad, int button)
    {
        if (pad == null)
            return null;

        return button switch
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

    static void FillCandidateButtons(InputDevice device)
    {
        buttonScratch.Clear();

        if (device == null)
            return;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is ButtonControl button && IsCandidateButton(button))
                buttonScratch.Add(button);
        }
    }

    static bool IsCandidateButton(ButtonControl button)
    {
        if (button == null || button.synthetic || button.noisy)
            return false;

        if (button.parent is DpadControl || button.parent is StickControl || button.parent is Vector2Control)
            return false;

        string path = button.path ?? "";
        if (Contains(path, "/dpad/") || Contains(path, "/stick/") || Contains(path, "/hatswitch/"))
            return false;

        return true;
    }

    static bool IsCandidateAxis(AxisControl axis)
    {
        if (axis == null || axis.synthetic || axis.noisy)
            return false;

        if (axis.parent is StickControl || axis.parent is DpadControl || axis.parent is Vector2Control)
            return false;

        string name = axis.name ?? "";
        string path = axis.path ?? "";
        if (Contains(name, "trigger") || Contains(path, "trigger"))
            return false;

        return true;
    }

    static DpadControl FindDpadControl(InputDevice device)
    {
        if (device == null)
            return null;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is DpadControl dpad)
                return dpad;
        }

        return null;
    }

    static Vector2Control FindDpadVectorControl(InputDevice device)
    {
        if (device == null)
            return null;

        if (device is Joystick joystick && joystick.hatswitch != null)
            return joystick.hatswitch;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is Vector2Control vector && IsDpadVectorControl(vector))
                return vector;
        }

        return null;
    }

    static bool IsDpadVectorControl(Vector2Control control)
    {
        if (control == null)
            return false;

        string name = control.name ?? "";
        string displayName = control.displayName ?? "";
        string path = control.path ?? "";

        return Contains(name, "dpad") ||
               Contains(name, "hat") ||
               Contains(displayName, "d-pad") ||
               Contains(displayName, "hat") ||
               Contains(path, "dpad") ||
               Contains(path, "hatswitch");
    }

    static bool IsDirectionalVectorControl(Vector2Control control)
    {
        if (control == null || control.synthetic || control.noisy)
            return false;

        return control is StickControl || IsDpadVectorControl(control) || Contains(control.name, "stick");
    }

    static void AddGenericDirectionalControls(
        InputDevice device,
        float threshold,
        bool includeSecondaryStick,
        ref bool up,
        ref bool down,
        ref bool left,
        ref bool right)
    {
        bool consumedPrimaryVector = false;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is DpadControl dpad)
            {
                up |= dpad.up.isPressed;
                down |= dpad.down.isPressed;
                left |= dpad.left.isPressed;
                right |= dpad.right.isPressed;
                continue;
            }

            if (device.allControls[i] is Vector2Control vector && IsDirectionalVectorControl(vector))
            {
                if (!includeSecondaryStick && consumedPrimaryVector && !IsDpadVectorControl(vector))
                    continue;

                AddVectorAsDigital(vector, threshold, ref up, ref down, ref left, ref right);

                if (!IsDpadVectorControl(vector))
                    consumedPrimaryVector = true;
            }
        }

        if (!consumedPrimaryVector)
            AddAxisPairAsDigital(device, threshold, ref up, ref down, ref left, ref right);
    }

    static void AddAxisPairAsDigital(InputDevice device, float threshold, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        AxisControl xAxis = null;
        AxisControl yAxis = null;

        for (int i = 0; i < device.allControls.Count; i++)
        {
            if (device.allControls[i] is not AxisControl axis || !IsCandidateAxis(axis))
                continue;

            string name = axis.name ?? "";
            string path = axis.path ?? "";

            if (xAxis == null && (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase) || Contains(path, "/x")))
            {
                xAxis = axis;
                continue;
            }

            if (yAxis == null && (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase) || Contains(path, "/y")))
            {
                yAxis = axis;
                continue;
            }

            if (xAxis == null)
            {
                xAxis = axis;
                continue;
            }

            if (yAxis == null)
            {
                yAxis = axis;
                break;
            }
        }

        if (xAxis == null || yAxis == null)
            return;

        float x = xAxis.ReadValue();
        float y = yAxis.ReadValue();

        up |= y >= threshold;
        down |= y <= -threshold;
        left |= x <= -threshold;
        right |= x >= threshold;
    }

    static void AddStickAsDigital(StickControl stick, float threshold, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        if (stick == null)
            return;

        AddVectorAsDigital(stick, threshold, ref up, ref down, ref left, ref right);
    }

    static void AddVectorAsDigital(Vector2Control control, float threshold, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        if (control == null)
            return;

        Vector2 value = control.ReadValue();

        up |= value.y >= threshold;
        down |= value.y <= -threshold;
        left |= value.x <= -threshold;
        right |= value.x >= threshold;
    }
}
