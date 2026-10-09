#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BattleModeComEditModeTests
{
    [TestCase(1, -1, 0, 6)]
    [TestCase(1, 1, 0, 8)]
    [TestCase(8, -1, 0, 1)]
    [TestCase(8, 1, 0, 6)]
    [TestCase(8, 0, 1, 7)]
    [TestCase(8, 0, -1, 0)]
    [TestCase(7, 0, -1, 8)]
    [TestCase(7, 0, 1, 0)]
    [TestCase(2, 1, 0, 8)]
    [TestCase(2, -1, 0, 1)]
    [TestCase(3, 1, 0, 7)]
    [TestCase(3, -1, 0, 2)]
    public void World3Hall_NavigationIncludesCentralChipAndExit(int stage, int x, int y, int expected)
    {
        var neighbor = typeof(StageAssets.World3HallPortalSelectionController).GetMethod(
            "Neighbor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreEqual(expected, neighbor.Invoke(null, new object[] { stage, new Vector2(x, y) }));
    }

    [TestCase(1, 1, 0, 2)]
    [TestCase(2, 1, 0, 3)]
    [TestCase(1, 0, 1, 2)]
    [TestCase(3, -1, 0, 2)]
    [TestCase(3, 0, -1, 2)]
    [TestCase(3, 1, 0, 7)]
    [TestCase(7, 1, 0, 4)]
    [TestCase(4, -1, 0, 7)]
    [TestCase(7, -1, 0, 3)]
    [TestCase(6, 1, 0, 0)]
    [TestCase(0, 1, 0, 1)]
    [TestCase(0, -1, 0, 6)]
    public void World3Hall_LockedChipKeepsNumericalNavigation(int stage, int x, int y, int expected)
    {
        var neighbor = typeof(StageAssets.World3HallPortalSelectionController).GetMethod(
            "SequentialNeighbor", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreEqual(expected, neighbor.Invoke(null, new object[] { stage, new Vector2(x, y) }));
    }

    [Test]
    public void World3Unlocks_DuelCompletionUnlocksFinalChipStageOnlyWithAllParts()
    {
        var slot = new Assets.Scripts.SaveSystem.StageSlot();
        var ensure = typeof(StageUnlockProgress).GetMethod("EnsureWorld3Unlocks", BindingFlags.Static | BindingFlags.NonPublic);
        for (int i = 1; i <= 6; i++) slot.clearedStages.Add($"Stage_3-{i}");
        ensure.Invoke(null, new object[] { slot });
        Assert.Contains("Stage_3-7", slot.unlockedStages);
        Assert.IsFalse(slot.unlockedStages.Contains("Stage_3-8"));
        slot.clearedStages.Add("Stage_3-7");
        ensure.Invoke(null, new object[] { slot });
        Assert.Contains("Stage_3-8", slot.unlockedStages);
        Assert.IsFalse(slot.clearedStages.Contains("Stage_3-8"), "Unlocking the chip must not mark the final stage cleared.");
        slot.clearedStages.Remove("Stage_3-1");
        ensure.Invoke(null, new object[] { slot });
        Assert.IsFalse(slot.unlockedStages.Contains("Stage_3-7"));
        Assert.IsFalse(slot.unlockedStages.Contains("Stage_3-8"));
    }

    [TestCase(0.29f, 0, 0)]
    [TestCase(0.31f, 1, 0)]
    [TestCase(0.61f, 1, 1)]
    [TestCase(0.91f, 2, 1)]
    public void SearchBombAwareness_PredictionPreservesTrailTurns(float until, int x, int y)
    {
        var player = new GameObject("Search prediction player");
        var bombObject = new GameObject("Search prediction bomb");
        try
        {
            var awareness = player.AddComponent<BattleModeComSearchBombAwarenessAbility>();
            var bomb = bombObject.AddComponent<Bomb>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(BattleModeComSearchBombAwarenessAbility).GetNestedType("Threat", BindingFlags.NonPublic);
            object threat = System.Activator.CreateInstance(type);
            type.GetField("Bomb").SetValue(threat, bomb);
            type.GetField("StepSeconds").SetValue(threat, 0.3f);
            var cells = new List<Vector2Int> { Vector2Int.right, new(1, 1), new(2, 1) };
            object[] args = { threat, cells, 0, Vector2Int.zero, 0.3f, until, false };
            typeof(BattleModeComSearchBombAwarenessAbility).GetMethod("AdvancePrediction", flags).Invoke(awareness, args);
            Assert.AreEqual(new Vector2Int(x, y), args[3]);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(bombObject);
        }
    }

    [Test]
    public void SearchBombAwareness_PredictionStopsAtBlockedTrailTile()
    {
        var player = new GameObject("Search prediction player");
        var bombObject = new GameObject("Search prediction bomb");
        var obstacle = new GameObject("Search prediction wall");
        try
        {
            obstacle.layer = LayerMask.NameToLayer("Stage");
            obstacle.transform.position = Vector2.right;
            obstacle.AddComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            var awareness = player.AddComponent<BattleModeComSearchBombAwarenessAbility>();
            var bomb = bombObject.AddComponent<Bomb>();
            var type = typeof(BattleModeComSearchBombAwarenessAbility).GetNestedType("Threat", BindingFlags.NonPublic);
            object threat = System.Activator.CreateInstance(type);
            type.GetField("Bomb").SetValue(threat, bomb);
            type.GetField("StepSeconds").SetValue(threat, 0.3f);
            object[] args = { threat, new List<Vector2Int> { Vector2Int.right, new(2, 0) },
                0, Vector2Int.zero, 0.3f, 3f, false };
            typeof(BattleModeComSearchBombAwarenessAbility).GetMethod("AdvancePrediction",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(awareness, args);
            Assert.AreEqual(Vector2Int.zero, args[3]);
        }
        finally
        {
            Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(bombObject);
        }
    }

    [Test]
    public void SearchBomb_TileTrailPreservesTurnsAndBacktracking()
    {
        var root = new GameObject("Search bomb trail");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        try
        {
            var search = root.AddComponent<SearchBomb>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var record = typeof(SearchBomb).GetMethod("RecordTargetTile", flags);
            var trail = (Queue<Vector2Int>)typeof(SearchBomb).GetField("targetTrail", flags).GetValue(search);
            Vector2Int[] visited = { new(1, 0), new(1, 1), new(2, 1), new(1, 1) };
            foreach (var cell in visited) record.Invoke(search, new object[] { cell });
            record.Invoke(search, new object[] { visited[visited.Length - 1] });
            CollectionAssert.AreEqual(visited, trail.ToArray(), "Turns and revisited tiles must not be shortcut.");
            search.SuspendPursuit();
            Assert.AreEqual(0, trail.Count, "External movement invalidates the old target trail.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SearchBomb_TileTrailFillsStraightSamplesAndClearsTeleportGaps()
    {
        var root = new GameObject("Search bomb trail samples");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        try
        {
            var search = root.AddComponent<SearchBomb>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var record = typeof(SearchBomb).GetMethod("RecordTargetTile", flags);
            var trail = (Queue<Vector2Int>)typeof(SearchBomb).GetField("targetTrail", flags).GetValue(search);
            record.Invoke(search, new object[] { new Vector2Int(3, 0) });
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) }, trail.ToArray());
            record.Invoke(search, new object[] { new Vector2Int(20, 0) });
            Assert.AreEqual(0, trail.Count, "Do not invent a trail across teleportation.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(3, 0, true)]
    [TestCase(2, 1, true)]
    [TestCase(4, 0, false)]
    [TestCase(3, 1, false)]
    public void SearchBomb_PursuitRangeRejectsTargetsBeyondThreeTiles(int x, int y, bool expected)
    {
        var root = new GameObject("Search bomb range");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        try
        {
            var search = root.AddComponent<SearchBomb>();
            var method = typeof(SearchBomb).GetMethod("IsWithinDetectionRange", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual(expected, method.Invoke(search, new object[] { Vector2Int.zero, new Vector2Int(x, y) }));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SearchBomb_DuelBlocksUseSceneAmountsAndPreserveOtherHiddenDrops()
    {
        var root = new GameObject("Duel drop distribution", typeof(Grid));
        root.SetActive(false);
        var child = new GameObject("Destructibles", typeof(Tilemap));
        child.transform.SetParent(root.transform, false);
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var map = child.GetComponent<Tilemap>();
            map.SetTile(Vector3Int.zero, tile);
            map.SetTile(Vector3Int.right, tile);
            var manager = root.AddComponent<GameManager>();
            manager.destructibleTilemap = map;
            manager.extraBombAmount = 1;
            manager.blastRadiusAmount = 0;
            manager.speedIncreaseAmount = 0;
            var hidden = (Dictionary<Vector3Int, GameObject>)typeof(GameManager)
                .GetField("hiddenObjectSpawnsByCell", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Vector3Int existing = new(9, 9, 0);
            hidden[existing] = null;
            manager.RegisterSpawnedDestructibles(new[] { Vector3Int.zero, Vector3Int.right, Vector3Int.zero });
            Assert.IsTrue(hidden.ContainsKey(existing));
            Assert.AreEqual(2, hidden.Count, "One scene-configured drop plus the existing unrelated mapping.");
            foreach (var entry in hidden)
                if (entry.Key != existing)
                    Assert.AreEqual(ItemType.ExtraBomb, entry.Value.GetComponent<ItemPickup>().type);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tile);
        }
    }

    [Test]
    public void SearchBomb_PathAcceptsEmptyInteriorOfSparseWallBounds()
    {
        var grid = new GameObject("Search bomb bounds", typeof(Grid));
        grid.SetActive(false);
        grid.transform.position = new Vector3(1000f, 1000f, 0f);
        var mapObject = new GameObject("Indestructibles", typeof(Tilemap));
        mapObject.transform.SetParent(grid.transform, false);
        var root = new GameObject("Search bomb bounds test");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        var wall = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var map = mapObject.GetComponent<Tilemap>();
            map.SetTile(new Vector3Int(-2, -2, 0), wall);
            map.SetTile(new Vector3Int(2, 2, 0), wall);
            var bomb = root.AddComponent<Bomb>();
            bomb.SetStageBoundsTilemap(map);
            typeof(Bomb).GetField("indestructibleTilemap", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bomb, map);
            Assert.IsFalse(map.HasTile(Vector3Int.zero));
            Assert.IsTrue(bomb.CanSearchMoveTo(new Vector2(1000.5f, 1000.5f), 1f),
                "Free cells inside a wall map's bounds must be usable for pursuit.");
            Assert.IsFalse(bomb.CanSearchMoveTo(new Vector2(1002.5f, 1002.5f), 1f));
            Assert.IsFalse(bomb.CanSearchMoveTo(new Vector2(1004.5f, 1000.5f), 1f));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(grid);
            Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void SearchBomb_SheetAndBothPrefabsUseTheFourInGameFrames()
    {
        const string sheetPath = "Assets/Resources/Sprites/BombItems/Itens.png";
        var sprites = new Dictionary<string, Sprite>();
        foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite sprite) sprites[sprite.name] = sprite;
        Sprite[] expected = new Sprite[4];
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.IsTrue(sprites.TryGetValue($"SearchBombFrame{i + 1}", out expected[i]));
            Assert.AreEqual(new Rect((22 + i) * 16, expected[i].texture.height - 32, 16, 16), expected[i].rect);
        }
        foreach (string path in new[] { "Assets/Prefabs/Bombs/SearchBomb.prefab",
                     "Assets/Resources/Bombs/SearchBomb.prefab" })
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab.GetComponent<SearchBomb>());
            var renderer = prefab.GetComponent<AnimatedSpriteRenderer>();
            CollectionAssert.AreEqual(expected, renderer.animationSprite);
            Assert.AreEqual(expected[0], renderer.idleSprite);
            Assert.AreEqual(expected[0], prefab.GetComponent<SpriteRenderer>().sprite);
            Assert.IsFalse(renderer.idle);
            Assert.IsTrue(renderer.loop);
        }
    }

    [Test]
    public void SearchBomb_PrettyBomberUsesSharedAbilityAndRenamedPrefab()
    {
        var pretty = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombers/PrettyBomber.prefab");
        var search = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/SearchBomb.prefab");
        var normal = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bombs/Bomb.prefab");
        Assert.IsNotNull(pretty.GetComponent<AbilitySystem>());
        Assert.IsTrue(pretty.GetComponent<SearchBombAbility>().IsEnabled);
        Assert.AreSame(search, pretty.GetComponent<BombController>().searchBombPrefab);
        Assert.AreSame(normal, pretty.GetComponent<BombController>().bombPrefab);
    }

    [TestCase("Player", true)]
    [TestCase("Enemy", false)]
    public void SearchBomb_EnemyOwnerOnlyPursuesPlayers(string candidateLayer, bool expected)
    {
        var owner = new GameObject("Search bomb enemy owner");
        owner.SetActive(false);
        owner.layer = LayerMask.NameToLayer("Enemy");
        var root = new GameObject("Search bomb targeting");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        var candidate = new GameObject("Search bomb target");
        candidate.layer = LayerMask.NameToLayer(candidateLayer);
        try
        {
            var controller = owner.AddComponent<BombController>();
            var bomb = root.AddComponent<Bomb>();
            var search = root.AddComponent<SearchBomb>();
            var collider = candidate.AddComponent<CircleCollider2D>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Bomb).GetField("owner", flags).SetValue(bomb, controller);
            typeof(SearchBomb).GetField("bomb", flags).SetValue(search, bomb);
            Assert.AreEqual(expected, typeof(SearchBomb).GetMethod("IsValidTarget", flags, null, new[] { typeof(Collider2D) }, null)
                .Invoke(search, new object[] { collider }));
        }
        finally
        {
            Object.DestroyImmediate(candidate);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void SearchBomb_LegacyBattleAmountsPreserveLineBombAndDefaultSearchToZero()
    {
        var previous = new int[20];
        for (int i = 0; i < previous.Length; i++) previous[i] = i;
        var convert = typeof(SaveSystem).GetMethod("ConvertBattleModeItemAmounts",
            BindingFlags.Static | BindingFlags.NonPublic);
        var converted = (int[])convert.Invoke(null, new object[]
        {
            previous, new int[21], GameManager.BattleModeHiddenDropEntries.Length
        });
        for (int i = 0; i < previous.Length; i++) Assert.AreEqual(previous[i], converted[i]);
        Assert.AreEqual(ItemType.LineBomb, GameManager.BattleModeHiddenDropEntries[19].ItemType);
        Assert.AreEqual(ItemType.SearchBomb, GameManager.BattleModeHiddenDropEntries[20].ItemType);
        Assert.AreEqual(0, converted[20]);
    }

    [Test]
    public void SearchBomb_AbilityReplacesOtherBombTypesAndCanBeReplaced()
    {
        var player = new GameObject("Search bomb exclusivity");
        player.SetActive(false);
        try
        {
            var abilities = player.AddComponent<AbilitySystem>();
            abilities.Enable(MagnetBombAbility.AbilityId);
            abilities.Enable(SearchBombAbility.AbilityId);
            Assert.IsTrue(abilities.IsEnabled(SearchBombAbility.AbilityId));
            Assert.IsFalse(abilities.IsEnabled(MagnetBombAbility.AbilityId));
            abilities.Enable(RubberBombAbility.AbilityId);
            Assert.IsFalse(abilities.IsEnabled(SearchBombAbility.AbilityId));
            Assert.IsTrue(abilities.IsEnabled(RubberBombAbility.AbilityId));
        }
        finally { Object.DestroyImmediate(player); }
    }

    [TestCase("Glove")]
    [TestCase("YellowLouie")]
    [TestCase("ExternalStop")]
    [TestCase("Moved")]
    public void SearchBomb_ExternalMovementSuspendsThenAllowsSearchAgain(string action)
    {
        var root = new GameObject("Search bomb cancellation");
        root.SetActive(false);
        root.AddComponent<CircleCollider2D>();
        try
        {
            var bomb = root.AddComponent<Bomb>();
            var search = root.AddComponent<SearchBomb>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SearchBomb).GetField("bomb", flags).SetValue(search, bomb);
            switch (action)
            {
                case "Glove": bomb.SetPowerGloveHeld(true); bomb.SetPowerGloveHeld(false); break;
                case "YellowLouie": bomb.SetYellowLouieKickMovement(true); bomb.SetYellowLouieKickMovement(false); break;
                case "ExternalStop": bomb.StopKickPunchMagnetRoutines(); break;
                case "Moved": bomb.MarkMovedByKickOrPunch(); break;
            }
            Assert.IsTrue((bool)typeof(SearchBomb).GetField("pursuitSuspended",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(search));
            Assert.IsFalse(bomb.HasExploded, "Suspending pursuit must leave the fuse alive.");
            var update = typeof(SearchBomb).GetMethod("Update", flags);
            bomb.SetPowerGloveHeld(true);
            update.Invoke(search, null);
            Assert.IsTrue((bool)typeof(SearchBomb).GetField("pursuitSuspended", flags).GetValue(search),
                "Search cannot resume while the glove holds the bomb.");
            bomb.SetPowerGloveHeld(false);
            update.Invoke(search, null);
            Assert.IsFalse((bool)typeof(SearchBomb).GetField("pursuitSuspended", flags).GetValue(search),
                "After all movement ends the bomb can search again.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SearchBomb_LoadoutSnapshotCopiesTheBombType()
    {
        var state = new PlayerPersistentStats.PlayerState { HasSearchBomb = true };
        var snapshot = PlayerPersistentStats.CloneState(state);
        Assert.IsTrue(snapshot.HasSearchBomb);
        Assert.IsFalse(snapshot.HasMagnetBomb);
        state.HasSearchBomb = false;
        Assert.IsTrue(snapshot.HasSearchBomb);
    }

    [Test]
    public void LineBombCom_MountedItemAndPurpleSourcesSurviveIndependentRemoval()
    {
        var player = new GameObject("LineBomb COM sources");
        player.SetActive(false);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var abilities = player.AddComponent<AbilitySystem>();
            abilities.Enable(LineBombAbility.AbilityId);
            var com = player.AddComponent<BattleModeComPurpleLouieBombLineAbility>();
            var movement = player.GetComponent<MovementController>();
            typeof(MovementController).GetField("isMounted", flags).SetValue(movement, true);
            Assert.IsTrue(com.IsAvailable);
            Assert.AreEqual("LineBombItemA", com.DiagnosticName);

            var state = typeof(BattleModeComPurpleLouieBombLineAbility).GetField("sequenceState", flags);
            state.SetValue(com, System.Enum.Parse(state.FieldType, "PreparingItemLine"));
            abilities.Enable(PurpleLouieBombLineAbility.AbilityId);
            Assert.IsTrue(com.IsAvailable);
            Assert.AreEqual("LineBombItemA", com.DiagnosticName, "In-flight item sequence keeps ActionA.");
            abilities.Enable(PowerGloveAbility.AbilityId);
            Assert.IsTrue(com.IsAvailable);
            Assert.AreEqual("PurpleLouieLineC", com.DiagnosticName);
            Assert.AreEqual("None", state.GetValue(com).ToString());

            abilities.Enable(LineBombAbility.AbilityId);
            abilities.Disable(PurpleLouieBombLineAbility.AbilityId);
            Assert.IsTrue(com.IsAvailable);
            Assert.AreEqual("LineBombItemA", com.DiagnosticName);
            Assert.IsFalse(abilities.IsEnabled(PowerGloveAbility.AbilityId));
            abilities.Disable(LineBombAbility.AbilityId);
            Assert.IsFalse(com.IsAvailable);
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void LineBombCom_ItemPlanReservesSeedAndIncludesItsBlast()
    {
        var player = new GameObject("LineBomb COM plan");
        player.SetActive(false);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var abilities = player.AddComponent<AbilitySystem>();
            abilities.Enable(LineBombAbility.AbilityId);
            var com = player.AddComponent<BattleModeComPurpleLouieBombLineAbility>();
            var bomb = player.GetComponent<BombController>();
            typeof(BombController).GetField("bombsRemaining", flags).SetValue(bomb, 3);
            Assert.IsTrue(com.IsAvailable);
            var type = com.GetType();
            foreach (string map in new[] { "groundTilemap", "destructibleTilemap", "indestructibleTilemap" })
                type.GetField(map, flags).SetValue(com, null);
            Vector2Int origin = new(1000, 1000);
            Assert.AreEqual(2, type.GetMethod("CountPlaceableLine", flags).Invoke(com,
                new object[] { origin, Vector2Int.right }));
            type.GetMethod("BuildPlannedLineBlast", flags).Invoke(com,
                new object[] { origin, Vector2Int.right, 2 });
            var bombs = (List<Vector2Int>)type.GetField("plannedBombTiles", flags).GetValue(com);
            var blast = (List<Vector2Int>)type.GetField("plannedBlastTiles", flags).GetValue(com);
            CollectionAssert.Contains(bombs, origin);
            CollectionAssert.Contains(blast, origin + Vector2Int.left);

            abilities.Enable(PurpleLouieBombLineAbility.AbilityId);
            Assert.IsTrue(com.IsAvailable);
            foreach (string map in new[] { "groundTilemap", "destructibleTilemap", "indestructibleTilemap" })
                type.GetField(map, flags).SetValue(com, null);
            Assert.AreEqual(3, type.GetMethod("CountPlaceableLine", flags).Invoke(com,
                new object[] { origin, Vector2Int.right }));
            type.GetMethod("BuildPlannedLineBlast", flags).Invoke(com,
                new object[] { origin, Vector2Int.right, 3 });
            CollectionAssert.DoesNotContain(bombs, origin);
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void LineBombCom_InPlaceAimAndActionASurviveDecisionConversion()
    {
        var decision = new BattleModeComAbilityDecision
        {
            FaceDirection = Vector2.left,
            FirstMove = Vector2.zero,
            TapActionA = true
        };
        var convert = typeof(BattleModeComController).GetMethod("ToCandidateAction",
            BindingFlags.Static | BindingFlags.NonPublic);
        object candidate = convert.Invoke(null, new object[] { decision });
        var type = candidate.GetType();
        Assert.AreEqual(Vector2.left, type.GetField("FaceDirection").GetValue(candidate));
        Assert.AreEqual(Vector2.zero, type.GetField("FirstMove").GetValue(candidate));
        Assert.AreEqual(true, type.GetField("TapActionA").GetValue(candidate));
        Assert.AreEqual(false, type.GetField("TapActionC").GetValue(candidate));
        Assert.AreEqual(false, type.GetField("TapBomb").GetValue(candidate));
    }

    [TestCase("Generic")]
    [TestCase("PowerZone")]
    [TestCase("Stage6")]
    public void LineBomb_HandicapRoundTripPreservesItemAndNormalizesExclusivity(string profile)
    {
        var source = new SaveData.BattleModeHandicapSave();
        source.players[0].lineBomb = true;
        source.players[0].powerGlove = true;
        source.players[0].mountedLouie = (int)MountedType.Purple;
        source.players[1].powerGlove = true;
        var kind = typeof(SaveSystem).GetNestedType("BattleModeHandicapProfileKind", BindingFlags.NonPublic);
        var normalize = typeof(SaveSystem).GetMethod("NormalizeBattleModeHandicap",
            BindingFlags.NonPublic | BindingFlags.Static);
        // Exercise the JSON shape as well as the normalization used by getters/setters.
        var restored = JsonUtility.FromJson<SaveData.BattleModeHandicapSave>(JsonUtility.ToJson(source));
        var result = (SaveData.BattleModeHandicapSave)normalize.Invoke(null,
            new object[] { restored, System.Enum.Parse(kind, profile) });
        Assert.IsTrue(result.players[0].lineBomb);
        Assert.IsFalse(result.players[0].powerGlove);
        Assert.AreEqual(profile == "PowerZone" ? MountedType.None : MountedType.Purple,
            (MountedType)result.players[0].mountedLouie);
        Assert.IsTrue(result.players[1].powerGlove);
        Assert.IsFalse(result.players[1].lineBomb);
        Assert.IsFalse(JsonUtility.FromJson<SaveData.BattleModeHandicapPlayerSave>(
            "{\"powerGlove\":true}").lineBomb);
    }

    [Test]
    public void LineBomb_ItemSwapsPreservePurpleLouieAbility()
    {
        var player = new GameObject("LineBomb exclusivity test");
        player.SetActive(false);
        try
        {
            var abilities = player.AddComponent<AbilitySystem>();
            abilities.Enable(PurpleLouieBombLineAbility.AbilityId);
            abilities.Enable(PowerGloveAbility.AbilityId);
            abilities.Enable(LineBombAbility.AbilityId);
            Assert.IsTrue(abilities.IsEnabled(LineBombAbility.AbilityId));
            Assert.IsFalse(abilities.IsEnabled(PowerGloveAbility.AbilityId));
            Assert.IsTrue(abilities.IsEnabled(PurpleLouieBombLineAbility.AbilityId));

            abilities.Enable(PowerGloveAbility.AbilityId);
            Assert.IsFalse(abilities.IsEnabled(LineBombAbility.AbilityId));
            Assert.IsTrue(abilities.IsEnabled(PowerGloveAbility.AbilityId));
            Assert.IsTrue(abilities.IsEnabled(PurpleLouieBombLineAbility.AbilityId));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void LineBomb_LegacyBattleItemAmountsKeepExistingPositions()
    {
        var previous = new int[19];
        for (int i = 0; i < previous.Length; i++) previous[i] = i;
        var convert = typeof(SaveSystem).GetMethod("ConvertBattleModeItemAmounts",
            BindingFlags.Static | BindingFlags.NonPublic);
        var converted = (int[])convert.Invoke(null, new object[]
        {
            previous, new int[20], GameManager.BattleModeHiddenDropEntries.Length
        });
        Assert.AreEqual(GameManager.BattleModeHiddenDropEntries.Length, converted.Length);
        for (int i = 0; i < previous.Length; i++) Assert.AreEqual(previous[i], converted[i]);
        Assert.AreEqual(0, converted[19]);
        Assert.AreEqual(ItemType.LineBomb, GameManager.BattleModeHiddenDropEntries[19].ItemType);
    }

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
