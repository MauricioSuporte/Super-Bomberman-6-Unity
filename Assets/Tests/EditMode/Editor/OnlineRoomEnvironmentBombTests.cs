using System.Collections;
using System.Reflection;
using Assets.Scripts.Netcode;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class OnlineRoomEditModeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ApprovedEnvironmentPullRetainsShortSegmentBeforeStopWithoutAbilityOrExtrapolation(bool previousPrediction)
    {
        var oldMode = NetSync.Mode;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var clientState = typeof(NetworkClient).GetField("connectState", BindingFlags.Static | BindingFlags.NonPublic);
        var oldClient = clientState.GetValue(null);
        var paused = typeof(GamePauseController).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var oldPaused = paused.GetValue(null);
        var actor = new GameObject("BoilerPresentationOwner"); actor.SetActive(false); actor.AddComponent<BoxCollider2D>();
        var replica = new GameObject("BoilerReplica"); replica.SetActive(false); replica.AddComponent<CircleCollider2D>();
        const uint bombId = uint.MaxValue - 230;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            clientState.SetValue(null, System.Enum.Parse(clientState.FieldType, "Connected")); paused.SetValue(null, false);
            var identity = actor.AddComponent<NetworkIdentity>(); var fx = actor.AddComponent<NetworkBombFx>();
            typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", flags).Invoke(identity, null);
            typeof(NetworkIdentity).GetProperty("isLocalPlayer").SetValue(identity, true);
            var bomb = replica.AddComponent<Bomb>(); var bombIdentity = replica.AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty("netId").SetValue(bombIdentity, bombId);
            NetworkClient.spawned.Add(bombId, bombIdentity); replica.SetActive(true);
            Vector2 start = new(710, 710); replica.transform.position = start;
            var motion = new NetworkBombFx.KickMotion { bombId = bombId, revision = 1, inputTick = 2,
                kind = NetworkBombFx.BombMotionKind.Environment, start = start, end = start + Vector2.up,
                duration = .08f, moving = true, first = true, serverTime = NetworkBombFx.KickServerNow - (previousPrediction ? 0 : 1),
                rubber = true, bounceDuration = .1f, bounceHeight = .5f };
            using (var writer = NetworkWriterPool.Get())
            { writer.Write(motion); motion = new NetworkReader(writer.ToArray()).Read<NetworkBombFx.KickMotion>(); }
            Assert.IsFalse(fx.BeginAbilityPrediction(bomb, motion, Vector2.up, 0), "Environment is approved presentation, never an owner-predicted action.");
            var present = typeof(NetworkBombFx).GetMethod("PresentBombMotions", flags);
            if (previousPrediction)
            {
                var pending = motion; pending.kind = NetworkBombFx.BombMotionKind.Search; pending.inputTick = 1;
                pending.duration = 1; pending.end = start + Vector2.right;
                Assert.IsTrue(fx.BeginAbilityPrediction(bomb, pending, Vector2.right, 0));
                present.Invoke(fx, new object[] { .15f, .15f });
            }
            fx.ReceiveKickMotion(motion); Assert.AreEqual((Vector3)start, replica.transform.position,
                "An autonomous pull must not inherit the lead of an earlier pending player action.");
            var stop = motion; stop.revision = 2; stop.first = false; stop.moving = false;
            stop.start = stop.end; stop.duration = 0; stop.serverTime += .08;
            fx.ReceiveKickMotion(stop);
            paused.SetValue(null, true); present.Invoke(fx, new object[] { .04f, .04f });
            Assert.AreEqual((Vector3)start, replica.transform.position);
            paused.SetValue(null, false); present.Invoke(fx, new object[] { .04f, .04f });
            Assert.AreEqual(start.y + .5f, replica.transform.position.y, .0001f,
                "A delayed start and its stop arriving together must still display the short pull.");
            Assert.IsTrue(replica.GetComponent<Collider2D>().enabled);
            Assert.IsFalse(fx.AbilityPredictionLocksMovement); Assert.IsFalse(fx.HasLocalMountAbilityPose);
            present.Invoke(fx, new object[] { .05f, .05f });
            Assert.AreEqual((Vector3)motion.end, replica.transform.position);
            Assert.IsFalse(bomb.HasExploded);
            fx.ReceiveKickMotion(new NetworkBombFx.KickMotion { bombId = bombId, revision = 3, inputTick = 1,
                kind = NetworkBombFx.BombMotionKind.Environment, start = start, end = motion.end,
                duration = .08f, moving = true, first = true, serverTime = NetworkBombFx.KickServerNow - 1,
                rubber = true, bounceDuration = .1f, bounceHeight = .5f });
            present.Invoke(fx, new object[] { .3f, .3f });
            Assert.AreEqual((Vector3)motion.end, replica.transform.position, "No next cell or Rubber bounce is approved by a boiler pull.");
            Assert.IsFalse(NetworkBombFx.IsFlightArc(NetworkBombFx.BombMotionKind.Environment));
        }
        finally
        {
            typeof(NetworkBombFx).GetMethod("ClearKickPlayback", flags)?.Invoke(actor.GetComponent<NetworkBombFx>(), null);
            NetworkClient.spawned.Remove(bombId); Object.DestroyImmediate(actor); Object.DestroyImmediate(replica);
            NetSync.Mode = oldMode; clientState.SetValue(null, oldClient); paused.SetValue(null, oldPaused);
        }
    }

    [Test]
    public void PureClientSpecialTileHandlersCannotMutatePuzzleScheduleExplosionOrChooseRocketTarget()
    {
        var oldMode = NetSync.Mode; var random = Random.state;
        var actor = new GameObject("SpecialTileAuthority"); actor.SetActive(false);
        var sourceObject = new GameObject("SpecialTileSource"); sourceObject.SetActive(false); sourceObject.AddComponent<BoxCollider2D>();
        var grid = new GameObject("SpecialTileGrid", typeof(Grid));
        var mapObject = new GameObject("Ground", typeof(Tilemap)); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.GetComponent<Tilemap>();
        var bombObject = new GameObject("CapturedBombCandidate"); bombObject.SetActive(false); bombObject.AddComponent<CircleCollider2D>();
        var a = ScriptableObject.CreateInstance<Tile>(); var b = ScriptableObject.CreateInstance<Tile>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            NetSync.Mode = NetSync.NetMode.Client;
            var source = sourceObject.AddComponent<BombController>(); source.groundTiles = map; source.destructibleTiles = map;
            bombObject.AddComponent<Bomb>(); map.SetTile(Vector3Int.zero, a);
            var boiler = actor.AddComponent<BoilerTriggerGroundTileHandler>();
            boiler.OnBombAt(source, Vector2.zero, Vector3Int.zero, a, bombObject);
            Assert.IsNull(bombObject.GetComponent<BoilerCapturedBomb>());
            Assert.IsFalse((bool)typeof(BoilerTriggerGroundTileHandler).GetField("triggeredOnce", flags).GetValue(boiler));
            var swap = actor.AddComponent<SwapGroundTileHandler>();
            typeof(SwapGroundTileHandler).GetField("tileA", flags).SetValue(swap, a);
            typeof(SwapGroundTileHandler).GetField("tileB", flags).SetValue(swap, b);
            swap.OnExplosionHit(source, Vector2.zero, Vector3Int.zero, a);
            var blackout = actor.AddComponent<SwapGroundTileBlackoutHandler>();
            typeof(SwapGroundTileBlackoutHandler).GetField("tileA", flags).SetValue(blackout, a);
            typeof(SwapGroundTileBlackoutHandler).GetField("tileB", flags).SetValue(blackout, b);
            blackout.OnExplosionHit(source, Vector2.zero, Vector3Int.zero, a);
            Assert.AreSame(a, map.GetTile(Vector3Int.zero));
            var dynamite = actor.AddComponent<DynamiteTileHandler>();
            Assert.IsFalse(dynamite.HandleHit(source, Vector2.zero, Vector3Int.zero));
            Assert.AreEqual(0, ((IDictionary)typeof(DynamiteTileHandler).GetField("_scheduledDetonationTimes", flags).GetValue(dynamite)).Count);
            Assert.IsFalse(actor.AddComponent<RocketTileHandler>().HandleHit(source, Vector2.zero, Vector3Int.zero));
            var rocket = actor.AddComponent<StageAssets.DestructibleRocketController>();
            rocket.Launch(Vector3.up, 1, null);
            typeof(StageAssets.DestructibleRocketController).GetMethod("Update", flags).Invoke(rocket, null);
            Assert.AreEqual(Vector3.zero, actor.transform.position); Assert.AreEqual(random, Random.state);
            var power = actor.AddComponent<PowerGroundTileHandler>(); int radius = 1; bool pierce = false;
            Assert.IsTrue(power.TryModifyExplosion(source, Vector2.zero, a, ref radius, ref pierce));
            Assert.AreEqual(power.BoostedRadius, radius, "Read-only radius previews retain the offline rule.");
        }
        finally
        {
            Object.DestroyImmediate(actor); Object.DestroyImmediate(sourceObject); Object.DestroyImmediate(grid);
            Object.DestroyImmediate(bombObject); Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            NetSync.Mode = oldMode; Random.state = random;
        }
    }
}
