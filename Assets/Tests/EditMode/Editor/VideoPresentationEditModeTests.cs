using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class VideoPresentationEditModeTests
{
    [TestCase(1920, 1080, 1234, 1080)]
    [TestCase(2560, 1080, 1234, 1080)]
    [TestCase(1280, 1024, 1170, 1024)]
    [TestCase(1080, 1920, 1080, 945)]
    public void ProportionalScalingFitsWithoutCropping(int width, int height, int expectedWidth, int expectedHeight)
    {
        Rect rect = PixelPerfectViewport.ResolveExpandedPixelRect(width, height, 256, 224, false);
        Assert.AreEqual(expectedWidth, rect.width);
        Assert.AreEqual(expectedHeight, rect.height);
        Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0));
        Assert.That(rect.xMax, Is.LessThanOrEqualTo(width));
        Assert.That(rect.yMax, Is.LessThanOrEqualTo(height));
        Assert.That(Mathf.Abs(rect.width / rect.height - 256f / 224f), Is.LessThan(0.001f));
    }

    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    [TestCase(1080, 1920)]
    public void StretchUsesTheEntireMonitor(int width, int height)
    {
        Assert.AreEqual(new Rect(0, 0, width, height),
            PixelPerfectViewport.ResolveExpandedPixelRect(width, height, 256, 224, true));
    }

    [Test]
    public void OldSaveKeepsPixelPerfectFullscreenAndBlackBars()
    {
        var settings = JsonUtility.FromJson<SavedVideoSettings>(
            "{\"fullscreen\":true,\"windowSizeMultiplier\":4}");
        Assert.AreEqual(0, settings.displayMode);
        Assert.AreEqual(0, settings.background);
        Assert.IsTrue(settings.fullscreen);
        var fresh = new SavedVideoSettings();
        Assert.AreEqual(0, fresh.displayMode);
        Assert.AreEqual(0, fresh.background);
        Assert.IsTrue(fresh.fullscreen);
    }

    [Test]
    public void AllBackgroundsLoadWithoutCompressionOrPlatformOverrides()
    {
        for (int i = 1; i <= 9; i++)
        {
            string path = $"Assets/Resources/VideoBackgrounds/Background{i:00}.png";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                Assert.IsFalse(importer.GetPlatformTextureSettings(platform).overridden, path);
        }
    }
    [Test]
    public void BlurMaterialReceivesTheCapturedTexture()
    {
        Shader shader = Resources.Load<Shader>("VideoBackgroundBlur");
        Assert.IsNotNull(shader);
        var material = new Material(shader);
        try
        {
            // Without this property Graphics.Blit samples the default white texture,
            // producing uniform grey even when the capture contains the game.
            Assert.IsTrue(material.HasProperty("_MainTex"));
        }
        finally { Object.DestroyImmediate(material); }
    }

    [Test]
    public void BlurPreservesSceneColorsAndSoftensEdgesOnTheGpu()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device; run in the open Editor.");
        var source = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                pixels[y * 64 + x] = x < 32 ? Color.red : Color.blue;
        source.SetPixels(pixels);
        source.Apply();
        source.wrapMode = TextureWrapMode.Clamp;
        var material = new Material(Resources.Load<Shader>("VideoBackgroundBlur"));
        var output = new RenderTexture(64, 64, 0);
        var result = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            output.Create();
            Graphics.Blit(source, output, material);
            RenderTexture.active = output;
            result.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            result.Apply();
            Color left = result.GetPixel(8, 32);
            Color right = result.GetPixel(56, 32);
            Color edge = result.GetPixel(32, 32);
            Assert.That(left.r, Is.GreaterThan(left.b + 0.2f));
            Assert.That(right.b, Is.GreaterThan(right.r + 0.2f));
            Assert.That(edge.r, Is.GreaterThan(0.05f));
            Assert.That(edge.b, Is.GreaterThan(0.05f));
        }
        finally
        {
            RenderTexture.active = previous;
            output.Release();
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(material);
        }
    }

    [TestCase(1024, 896, 0, 4f, 4f)]
    [TestCase(1234, 1080, 1, 4.8203125f, 4.8203125f)]
    [TestCase(1920, 1080, 2, 7.5f, 4.8214286f)]
    [TestCase(2560, 1080, 2, 10f, 4.8214286f)]
    public void LobbyUiUsesTheSelectedPresentationScale(int width, int height, int mode, float x, float y)
    {
        Vector3 scale = PixelPerfectViewport.ResolveUiScale(new Vector2(width, height), mode);
        Assert.That(scale.x, Is.EqualTo(x).Within(0.00001f));
        Assert.That(scale.y, Is.EqualTo(y).Within(0.00001f));
        Assert.AreEqual(1f, scale.z);
    }

    [Test]
    public void RefreshingTheTitleMenuRestoresItsAuthoredPosition()
    {
        var root = new GameObject("TitleLayoutTest", typeof(RectTransform));
        root.SetActive(false); // Avoid running the scene bootstrap and changing display/save settings.
        try
        {
            var layout = root.GetComponent<RectTransform>();
            layout.sizeDelta = new Vector2(1024, 896);
            var menu = new GameObject("Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            menu.SetParent(layout, false);
            var title = root.AddComponent<TitleScreenController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(TitleScreenController).GetField("layoutRoot", flags).SetValue(title, layout);
            typeof(TitleScreenController).GetField("menuRect", flags).SetValue(title, menu);
            title.RefreshMenuText();
            Vector2 authored = menu.anchoredPosition;
            Assert.That(authored.x, Is.Not.EqualTo(-999f));
            menu.anchoredPosition = new Vector2(-999f, -999f);
            title.RefreshMenuText();
            Assert.AreEqual(authored, menu.anchoredPosition);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(1, 1234, 1080)]
    [TestCase(2, 1920, 1080)]
    public void LobbyPanelAndTimeoutFillTheResolvedViewport(int mode, int width, int height)
    {
        var root = new GameObject("LobbyLayoutTest", typeof(RectTransform));
        root.SetActive(false);
        SavedVideoSettings settings = SaveSystem.GetVideoSettings();
        int savedMode = settings.displayMode;
        try
        {
            settings.displayMode = mode;
            var safe = root.GetComponent<RectTransform>();
            safe.sizeDelta = new Vector2(width, height);
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(safe, false);
            panel.sizeDelta = new Vector2(256, 224);
            var timeout = new GameObject("Timeout", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI))
                .GetComponent<TMPro.TextMeshProUGUI>();
            timeout.transform.SetParent(safe, false);
            var lobby = root.AddComponent<Assets.Scripts.Netcode.OnlineLobbyMenu>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(Assets.Scripts.Netcode.OnlineLobbyMenu);
            type.GetField("safeRect", flags).SetValue(lobby, safe);
            type.GetField("panelRect", flags).SetValue(lobby, panel);
            type.GetField("timeoutLabel", flags).SetValue(lobby, timeout);
            type.GetMethod("LateUpdate", flags).Invoke(lobby, null);
            Assert.That(panel.rect.width * panel.localScale.x, Is.EqualTo(width).Within(1f));
            Assert.That(panel.rect.height * panel.localScale.y, Is.EqualTo(height).Within(1f));
            Assert.AreEqual(panel.localScale, timeout.rectTransform.localScale);
        }
        finally
        {
            settings.displayMode = savedMode;
            Object.DestroyImmediate(root);
        }
    }

}
