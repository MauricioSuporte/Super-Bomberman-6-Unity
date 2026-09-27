#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class TouchControlsEditModeTests
{
    [Test]
    public void DefaultSettingsUseDpadAndIndependentOrientationLayouts()
    {
        var settings = new SavedTouchControls();
        settings.Normalize();
        Assert.IsFalse(settings.analog);
        Assert.IsTrue(settings.blockDiagonals);
        settings.portrait[2].positioned = true;
        settings.portrait[2].x = 0.25f;
        settings.portrait[2].scale = 1.5f;
        Assert.IsFalse(settings.landscape[2].positioned);
        Assert.AreEqual(1f, settings.landscape[2].scale);
    }

    [Test]
    public void LegacySaveWithoutTouchSettingsReceivesDefaults()
    {
        var data = JsonUtility.FromJson<SaveData>("{\"activeSlotIndex\":-1}");
        data.touchControls ??= new SavedTouchControls();
        data.touchControls.Normalize();
        Assert.IsFalse(data.touchControls.analog);
        Assert.AreEqual(8, data.touchControls.portrait.Length);
        Assert.IsNotNull(data.touchControls.portrait[7]);
    }

    [Test]
    public void NormalizeRepairsInvalidAndIncompleteSettings()
    {
        var settings = new SavedTouchControls {
            portrait = null, landscape = new TouchControlLayout[1],
            deadzone = float.NaN, opacity = float.PositiveInfinity, hideDelay = -1
        };
        settings.Normalize();
        Assert.AreEqual(0.18f, settings.deadzone);
        Assert.AreEqual(0.8f, settings.opacity);
        Assert.AreEqual(1f, settings.hideDelay);
        Assert.AreEqual(8, settings.landscape.Length);
        Assert.AreEqual(1f, settings.landscape[7].scale);
    }

    [TestCase(0.1f, 0.1f, true, 0f, 0f)]
    [TestCase(0.3f, 0.05f, true, 1f, 0f)]
    [TestCase(-0.3f, 0.7f, true, 0f, 1f)]
    [TestCase(0.5f, -0.5f, false, 1f, -1f)]
    [TestCase(-0.5f, 0.5f, false, -1f, 1f)]
    public void DirectionHonorsDeadzoneAndDiagonalSetting(float x, float y, bool block, float expectedX, float expectedY)
    {
        Assert.AreEqual(new Vector2(expectedX, expectedY),
            MobileDynamicJoystick.ResolveDirection(new Vector2(x, y), 0.2f, block));
    }

    [Test]
    public void SavedLayoutsRoundTripWithoutMixingOrientations()
    {
        var settings = new SavedTouchControls();
        settings.Normalize();
        settings.portrait[1].positioned = true;
        settings.portrait[1].x = 0.3f;
        settings.landscape[1].scale = 1.7f;
        var restored = JsonUtility.FromJson<SavedTouchControls>(JsonUtility.ToJson(settings));
        restored.Normalize();
        Assert.AreEqual(0.3f, restored.portrait[1].x);
        Assert.IsFalse(restored.landscape[1].positioned);
        Assert.AreEqual(1.7f, restored.landscape[1].scale);
    }

    [Test]
    public void HidingBridgeClearsHeldAndPendingActions()
    {
        var go = new GameObject("Touch input test");
        try
        {
            var bridge = go.AddComponent<MobileInputBridge>();
            bridge.Press(PlayerAction.ActionA);
            bridge.SetMoveVector(Vector2.up);
            go.SetActive(false);
            Assert.IsFalse(bridge.Get(PlayerAction.ActionA));
            Assert.IsFalse(bridge.GetDown(PlayerAction.ActionA));
            Assert.AreEqual(Vector2.zero, bridge.MoveVector);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
#endif