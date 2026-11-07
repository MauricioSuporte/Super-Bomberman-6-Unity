using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using System.Reflection;

public sealed class VideoScreenFilterEditModeTests
{
    [Test]
    public void OldSavesStartWithTheFilterOffAndUsefulIntensity()
    {
        var old = JsonUtility.FromJson<SavedVideoSettings>("{\"fullscreen\":true,\"windowSizeMultiplier\":4}");
        old.NormalizeScreenFilter();
        Assert.AreEqual(0, old.screenFilter);
        Assert.AreEqual(50, old.filterIntensity);
        Assert.IsTrue(old.fullscreen);
        var legacy = new SavedVideoSettings { screenFilterInitialized = false, screenFilter = 4, filterIntensity = 0 };
        legacy.NormalizeScreenFilter();
        Assert.AreEqual(0, legacy.screenFilter);
        Assert.AreEqual(50, legacy.filterIntensity);
    }

    [Test]
    public void FilterChoicesRoundTripAndInvalidValuesAreNormalized()
    {
        var settings = new SavedVideoSettings { screenFilter = 3, filterIntensity = 75, displayMode = 2, background = 4 };
        var restored = JsonUtility.FromJson<SavedVideoSettings>(JsonUtility.ToJson(settings));
        restored.NormalizeScreenFilter();
        Assert.AreEqual(3, restored.screenFilter);
        Assert.AreEqual(75, restored.filterIntensity);
        Assert.AreEqual(2, restored.displayMode);
        Assert.AreEqual(4, restored.background);
        restored.screenFilter = 99;
        restored.filterIntensity = -9;
        restored.NormalizeScreenFilter();
        Assert.AreEqual(SavedVideoSettings.ScreenFilterCount - 1, restored.screenFilter);
        Assert.AreEqual(0, restored.filterIntensity);
    }

    [Test]
    public void EveryLanguageHasTheSameFilterChoices()
    {
        foreach (var language in GameTextDatabase.SupportedLanguages)
        {
            var text = GameTextDatabase.GetTitleScreenText(language);
            Assert.IsNotEmpty(text.ScreenFilter);
            Assert.IsNotEmpty(text.FilterIntensity);
            Assert.AreEqual(SavedVideoSettings.ScreenFilterCount, text.ScreenFilters.Length);
            foreach (string choice in text.ScreenFilters) Assert.IsNotEmpty(choice);
        }
    }

    [Test]
    public void DisabledFilterDoesNotCreateACanvasOrMaterial()
    {
        var settings = SaveSystem.GetVideoSettings();
        int original = settings.screenFilter;
        var root = new GameObject("DisabledFilterTest");
        root.SetActive(false);
        try
        {
            settings.screenFilter = 0;
            root.AddComponent<Camera>();
            var presenter = root.AddComponent<VideoFilterPresenter>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(VideoFilterPresenter).GetMethod("LateUpdate", flags).Invoke(presenter, null);
            Assert.IsNull(root.GetComponentInChildren<Canvas>(true));
            Assert.IsNull(typeof(VideoFilterPresenter).GetField("filterMaterial", flags).GetValue(presenter));
        }
        finally { settings.screenFilter = original; Object.DestroyImmediate(root); }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void EachFilterProducesANonUniformGpuMask(int filter)
    {
        Color[] colors = RenderMask(filter, 1f);
        float minimum = 1f, maximum = 0f, sum = 0f;
        foreach (Color color in colors)
        {
            minimum = Mathf.Min(minimum, color.r, color.g, color.b);
            maximum = Mathf.Max(maximum, color.r, color.g, color.b);
            sum += (color.r + color.g + color.b) / 3f;
        }
        Assert.That(maximum - minimum, Is.GreaterThan(0.03f));
        Assert.That(minimum, Is.GreaterThan(0.25f), "Reject black/error shader output.");
        Assert.That(sum / colors.Length, Is.GreaterThan(0.55f), "The effect must preserve readable bright UI.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void ZeroIntensityIsVisuallyNeutral(int filter)
    {
        foreach (Color color in RenderMask(filter, 0f))
        {
            Assert.That(color.r, Is.GreaterThan(0.99f));
            Assert.That(color.g, Is.GreaterThan(0.99f));
            Assert.That(color.b, Is.GreaterThan(0.99f));
        }
    }

    [Test]
    public void OffIsVisuallyNeutralEvenAtFullIntensity()
    {
        foreach (Color color in RenderMask(0, 1f))
            Assert.That(Mathf.Min(color.r, color.g, color.b), Is.GreaterThan(0.99f));
    }

    static Color[] RenderMask(int filter, float strength)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires the graphical Unity Editor.");
        Shader shader = Resources.Load<Shader>("VideoScreenFilter");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        var material = new Material(shader);
        var output = new RenderTexture(64, 56, 0, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(64, 56, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            material.SetFloat("_Filter", filter);
            material.SetFloat("_Strength", strength);
            material.SetVector("_ReferenceSize", new Vector4(16, 14, 0, 0));
            material.SetVector("_OutputSize", new Vector4(64, 56, 0, 0));
            output.Create();
            RenderTexture.active = output;
            GL.Clear(false, true, Color.white);
            Graphics.Blit(Texture2D.whiteTexture, output, material);
            RenderTexture.active = output;
            readback.ReadPixels(new Rect(0, 0, 64, 56), 0, 0);
            readback.Apply();
            return readback.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
            output.Release();
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(material);
        }
    }
}
