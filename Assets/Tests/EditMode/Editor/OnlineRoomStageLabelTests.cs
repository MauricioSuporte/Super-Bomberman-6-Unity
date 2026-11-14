using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(1f, 1f, .25f)]
    [TestCase(2f, 1f, .5f)]
    [TestCase(4f, 1f, 1f)]
    [TestCase(8f, 1f, 2f)]
    [TestCase(1f, 2f, .125f)]
    [TestCase(8f, .5f, 4f)]
    [TestCase(32f, 1f, 5f)]
    public void StageLabelLimitsPixelZoomBeforeDesignAndCanvasCompensation(float zoom, float canvas, float expected)
    {
        var method = typeof(StageLabel).GetMethod("ResolveDesignScale", BindingFlags.NonPublic | BindingFlags.Static);
        float scale = (float)method.Invoke(null, new object[] { zoom, canvas, 4, 1f, 1f, 20f });
        Assert.AreEqual(expected, scale, .00001f);
        Assert.AreEqual(44f * Mathf.Min(zoom, 20f) / 4f, 44f * scale * canvas, .0001f);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void StageLabelRebuildsItsCurrentViewLocallyWhenCanvasScaleChanges(int view)
    {
        var root = new GameObject("StageLabelCanvas", typeof(Canvas));
        var child = new GameObject("StageLabelText", typeof(RectTransform)); child.transform.SetParent(root.transform, false);
        try
        {
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.scaleFactor = 1;
            var text = child.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF"); Assert.IsNotNull(text.font);
            var label = child.AddComponent<StageLabel>(); label.stageText = text;
            if (view == 0) label.SetStage(2, 3);
            else if (view == 1) label.SetPauseMenu(2, 3, 2, false);
            else if (view == 2) label.SetBattleModePauseMenu(2, 3, 2);
            else if (view == 3) label.SetPauseConfirmReturnToWorldMap(2, 3, 1);
            else label.SetPauseMenu(2, 3, 2, true);
            string before = text.text;
            float baseSizeBefore = text.fontSize;
            Vector2 preferredBefore = new(text.preferredWidth, text.preferredHeight);
            var sizes = Regex.Matches(before, @"<size=([\d.]+)>"); Assert.Greater(sizes.Count, 0);
            canvas.scaleFactor = 2;
            typeof(StageLabel).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(label, null);
            Assert.AreEqual(baseSizeBefore, text.fontSize * 2, .003f, "Inter-span spaces and blank lines must scale too.");
            Assert.AreEqual(preferredBefore.x, text.preferredWidth * 2, 1.5f, "Integer indents can round by half a local pixel on each Canvas.");
            Assert.AreEqual(preferredBefore.y, text.preferredHeight * 2, 1.5f);
            var after = Regex.Matches(text.text, @"<size=([\d.]+)>"); Assert.AreEqual(sizes.Count, after.Count);
            for (int i = 0; i < sizes.Count; i++)
                Assert.AreEqual(float.Parse(sizes[i].Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(after[i].Groups[1].Value, CultureInfo.InvariantCulture) * 2, .003f);
            Assert.AreEqual(Regex.Replace(before, @"<(?:size|indent)=[^>]+>", ""), Regex.Replace(text.text, @"<(?:size|indent)=[^>]+>", ""),
                "Only local layout changes; stage, options and selection must stay intact.");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
