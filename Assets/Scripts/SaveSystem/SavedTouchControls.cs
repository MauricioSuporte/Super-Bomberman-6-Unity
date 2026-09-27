using System;
using UnityEngine;

[Serializable]
public sealed class SavedTouchControls
{
    public bool analog;
    public bool dynamicAnalog = true;
    public bool blockDiagonals = true;
    public float deadzone = 0.18f;
    public bool autoHide;
    public float hideDelay = 5f;
    public bool actionIcons = true;
    public bool hideUnused;
    public float opacity = 0.8f;
    public TouchControlLayout[] portrait = new TouchControlLayout[8];
    public TouchControlLayout[] landscape = new TouchControlLayout[8];

    public void Normalize()
    {
        deadzone = FiniteClamp(deadzone, 0f, 0.8f, 0.18f);
        hideDelay = FiniteClamp(hideDelay, 1f, 60f, 5f);
        opacity = FiniteClamp(opacity, 0.1f, 1f, 0.8f);
        NormalizeLayout(ref portrait);
        NormalizeLayout(ref landscape);
    }

    static float FiniteClamp(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    static void NormalizeLayout(ref TouchControlLayout[] layout)
    {
        if (layout == null) layout = new TouchControlLayout[8];
        if (layout.Length != 8) Array.Resize(ref layout, 8);
        for (int i = 0; i < layout.Length; i++)
        {
            layout[i] ??= new TouchControlLayout();
            layout[i].x = FiniteClamp(layout[i].x, 0f, 1f, 0.5f);
            layout[i].y = FiniteClamp(layout[i].y, 0f, 1f, 0.5f);
            layout[i].scale = FiniteClamp(layout[i].scale, 0.5f, 2f, 1f);
        }
    }

    public TouchControlLayout[] Layout(bool isPortrait) => isPortrait ? portrait : landscape;
}

[Serializable]
public sealed class TouchControlLayout
{
    public bool positioned;
    public float x = 0.5f;
    public float y = 0.5f;
    public float scale = 1f;
}