using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct RevengeCartSprite { public ushort index; public int sortingLayer, sortingOrder; public NetworkWorldMount.MountFrame frame; }
    public struct RevengeCartState
    {
        public int id, owner, rechargeSprite;
        public uint generation, playerNetId, acknowledged, launchSerial;
        public Vector3 position, recoilOffset, landingPosition;
        public float perimeter, spawnPerimeter;
        public bool animating, suppressSpawnCorner;
        public float[] geometry;
        public RevengeCartSprite[] sprites;
    }
    public struct RevengeCartRoster : NetworkMessage
    {
        public string scene;
        public uint revision;
        public double time;
        public RevengeCartState[] carts;
    }
    public struct RevengeCartInput : NetworkMessage
    {
        public string scene;
        public int cart;
        public uint generation, tick;
        public int held;
        public bool heartbeat;
    }

    // Input survives the inactive player, but receives no simulation time from packets.
    public sealed class RevengeCartInputBuffer
    {
        public const int ValidMask = 1 | 2 | 4 | 8 | 16 | 64;
        readonly Queue<NetworkInputFrame> frames = new();
        readonly NetworkInputStepBudget budget = new();
        uint received;
        bool hasReceived;
        public uint Acknowledged { get; private set; }
        public int Held { get; private set; }
        public double HeardAt { get; private set; } = double.NegativeInfinity;
        public int Count => frames.Count;
        public bool Overflowed { get; private set; }
        public bool Push(uint tick, int held, double now)
        {
            Overflowed = false;
            if (!double.IsFinite(now) || (hasReceived && !NetworkInputHistory.IsNewer(tick, received))) return false;
            received = tick; hasReceived = true; HeardAt = now;
            if (frames.Count == 8) { Cancel(); Overflowed = true; return false; }
            frames.Enqueue(new NetworkInputFrame { tick = tick, held = held & ValidMask });
            return true;
        }
        public void Heartbeat(double now) { if (double.IsFinite(now)) HeardAt = now; }
        public void Advance() => budget.Advance();
        public bool Consume(out int held)
        {
            held = 0;
            if (frames.Count == 0 || !budget.TryConsume()) return false;
            var frame = frames.Dequeue(); Acknowledged = frame.tick; Held = held = frame.held;
            return true;
        }
        public void Pause() { frames.Clear(); Acknowledged = received; budget.Reset(); } // Keep A held; pause must not fabricate release.
        public void Cancel() { frames.Clear(); Held = 0; Acknowledged = received; budget.Reset(); }
    }

    public sealed class RevengeCartPredictionHistory
    {
        readonly List<(uint tick, int held, float dt)> frames = new();
        public int Count => frames.Count;
        public (uint tick, int held, float dt) this[int i] => frames[i];
        public void Add(uint tick, int held, float dt)
        {
            if (frames.Count == NetworkInputHistory.Capacity) frames.RemoveAt(0);
            frames.Add((tick, held, dt));
        }
        public void DiscardThrough(uint tick)
        {
            int count = 0;
            while (count < frames.Count && !NetworkInputHistory.IsNewer(frames[count].tick, tick)) count++;
            if (count > 0) frames.RemoveRange(0, count);
        }
    }

    public partial class BombermanNetworkManager
    {
        sealed class RevengeServerInput
        {
            public uint generation;
            public readonly RevengeCartInputBuffer buffer = new();
        }
        sealed class RevengeClientView
        {
            public BattleRevengeController cart;
            public RevengeCartState current;
            public readonly List<(double time, RevengeCartState state)> history = new();
            public readonly RevengeCartPredictionHistory inputs = new();
            public uint tick;
            public float predicted;
            public double receivedAt;
            public bool presented, presentedAnimating;
            public double nextDiagnostic;
            public Vector3 diagnosticPosition;
        }
        readonly List<BattleRevengeController> revengeCarts = new();
        readonly Dictionary<int, RevengeServerInput> revengeServerInputs = new();
        readonly Dictionary<int, RevengeClientView> revengeViews = new();
        readonly List<int> revengeRemoved = new();
        static readonly PlayerAction[] RevengeActions = { PlayerAction.MoveUp, PlayerAction.MoveDown,
            PlayerAction.MoveLeft, PlayerAction.MoveRight, PlayerAction.ActionA, PlayerAction.ActionC };
        string revengeScene;
        Scene revengeLoadedScene;
        uint revengeRevision, revengeReceivedRevision;
        bool revengeReceived;
        float revengeNextSnapshot, revengeNextHeartbeat, revengeNextServerDiagnostic;
        int revengeHeld, revengePressed;
        static bool InRevengeArena => NetSync.IsOnline && SceneManager.GetActiveScene().name.StartsWith("BattleMode_", StringComparison.Ordinal);

        public static void RevengeCartChanged()
        {
            if (NetworkServer.active && singleton is BombermanNetworkManager manager) manager.revengeNextSnapshot = 0;
        }

        void InitializeRevengeServer() => NetworkServer.RegisterHandler<RevengeCartInput>(ReceiveRevengeInput);
        void InitializeRevengeClient() => NetworkClient.RegisterHandler<RevengeCartRoster>(ReceiveRevengeRoster);

        NetworkIdentity FindRevengePlayer(int id, bool server)
        {
            foreach (var identity in server ? NetworkServer.spawned.Values : NetworkClient.spawned.Values)
                if (identity != null && identity.TryGetComponent<NetworkPlayerSetup>(out var setup) && setup.PlayerId == id)
                    return identity;
            return null;
        }

        void RefreshRevengeCarts()
        {
            revengeCarts.Clear();
            if (BattleRevengeSystem.Instance != null) BattleRevengeSystem.Instance.CopyNetworkCarts(revengeCarts);
        }

        public static bool MatchesRevengeCartInput(RevengeCartInput input, RevengeCartState cart, int assignedOwner,
            string scene, bool authenticated, bool ownsIdentity) => authenticated && ownsIdentity &&
            assignedOwner == cart.owner && input.scene == scene && input.cart == cart.id && input.generation == cart.generation;

        void ReceiveRevengeInput(NetworkConnectionToClient connection, RevengeCartInput message)
        {
            if (!InRevengeArena || !connection.isAuthenticated || connection == NetworkServer.localConnection ||
                message.scene != SceneManager.GetActiveScene().name ||
                !playerIdByConnection.TryGetValue(connection.connectionId, out int owner)) return;
            RefreshRevengeCarts();
            foreach (var cart in revengeCarts)
            {
                if (cart.NetworkCartId != message.cart) continue;
                var player = FindRevengePlayer(cart.OwnerPlayerId, true);
                if (player == null || !MatchesRevengeCartInput(message,
                    new RevengeCartState { id = cart.NetworkCartId, owner = cart.OwnerPlayerId, generation = cart.NetworkGeneration },
                    owner, SceneManager.GetActiveScene().name, connection.isAuthenticated, player == connection.identity)) return;
                var state = GetRevengeServerInput(cart);
                if (message.heartbeat) state.buffer.Heartbeat(Time.realtimeSinceStartupAsDouble);
                else if (!state.buffer.Push(message.tick, message.held, Time.realtimeSinceStartupAsDouble))
                { if (state.buffer.Overflowed) cart.CancelNetworkCharge(); }
                return;
            }
        }

        RevengeServerInput GetRevengeServerInput(BattleRevengeController cart)
        {
            if (!revengeServerInputs.TryGetValue(cart.NetworkCartId, out var state) || state.generation != cart.NetworkGeneration)
                revengeServerInputs[cart.NetworkCartId] = state = new RevengeServerInput { generation = cart.NetworkGeneration };
            return state;
        }

        void TickRevengeUpdate()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (SceneManager.GetActiveScene() != revengeLoadedScene || !InRevengeArena)
            {
                ClearRevengeCarts(false); revengeScene = scene; revengeLoadedScene = SceneManager.GetActiveScene();
                if (!InRevengeArena) return;
            }
            if (NetworkServer.active)
            {
                RefreshRevengeCarts();
                foreach (var cart in revengeCarts)
                {
                    var player = FindRevengePlayer(cart.OwnerPlayerId, true);
                    bool remote = player != null && player.connectionToClient != null && player.connectionToClient != NetworkServer.localConnection;
                    cart.NetworkTickControlled = remote;
                    if (remote && GamePauseController.IsPaused) GetRevengeServerInput(cart).buffer.Pause();
                    if (remote && Time.realtimeSinceStartupAsDouble - GetRevengeServerInput(cart).buffer.HeardAt > .15)
                    { cart.CancelNetworkCharge(); GetRevengeServerInput(cart).buffer.Cancel(); }
                }
                return;
            }
            if (!NetworkClient.ready) return;
            bool ownsCart = false;
            foreach (var view in revengeViews.Values) if (view.current.owner == LocalRoomPlayerId) { ownsCart = true; break; }
            if (!ownsCart) { revengeHeld = revengePressed = 0; return; }
            var input = PlayerInputManager.Instance;
            revengeHeld = 0;
            foreach (var action in RevengeActions)
                if (input != null && input.Get(1, action)) revengeHeld |= 1 << (int)action;
            if (input != null && input.GetDown(1, PlayerAction.ActionA)) revengePressed |= 16;
            if (Time.unscaledTime >= revengeNextHeartbeat)
            {
                revengeNextHeartbeat = Time.unscaledTime + .05f;
                foreach (var view in revengeViews.Values)
                    if (view.current.owner == LocalRoomPlayerId)
                        NetworkClient.Send(new RevengeCartInput { scene = scene, cart = view.current.id,
                            generation = view.current.generation, heartbeat = true });
            }
        }

        void FixedUpdate()
        {
            if (!InRevengeArena) return;
            float dt = Time.fixedUnscaledDeltaTime;
            if (NetworkServer.active)
            {
                RefreshRevengeCarts();
                foreach (var cart in revengeCarts)
                {
                    if (!cart.NetworkTickControlled) continue;
                    var buffer = GetRevengeServerInput(cart).buffer;
                    buffer.Advance();
                    if (Time.realtimeSinceStartupAsDouble - buffer.HeardAt > .15)
                    { buffer.Cancel(); cart.CancelNetworkCharge(); cart.SimulateNetworkCartInput(0, dt); continue; }
                    int steps = 0;
                    while (steps < 4 && buffer.Consume(out int held)) { cart.SimulateNetworkCartInput(held, dt); steps++; }
                    if (steps == 0) cart.SimulateNetworkCartInput(buffer.Held, 0);
                }
                return;
            }
            if (!NetworkClient.ready) return;
            foreach (var view in revengeViews.Values)
            {
                if (view.current.owner != LocalRoomPlayerId) continue;
                int held = revengeHeld | revengePressed;
                var frame = new NetworkInputFrame { tick = ++view.tick, held = held };
                NetworkClient.Send(new RevengeCartInput { scene = revengeScene, cart = view.current.id,
                    generation = view.current.generation, tick = frame.tick, held = held });
                view.inputs.Add(frame.tick, frame.held, dt);
                if (CanPredictRevenge(view)) view.predicted = view.cart.PredictNetworkPerimeter(view.predicted, held, dt);
            }
            revengePressed = 0;
        }

        void TickRevengePresentation()
        {
            if (!InRevengeArena) return;
            if (NetworkServer.active)
            {
                if (Time.unscaledTime < revengeNextSnapshot) return;
                revengeNextSnapshot = Time.unscaledTime + .05f;
                RefreshRevengeCarts();
                var states = new List<RevengeCartState>();
                bool diagnostic = GameplayLatencyDiagnostics.Enabled && Time.unscaledTime >= revengeNextServerDiagnostic;
                foreach (var cart in revengeCarts)
                {
                    var player = FindRevengePlayer(cart.OwnerPlayerId, true); if (player == null) continue;
                    var input = GetRevengeServerInput(cart).buffer;
                    states.Add(cart.CaptureNetworkCart(player.netId, input.Acknowledged));
                    if (diagnostic)
                    {
                        var entry = GameplayLatencyDiagnostics.For("revenge_cart_authority", player.GetComponent<MovementController>(),
                            detail: "cart=" + cart.NetworkCartId + ";generation=" + cart.NetworkGeneration + ";perimeter=" + cart.NetworkPerimeter);
                        entry.position = cart.transform.position; entry.authoritativeTick = input.Acknowledged;
                        entry.held = input.Held; entry.queuedInputs = input.Count; GameplayLatencyDiagnostics.Write(entry);
                    }
                }
                if (diagnostic) revengeNextServerDiagnostic = Time.unscaledTime + .1f;
                revengeRemoved.Clear();
                foreach (int id in revengeServerInputs.Keys)
                {
                    bool exists = false;
                    foreach (var cart in revengeCarts) if (cart.NetworkCartId == id) { exists = true; break; }
                    if (!exists) revengeRemoved.Add(id);
                }
                foreach (int id in revengeRemoved) revengeServerInputs.Remove(id);
                if (states.Count == 0) revengeNextSnapshot = Time.unscaledTime + .5f;
                var message = new RevengeCartRoster { scene = revengeScene, revision = ++revengeRevision,
                    time = NetworkTime.localTime, carts = states.ToArray() };
                // Host keeps its native carts and intentionally has no remote-view handler.
                foreach (var peer in NetworkServer.connections.Values)
                    if (peer != null && peer != NetworkServer.localConnection && peer.isReady && peer.isAuthenticated) peer.Send(message);
                return;
            }
            foreach (var view in revengeViews.Values)
            {
                if (view.cart == null || view.history.Count == 0) continue;
                var a = view.history[0]; var b = a;
                double time = NetworkTime.time;
                for (int i = 1; i < view.history.Count; i++)
                { b = view.history[i]; if (b.time >= time) break; a = b; }
                float t = b.time > a.time ? Mathf.Clamp01((float)((time - a.time) / (b.time - a.time))) : 0;
                if (b.time - a.time > .25) { a = b; t = 0; }
                if (GamePauseController.IsPaused && view.presented && !view.current.animating && !view.presentedAnimating)
                {
                    // Native recoil continues unscaled; walking/frame/HUD pose stays frozen.
                    view.cart.PresentPausedNetworkRecoil(Vector3.Lerp(a.state.recoilOffset, b.state.recoilOffset, t));
                    view.cart.PresentNetworkCartHud(); continue;
                }
                view.cart.PresentNetworkCart(a.state, b.state, t);
                view.presented = true; view.presentedAnimating = a.state.animating || b.state.animating;
                if (view.current.owner == LocalRoomPlayerId && !view.current.animating)
                {
                    var landing = view.cart.CaptureApprovedLandingPosition();
                    view.cart.RestoreNetworkMotor(view.current); view.cart.SetNetworkPerimeter(view.predicted);
                    if (CanPredictRevenge(view))
                        view.cart.StepNetworkMotor(revengeHeld, Mathf.Clamp(Time.unscaledTime - Time.fixedUnscaledTime, 0, Time.fixedUnscaledDeltaTime));
                    view.cart.RestoreApprovedLandingPosition(landing);
                }
                view.cart.PresentNetworkCartHud();
                if (GameplayLatencyDiagnostics.Enabled && Time.realtimeSinceStartupAsDouble >= view.nextDiagnostic)
                {
                    var player = FindRevengePlayer(view.current.owner, false);
                    var entry = GameplayLatencyDiagnostics.For("revenge_cart_present", player != null ? player.GetComponent<MovementController>() : null,
                        detail: "cart=" + view.current.id + ";generation=" + view.current.generation + ";owner=" + (view.current.owner == LocalRoomPlayerId) + ";after_presenter");
                    entry.position = view.cart.transform.position;
                    entry.delta = view.nextDiagnostic > 0 ? entry.position - (Vector2)view.diagnosticPosition : Vector2.zero;
                    entry.durationMs = view.nextDiagnostic > 0 ? (Time.realtimeSinceStartupAsDouble - (view.nextDiagnostic - .1)) * 1000 : -1;
                    entry.inputTick = view.tick; entry.authoritativeTick = view.current.acknowledged; entry.pendingInputs = view.inputs.Count;
                    entry.messageAgeMs = Math.Max(0, NetworkBombFx.KickServerNow - view.history[view.history.Count - 1].time) * 1000;
                    GameplayLatencyDiagnostics.Write(entry); view.diagnosticPosition = view.cart.transform.position;
                    view.nextDiagnostic = Time.realtimeSinceStartupAsDouble + .1;
                }
            }
        }

        static bool CanPredictRevenge(RevengeClientView view) => !view.current.animating && !GamePauseController.IsPaused &&
            Time.realtimeSinceStartupAsDouble - view.receivedAt <= .25;

        void ReceiveRevengeRoster(RevengeCartRoster message)
        {
            if (NetworkServer.active || !InRevengeArena || NetworkClient.isLoadingScene ||
                message.scene != SceneManager.GetActiveScene().name || !double.IsFinite(message.time) ||
                (revengeReceived && !NetworkInputHistory.IsNewer(message.revision, revengeReceivedRevision)) ||
                message.carts == null || message.carts.Length > GameSession.MaxPlayerId) return;
            if (revengeLoadedScene != SceneManager.GetActiveScene())
            { ClearRevengeCarts(false); revengeScene = message.scene; revengeLoadedScene = SceneManager.GetActiveScene(); }
            revengeReceived = true; revengeReceivedRevision = message.revision;
            revengeRemoved.Clear(); revengeRemoved.AddRange(revengeViews.Keys);
            foreach (var state in message.carts)
            {
                if (!GameSession.IsValidPlayerId(state.owner) || state.geometry == null || state.geometry.Length != 10 ||
                    state.sprites == null || state.sprites.Length > 128 || !float.IsFinite(state.perimeter) ||
                    Array.Exists(state.geometry, value => !float.IsFinite(value))) continue;
                if (!NetworkClient.spawned.TryGetValue(state.playerNetId, out var player) ||
                    !player.TryGetComponent<MovementController>(out var movement) ||
                    player.gameObject.scene != SceneManager.GetActiveScene() ||
                    player.GetComponent<NetworkPlayerSetup>()?.PlayerId != state.owner || BattleRevengeSystem.Instance == null) continue;
                bool replaced = !revengeViews.TryGetValue(state.id, out var view) || view.current.generation != state.generation || view.current.owner != state.owner;
                if (replaced)
                {
                    if (view?.cart != null) Destroy(view.cart.gameObject);
                    player.GetComponent<NetworkPlayerState>()?.RefreshConfirmedSkin();
                    var cart = BattleRevengeSystem.Instance.CreateNetworkCartView(movement); if (cart == null) continue;
                    revengeViews[state.id] = view = new RevengeClientView { cart = cart };
                }
                else if (NetworkInputHistory.IsNewer(state.launchSerial, view.current.launchSerial)) view.cart.PlayConfirmedNetworkLaunchSound();
                revengeRemoved.Remove(state.id);
                bool keepPaused = !replaced && GamePauseController.IsPaused && !state.animating && !view.current.animating;
                view.current = state; view.receivedAt = Time.realtimeSinceStartupAsDouble;
                if (replaced || NetworkInputHistory.IsNewer(state.acknowledged, view.tick)) view.tick = state.acknowledged;
                if (!view.cart.RestoreNetworkMotor(state, false)) continue;
                view.inputs.DiscardThrough(state.acknowledged);
                if (!keepPaused)
                {
                    view.predicted = state.perimeter;
                    if (state.owner == LocalRoomPlayerId && !state.animating && !GamePauseController.IsPaused)
                        for (int i = 0; i < view.inputs.Count; i++)
                            view.predicted = view.cart.PredictNetworkPerimeter(view.predicted, view.inputs[i].held, view.inputs[i].dt);
                }
                view.history.Add((message.time, state)); if (view.history.Count > 32) view.history.RemoveAt(0);
            }
            foreach (int id in revengeRemoved) { if (revengeViews[id].cart != null) Destroy(revengeViews[id].cart.gameObject); revengeViews.Remove(id); }
        }

        void ClearRevengeCarts(bool resetRevision)
        {
            foreach (var view in revengeViews.Values) if (view.cart != null) Destroy(view.cart.gameObject);
            foreach (var cart in revengeCarts) if (cart != null) cart.NetworkTickControlled = false;
            revengeViews.Clear(); revengeServerInputs.Clear(); revengeCarts.Clear(); revengeHeld = revengePressed = 0;
            if (resetRevision) revengeReceived = false;
        }
    }
}
