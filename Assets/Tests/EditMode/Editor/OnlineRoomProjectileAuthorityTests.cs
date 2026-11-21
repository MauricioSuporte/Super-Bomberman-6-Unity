using System.Reflection;
using Assets.Scripts.Netcode;
using NUnit.Framework;
using UnityEngine;

public sealed partial class OnlineRoomEditModeTests
{
    [Test]
    public void RejectedGenericEggTouchDoesNotChooseLouieOrAdvanceRandom()
    {
        var originalRandom = Random.state;
        var player = new GameObject("FullEggOwner"); player.SetActive(false); player.AddComponent<BoxCollider2D>();
        var pickupRoot = new GameObject("GenericWorldEgg"); pickupRoot.SetActive(false); pickupRoot.AddComponent<BoxCollider2D>();
        var worldRoot = new GameObject("FullEggQueueWorld");
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var movement = player.AddComponent<MovementController>();
            typeof(MovementController).GetField("isMounted", flags).SetValue(movement, true);
            var queue = player.AddComponent<MountEggQueue>();
            typeof(MountEggQueue).GetField("maxEggsInQueue", flags).SetValue(queue, 1);
            typeof(MountEggQueue).GetField("_worldRoot", flags).SetValue(queue, worldRoot.transform);
            var eggs = (System.Collections.IList)typeof(MountEggQueue).GetField("_eggs", flags).GetValue(queue);
            eggs.Add(System.Activator.CreateInstance(eggs.GetType().GetGenericArguments()[0]));
            Assert.IsTrue(queue.IsFull);
            var pickup = pickupRoot.AddComponent<ItemPickup>(); pickup.type = ItemType.RandomLouieEgg;
            Random.InitState(112233); var beforeTouch = Random.state;
            typeof(ItemPickup).GetMethod("OnItemPickup", flags).Invoke(pickup, new object[] { player });
            Assert.AreEqual(ItemType.RandomLouieEgg, pickup.type);
            Assert.AreEqual(beforeTouch, Random.state);
            Assert.AreEqual(1, queue.Count);
        }
        finally { Random.state = originalRandom; Object.DestroyImmediate(player); Object.DestroyImmediate(pickupRoot); Object.DestroyImmediate(worldRoot); }
    }

    [Test]
    public void SpotlightBecomingReadyReleasesTheEarlierHiddenFade()
    {
        var root = new GameObject("SpotlightNetwork"); root.SetActive(false);
        var imageObject = new GameObject("Spotlight", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        Material material = null;
        try
        {
            var manager = root.AddComponent<BombermanNetworkManager>();
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            material = new Material(Shader.Find("UI/SpotlightMask")); image.material = material;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var images = (System.Collections.IDictionary)typeof(BombermanNetworkManager).GetField("coopClientImages", flags).GetValue(manager);
            images["Spotlight"] = image;
            var apply = typeof(BombermanNetworkManager).GetMethod("ApplyCoopCinematics", flags);
            apply.Invoke(manager, new object[] { new[] { new CoopCinematicFrame { path = "Spotlight", color = Color.clear, visible = false } } });
            apply.Invoke(manager, new object[] { new[] { new CoopCinematicFrame { path = "Spotlight", color = Color.white, visible = true,
                spotlight = true, materialColor = new Color(0, 0, 0, .8f), radius = .2f, softness = .03f, ellipseX = .6f, ellipseY = .6f } } });
            typeof(BombermanNetworkManager).GetMethod("PresentCoopFades", flags).Invoke(manager, null);
            Assert.IsTrue(image.gameObject.activeSelf); Assert.IsTrue(image.enabled);
            Assert.AreEqual(Color.white, image.color);
            Assert.That(material.GetColor("_Color").a, Is.EqualTo(.8f).Within(.001f));
            var fades = (System.Collections.IDictionary)typeof(BombermanNetworkManager).GetField("coopFadeSamples", flags).GetValue(manager);
            Assert.AreEqual(0, fades.Count);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(imageObject); if (material != null) Object.DestroyImmediate(material); }
    }

    [Test]
    public void CoopTileRebuildPreservesEmptyAuthoredMarginsAndSortBounds()
    {
        var root = new GameObject("AuthoredMap", typeof(Grid));
        var mapObject = new GameObject("Ground", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
        mapObject.transform.SetParent(root.transform, false);
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        try
        {
            var map = mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            Vector3Int origin = new(-7, -7, 0), size = new(29, 24, 1);
            map.SetTile(Vector3Int.zero, tile); map.origin = origin; map.size = size;
            Bounds expected = map.localBounds;
            map.ClearAllTiles(); map.SetTile(Vector3Int.zero, tile);
            Assert.AreNotEqual(expected, map.localBounds);
            var apply = typeof(BombermanNetworkManager).GetMethod("ApplyCoopMapBounds", BindingFlags.Static | BindingFlags.NonPublic);
            apply.Invoke(null, new object[] { map, origin, size });
            Assert.AreEqual(expected, map.localBounds);
            apply.Invoke(null, new object[] { map, origin, new Vector3Int(2000, 24, 1) });
            Assert.AreEqual(expected, map.localBounds, "Malformed bounds cannot grow the client map");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
    }

    [Test]
    public void WrappedPresenceNameOutlineUsesTheHistoryGlyphPositions()
    {
        var root = new GameObject("WrappedPresence", typeof(RectTransform), typeof(Canvas));
        try
        {
            TMPro.TextMeshProUGUI Create(string name, string text)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                go.transform.SetParent(root.transform, false);
                var label = go.GetComponent<TMPro.TextMeshProUGUI>();
                label.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Retro Gaming SDF");
                label.fontSize = 7; label.richText = true;
                label.verticalAlignment = TMPro.VerticalAlignmentOptions.Bottom;
                label.rectTransform.sizeDelta = new Vector2(120, 65);
                label.text = text; return label;
            }
            var body = Create("Body", "<color=#000000>IdleRegression2</color> ENTROU NA SALA.\nVOCE ENTROU NA SALA.");
            var names = Create("Names", "<color=#000000>IdleRegression2</color><color=#FFFFFF00> ENTROU NA SALA.\nVOCE ENTROU NA SALA.</color>");
            typeof(OnlineChatOverlay).GetMethod("AlignNameContrast", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { body, names });
            Assert.Greater(body.textInfo.lineCount, 1);
            Assert.AreEqual(body.textInfo.characterCount, names.textInfo.characterCount);
            for (int i = 0; i < "IdleRegression2".Length; i++)
            {
                var source = body.textInfo.characterInfo[i]; var target = names.textInfo.characterInfo[i];
                var sourceVertices = body.textInfo.meshInfo[source.materialReferenceIndex].vertices;
                var targetVertices = names.textInfo.meshInfo[target.materialReferenceIndex].vertices;
                Assert.That(Vector3.Distance(sourceVertices[source.vertexIndex], targetVertices[target.vertexIndex]), Is.LessThan(.001f), "glyph " + i);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LargerChatEntryFitsActualGlyphsAndPreservesTheFullDraft(bool japanese)
    {
        var root = new GameObject("ChatGlyphFit", typeof(RectTransform)); root.SetActive(false);
        var labelObject = new GameObject("Entry", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        SaveSystem.BeginOnlineCoopSettings();
        try
        {
            SaveSystem.Data.language = (int)(japanese ? GameLanguage.Japanese : GameLanguage.English);
            var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
            label.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Retro Gaming SDF"); Assert.IsNotNull(label.font);
            label.fontSize = 7; label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; label.richText = false;
            LocalizedTmpFontFallback.Apply(label);
            label.rectTransform.sizeDelta = new Vector2(200, 14);
            var chat = root.AddComponent<OnlineChatOverlay>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string draft = japanese ? "" : new string('W', 80);
            if (japanese) for (int i = 0; i < 40; i++) draft += "ルーイ";
            typeof(OnlineChatOverlay).GetField("entry", flags).SetValue(chat, label);
            typeof(OnlineChatOverlay).GetField("typing", flags).SetValue(chat, true);
            typeof(OnlineChatOverlay).GetField("draft", flags).SetValue(chat, draft);
            var refresh = typeof(OnlineChatOverlay).GetMethod("RefreshEntry", flags);
            refresh.Invoke(chat, null);
            Assert.That(label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x, Is.LessThanOrEqualTo(200));
            label.rectTransform.sizeDelta = new Vector2(100, 14); refresh.Invoke(chat, null);
            Assert.That(label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x, Is.LessThanOrEqualTo(100));
            Assert.AreEqual(draft, typeof(OnlineChatOverlay).GetField("draft", flags).GetValue(chat));
            StringAssert.StartsWith("> ", label.text);
        }
        finally { SaveSystem.EndOnlineCoopSettings(); Object.DestroyImmediate(root); }
    }

    [TestCase(NetSync.NetMode.Offline, true, false, true)]
    [TestCase(NetSync.NetMode.Host, true, false, true)]
    [TestCase(NetSync.NetMode.Client, true, false, false)]
    [TestCase(NetSync.NetMode.Client, false, true, false)]
    public void CreditsOnlyApplyApprovedSkipAndFinalPhaseOnObservers(NetSync.NetMode mode, bool skipped, bool finalMessage, bool keepsRolling)
    {
        var root = new GameObject("CreditsPhase", typeof(RectTransform)); root.SetActive(false);
        var labelObject = new GameObject("Credits", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        var oldMode = NetSync.Mode;
        try
        {
            NetSync.Mode = mode;
            var ending = root.AddComponent<EndingScreenController>(); ending.messageText = labelObject.GetComponent<TMPro.TMP_Text>();
            typeof(EndingScreenController).GetProperty(nameof(EndingScreenController.Running)).SetValue(ending, true);
            ending.ApplyOnlineCreditsPhase(skipped, finalMessage);
            var roll = (System.Collections.IEnumerator)typeof(EndingScreenController).GetMethod("RollCreditsRoutine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ending, new object[] { 100f });
            Assert.AreEqual(keepsRolling, roll.MoveNext());
            (roll as System.IDisposable)?.Dispose();
            Assert.AreEqual(mode == NetSync.NetMode.Client && skipped, ending.CreditsSkipped);
            ending.ForceHide(); Assert.IsFalse(ending.Running); Assert.IsFalse(ending.CreditsSkipped); Assert.IsFalse(ending.ShowingFinalMessage);
        }
        finally { NetSync.Mode = oldMode; Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RegeneratedBlockCannotRevealTheSameHiddenRewardAgain(bool initiallyReserved)
    {
        var root = new GameObject("RegeneratingReward"); root.SetActive(false);
        var ground = new GameObject("RewardGround", typeof(UnityEngine.Tilemaps.Tilemap));
        var reward = new GameObject("AssignedReward");
        try
        {
            var game = root.AddComponent<GameManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameManager).GetField("totalDestructibleBlocks", flags).SetValue(game, 1);
            game.groundTilemap = ground.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            var assignments = (System.Collections.Generic.Dictionary<Vector3Int, GameObject>)typeof(GameManager).GetField("hiddenObjectSpawnsByCell", flags).GetValue(game);
            var cell = new Vector3Int(2, 3, 0); assignments[cell] = reward;
            if (initiallyReserved)
            {
                Assert.IsTrue(game.TryReservePendingHiddenItemCell(cell));
                Assert.IsNull(game.GetSpawnForDestroyedBlock(cell));
                Assert.IsTrue(assignments.ContainsKey(cell), "A rejected reveal keeps its assignment.");
                game.ReleasePendingHiddenItemCell(cell);
            }
            Assert.AreSame(reward, game.GetSpawnForDestroyedBlock(cell));
            game.ReleasePendingHiddenItemCell(cell);
            Assert.IsNull(game.GetSpawnForDestroyedBlock(cell), "Regrowing and destroying the block must not return its item.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(ground); Object.DestroyImmediate(reward); }
    }

    [Test]
    public void HallPortalRecipePreservesAuthoredMaskAndClearsItAfterReveal()
    {
        var host = new GameObject("PortalRecipeManager"); host.SetActive(false);
        var source = new GameObject("PortalRecipeSource", typeof(SpriteRenderer)); source.SetActive(false);
        var view = new GameObject("PortalRecipeView", typeof(SpriteRenderer));
        var texture = new Texture2D(4, 4);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 16);
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            catalog.sprites = new[] { sprite };
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            ((System.Collections.Generic.Dictionary<Sprite, int>)typeof(BombermanNetworkManager).GetField("coopSpriteIds", flags).GetValue(manager))[sprite] = 0;
            var portal = source.AddComponent<global::StageAssets.World3HallPortal>();
            var renderer = source.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            typeof(global::StageAssets.World3HallPortal).GetField("visual", flags).SetValue(portal, renderer);
            var material = Resources.Load<Material>("Portal/World3HallPortalPalette"); Assert.IsNotNull(material);
            renderer.sharedMaterial = material; view.GetComponent<SpriteRenderer>().sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_RevealMode", 2); properties.SetFloat("_RevealY", 5); properties.SetFloat("_BeamMaskEnabled", 1);
            properties.SetFloat("_PortalBaseMaskHeight", .25f);
            properties.SetVector("_PortalWorldRect", new Vector4(2, 3, 1, 2));
            properties.SetVector("_RenderWorldRect", new Vector4(2, 3, 1, 4));
            properties.SetVector("_PortalUVRect", new Vector4(0, 0, 1, 1));
            properties.SetVector("_RenderUVRect", new Vector4(0, 0, 1, 1)); renderer.SetPropertyBlock(properties);
            var recipe = (CoopPortalPalette)typeof(BombermanNetworkManager).GetMethod("CaptureCoopPortal", flags).Invoke(manager, new object[] { renderer });
            Assert.IsNotNull(recipe); Assert.IsTrue(recipe.IsValid()); Assert.AreEqual(0, recipe.maskSprite); Assert.IsTrue(recipe.beamMask);
            var target = view.GetComponent<SpriteRenderer>();
            Assert.IsTrue((bool)typeof(BombermanNetworkManager).GetMethod("ApplyCoopPortal", flags).Invoke(manager, new object[] { target, recipe }));
            target.GetPropertyBlock(properties); Assert.AreSame(texture, properties.GetTexture("_PortalMaskTex"));
            Assert.AreEqual(5, properties.GetFloat("_RevealY")); Assert.AreEqual(new Vector4(2, 3, 1, 4), properties.GetVector("_RenderWorldRect"));
            typeof(BombermanNetworkManager).GetMethod("ReleaseCoopPortal", flags).Invoke(manager, new object[] { target });
            target.GetPropertyBlock(properties); Assert.IsTrue(properties.isEmpty);
            recipe.revealY = float.NaN; Assert.IsFalse(recipe.IsValid());
        }
        finally
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(source); Object.DestroyImmediate(view);
            Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void RoomUnlockNotificationsIgnoreBaselineAndDuplicatesWithoutChangingOfflineSave()
    {
        var offline = SaveSystem.Data;
        string original = JsonUtility.ToJson(offline);
        var oldMode = NetSync.Mode;
        int notifications = 0;
        System.Action onUnlock = () => notifications++;
        UnlockProgress.OnBossRushUnlocked += onUnlock;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var incoming = JsonUtility.FromJson<SaveData>(original);
            incoming.bossRushUnlocked = false;
            Assert.IsTrue(SaveSystem.ApplyRoomProgress(JsonUtility.ToJson(incoming), false));
            Assert.AreEqual(0, notifications);
            incoming.bossRushUnlocked = true;
            string unlocked = JsonUtility.ToJson(incoming);
            Assert.IsTrue(SaveSystem.ApplyRoomProgress(unlocked, false));
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(SaveSystem.ApplyRoomProgress(unlocked, false));
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(original, JsonUtility.ToJson(offline));
        }
        finally
        {
            UnlockProgress.OnBossRushUnlocked -= onUnlock;
            SaveSystem.EndOnlineBattleSettings(); SaveSystem.EndOnlineCoopSettings(); NetSync.Mode = oldMode;
        }
        Assert.AreSame(offline, SaveSystem.Data);
    }

    [Test]
    public void GeyserBlocksExplosionOnlyWhileItsColliderIsActive()
    {
        var actor = new GameObject("GeyserBombSource"); actor.SetActive(false);
        var geyser = new GameObject("ExplosionBarrier"); geyser.SetActive(false);
        geyser.transform.position = new Vector3(75, 75, 0);
        try
        {
            actor.AddComponent<BoxCollider2D>();
            var bombs = actor.AddComponent<BombController>();
            var collider = geyser.AddComponent<BoxCollider2D>(); collider.isTrigger = true;
            geyser.AddComponent<GeyserLauncherBootstrap>();
            geyser.SetActive(true); Physics2D.SyncTransforms();
            var query = typeof(BombController).GetMethod("HasIndestructibleAt", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsTrue((bool)query.Invoke(bombs, new object[] { new Vector2(75, 75) }));
            Assert.IsFalse((bool)query.Invoke(bombs, new object[] { new Vector2(76, 75) }));
            collider.enabled = false; Physics2D.SyncTransforms();
            Assert.IsFalse((bool)query.Invoke(bombs, new object[] { new Vector2(75, 75) }));
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(geyser); }
    }

    [Test]
    public void PinkGroundedHandoffReenablesShadowAfterWalkingPoseSuppressedIt()
    {
        var mount = new GameObject("PinkShadowHandoff"); mount.SetActive(false);
        var shadowObject = new GameObject("Shadow"); shadowObject.transform.SetParent(mount.transform);
        try
        {
            var shadow = shadowObject.AddComponent<PinkLouieShadowController>();
            var animator = shadowObject.AddComponent<PinkLouieShadowAnimator>(); shadow.animator = animator;
            var walk = new GameObject("Walk", typeof(SpriteRenderer)); walk.transform.SetParent(shadowObject.transform);
            var jump = new GameObject("Jump", typeof(SpriteRenderer)); jump.transform.SetParent(shadowObject.transform);
            animator.walkShadow = walk.AddComponent<AnimatedSpriteRenderer>(); animator.jumpShadow = jump.AddComponent<AnimatedSpriteRenderer>();
            typeof(PinkLouieShadowAnimator).GetMethod("CacheRefs", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animator, null);
            shadow.BindToPinkLouieRoot(mount.transform); shadow.BeginJump(Vector2.one); shadow.EndJump();
            animator.walkShadow.enabled = false; walk.GetComponent<SpriteRenderer>().enabled = false;
            mount.transform.position = new Vector3(4, 5, 0);
            shadow.PresentNetworkGrounded(.1f);
            Assert.IsTrue(walk.GetComponent<SpriteRenderer>().enabled); Assert.IsTrue(animator.walkShadow.enabled);
            Assert.IsFalse(jump.GetComponent<SpriteRenderer>().enabled);
            Assert.AreEqual(new Vector3(4, 5 + shadow.localY, shadow.worldZ), shadowObject.transform.position);
        }
        finally { Object.DestroyImmediate(mount); }
    }

    [Test]
    public void BoatSeatReplacesMountedHeadOffsetAndRestoresItOnRelease()
    {
        var parent = new GameObject("BoatHeadRoot"); parent.SetActive(false);
        var actor = new GameObject("BoatHead", typeof(SpriteRenderer)); actor.transform.SetParent(parent.transform, false);
        try
        {
            actor.transform.localPosition = new Vector3(.25f, .5f, 0);
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            typeof(AnimatedSpriteRenderer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, null);
            animation.idle = true;
            animation.SetRuntimeBaseLocalY(2f);
            animation.SetRuntimeBaseLocalX(1f);
            Vector3 mounted = actor.transform.localPosition;
            animation.SetExternalBaseOffsetFromInitial(new Vector3(.125f, -.25f, 0), overrideRuntimeOffset: true);
            Assert.That(Vector3.Distance(actor.transform.localPosition, new Vector3(.375f, .25f, 0)), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(animation.RestingLocalPosition, actor.transform.localPosition), Is.LessThan(.00001f), "Ride anchors must use the same boat seat as the rendered head.");
            animation.ClearExternalBase();
            Assert.That(Vector3.Distance(actor.transform.localPosition, mounted), Is.LessThan(.00001f));
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test]
    public void GeneratedChipCelebrationSpritesRemainReadableForApprovedDefinitions()
    {
        var source = new GameObject("ChipSource");
        GameObject effect = null;
        try
        {
            global::StageAssets.World3EndStageCelebrationEffect.Play(Vector3.one, source.transform);
            effect = GameObject.Find("World3EndStageCelebration"); Assert.IsNotNull(effect);
            var sprites = effect.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.AreEqual(12, sprites.Length);
            foreach (var sprite in sprites) { Assert.IsNotNull(sprite.sprite); Assert.IsTrue(sprite.sprite.texture.isReadable); }
        }
        finally { if (effect != null) Object.DestroyImmediate(effect); Object.DestroyImmediate(source); }
    }

    [Test]
    public void CampaignEggAssignmentsStayGenericWithoutChoosingGameplayRandomEarly()
    {
        var originalRandom = Random.state;
        var actor = new GameObject("GenericEggAssignment"); actor.SetActive(false);
        try
        {
            var manager = actor.AddComponent<GameManager>();
            var prefab = AutoItemDatabase.Get(ItemType.RandomLouieEgg);
            Assert.IsNotNull(prefab); Assert.AreEqual(ItemType.RandomLouieEgg, prefab.type);
            Assert.AreNotEqual(0u, prefab.GetComponent<Mirror.NetworkIdentity>().assetId);
            var cells = new System.Collections.Generic.List<Vector3Int> { Vector3Int.zero, Vector3Int.right };
            object[] args = { cells, 0, 2 };
            typeof(GameManager).GetMethod("TryAssignRandomSb5Eggs", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args);
            Assert.AreEqual(2, args[1]); Assert.AreEqual(originalRandom, Random.state);
            var spawns = (System.Collections.Generic.Dictionary<Vector3Int, GameObject>)typeof(GameManager)
                .GetField("hiddenObjectSpawnsByCell", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            foreach (var cell in cells) Assert.AreSame(prefab.gameObject, spawns[cell]);
            var lobby = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/OnlineLobby.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                Mirror.NetworkManager network = null;
                foreach (var root in lobby.GetRootGameObjects()) network ??= root.GetComponentInChildren<Mirror.NetworkManager>(true);
                Assert.IsNotNull(network); CollectionAssert.Contains(network.spawnPrefabs, prefab.gameObject);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(lobby, true); }
        }
        finally { Object.DestroyImmediate(actor); Random.state = originalRandom; }
    }

    [Test]
    public void EnemyInvulnerabilityDoesNotTintCarriedCoreOrDetachedObjective()
    {
        var actor = new GameObject("BlinkOwner"); actor.SetActive(false);
        var core = new GameObject("CarriedCore"); core.transform.SetParent(actor.transform);
        var nested = new GameObject("NestedActor"); nested.transform.SetParent(actor.transform);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            actor.AddComponent<BoxCollider2D>();
            var body = actor.AddComponent<SpriteRenderer>();
            var health = actor.AddComponent<CharacterHealth>();
            core.AddComponent<AttachedCoreMechanismsController>();
            var objective = core.AddComponent<SpriteRenderer>(); objective.color = new Color(.4f, .6f, .8f, .7f);
            Color objectiveColor = objective.color;
            nested.AddComponent<BoxCollider2D>(); nested.AddComponent<CharacterHealth>();
            var nestedBody = nested.AddComponent<SpriteRenderer>(); nestedBody.color = Color.green;
            typeof(CharacterHealth).GetMethod("EnsureRendererCache", flags).Invoke(health, null);
            var blink = (System.Collections.IEnumerator)typeof(CharacterHealth).GetMethod("TemporaryInvulnerabilityRoutine", flags)
                .Invoke(health, new object[] { .1f });
            Assert.IsTrue(blink.MoveNext()); Assert.AreEqual(.2f, body.color.a);
            Assert.AreEqual(objectiveColor, objective.color); Assert.AreEqual(Color.green, nestedBody.color);
            core.transform.SetParent(null);
            while (blink.MoveNext()) { }
            Assert.AreEqual(Color.white, body.color);
            Assert.AreEqual(objectiveColor, objective.color, "Finishing enemy blink must not restore a stale color on a released objective.");
        }
        finally { Object.DestroyImmediate(core); Object.DestroyImmediate(actor); }
    }

    [TestCase("Stage_1-1", false, null, 1, 1)]
    [TestCase("Stage_1-2", false, null, 1, 1)]
    [TestCase("Stage_1-1", false, "Stage_2-5", 1, 2)]
    [TestCase("Stage_World3Hall", false, "Stage_1-1", 1, 2)]
    [TestCase("Stage_3-6", false, "Stage_1-1", 5, 5)]
    [TestCase("Stage_1-2", true, "Stage_1-1", 1, 1)]
    [TestCase("BattleMode_1", false, "Stage_1-1", 1, 1)]
    public void CampaignExplosionMinimumUsesSlotProgress(string scene, bool bossRush, string clearedStage, int radius, int expected)
    {
        var slot = new Assets.Scripts.SaveSystem.StageSlot();
        slot.unlockedStages.Add(scene);
        if (clearedStage != null)
            slot.clearedStages.Add(clearedStage);

        var resolve = typeof(PlayerPersistentStats).GetMethod("ResolveSceneExplosionRadius", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.AreEqual(expected, resolve.Invoke(null, new object[] { radius, scene, bossRush, slot }));
    }

    [Test]
    public void PlainCoopRendererRegistersBeforeDiscoveryWithoutDuplicatingOrOwningGameplay()
    {
        bool oldNetworkScene = NetSync.IsNetworkedScene;
        var host = new GameObject("ProjectileRegistration"); host.SetActive(false);
        var actor = new GameObject("PlainProjectile", typeof(SpriteRenderer));
        var parent = new GameObject("AlreadyNetworked", typeof(Mirror.NetworkIdentity));
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>();
            typeof(BombermanNetworkManager).GetField("coopCatalog", flags).SetValue(manager, catalog);
            typeof(BombermanNetworkManager).GetField("coopScene", flags).SetValue(manager, actor.scene.name);
            var track = typeof(BombermanNetworkManager).GetMethod("TrackCoopRenderer", flags);
            var pending = (System.Collections.Generic.HashSet<SpriteRenderer>)typeof(BombermanNetworkManager)
                .GetField("coopPendingRenderers", flags).GetValue(manager);
            var renderer = actor.GetComponent<SpriteRenderer>();
            track.Invoke(manager, new object[] { renderer }); track.Invoke(manager, new object[] { renderer });
            Assert.AreEqual(1, pending.Count); Assert.IsTrue(pending.Contains(renderer));
            Assert.IsNull(actor.GetComponent<Rigidbody2D>()); Assert.IsNull(actor.GetComponent<Mirror.NetworkIdentity>());
            typeof(BombermanNetworkManager).GetMethod("ClearCoopSprites", flags).Invoke(manager, null);
            Assert.AreEqual(0, pending.Count); Assert.IsNotNull(renderer);
            actor.transform.SetParent(parent.transform);
            track.Invoke(manager, new object[] { renderer }); Assert.AreEqual(0, pending.Count);
            actor.transform.SetParent(null);
            typeof(BombermanNetworkManager).GetField("coopScene", flags).SetValue(manager, "OtherScene");
            track.Invoke(manager, new object[] { renderer }); Assert.AreEqual(0, pending.Count);
        }
        finally
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(actor); Object.DestroyImmediate(parent);
            Object.DestroyImmediate(catalog); NetSync.IsNetworkedScene = oldNetworkScene;
        }
    }

    [Test]
    public void ClientProjectileFactoriesNeverCreateGameplayObjectsOrChooseTargets()
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        int bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include).Length;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            HeliProjectile.Create(Vector2.zero, Vector2.up, null, null, null, null);
            IcicleIceProjectile.Create(Vector2.zero, Vector2.up, null, null, null, null);
            Assert.IsNull(MatangoProjectile.Create(Vector2.zero, Vector2.up, 4, 1, .2f, null));
            Assert.IsNull(FreezerVenusProjectile.Create(null, FreezerVenusProjectile.AttackKind.Tornado,
                System.Array.Empty<Sprite>(), Vector3.zero, Vector2.up));
            Assert.IsNull(QueenOyobeeProjectile.Create(null, true, 0));
            Assert.IsNull(QueenOyobeeProjectile.CreateTelegraph(null, null));
            Assert.IsNull(QueenOyobeeProjectile.CreateRitual(null));
            DespiderWebProjectile.Launch(null, null, Vector2.zero, Vector2.up, 1);
            var fire = new GameObject("ClientFarohLaunch");
            try
            {
                fire.AddComponent<FarohFireProjectile>().Launch(Vector2.up, 2, 1, 5, .7f, 0);
                Assert.IsNull(fire.GetComponent<CoopProjectileVisual>());
                Assert.AreEqual(Vector3.zero, fire.transform.position);
            }
            finally { Object.DestroyImmediate(fire); }
            Assert.AreEqual(bodies, Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(random, Random.state);
        }
        finally { NetSync.Mode = oldMode; Random.state = random; }
    }

    [TestCase(typeof(HeliProjectile))]
    [TestCase(typeof(IcicleIceProjectile))]
    [TestCase(typeof(FishManSpear))]
    [TestCase(typeof(MatangoProjectile))]
    [TestCase(typeof(FreezerVenusProjectile))]
    [TestCase(typeof(QueenOyobeeProjectile))]
    [TestCase(typeof(BirdRobotRocket))]
    [TestCase(typeof(DespiderWebProjectile))]
    [TestCase(typeof(FarohFireProjectile))]
    [TestCase(typeof(JellyFishShot))]
    public void PureClientProjectileCannotAdvanceNativeMotionOrRunContacts(System.Type type)
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        var actor = new GameObject("ClientProjectileAuthority"); actor.SetActive(false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.transform.position = new Vector3(21, 22, 0);
            actor.AddComponent<Rigidbody2D>(); actor.AddComponent<CircleCollider2D>();
            var component = actor.AddComponent(type);
            Assert.IsNotNull(component);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetMethod("FixedUpdate", flags).Invoke(component, null);
            type.GetMethod("OnTriggerEnter2D", flags)?.Invoke(component, new object[] { null });
            Assert.AreEqual(new Vector3(21, 22, 0), actor.transform.position);
            Assert.AreEqual(random, Random.state);
            BombermanNetworkManager.CoopTrackRenderer(actor.GetComponent<SpriteRenderer>());
            BombermanNetworkManager.CoopTrackAnimation(null);
            Assert.IsNotNull(actor);
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; Random.state = random; }
    }

    [Test]
    public void R05ProjectileEdgesPreserveRotationScaleBlinkAndStopWithoutAClientCollider()
    {
        var actor = new GameObject("R05Edges", typeof(SpriteRenderer));
        var host = new GameObject("R05Stop"); host.SetActive(false);
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(BombermanNetworkManager);
        try
        {
            catalog.sprites = System.Array.Empty<Sprite>();
            var manager = host.AddComponent<BombermanNetworkManager>();
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            var renderer = actor.GetComponent<SpriteRenderer>();
            var frame = new CoopSpriteFrame { id = renderer.GetEntityId().GetHashCode(), visible = true,
                rotation = Quaternion.identity, scale = Vector3.one, color = Color.white, time = Mirror.NetworkTime.localTime - .1 };
            var edge = type.GetMethod("NeedsCoopProjectileEdge", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsFalse((bool)edge.Invoke(null, new object[] { renderer, frame }));
            actor.transform.rotation = Quaternion.Euler(0, 0, 90);
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            actor.transform.rotation = Quaternion.identity; renderer.enabled = false;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            renderer.enabled = true; renderer.flipX = true;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            renderer.flipX = false; actor.transform.localScale = Vector3.one * 2;
            Assert.IsTrue((bool)edge.Invoke(null, new object[] { renderer, frame }));
            var frames = (System.Collections.Generic.Dictionary<int, CoopSpriteFrame>)type.GetField("coopLastSprites", flags).GetValue(manager);
            frames[frame.id] = frame;
            actor.transform.position = Vector3.right * 4;
            type.GetMethod("PublishCoopProjectileEnd", flags).Invoke(manager, new object[] { renderer });
            var stopped = frames[frame.id];
            Assert.IsFalse(stopped.visible);
            Assert.AreEqual(actor.transform.position, stopped.position);
            Assert.Greater(stopped.time, frame.time);
            Assert.IsNull(actor.GetComponent<Collider2D>());
            Assert.IsNull(actor.GetComponent<Rigidbody2D>());
            type.GetMethod("ClearCoopSprites", flags).Invoke(manager, null);
            Assert.AreEqual(0, ((System.Collections.IDictionary)type.GetField("coopProjectiles", flags).GetValue(manager)).Count);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor); Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void R05SameFrameFullStateFinishesInitializationWithoutRewindingOrRevivingRemoval()
    {
        var host = new GameObject("R05IntraFrame"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var manager = host.AddComponent<BombermanNetworkManager>(); var type = typeof(BombermanNetworkManager);
            var append = type.GetMethod("AppendCoopSpriteFrame", flags);
            var all = (System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<CoopVisualSample>>)type.GetField("coopVisualSamples", flags).GetValue(manager);
            var pending = new CoopSpriteFrame { id = 9, time = 10, visible = false, sprite = -1 };
            append.Invoke(manager, new object[] { pending });
            var ready = pending; ready.visible = true; ready.sprite = 5;
            append.Invoke(manager, new object[] { ready });
            Assert.AreEqual(1, all[9].Count);
            Assert.IsTrue(all[9][0].frame.visible);
            Assert.AreEqual(5, all[9][0].frame.sprite);
            pending.time = 9; append.Invoke(manager, new object[] { pending });
            Assert.IsTrue(all[9][0].frame.visible, "An older frame cannot undo initialization.");
            ((System.Collections.Generic.Dictionary<int, double>)type.GetField("coopVisualRemovals", flags).GetValue(manager))[9] = 10;
            append.Invoke(manager, new object[] { pending }); append.Invoke(manager, new object[] { ready });
            Assert.AreEqual(1, all[9].Count, "A removed identity cannot be revived by a duplicate full frame.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void R05PureClientFlameLaunchesDoNotCreatePhysicsOrStartNativeCoroutines()
    {
        var oldMode = NetSync.Mode;
        var actor = new GameObject("R05PureFlame");
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            actor.AddComponent<FuramaFlameVisual>().Play(Vector2.one, Vector2.up, 1, 3, 1, .5f);
            actor.AddComponent<BubbleFuramaFlame>().Launch(null, Vector2.up, 1, 3, null, null);
            typeof(BubbleFuramaFlame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor.GetComponent<BubbleFuramaFlame>(), null);
            Assert.AreEqual(Vector3.zero, actor.transform.position);
            Assert.IsNull(actor.GetComponent<Collider2D>());
            Assert.IsNull(actor.GetComponent<Rigidbody2D>());
        }
        finally { Object.DestroyImmediate(actor); NetSync.Mode = oldMode; }
    }

    [Test]
    public void R05PendingMergePreservesR03FirstShadowFrameBeforePeriodicDiscovery()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = typeof(BombermanNetworkManager);
        var host = new GameObject("R05FreshShadowManager"); host.SetActive(false);
        var shadow = new GameObject("R03FreshShadow", typeof(SpriteRenderer));
        var catalog = ScriptableObject.CreateInstance<CoopSceneCatalog>();
        try
        {
            catalog.sprites = System.Array.Empty<Sprite>();
            var manager = host.AddComponent<BombermanNetworkManager>(); var renderer = shadow.GetComponent<SpriteRenderer>();
            int id = renderer.GetEntityId().GetHashCode();
            type.GetField("coopScene", flags).SetValue(manager, shadow.scene.name);
            type.GetField("coopCatalog", flags).SetValue(manager, catalog);
            ((System.Collections.Generic.Dictionary<int, float>)type.GetField("coopEnemyVisualLimits", flags).GetValue(manager))[id] = 1.5f;
            ((System.Collections.Generic.HashSet<SpriteRenderer>)type.GetField("coopPendingRenderers", flags).GetValue(manager)).Add(renderer);
            type.GetMethod("MergeCoopPendingRenderers", flags).Invoke(manager, null);
            Assert.AreEqual(0, ((System.Collections.Generic.HashSet<SpriteRenderer>)type.GetField("coopPendingRenderers", flags).GetValue(manager)).Count);
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            var frames = (System.Collections.Generic.List<CoopSpriteFrame>)type.GetField("coopEnemyTransitions", flags).GetValue(manager);
            Assert.AreEqual(1, frames.Count, "Merging the renderer must retain its native first shadow edge.");
            Assert.AreEqual(id, frames[0].id); Assert.IsTrue(frames[0].visible);
            type.GetMethod("PublishCoopEnemyTransitions", flags).Invoke(manager, new object[] { false });
            Assert.AreEqual(0, frames.Count, "The initial shadow is published once.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(shadow); Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void R05ProjectileSnapshotDoesNotInventFramesOrFreezeNativeAnimator()
    {
        var actor = new GameObject("R05NativeAnimation", typeof(SpriteRenderer));
        try
        {
            var animation = actor.AddComponent<AnimatedSpriteRenderer>();
            animation.idle = false;
            var before = animation.CapturePlayback();
            var approved = (AnimatedSpriteRenderer.PlaybackState)typeof(BombermanNetworkManager).GetMethod("CaptureCoopProjectilePlayback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { animation });
            Assert.AreEqual(0, approved.speed);
            Assert.AreEqual(before.frame, approved.frame);
            Assert.AreEqual(before.speed, animation.CapturePlayback().speed);
            Assert.IsFalse(animation.ManualAnimationUpdate);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void R05ObserverFreezesVisualWhileDecodedRootMovesAndWaitsForResumeSample()
    {
        var oldMode = NetSync.Mode;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var clientState = typeof(Mirror.NetworkClient).GetField("connectState", BindingFlags.NonPublic | BindingFlags.Static);
        var oldState = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetProperty(nameof(GamePauseController.IsPaused));
        bool oldPaused = GamePauseController.IsPaused;
        var projectile = new GameObject("R05ObserverShot"); projectile.SetActive(false);
        var child = new GameObject("ShotSprite", typeof(SpriteRenderer)); child.transform.SetParent(projectile.transform, false);
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected"));
            projectile.AddComponent<BoxCollider2D>();
            var identity = projectile.AddComponent<Mirror.NetworkIdentity>();
            var shot = projectile.AddComponent<NetworkTankShot>();
            typeof(Mirror.NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkTankShot).GetField("direction", flags).SetValue(shot, Vector2.right);
            typeof(NetworkTankShot).GetField("launchSpeed", flags).SetValue(shot, 8f);
            typeof(NetworkTankShot).GetField("launchTime", flags).SetValue(shot, NetworkBombFx.KickServerNow - 10);
            paused.SetValue(null, false);
            var update = typeof(NetworkTankShot).GetMethod("LateUpdate", flags);
            update.Invoke(shot, null);
            Assert.IsTrue((bool)typeof(NetworkTankShot).GetProperty("ObserverPresentationActive", flags).GetValue(shot));
            Assert.That(child.transform.position.x, Is.InRange(0f, 1f), "Confirmed flight has a bounded preview even with old snapshots.");
            Assert.AreEqual(Vector3.zero, projectile.transform.position);
            Vector3 displayed = child.transform.position;
            paused.SetValue(null, true); projectile.transform.position = Vector3.right * 10;
            update.Invoke(shot, null);
            Assert.That(Vector3.Distance(displayed, child.transform.position), Is.LessThan(.00001f));
            Assert.AreEqual(Vector3.right * 10, projectile.transform.position);
            paused.SetValue(null, false); update.Invoke(shot, null);
            Assert.That(Vector3.Distance(displayed, child.transform.position), Is.LessThan(.00001f));
            typeof(NetworkTankShot).GetMethod("OnDisable", flags).Invoke(shot, null);
            Assert.AreEqual(Vector3.zero, child.transform.localPosition);
            Assert.IsFalse((bool)typeof(NetworkTankShot).GetProperty("ObserverPresentationActive", flags).GetValue(shot));
        }
        finally
        {
            paused.SetValue(null, oldPaused); NetSync.Mode = oldMode; clientState.SetValue(null, oldState);
            Object.DestroyImmediate(projectile);
        }
    }
}
