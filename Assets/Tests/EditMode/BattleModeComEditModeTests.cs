#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleModeComEditModeTests
{
    [TearDown]
    public void TearDown()
    {
        DestroyInputManagerIfPresent();
    }

    [Test]
    public void PlayerInputManager_RecognizesSyntheticHeldAndDown()
    {
        DestroyInputManagerIfPresent();

        var go = new GameObject("PlayerInputManager_Test");
        var input = go.AddComponent<PlayerInputManager>();

        input.SetSyntheticHeld(2, PlayerAction.ActionA, true);

        Assert.IsTrue(input.Get(2, PlayerAction.ActionA));
        Assert.IsTrue(input.GetDown(2, PlayerAction.ActionA));

        InvokeLateUpdate(input);

        Assert.IsTrue(input.Get(2, PlayerAction.ActionA));
        Assert.IsFalse(input.GetDown(2, PlayerAction.ActionA));

        input.TapSynthetic(2, PlayerAction.ActionB);

        Assert.IsTrue(input.Get(2, PlayerAction.ActionB));
        Assert.IsTrue(input.GetDown(2, PlayerAction.ActionB));

        input.ClearSyntheticPlayer(2);

        Assert.IsFalse(input.Get(2, PlayerAction.ActionA));
        Assert.IsFalse(input.Get(2, PlayerAction.ActionB));
    }

    [Test]
    public void ExplosionLine_IsBlockedBySolidTile()
    {
        var blockers = new HashSet<Vector2Int>
        {
            new(1, 0)
        };

        Assert.IsTrue(BattleModeComController.DebugIsTileInExplosionLine(Vector2Int.zero, new Vector2Int(1, 0), 3, blockers));
        Assert.IsFalse(BattleModeComController.DebugIsTileInExplosionLine(Vector2Int.zero, new Vector2Int(2, 0), 3, blockers));
        Assert.IsFalse(BattleModeComController.DebugIsTileInExplosionLine(Vector2Int.zero, new Vector2Int(1, 1), 3, blockers));
    }

    [Test]
    public void CombatPlantEscape_ReturnsFalseWhenNoRouteLeavesBlast()
    {
        var walkable = new HashSet<Vector2Int>
        {
            Vector2Int.zero,
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        bool canEscape = BattleModeComController.DebugCanPlantBombWithEscape(
            Vector2Int.zero,
            2,
            walkable,
            blockingTiles: null,
            maxDepth: 4);

        Assert.IsFalse(canEscape);
    }

    [Test]
    public void SummaryLog_ContainsActionReasonAndPlayerId()
    {
        string summary = BattleModeComDiagnostics.FormatSummary(
            "BattleMode_1",
            123,
            3,
            BattleModeComputerLevel.Normal,
            BattleModeComActionType.CombatPlant,
            "(2, 0)",
            Vector2Int.zero,
            "safe",
            "found",
            "target in blast line",
            "ActionA");

        StringAssert.Contains("playerId:3", summary);
        StringAssert.Contains("action:CombatPlant", summary);
        StringAssert.Contains("reason:target in blast line", summary);
    }

    private static void InvokeLateUpdate(PlayerInputManager input)
    {
        MethodInfo method = typeof(PlayerInputManager).GetMethod(
            "LateUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(input, null);
    }

    private static void DestroyInputManagerIfPresent()
    {
        if (PlayerInputManager.Instance != null)
            Object.DestroyImmediate(PlayerInputManager.Instance.gameObject);
    }
}
#endif
