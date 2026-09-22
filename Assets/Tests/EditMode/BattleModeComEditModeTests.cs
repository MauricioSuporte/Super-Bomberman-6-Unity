#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BattleModeComEditModeTests
{
    [Test]
    public void PowderTrailAnimation_RestoresTilesAndTransformsWithoutTouchingOtherCells()
    {
        var root = new GameObject("Powder trail test", typeof(Grid));
        root.SetActive(false);
        var mapObject = new GameObject("Ground", typeof(Tilemap));
        mapObject.transform.SetParent(root.transform);
        Tilemap map = mapObject.GetComponent<Tilemap>();
        var controller = root.AddComponent<BattleMode3PowderTrailController>();
        var original = ScriptableObject.CreateInstance<Tile>();
        var explosion = ScriptableObject.CreateInstance<Tile>();
        original.flags = explosion.flags = TileFlags.None;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        System.Type type = typeof(BattleMode3PowderTrailController);
        try
        {
            type.GetField("groundTilemap", flags).SetValue(controller, map);
            type.GetMethod("BuildTrailCells", flags).Invoke(controller, null);
            var cells = (List<Vector3Int>)type.GetField("trailCells", flags).GetValue(controller);
            var transforms = (Dictionary<Vector3Int, Matrix4x4>)type.GetField("explosionTransformByCell", flags).GetValue(controller);
            foreach (string field in new[] { "horizontalExplosionTiles", "horizontalExplosionTilesFlippedY",
                "verticalExplosionTiles", "verticalExplosionTilesFlippedX", "cornerExplosionTiles" })
                type.GetField(field, flags).SetValue(controller, new[] { explosion });
            Matrix4x4 originalTransform = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            foreach (Vector3Int cell in cells)
            {
                map.SetTile(cell, original);
                map.SetTransformMatrix(cell, originalTransform);
            }
            Vector3Int outside = new Vector3Int(20, 20, 0);
            map.SetTile(outside, original);
            map.SetTransformMatrix(outside, originalTransform);
            type.GetMethod("CacheOriginalTiles", flags).Invoke(controller, new object[] { true });
            // Exercise both ordinary completion and restoration on re-ignition.
            foreach (string restore in new[] { "RestoreOriginalTiles", "RestoreStableTrailStateIfNeeded" })
            {
                type.GetMethod("ApplyExplosionTileFrame", flags).Invoke(controller, new object[] { 0 });
                foreach (Vector3Int cell in cells)
                {
                    Assert.AreSame(explosion, map.GetTile(cell));
                    Assert.AreEqual(transforms[cell], map.GetTransformMatrix(cell));
                }
                type.GetMethod(restore, flags).Invoke(controller,
                    restore == "RestoreOriginalTiles" ? null : new object[] { true });
                foreach (Vector3Int cell in cells)
                {
                    Assert.AreSame(original, map.GetTile(cell));
                    Assert.AreEqual(originalTransform, map.GetTransformMatrix(cell));
                }
                Assert.AreSame(original, map.GetTile(outside));
                Assert.AreEqual(originalTransform, map.GetTransformMatrix(outside));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(explosion);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CandidateScorePruning_PreservesExhaustiveWinnerWithDetoursAndUnreachableTargets(bool farm)
    {
        var random = new System.Random(81723);
        int skipped = 0;
        for (int scenario = 0; scenario < 100; scenario++)
        {
            float exhaustiveScore = float.NegativeInfinity;
            float prunedScore = float.NegativeInfinity;
            int exhaustiveWinner = -1, prunedWinner = -1;
            for (int candidate = 0; candidate < 64; candidate++)
            {
                int minimumDistance = random.Next(0, 15);
                int actualDistance = minimumDistance + random.Next(0, 8);
                float baseScore = farm ? random.Next(1, 6) * 1000f : random.Next(0, 3) * 0.15f;
                float noise = (float)random.NextDouble() - 0.5f;
                bool reachable = random.Next(0, 4) != 0;
                float score = baseScore - actualDistance * 10f + noise;
                if (reachable && score > exhaustiveScore)
                {
                    exhaustiveScore = score;
                    exhaustiveWinner = candidate;
                }
                if (!BattleModeComController.CanDistanceScoredCandidateImprove(
                        minimumDistance, farm ? baseScore : 0.3f, noise, prunedScore))
                {
                    skipped++;
                    continue;
                }
                if (reachable && score > prunedScore)
                {
                    prunedScore = score;
                    prunedWinner = candidate;
                }
            }
            Assert.AreEqual(exhaustiveWinner, prunedWinner, $"Scenario {scenario}");
            Assert.AreEqual(exhaustiveScore, prunedScore);
        }
        Assert.Greater(skipped, 0, "The bound must actually avoid unnecessary searches.");
    }

    [Test]
    public void CandidateScorePruning_PreservesFirstWinnerOnExactTie()
    {
        Assert.IsFalse(BattleModeComController.CanDistanceScoredCandidateImprove(2, 1000f, 0f, 980f));
        Assert.IsTrue(BattleModeComController.CanDistanceScoredCandidateImprove(2, 2000f, 0f, 980f));
        Assert.IsTrue(BattleModeComController.CanDistanceScoredCandidateImprove(20, 0.3f, 0f, float.NegativeInfinity));
    }

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
