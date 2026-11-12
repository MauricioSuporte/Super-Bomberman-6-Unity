using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    public struct CoopWorldBegin : NetworkMessage { public string scene, hash; }
    public struct CoopSpriteFrame
    {
        public int id, sprite, material, sortingLayer, order, animation;
        public double time;
        public AnimatedSpriteRenderer.PlaybackState playback;
        public Vector3 position, scale, offsetRight, offsetUp;
        public Quaternion rotation;
        public Color color;
        public bool flipX, flipY, visible, pauseWithGameplay;
        public byte mask;
        public float enemyStepLimit;
        public CoopBlackoutMask blackout;
        public CoopWaterSurface water;
        public CoopAbsorptionLight absorption;
        public bool Matches(CoopSpriteFrame other) => animation == 0 && other.animation == 0 && id == other.id && sprite == other.sprite && material == other.material &&
            sortingLayer == other.sortingLayer && order == other.order && position == other.position && scale == other.scale &&
            rotation == other.rotation && color == other.color && flipX == other.flipX && flipY == other.flipY && visible == other.visible && mask == other.mask &&
            enemyStepLimit == other.enemyStepLimit && pauseWithGameplay == other.pauseWithGameplay && (blackout == null ? other.blackout == null : blackout.Equals(other.blackout)) &&
            (water == null ? other.water == null : water.Equals(other.water)) &&
            (absorption == null ? other.absorption == null : absorption.Equals(other.absorption));
    }
    public struct CoopSprites : NetworkMessage { public double time; public string scene; public CoopSpriteFrame[] frames; public int[] removed; }

    // R02: the server capture of the co-op world runs after every LateUpdate. The manager itself runs at -150, before the
    // camera follow and the screen-space overlays (sandstorm, order 0) move in their LateUpdate; sampling there stamped the
    // previous frame's pose with this frame's time, which bends the approved trajectory after every long server frame.
    [DefaultExecutionOrder(32000)]
    public sealed class CoopLateCaptureDriver : MonoBehaviour
    {
        internal BombermanNetworkManager manager;
        void LateUpdate() { if (manager != null) manager.CaptureCoopWorldLate(); }
    }

    /// <summary>
    /// R02: one buffered presentation sample. Never serialized: the client-only bookkeeping stays out of
    /// <see cref="CoopSpriteFrame"/>, whose size is paid by every server and client message array.
    /// The server's Development shape reconstruction uses the same type so both follow one segment rule.
    /// </summary>
    public struct CoopVisualSample
    {
        public CoopSpriteFrame frame;
        // Time of the full frame whose animation playback this sample shows (position-only samples keep it).
        public double playbackOrigin;
        // Explicit reposition: the segment from the previous sample is never interpolated (wrap/camera cut).
        public bool cut;

        public static CoopVisualSample Full(in CoopSpriteFrame frame) => new() { frame = frame, playbackOrigin = frame.time };

        public CoopVisualSample At(double time, Vector3 position, bool reposition = false)
        {
            var sample = this; sample.frame.time = time; sample.frame.position = position; sample.cut = reposition;
            return sample;
        }

        // Linear between two visible samples unless the server marked a reposition or an enemy discontinuity.
        public static bool Interpolates(in CoopVisualSample from, in CoopVisualSample to)
        {
            if (to.cut || !from.frame.visible || !to.frame.visible || to.frame.time <= from.frame.time) return false;
            return !(from.frame.enemyStepLimit > 0 && (to.frame.time - from.frame.time > .25 ||
                Vector3.Distance(from.frame.position, to.frame.position) > from.frame.enemyStepLimit));
        }
    }
    public struct CoopTileFrame
    {
        public Vector3Int cell; public int tile; public Color color; public Matrix4x4 matrix;
        public byte collisionType;
        public int collisionSprite;
        public bool animated;
        public int animationFrame;
        public bool Matches(CoopTileFrame other) => cell == other.cell && tile == other.tile && color == other.color && matrix == other.matrix &&
            collisionType == other.collisionType && collisionSprite == other.collisionSprite &&
            animated == other.animated && animationFrame == other.animationFrame;
    }
    public struct CoopTiles : NetworkMessage
    {
        public string scene, path;
        public bool reset, complete, collisionEnabled;
        public Vector2 collisionOffset;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Color color;
        public CoopTileFrame[] cells;
    }
    public struct BattleFeedback : NetworkMessage { public string scene; public byte kind; public uint netId; public Vector3Int cell; public Vector2 direction; public float value, duration; public int steps; public Vector3Int[] path; }
    public struct CoopSound : NetworkMessage { public string scene, resource; public int clip; public float volume, pitch; public bool voice, exclusiveExplosion; }

    /// <summary>
    /// A co-op stage executes its scene behaviours only on the server. Clients keep
    /// the local tilemaps/camera/HUD and receive bounded visual deltas for scene actors.
    /// Mirror players, bombs, items and mounts retain their existing network paths.
    /// </summary>
    public partial class BombermanNetworkManager
    {
        public static bool IsCoopRoom => NetSync.IsOnline && RoomMode != OnlineRoomMode.BattleMode;
        public static bool IsCoopStage => IsCoopRoom && SceneManager.GetActiveScene().name.StartsWith("Stage_", StringComparison.Ordinal);
        static readonly Unity.Profiling.ProfilerMarker CoopPresentationMarker = new("SB6.Coop.Presentation");
        CoopSceneCatalog coopCatalog;
        readonly Dictionary<Sprite, int> coopSpriteIds = new();
        readonly Dictionary<TileBase, int> coopTileIds = new();
        readonly Dictionary<Material, int> coopMaterialIds = new();
        readonly Dictionary<AudioClip, int> coopSoundIds = new();
        readonly Dictionary<int, CoopSpriteFrame> coopLastSprites = new();
        readonly Dictionary<int, SpriteRenderer> coopViews = new();
        readonly Dictionary<int, NetworkBombFx> coopOwnerBoatSprites = new();
        readonly Dictionary<int, bool> coopOwnerBoatVisibility = new();

        public static void SetOwnerBoatSpriteSuppression(NetworkBombFx owner, int[] ids, bool hidden)
        {
            if (NetworkServer.active || singleton is not BombermanNetworkManager manager || ids == null) return;
            foreach (int id in ids)
            {
                if (id == 0) continue;
                if (hidden)
                {
                    manager.coopOwnerBoatSprites[id] = owner;
                    if (manager.coopViews.TryGetValue(id, out var view) && view != null)
                    {
                        if (!manager.coopOwnerBoatVisibility.ContainsKey(id)) manager.coopOwnerBoatVisibility[id] = view.enabled;
                        view.enabled = false;
                    }
                }
                else if (manager.coopOwnerBoatSprites.TryGetValue(id, out var current) && current == owner)
                {
                    manager.coopOwnerBoatSprites.Remove(id);
                    if (manager.coopOwnerBoatVisibility.Remove(id, out bool visible) && manager.coopViews.TryGetValue(id, out var view) && view != null)
                        view.enabled = visible;
                }
            }
        }
        readonly Dictionary<string, Tilemap> coopMaps = new();
        readonly Dictionary<string, Dictionary<Vector3Int, CoopTileFrame>> coopLastTiles = new();
        readonly Dictionary<string, Dictionary<Vector3Int, CoopTileFrame>> coopTileScratch = new();
        readonly List<NetworkConnectionToClient> coopPeerBuffer = new();
        readonly List<CoopSpriteFrame> coopChangedSprites = new();
        readonly List<int> coopRemovedSprites = new();
        readonly HashSet<int> coopCurrentSprites = new();
        readonly List<CoopTileFrame> coopChangedTiles = new();
        readonly HashSet<int> coopInitializedPeers = new();
        readonly HashSet<string> coopMissingAssets = new();
        SpriteRenderer[] coopRenderers = Array.Empty<SpriteRenderer>();
        StageBlackout[] coopBlackoutSources = Array.Empty<StageBlackout>();
        BattleMode5ConveyorController[] coopConveyorSources = Array.Empty<BattleMode5ConveyorController>();
        float coopNextDiscover, coopNextFrame, coopNextTiles;
        string coopScene;
        GameObject coopViewRoot;

        void InitializeCoopClient()
        {
            NetworkClient.RegisterHandler<CoopWorldBegin>(ApplyCoopBegin);
            NetworkClient.RegisterHandler<BombAnimations>(ApplyBombAnimations);
            NetworkClient.RegisterHandler<CoopSprites>(ApplyCoopSprites);
            NetworkClient.RegisterHandler<CoopMotion>(ApplyCoopMotion);
            NetworkClient.RegisterHandler<CoopTiles>(ApplyCoopTiles);
            NetworkClient.RegisterHandler<CoopTileAnimation>(ApplyCoopTileAnimation);
            NetworkClient.RegisterHandler<CoopSound>(ApplyCoopSound);
            NetworkClient.RegisterHandler<CoopEnvironmentLoops>(ApplyCoopEnvironmentLoops);
            NetworkClient.RegisterHandler<BattleFeedback>(ApplyBattleFeedback);
            NetworkClient.RegisterHandler<SuddenDeathState>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                var suddenDeath = FindAnyObjectByType<BattleSuddenDeathController>();
                if (suddenDeath != null) suddenDeath.ReceiveNetworkSuddenDeath(message);
            });
            NetworkClient.RegisterHandler<ConveyorPhase>(ApplyConveyorPhase);
            NetworkClient.RegisterHandler<PortalMaskSnapshot>(ApplyPortalMasks);
            NetworkClient.RegisterHandler<RoofTileState>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode8RoofController>()) controller.ReceiveNetworkRoof(message);
            });
            NetworkClient.RegisterHandler<RopeTileView>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode11ImpulseRopeController>()) controller.ReceiveNetworkRope(message);
            });
            NetworkClient.RegisterHandler<BlackoutView>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var blackout in coopBlackoutSources) if (blackout != null) blackout.ReceiveNetworkBlackout(message);
            });
            NetworkClient.RegisterHandler<PowderTrailFrame>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode3PowderTrailController>()) controller.ReceiveNetworkPowder(message);
            });
            NetworkClient.RegisterHandler<MagnetFieldPhase>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var handler in FindObjectsByType<MagnetIndestructibleTileHandler>()) handler.ReceiveNetworkField(message);
            });
            NetworkClient.RegisterHandler<PortalStarView>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode9MinecartController>()) controller.ReceiveNetworkStar(message);
                foreach (var controller in FindObjectsByType<BattleMode7PortalController>()) controller.ReceiveNetworkPortalStar(message);
            });
            NetworkClient.RegisterHandler<MinecartView>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode9MinecartController>()) controller.ReceiveNetworkCart(message);
            });
            NetworkClient.RegisterHandler<FallingBombTarget>(message =>
            {
                if (message.scene != SceneManager.GetActiveScene().name) return;
                foreach (var controller in FindObjectsByType<BattleMode2FallingBombController>())
                    controller.ReceiveNetworkTarget(message);
            });
            NetworkClient.RegisterHandler<CoopSpriteDefinition>(ApplyCoopSpriteDefinition);
            NetworkClient.RegisterHandler<CoopAnimationDefinition>(ApplyCoopAnimationDefinition);
#if UNITY_EDITOR || DEBUG
            LocalProjectileSmoke.RegisterMessages();
#endif
            NetworkClient.RegisterHandler<CoopMusic>(ApplyCoopMusic);
            NetworkClient.RegisterHandler<CoopStageStatus>(ApplyCoopStageStatus);
            NetworkClient.RegisterHandler<CoopCollisions>(ApplyCoopCollisions);
        }

        void CoopSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearCoopWorld();
            if (!IsCoopStage) return;
            coopScene = scene.name;
            coopCatalog = Resources.Load<CoopSceneCatalog>("CoopScenes/" + scene.name);
            if (coopCatalog == null)
            {
                Debug.LogError("[Coop] Missing scene catalog: " + scene.name + ". Refresh Co-op Scene Catalogs and rebuild both peers.");
                return;
            }
            IndexCatalog(coopCatalog.sprites, coopSpriteIds);
            IndexCatalog(coopCatalog.tiles, coopTileIds);
            IndexCatalog(coopCatalog.materials, coopMaterialIds);
            IndexCatalog(coopCatalog.sounds, coopSoundIds);
            var blackouts = new List<StageBlackout>();
            foreach (var blackout in FindObjectsByType<StageBlackout>(FindObjectsInactive.Include))
                if (blackout.gameObject.scene == scene) blackouts.Add(blackout);
            coopBlackoutSources = blackouts.ToArray();
            var conveyors = new List<BattleMode5ConveyorController>();
            foreach (var conveyor in FindObjectsByType<BattleMode5ConveyorController>(FindObjectsInactive.Include))
                if (conveyor.gameObject.scene == scene) conveyors.Add(conveyor);
            coopConveyorSources = conveyors.ToArray();
            foreach (var map in FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
                if (map.gameObject.scene == scene) coopMaps[CoopPath(map.transform)] = map;
            foreach (var health in FindObjectsByType<CharacterHealth>(FindObjectsInactive.Include))
                if (health.gameObject.scene == scene && !HasNetworkParent(health.transform)) coopSceneHealth[CoopPath(health.transform)] = health;
            InitializeCoopCinematics();
            if (NetworkServer.active)
            {
                Tilemap.tilemapTileChanged -= OnCoopTileChanged;
                Tilemap.tilemapTileChanged += OnCoopTileChanged;
                if (StageIntroTransition.Instance == null) GameMusicController.Instance?.PlayDefaultMusic();
                return;
            }
            if (BossRushSession.IsActive) BossRushTimerPresenter.EnsureInScene();
            if (StageIntroTransition.Instance != null)
            {
                if (StageIntroTransition.Instance.fadeImage != null)
                {
                    var fade = StageIntroTransition.Instance.fadeImage;
                    fade.gameObject.SetActive(true);
                    fade.color = Color.black;
                    fade.transform.SetAsLastSibling();
                }
                if (StageIntroTransition.Instance.stageLabel != null) StageIntroTransition.Instance.stageLabel.gameObject.SetActive(false);
            }

            coopViewRoot = new GameObject("CoopServerPresentation");
            SceneManager.MoveGameObjectToScene(coopViewRoot, scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || HasNetworkParent(behaviour.transform) || KeepCoopPresentation(behaviour)) continue;
                    behaviour.enabled = false;
                    behaviour.StopAllCoroutines();
                }
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    if (!HasNetworkParent(animator.transform)) animator.enabled = false;
                foreach (var body in root.GetComponentsInChildren<Rigidbody2D>(true))
                    if (!HasNetworkParent(body.transform)) body.simulated = false;
                foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (!HasNetworkParent(collider.transform)) collider.enabled = false;
                foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (!HasNetworkParent(sprite.transform)) sprite.forceRenderingOff = true;
            }
            CreateCoopCollisionMaps();
            Debug.Log("[Coop] Client stage simulation disabled; awaiting server presentation and query geometry: " + coopScene);
        }

        static bool KeepCoopPresentation(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();
            if (type.Assembly != typeof(GameManager).Assembly || behaviour.GetComponentInParent<Canvas>(true) != null) return true;
            string name = type.Name;
            return behaviour is BombermanNetworkManager or OnlineLobbyMenu or OnlineRoomBrowser or OnlineChatOverlay or GamePauseController or StageBlackout or WorldBlackoutRenderer or BattleMode9MinecartController ||
                name.Contains("Camera", StringComparison.Ordinal) || name.Contains("Hud", StringComparison.Ordinal) ||
                name.Contains("HUD", StringComparison.Ordinal) || name.Contains("Viewport", StringComparison.Ordinal) ||
                name.StartsWith("Localized", StringComparison.Ordinal) || name == "PlayerInputManager" ||
                name == "GameSession" || name == "GameMusicController" || name == "BossRushTimerPresenter";
        }

        static bool HasNetworkParent(Transform value)
        {
            for (; value != null; value = value.parent)
                if (value.TryGetComponent<NetworkIdentity>(out _)) return true;
            return false;
        }

        static string CoopPath(Transform value)
        {
            string path = value.name;
            while (value.parent != null) { value = value.parent; path = value.name + "/" + path; }
            return path;
        }

        static void IndexCatalog<T>(T[] values, Dictionary<T, int> index) where T : UnityEngine.Object
        {
            index.Clear();
            for (int i = 0; i < values.Length; i++) if (values[i] != null) index[values[i]] = i;
        }

        void ClearCoopWorld()
        {
            ClearWorldBombMotions();
            coopOwnerBoatSprites.Clear();
            coopOwnerBoatVisibility.Clear();
            GetComponent<NetworkBombMotionLifetime>()?.Clear();
            ClearPortalMaskViews();
            ClearCoopCollisionWorld();
            ClearCoopSprites();
            ClearCoopTileAnimation();
            ClearCoopMotion();
            coopLastSprites.Clear(); coopViews.Clear(); coopMaps.Clear(); coopLastTiles.Clear();
            coopTileScratch.Clear(); coopPeerBuffer.Clear(); coopChangedSprites.Clear();
            coopRemovedSprites.Clear(); coopCurrentSprites.Clear(); coopChangedTiles.Clear();
            coopInitializedPeers.Clear(); coopMissingAssets.Clear();
            coopSpriteIds.Clear(); coopTileIds.Clear(); coopMaterialIds.Clear(); coopSoundIds.Clear();
            coopFadeSamples.Clear();
            coopCinematicImages.Clear(); coopClientImages.Clear();
            coopClientCameras.Clear(); coopApprovedCamera = null;
            coopSpotlightImage = null;
            coopSceneHealth.Clear(); coopServerHealth = Array.Empty<CharacterHealth>(); coopNextStatus = 0;
            coopRenderers = Array.Empty<SpriteRenderer>();
            coopBlackoutSources = Array.Empty<StageBlackout>();
            coopConveyorSources = Array.Empty<BattleMode5ConveyorController>();
            coopCatalog = null; coopScene = null; coopHasMusic = false;
            if (coopViewRoot != null) Destroy(coopViewRoot);
            coopViewRoot = null;
            coopNextDiscover = coopNextFrame = coopNextTiles = 0;
        }

        void TickCoopWorld()
        {
            if (!NetworkServer.active) PresentCoopFades();
            TickWorldBombMotions();
            TickCoopEnvironmentAudio();
            PublishPortalMasks();
            PresentPortalMaskViews();
            foreach (var blackout in coopBlackoutSources)
                if (blackout != null)
                {
                    if (NetworkServer.active) blackout.PublishNetworkBlackout();
                    else blackout.PresentNetworkBlackout(CoopVisualClock);
                }
            if (!NetworkServer.active) return;
            if (!CoopLateCapture) { CaptureCoopWorld(); return; }
            if (coopLateDriver == null) { coopLateDriver = gameObject.AddComponent<CoopLateCaptureDriver>(); coopLateDriver.manager = this; }
            if (CoopShapeDiagnostics && IsCoopStage) ProbeCoopPhaseEarly();
        }

        CoopLateCaptureDriver coopLateDriver;
        static int coopCaptureState;

        // Development A/B only: "-coop-r02-capture-early" restores the old capture phase (manager LateUpdate, order -150).
        internal static bool CoopLateCapture
        {
            get
            {
                if (coopCaptureState == 0)
                    coopCaptureState = Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "-coop-r02-capture-early") >= 0 ? 1 : 2;
                return coopCaptureState == 2;
            }
        }

        internal void CaptureCoopWorldLate()
        {
            if (!NetworkServer.active || !CoopLateCapture) return;
            if (CoopShapeDiagnostics) ProbeCoopPhaseLate();
            CaptureCoopWorld();
            RecordR03Authority();
        }

        void CaptureCoopWorld()
        {
            if (!NetworkServer.active || !IsCoopStage || coopCatalog == null || NetworkServer.isLoadingScene) return;
            bool shapeDiagnostics = CoopShapeDiagnostics;
            MergeCoopPendingRenderers();
            PublishCoopProjectileEdges();
            if (shapeDiagnostics) SampleCoopTruth();
            TrackCoopOverlays(shapeDiagnostics);
            if (Time.unscaledTime < coopNextFrame) { PublishCoopEnemyTransitions(shapeDiagnostics); PublishCoopMotion(shapeDiagnostics); return; }
            using var scope = CoopPresentationMarker.Auto();
            coopNextFrame = Time.unscaledTime + 0.05f;
            CaptureCoopCollisionState();
            var peers = coopPeerBuffer;
            peers.Clear();
            foreach (var peer in NetworkServer.connections.Values)
            {
                if (peer == null || !peer.isAuthenticated || !peer.isReady || peer == NetworkServer.localConnection) continue;
                peers.Add(peer);
                if (!coopInitializedPeers.Add(peer.connectionId)) continue;
                peer.Send(new CoopWorldBegin { scene = coopScene, hash = coopCatalog.contentHash });
#if UNITY_EDITOR || DEBUG
                LocalProjectileSmoke.SendDefinitions(peer);
#endif
                if (coopHasMusic) peer.Send(coopMusic);
                foreach (var definition in coopDefinitions.Values) peer.Send(definition);
                foreach (var definition in coopAnimationDefinitions.Values) peer.Send(definition);
                SendCoopSprites(peer, new List<CoopSpriteFrame>(coopLastSprites.Values), Array.Empty<int>());
                foreach (var map in coopMaps)
                    if (map.Value != null) SendCoopTiles(peer, map.Key, map.Value, true,
                        coopLastTiles.TryGetValue(map.Key, out var cells) ? new List<CoopTileFrame>(cells.Values) : ReadCoopTileFrames(map.Value));
                SendCoopCollisionFrames(peer, new List<CoopColliderFrame>(coopLastCollisions.Values), Array.Empty<int>(), true);
            }
            if (Time.unscaledTime >= coopNextDiscover)
            {
                coopNextDiscover = Time.unscaledTime + 0.25f;
                coopServerHealth = FindObjectsByType<CharacterHealth>(FindObjectsInactive.Include);
                // Keep pooled renderers tracked so even short-lived activations are
                // sampled at 20 Hz rather than waiting for the next discovery pass.
                var found = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
                var renderers = new List<SpriteRenderer>();
                foreach (var renderer in found)
                    if (renderer.gameObject.scene.name == coopScene && !HasNetworkParent(renderer.transform)) renderers.Add(renderer);
                coopRenderers = renderers.ToArray();
            }
            if (coopPendingRenderers.Count > 0)
            {
                var tracked = new HashSet<SpriteRenderer>(coopRenderers);
                foreach (var renderer in coopPendingRenderers)
                    if (renderer != null && renderer.gameObject.scene.name == coopScene) tracked.Add(renderer);
                coopRenderers = new SpriteRenderer[tracked.Count]; tracked.CopyTo(coopRenderers);
                coopPendingRenderers.Clear();
            }
            CaptureCoopStageStatus();
            var changed = coopChangedSprites;
            var current = coopCurrentSprites;
            changed.Clear(); current.Clear();
            foreach (var renderer in coopRenderers)
            {
                if (renderer == null) continue;
                int id = renderer.GetEntityId().GetHashCode();
                current.Add(id);
                bool active = renderer.gameObject.activeInHierarchy;
                // Send the visible -> hidden transition once. Sleeping pool objects
                // need no repeated transform/material/sprite reads or initial clone.
                if (!active && (!coopLastSprites.TryGetValue(id, out var sleeping) || !sleeping.visible))
                {
                    if (shapeDiagnostics) ForgetCoopShape(id);
                    continue;
                }
                long metricStarted = GameplayLatencyDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                var frame = CaptureCoopFrame(renderer);
                bool hadPrevious = coopLastSprites.TryGetValue(id, out var previous);
                bool frameChanged = !hadPrevious || !frame.Matches(previous);
                if (shapeDiagnostics) TakeCoopShapePending(id, coopShapeSent);
                int extraFrames = 0;
                if (frameChanged && hadPrevious && !CoopScreenSpace(renderer, id) && TryCoopRestAnchor(id, previous, frame, out var anchor))
                {
                    // R02: after unsent (unchanged) ticks the client would interpolate from the old sample across the
                    // whole rest. The approved rest pose at the previous server frame starts the segment where it began.
                    changed.Add(anchor); extraFrames++;
                    if (shapeDiagnostics) coopShapeSent.Add(CoopVisualSample.Full(anchor));
                }
                if (frameChanged) { changed.Add(frame); coopLastSentTime[id] = frame.time; }
                RecordCoopPresentationMetric(renderer, frame, frameChanged, metricStarted);
                if (shapeDiagnostics)
                {
                    // A reposition of this server frame was already queued by TrackCoopOverlays (anchor + cut sample).
                    if (frameChanged) coopShapeSent.Add(new CoopVisualSample { frame = frame, playbackOrigin = frame.time, cut = coopRepositionIds.Contains(id) });
                    EvaluateCoopShape(renderer, id, coopShapeSent, frame.time, extraFrames);
                }
                coopLastSprites[id] = frame;
            }
            FlushCoopPresentationMetrics();
            var removed = coopRemovedSprites;
            removed.Clear();
            foreach (int id in coopLastSprites.Keys) if (!current.Contains(id)) removed.Add(id);
            foreach (int id in removed) { coopLastSprites.Remove(id); coopBlackoutMasks.Remove(id); coopEnemyVisualLimits.Remove(id); coopPhysicsPause.Remove(id); coopEnvironmentFamilies.Remove(id); coopMetricFamilies.Remove(id); ForgetCoopShape(id); ForgetCoopMotion(id); }
            SendCoopSpriteDeltas(peers, changed, removed.Count == 0 ? Array.Empty<int>() : removed.ToArray());
            if (coopChangedCollisions.Count > 0 || coopRemovedCollisions.Count > 0)
                foreach (var peer in peers) SendCoopCollisionFrames(peer, coopChangedCollisions, coopRemovedCollisions.ToArray(), false);
            bool periodicTiles = Time.unscaledTime >= coopNextTiles;
            if (periodicTiles) coopNextTiles = Time.unscaledTime + 0.25f;
            foreach (var map in coopMaps)
            {
                if (map.Value != null && (periodicTiles || coopDirtyMaps.Contains(map.Value) || CoopMapCollisionPoseChanged(map.Key, map.Value)))
                    CaptureCoopTiles(map.Key, map.Value, peers);
            }
            CaptureCoopTileAnimations(peers);
            coopDirtyMaps.Clear();
            PublishCoopMotion(shapeDiagnostics);
        }

        CoopSpriteFrame CaptureCoopFrame(SpriteRenderer renderer)
        {
            var frame = new CoopSpriteFrame
            {
                id = renderer.GetEntityId().GetHashCode(), sprite = CoopSpriteId(renderer.sprite), time = NetworkTime.localTime,
                material = CatalogId(renderer.sharedMaterial, coopMaterialIds), blackout = CaptureCoopBlackoutMask(renderer.GetEntityId().GetHashCode()), water = CaptureCoopWater(renderer), absorption = CaptureCoopAbsorption(renderer),
                enemyStepLimit = CoopEnemyVisualLimit(renderer), pauseWithGameplay = CoopPauseWithGameplay(renderer),
                position = renderer.transform.position, rotation = renderer.transform.rotation,
                scale = renderer.transform.lossyScale, color = renderer.color,
                sortingLayer = renderer.sortingLayerID, order = renderer.sortingOrder,
                visible = renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff,
                flipX = renderer.flipX, flipY = renderer.flipY, mask = (byte)renderer.maskInteraction
            };
            if (renderer.TryGetComponent<AnimatedSpriteRenderer>(out var animation) && animation.isActiveAndEnabled)
            {
                frame.pauseWithGameplay |= animation.RespectGamePause;
                frame.animation = CaptureCoopAnimation(animation);
                frame.playback = coopProjectiles.ContainsKey(frame.id) ? CaptureCoopProjectilePlayback(animation) :
                    (IsCoopBossRenderer(renderer) || IsCoopEnvironmentRenderer(renderer)) ? CaptureCoopBossPlayback(animation) : CaptureCoopPlayback(animation);
                // Remove the sampled jump offset; the client reapplies the offset for its rendered animation frame.
                frame.offsetRight = animation.FrameOffsetToWorld(Vector2.right);
                frame.offsetUp = animation.FrameOffsetToWorld(Vector2.up);
                Vector2 offset = animation.CurrentSpriteOffset;
                frame.position -= frame.offsetRight * offset.x + frame.offsetUp * offset.y;
            }
            RecordR06Boss("r06_native", renderer, frame, frame.time, frame.time, frame.time);
            return frame;
        }

        int CatalogId<T>(T value, Dictionary<T, int> index) where T : UnityEngine.Object
        {
            if (value == null) return -1;
            if (index.TryGetValue(value, out int id)) return id;
            if (value is Material) return -1;
            string key = typeof(T).Name + ":" + value.name;
            if (coopMissingAssets.Add(key)) Debug.LogWarning("[Coop] Catalog asset unavailable: " + key + " scene=" + coopScene);
            return -1;
        }

        void SendCoopSprites(NetworkConnectionToClient peer, List<CoopSpriteFrame> frames, int[] removed)
        {
            const int batch = 48;
            if (frames.Count == 0 && removed.Length > 0)
                peer.Send(new CoopSprites { time = NetworkTime.localTime, scene = coopScene, frames = Array.Empty<CoopSpriteFrame>(), removed = removed });
            for (int start = 0; start < frames.Count; start += batch)
                peer.Send(new CoopSprites { time = NetworkTime.localTime, scene = coopScene, frames = frames.GetRange(start, Mathf.Min(batch, frames.Count - start)).ToArray(),
                    removed = start == 0 ? removed : Array.Empty<int>() });
        }

        void SendCoopSpriteDeltas(List<NetworkConnectionToClient> peers, List<CoopSpriteFrame> frames, int[] removed)
        {
            const int batch = 48;
            if (frames.Count == 0 && removed.Length == 0) return;
            for (int start = 0; start < Mathf.Max(1, frames.Count); start += batch)
            {
                int count = Mathf.Min(batch, frames.Count - start);
                var chunk = count == 0 ? Array.Empty<CoopSpriteFrame>() : new CoopSpriteFrame[count];
                if (count > 0) frames.CopyTo(start, chunk, 0, count);
                var message = new CoopSprites { time = NetworkTime.localTime, scene = coopScene, frames = chunk,
                    removed = start == 0 ? removed : Array.Empty<int>() };
                // Send serializes synchronously. All observers can share the frame array.
                foreach (var peer in peers) peer.Send(message);
            }
        }

        void CaptureCoopTiles(string path, Tilemap map, List<NetworkConnectionToClient> peers)
        {
            bool first = !coopLastTiles.TryGetValue(path, out var previous);
            previous ??= new Dictionary<Vector3Int, CoopTileFrame>();
            if (!coopTileScratch.TryGetValue(path, out var current)) current = new Dictionary<Vector3Int, CoopTileFrame>();
            current.Clear();
            var changed = coopChangedTiles;
            changed.Clear();
            if (!coopAnimatedCells.TryGetValue(path, out var animatedCells)) coopAnimatedCells[path] = animatedCells = new List<Vector3Int>();
            animatedCells.Clear();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell);
                if (tile == null) continue;
                var frame = ReadCoopTileFrame(map, cell);
                current[cell] = frame;
                if (frame.animated) animatedCells.Add(cell);
                if (!previous.TryGetValue(cell, out var old)) changed.Add(frame);
                else
                {
                    var structural = frame; structural.animationFrame = old.animationFrame;
                    if (!structural.Matches(old)) changed.Add(frame);
                    else current[cell] = old; // Compact animation deltas update this below.
                }
            }
            foreach (var cell in previous.Keys)
                if (!current.ContainsKey(cell)) changed.Add(new CoopTileFrame { cell = cell, tile = -1 });
            coopLastTiles[path] = current;
            coopTileScratch[path] = previous;
            var pose = CaptureMapCollisionPose(map); coopMapCollisionPoses[path] = pose;
            // Map pose/color can change even without individual tile edits.
            const int batch = 48;
            for (int start = 0; start < Mathf.Max(1, changed.Count); start += batch)
            {
                int count = Mathf.Min(batch, changed.Count - start);
                var chunk = count == 0 ? Array.Empty<CoopTileFrame>() : new CoopTileFrame[count];
                if (count > 0) changed.CopyTo(start, chunk, 0, count);
                var message = new CoopTiles { scene = coopScene, path = path, reset = first && start == 0,
                    position = map.transform.position, rotation = map.transform.rotation, scale = map.transform.lossyScale,
                    color = map.color, cells = chunk, complete = start + count >= changed.Count,
                    collisionEnabled = pose.enabled, collisionOffset = pose.offset };
                foreach (var peer in peers) peer.Send(message);
            }
        }

        void SendCoopTiles(NetworkConnectionToClient peer, string path, Tilemap map, bool reset, List<CoopTileFrame> cells)
        {
            const int batch = 48;
            var pose = CaptureMapCollisionPose(map);
            for (int start = 0; start < Mathf.Max(1, cells.Count); start += batch)
                peer.Send(new CoopTiles { scene = coopScene, path = path, reset = reset && start == 0,
                    position = map.transform.position, rotation = map.transform.rotation, scale = map.transform.lossyScale,
                    color = map.color, cells = cells.GetRange(start, Mathf.Min(batch, cells.Count - start)).ToArray(),
                    complete = start + batch >= cells.Count, collisionEnabled = pose.enabled, collisionOffset = pose.offset });
        }

        CoopTileFrame ReadCoopTileFrame(Tilemap map, Vector3Int cell)
        {
            var type = map.GetColliderType(cell);
            var tile = map.GetTile(cell);
            bool conveyorOwned = false;
            if (tile is AnimatedTile)
                foreach (var conveyor in coopConveyorSources)
                    if (conveyor != null && conveyor.TryResolveNetworkTileOrigin(map, tile, out var original) &&
                        coopTileIds.TryGetValue(original, out int catalogId))
                    { coopTileIds[tile] = catalogId; conveyorOwned = true; break; }
            bool animated = !conveyorOwned && tile is AnimatedTile animation && animation.m_AnimatedSprites != null && animation.m_AnimatedSprites.Length > 0;
            return new CoopTileFrame { cell = cell, tile = CatalogId(tile, coopTileIds), color = map.GetColor(cell),
                matrix = map.GetTransformMatrix(cell), collisionType = (byte)type,
                collisionSprite = type == Tile.ColliderType.Sprite ? CoopSpriteId(map.GetSprite(cell)) : -1,
                animated = animated, animationFrame = animated ? ReadApprovedTileFrame(map, cell, (AnimatedTile)tile) : 0 };
        }

        List<CoopTileFrame> ReadCoopTileFrames(Tilemap map)
        {
            var frames = new List<CoopTileFrame>();
            foreach (var cell in map.cellBounds.allPositionsWithin) if (map.HasTile(cell)) frames.Add(ReadCoopTileFrame(map, cell));
            return frames;
        }

        bool CanApplyCoop(string scene) => !NetworkServer.active && IsCoopStage && coopCatalog != null && scene == coopScene;
        void ApplyCoopBegin(CoopWorldBegin message)
        {
            if (!CanApplyCoop(message.scene)) return;
            if (message.hash != coopCatalog.contentHash)
            {
                Debug.LogError("[Coop] Scene catalog mismatch. Rebuild client and server together: " + message.scene);
                StopClient();
                return;
            }
            // Development evidence for scene/session cleanup: a new stream must start with no history of another one.
            if (Debug.isDebugBuild)
                Debug.Log("[Coop] stream begin scene=" + message.scene + " priorSamples=" + coopVisualSamples.Count + " priorRemovals=" + coopVisualRemovals.Count +
                    " priorViews=" + coopViews.Count + " priorStats=" + coopPresentStats.Count + " priorBombSamples=" + bombVisualSamples.Count);
            foreach (var view in coopViews.Values) if (view != null) Destroy(view.gameObject);
            coopViews.Clear();
            coopCollisionObjectsReceived = false;
        }

        void ApplyCoopSprites(CoopSprites message)
        {
            if (!CanApplyCoop(message.scene)) return;
            foreach (var frame in message.frames ?? Array.Empty<CoopSpriteFrame>()) AppendCoopSpriteFrame(frame);
            foreach (int id in message.removed ?? Array.Empty<int>())
                coopVisualRemovals[id] = message.time;
        }

        void AppendCoopSpriteFrame(CoopSpriteFrame frame)
        {
            if (coopVisualRemovals.TryGetValue(frame.id, out double removal) && frame.time <= removal) return;
            if (!coopVisualSamples.TryGetValue(frame.id, out var samples))
                coopVisualSamples[frame.id] = samples = new List<CoopVisualSample>();
            if (samples.Count > 0 && frame.time < samples[^1].frame.time) return;
            if (samples.Count > 0 && frame.time == samples[^1].frame.time)
            {
                // Reliable sender order resolves changes within one authority frame
                // (creation followed by the initialized sprite/visibility).
                samples[^1] = CoopVisualSample.Full(frame);
                return;
            }
            var received = CoopVisualSample.Full(frame);
            if (CoopShapeDiagnostics) RecordCoopArrival(frame.id, samples, received);
            samples.Add(received);
            if (samples.Count > 64) samples.RemoveAt(0);
        }

        void PresentCoopSprite(CoopSpriteFrame frame, double playbackOrigin)
        {
            if (!coopViews.TryGetValue(frame.id, out var view) || view == null)
            {
                coopViewMaterialIds.Remove(frame.id);
                var go = new GameObject("ServerActor " + frame.id, typeof(SpriteRenderer));
                go.transform.SetParent(coopViewRoot.transform, false);
                view = go.GetComponent<SpriteRenderer>(); coopViews[frame.id] = view;
                coopDefaultSpriteMaterial = coopDefaultSpriteMaterial != null ? coopDefaultSpriteMaterial : view.sharedMaterial;
            }
            view.transform.SetPositionAndRotation(frame.position, frame.rotation);
            view.transform.localScale = frame.scale;
            coopViewSpriteIds[frame.id] = frame.sprite;
            view.sprite = CoopSpriteAt(frame.sprite);
            PresentCoopAnimation(view, frame, playbackOrigin);
            // A held recipe cannot override an explicit native sprite with a null/stale idle frame.
            if (frame.animation != 0 && frame.playback.speed == 0) view.sprite = CoopSpriteAt(frame.sprite);
            bool materialReady = true;
            if (frame.absorption == null) ReleaseCoopAbsorption(view);
            if (frame.absorption != null)
            {
                coopViewMaterialIds.Remove(frame.id);
                ReleaseCoopWater(view);
                ReleaseCoopMaskMaterial(frame.id, view);
                materialReady = ApplyCoopAbsorption(view, frame.absorption);
            }
            else if (frame.blackout != null)
            {
                coopViewMaterialIds.Remove(frame.id);
                ReleaseCoopWater(view);
                materialReady = ApplyCoopBlackoutMask(frame.id, view, frame.blackout);
            }
            else
            {
                ReleaseCoopMaskMaterial(frame.id, view);
                if (frame.water != null)
                {
                    coopViewMaterialIds.Remove(frame.id);
                    materialReady = ApplyCoopWater(view, frame.water);
                }
                else ApplyCoopRegularMaterial(frame.id, view, frame.material);
            }
            bool ownerBoatHidden = coopOwnerBoatSprites.ContainsKey(frame.id);
            if (ownerBoatHidden) coopOwnerBoatVisibility[frame.id] = frame.visible;
            view.enabled = frame.visible && !ownerBoatHidden && materialReady; view.color = frame.color;
            view.flipX = frame.flipX; view.flipY = frame.flipY; view.maskInteraction = (SpriteMaskInteraction)frame.mask;
            view.sortingLayerID = frame.sortingLayer; view.sortingOrder = frame.order;
            RecordR03View(frame, CoopCarrierClock(frame.id, CoopVisualClock), view);
        }

        void ApplyCoopTiles(CoopTiles message)
        {
            if (!CanApplyCoop(message.scene) || !coopMaps.TryGetValue(message.path, out var map)) return;
            if (message.reset) map.ClearAllTiles();
            map.transform.SetPositionAndRotation(message.position, message.rotation);
            var parent = map.transform.parent;
            Vector3 scale = parent == null ? Vector3.one : parent.lossyScale;
            map.transform.localScale = new Vector3(message.scale.x / scale.x, message.scale.y / scale.y, message.scale.z / scale.z);
            map.color = message.color;
            ApplyCoopTileCells(map, message.cells ?? Array.Empty<CoopTileFrame>());
            ApplyCoopCollisionTiles(message);
        }

        void ApplyCoopTileCells(Tilemap map, CoopTileFrame[] cells)
        {
            foreach (var cell in cells)
                map.SetTile(cell.cell, CoopSafeVisualTile(At(coopCatalog.tiles, cell.tile)));
            foreach (var conveyor in coopConveyorSources)
                if (conveyor != null) conveyor.RefreshNetworkTileVisuals(map);
            // Replacing a catalog tile with its animated variant must not erase
            // the color/transform approved in the same world snapshot.
            foreach (var cell in cells)
            {
                if (cell.tile < 0) continue;
                map.SetTileFlags(cell.cell, TileFlags.None);
                map.SetColor(cell.cell, cell.color);
                map.SetTransformMatrix(cell.cell, cell.matrix);
                if (cell.animated) SetCoopTileAnimationFrame(map, cell.cell, cell.animationFrame);
            }
        }

        static T At<T>(T[] values, int index) where T : UnityEngine.Object => index >= 0 && index < values.Length ? values[index] : null;

        public static void SendBattleFeedback(byte kind, GameObject target = null, Vector3Int cell = default, float value = 0, Vector2 direction = default, int steps = 0, Vector3Int[] path = null, float duration = 0)
        {
            if (!NetworkServer.active || !NetSync.IsOnline) return;
            uint id = target != null && target.TryGetComponent<NetworkIdentity>(out var identity) ? identity.netId : 0;
            NetworkServer.SendToReady(new BattleFeedback { scene = SceneManager.GetActiveScene().name, kind = kind, netId = id, cell = cell, value = value, direction = direction, steps = steps, path = path, duration = duration });
        }

        void ApplyBattleFeedback(BattleFeedback message)
        {
            if (NetworkServer.active || !NetSync.IsOnline || message.scene != SceneManager.GetActiveScene().name) return;
            if (message.kind < 2)
            {
                var suddenDeath = FindAnyObjectByType<BattleSuddenDeathController>();
                if (suddenDeath != null) suddenDeath.ApplyNetworkDrop(message.kind == 0, message.cell, message.value, message.path, message.duration);
                return;
            }
            if (!NetworkClient.spawned.TryGetValue(message.netId, out var identity) || !identity.TryGetComponent<ItemPickup>(out var item)) return;
            switch (message.kind)
            {
                case 2: item.DestroyWithAnimation(); break;
                case 3: item.DestroyWithExplosionAnimation(); break;
                case 4: item.DestroySilently(); break;
                case 5: item.DestroyFromMovingBombImpact(); break;
                // Item motion uses approved revisioned segments, never client terrain decisions.
            }
        }

        AudioSource remoteExplosionAudio;

        public static void CoopPlaySound(AudioClip clip, float volume = 1f, float pitch = 1f, bool voice = false, bool exclusiveExplosion = false)
        {
            if (!NetSync.IsOnline || !NetworkServer.active || singleton is not BombermanNetworkManager manager) return;
            if (!IsCoopStage && !SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal)) return;
            if (!IsCoopStage && GameManager.Instance != null && GameManager.Instance.OnlineRoundPresentationActive) return;
            int id = manager.CoopSoundId(clip);
            string resource = manager.CoopSoundResource(clip);
            if (id >= 0 || resource != null) NetworkServer.SendToReady(new CoopSound { scene = SceneManager.GetActiveScene().name, clip = id, resource = resource, volume = volume, pitch = pitch, voice = voice, exclusiveExplosion = exclusiveExplosion });
        }

        void ApplyCoopSound(CoopSound message)
        {
            if (NetworkServer.active || !NetSync.IsOnline || message.scene != SceneManager.GetActiveScene().name) return;
            var clip = (SoundCatalog == null ? null : At(SoundCatalog.sounds, message.clip)) ?? (string.IsNullOrEmpty(message.resource) ? null : Resources.Load<AudioClip>(message.resource));
            if (clip == null) return;
            if (message.exclusiveExplosion && remoteExplosionAudio != null)
            {
                remoteExplosionAudio.Stop();
                Destroy(remoteExplosionAudio.gameObject);
            }
            var go = new GameObject("CoopSound", typeof(AudioSource));
            var source = go.GetComponent<AudioSource>();
            if (message.exclusiveExplosion) remoteExplosionAudio = source;
            source.ignoreListenerPause = true;
            source.clip = clip; source.volume = message.voice ? GameAudioSettings.ApplyVoiceSfxVolume(message.volume) : GameAudioSettings.ApplySfxVolume(message.volume); source.pitch = message.pitch;
            if (!message.voice && message.volume > 1f)
            {
                // AudioSource.volume clamps to one; the native Venus/Pretty gain uses one-shot scale.
                source.volume = GameAudioSettings.ApplySfxVolume(1f);
                source.PlayOneShot(clip, message.volume);
            }
            else source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, Mathf.Abs(message.pitch)) + 0.5f);
        }
    }
}
