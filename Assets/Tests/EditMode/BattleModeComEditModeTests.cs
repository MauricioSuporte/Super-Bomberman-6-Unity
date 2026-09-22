#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleModeComEditModeTests
{
    [Test]
    public void PathDirectionOrder_PreservesTiesMovementPreferenceAndCallerStorage()
    {
        System.Span<Vector2Int> first = stackalloc Vector2Int[4];
        System.Span<Vector2Int> second = stackalloc Vector2Int[4];
        BattleModeComController.FillPathDirectionOrder(Vector2Int.zero, Vector2Int.one, Vector2Int.zero, first);
        Assert.AreEqual(Vector2Int.up, first[0]);
        Assert.AreEqual(Vector2Int.right, first[1]);
        Assert.AreEqual(Vector2Int.down, first[2]);
        Assert.AreEqual(Vector2Int.left, first[3]);
        BattleModeComController.FillPathDirectionOrder(Vector2Int.zero, Vector2Int.zero, Vector2Int.down, second);
        Assert.AreEqual(Vector2Int.down, second[0]);
        Assert.AreEqual(Vector2Int.up, first[0], "Another search must not overwrite its caller's directions.");
        BattleModeComController.FillPathDirectionOrder(Vector2Int.zero, Vector2Int.one, Vector2Int.right, first);
        Assert.AreEqual(Vector2Int.right, first[0]);

        long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++)
            BattleModeComController.FillPathDirectionOrder(Vector2Int.zero, Vector2Int.one, Vector2Int.right, first);
        long allocatedAfter = System.GC.GetAllocatedBytesForCurrentThread();
        Assert.AreEqual(allocatedBefore, allocatedAfter, "Ordering each BFS node must not allocate an array.");
    }

    [Test]
    public void DisabledItemPrioritySummary_DoesNotProbeSceneOrRequireSettings()
    {
        var owner = new GameObject("DisabledSummary_Test");
        owner.SetActive(false);
        try
        {
            var controller = owner.AddComponent<BattleModeComController>();
            MethodInfo summary = typeof(BattleModeComController).GetMethod(
                "BuildItemPrioritySummary", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(summary);
            Assert.AreEqual(string.Empty, summary.Invoke(controller, new object[] { null, Vector2Int.zero, 12 }));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void ExpensiveCalls_RetainOnlyThreeLargestCallsWithPlayerAndAllocations()
    {
        var frame = new GameplayPerformanceCapture.Frame();
        long millisecond = System.Diagnostics.Stopwatch.Frequency / 1000;
        frame.RecordExpensiveCall("farm", 2, millisecond * 10, 1024);
        frame.RecordExpensiveCall("path", 3, millisecond * 30, 2048);
        frame.RecordExpensiveCall("items", 1, millisecond * 20, 4096);
        frame.RecordExpensiveCall("tiny", 4, millisecond, 128);
        Assert.AreEqual("path", frame.ExpensiveCalls[0].Operation);
        Assert.AreEqual(3, frame.ExpensiveCalls[0].PlayerId);
        Assert.AreEqual(2048, frame.ExpensiveCalls[0].AllocatedBytes);
        Assert.AreEqual("items", frame.ExpensiveCalls[1].Operation);
        Assert.AreEqual("farm", frame.ExpensiveCalls[2].Operation);
    }

    [TestCase(BattleModeComputerLevel.Easy, 0.28f, 0.1f, 6)]
    [TestCase(BattleModeComputerLevel.Normal, 0.22f, 0.06f, 9)]
    [TestCase(BattleModeComputerLevel.Hard, 0.14f, 0.035f, 12)]
    public void DifficultyReset_RestoresEveryFieldWithoutMutatingOtherPlayers(
        BattleModeComputerLevel level, float decisionInterval, float dangerInterval, int searchDepth)
    {
        var playerOne = BattleModeComDifficultySettings.For(level);
        var playerTwo = BattleModeComDifficultySettings.For(level);
        var fields = typeof(BattleModeComDifficultySettings).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var expected = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            expected[i] = fields[i].GetValue(playerTwo);
            object changed = fields[i].FieldType == typeof(float) ? (object)(-123f) :
                fields[i].FieldType == typeof(int) ? -123 : (object)(BattleModeComputerLevel)(-123);
            fields[i].SetValue(playerOne, changed);
        }

        playerOne.ResetFor(level);
        Assert.AreNotSame(playerOne, playerTwo);
        for (int i = 0; i < fields.Length; i++)
        {
            Assert.AreEqual(expected[i], fields[i].GetValue(playerOne), fields[i].Name);
            Assert.AreEqual(expected[i], fields[i].GetValue(playerTwo), "Other player: " + fields[i].Name);
        }
        Assert.AreEqual(decisionInterval, playerOne.decisionInterval);
        Assert.AreEqual(dangerInterval, playerOne.dangerDecisionInterval);
        Assert.AreEqual(searchDepth, playerOne.searchDepth);
    }

    [Test]
    public void DifficultyReset_HandlesLevelChangesAndUnknownLevelAsNormal()
    {
        var settings = BattleModeComDifficultySettings.For(BattleModeComputerLevel.Hard);
        settings.ResetFor(BattleModeComputerLevel.Easy);
        Assert.AreEqual(0f, settings.advancedBombPlanChance);
        Assert.AreEqual(0.25f, settings.escapeAbilityChance);
        settings.ResetFor((BattleModeComputerLevel)999);
        Assert.AreEqual(BattleModeComputerLevel.Normal, settings.difficulty);
        Assert.AreEqual(1f, settings.advancedBombPlanChance);
        Assert.AreEqual(0.5f, settings.escapeAbilityChance);
    }

    [Test]
    public void FrameStatistics_PreservesLongStallsAndComputesNearestRankPercentiles()
    {
        var stats = new GameplayFrameStatistics();
        for (int i = 1; i <= 99; i++) Assert.IsTrue(stats.Add(i));
        Assert.IsTrue(stats.Add(88436.61f));
        Assert.AreEqual(95f, stats.Percentile(0.95f));
        Assert.AreEqual(99f, stats.Percentile(0.99f));
        Assert.AreEqual(88436.61f, stats.MaximumMilliseconds);
        Assert.That(stats.AverageMilliseconds, Is.EqualTo((4950d + 88436.61f) / 100d).Within(0.001d));
        Assert.AreEqual(84, stats.OverBudget);
        Assert.AreEqual(81, stats.Slow20);
        Assert.AreEqual(67, stats.Slow33);
    }

    [Test]
    public void FrameStatistics_ResetsRejectsInvalidSamplesAndBoundsStorage()
    {
        var stats = new GameplayFrameStatistics();
        Assert.IsFalse(stats.Add(float.NaN));
        Assert.IsFalse(stats.Add(float.PositiveInfinity));
        Assert.IsFalse(stats.Add(-1));
        for (int i = 0; i < GameplayFrameStatistics.Capacity; i++) Assert.IsTrue(stats.Add(10));
        Assert.IsFalse(stats.Add(100));
        Assert.AreEqual(10f, stats.Percentile(0.99f));
        stats.Reset();
        Assert.AreEqual(0, stats.Count);
        Assert.AreEqual(0, stats.Slow20);
        Assert.AreEqual(0f, stats.Percentile(0.95f));
        stats.Add(20);
        stats.Add(5);
        Assert.AreEqual(12.5d, stats.AverageMilliseconds);
        Assert.AreEqual(20f, stats.Percentile(0.95f));
    }

    [Test]
    public void CaptureFrames_ExcludeOnlyConfirmedInterruptionsAndResetReusedSlots()
    {
        GameplayPerformanceCapture.Start();
        try
        {
            var frame = GameplayPerformanceCapture.GetFrame(10);
            frame.Milliseconds = 88436.61f;
            Assert.IsTrue(frame.IsGameplaySample, "A long stall with missing state must remain visible.");
            frame.Interruption = GameplayPerformanceInterruption.EditorPause;
            Assert.IsFalse(frame.IsGameplaySample);
            frame.Metrics[0] = 123;
            var reused = GameplayPerformanceCapture.GetFrame(10 + GameplayPerformanceCapture.Capacity);
            Assert.AreSame(frame, reused);
            Assert.AreEqual(0, reused.Milliseconds);
            Assert.IsTrue(double.IsNaN(reused.Metrics[0]));
            Assert.IsTrue(reused.IsGameplaySample);
            Assert.AreEqual(0, reused.Phases[(int)GameplayPerformancePhase.ComThink].Calls);
        }
        finally
        {
            GameplayPerformanceCapture.Stop();
        }
    }

    [Test]
    public void CaptureScopes_AreDisabledByDefaultAndDoNotDoubleCountNestedCategoryTime()
    {
        GameplayPerformanceCapture.Start();
        try
        {
            using (GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Pathfinding))
            {
                using (GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Pathfinding)) { }
            }
            var frame = GameplayPerformanceCapture.GetFrame(Time.frameCount);
            var sample = frame.Phases[(int)GameplayPerformancePhase.Pathfinding];
            Assert.AreEqual(2, sample.Calls);
            Assert.AreEqual(sample.MaximumTicks, sample.TotalTicks, "Only the outer scope contributes to total.");
            GameplayPerformanceCapture.Stop();
            using (GameplayPerformanceCapture.Measure(GameplayPerformancePhase.Pathfinding)) { }
            Assert.AreEqual(2, frame.Phases[(int)GameplayPerformancePhase.Pathfinding].Calls);
        }
        finally
        {
            GameplayPerformanceCapture.Stop();
        }
    }

    [TearDown]
    public void TearDown()
    {
        DestroyInputManagerIfPresent();
    }

    [TestCase(PlayerAction.ActionA)]
    [TestCase(PlayerAction.Select)]
    public void PlayerInputManager_RecognizesSyntheticHeldAndDown(PlayerAction heldAction)
    {
        DestroyInputManagerIfPresent();

        var go = new GameObject("PlayerInputManager_Test");
        var input = go.AddComponent<PlayerInputManager>();

        input.SetSyntheticHeld(2, heldAction, true);

        Assert.IsTrue(input.Get(2, heldAction));
        Assert.IsTrue(input.GetDown(2, heldAction));

        InvokeLateUpdate(input);

        Assert.IsTrue(input.Get(2, heldAction));
        Assert.IsFalse(input.GetDown(2, heldAction));

        input.TapSynthetic(2, PlayerAction.ActionB);

        Assert.IsTrue(input.Get(2, PlayerAction.ActionB));
        Assert.IsTrue(input.GetDown(2, PlayerAction.ActionB));

        input.ClearSyntheticPlayer(2);

        Assert.IsFalse(input.Get(2, heldAction));
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
