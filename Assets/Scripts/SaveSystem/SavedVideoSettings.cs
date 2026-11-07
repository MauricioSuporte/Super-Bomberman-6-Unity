using System;

[Serializable]
public sealed class SavedVideoSettings
{
    // Zero preserves the existing safe frame for older saves.
    public int displayMode;
    public int background;
    public const int ScreenFilterCount = 5;
    public int screenFilter;
    public int filterIntensity = 50;
    public bool screenFilterInitialized = true;

    public void NormalizeScreenFilter()
    {
        if (!screenFilterInitialized)
        {
            screenFilter = 0;
            filterIntensity = 50;
            screenFilterInitialized = true;
        }
        screenFilter = Math.Clamp(screenFilter, 0, ScreenFilterCount - 1);
        filterIntensity = Math.Clamp(filterIntensity, 0, 100);
    }
    public bool fullscreen = true;
    public int windowSizeMultiplier = 4;
    public bool mobileTouchButtonsVisible = true;
    public bool mobileTouchButtonsVisibilityInitialized = true;
}
