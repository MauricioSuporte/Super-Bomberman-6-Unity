using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct WorldBombMotion : NetworkMessage
    {
        public string scene;
        public NetworkBombFx.KickMotion motion;
    }

    public partial class BombermanNetworkManager
    {
        sealed class WorldBombState
        {
            public WorldBombMotion message;
            public NetworkConnectionToClient excludedConnection;
            public readonly Dictionary<int, (NetworkConnectionToClient peer, uint revision)> sent = new();
            public readonly List<int> expiredPeers = new();
        }
        readonly Dictionary<uint, WorldBombState> worldBombStates = new();
        readonly Dictionary<uint, WorldBombMotion> pendingWorldBombMotions = new();
        readonly List<uint> expiredWorldBombMotions = new();
        string worldBombScene;
        Scene worldBombLoadedScene;
        float worldBombNextTick;

        public static bool SendWorldBombMotion(Bomb target, NetworkBombFx.BombMotionKind kind, Vector2 start, Vector2 end,
            float duration, bool moving, bool first, float arcHeight = 0, float carryHeight = 0, float curveDuration = 0,
            NetworkConnectionToClient exclude = null)
        {
            if (!NetSync.IsServer || !NetworkServer.active || target == null ||
                singleton is not BombermanNetworkManager manager || manager == null || !target.TryGetComponent<NetworkIdentity>(out var identity) || identity.netId == 0) return false;
            var motion = new NetworkBombFx.KickMotion
            {
                bombId = identity.netId, revision = target.NextNetworkMotionRevision(), worldMotion = true,
                kind = kind, start = start, end = end, duration = duration, curveDuration = curveDuration,
                arcHeight = arcHeight, carryHeight = carryHeight, moving = moving, first = first,
                rubber = target.IsRubberBomb, bounceSeed = target.RubberBounceSeed, bounceIndex = target.RubberBounceIndex,
                bounceDuration = target.RubberBounceDuration, bounceHeight = target.RubberBounceHeight, serverTime = NetworkTime.localTime
            };
            manager.EnsureWorldBombScene();
            if (!manager.worldBombStates.TryGetValue(identity.netId, out var state))
            {
                if (manager.worldBombStates.Count >= 256) return false;
                manager.worldBombStates[identity.netId] = state = new WorldBombState();
            }
            state.message = new WorldBombMotion { scene = target.gameObject.scene.name, motion = motion };
            state.excludedConnection = exclude;
            manager.SendWorldBombState(state);
            return true;
        }

        public static bool ValidWorldBombMotion(NetworkBombFx.KickMotion motion) => motion.worldMotion && motion.bombId != 0 &&
            double.IsFinite(motion.serverTime) && float.IsFinite(motion.start.x) && float.IsFinite(motion.start.y) &&
            float.IsFinite(motion.end.x) && float.IsFinite(motion.end.y) && float.IsFinite(motion.duration) && motion.duration >= 0 && motion.duration <= 10 &&
            float.IsFinite(motion.arcHeight) && motion.arcHeight >= 0 && motion.arcHeight <= 128 &&
            float.IsFinite(motion.carryHeight) && motion.carryHeight >= 0 && motion.carryHeight <= 128 &&
            float.IsFinite(motion.curveDuration) && motion.curveDuration >= 0 && motion.curveDuration <= 10 &&
            float.IsFinite(motion.bounceDuration) && motion.bounceDuration >= 0 && motion.bounceDuration <= 10 &&
            float.IsFinite(motion.bounceHeight) && motion.bounceHeight >= 0 && motion.bounceHeight <= 128 &&
            System.Enum.IsDefined(typeof(NetworkBombFx.BombMotionKind), motion.kind);

        void ClearWorldBombMotions()
        {
            worldBombStates.Clear(); pendingWorldBombMotions.Clear(); expiredWorldBombMotions.Clear();
            worldBombScene = null; worldBombLoadedScene = default; worldBombNextTick = 0;
        }

        void EnsureWorldBombScene()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (worldBombLoadedScene == SceneManager.GetActiveScene() && worldBombScene == scene) return;
            worldBombLoadedScene = SceneManager.GetActiveScene(); worldBombScene = scene; worldBombStates.Clear(); pendingWorldBombMotions.Clear(); expiredWorldBombMotions.Clear();
        }
        void SendWorldBombState(WorldBombState state)
        {
            state.expiredPeers.Clear();
            foreach (var pair in state.sent)
                if (!NetworkServer.connections.TryGetValue(pair.Key, out var current) || current != pair.Value.peer)
                    state.expiredPeers.Add(pair.Key);
            foreach (int id in state.expiredPeers) state.sent.Remove(id);
            foreach (var peer in NetworkServer.connections.Values)
            {
                if (peer == null || peer == NetworkServer.localConnection || !peer.isReady || !peer.isAuthenticated ||
                    peer == state.excludedConnection ||
                    (state.sent.TryGetValue(peer.connectionId, out var sent) && sent.peer == peer && sent.revision == state.message.motion.revision)) continue;
                peer.Send(state.message); state.sent[peer.connectionId] = (peer, state.message.motion.revision);
            }
        }
        void InitializeWorldBombClient() => NetworkClient.RegisterHandler<WorldBombMotion>(ReceiveWorldBombMotion);

        void ReceiveWorldBombMotion(WorldBombMotion message)
        {
            if (NetworkServer.active || !NetworkClient.active || NetworkClient.isLoadingScene ||
                !NetSync.IsOnline || message.scene != SceneManager.GetActiveScene().name || !ValidWorldBombMotion(message.motion)) return;
            EnsureWorldBombScene();
            if (PresentWorldBombMotion(message)) return;
            if (pendingWorldBombMotions.Count >= 256 && !pendingWorldBombMotions.ContainsKey(message.motion.bombId)) return;
            if (!pendingWorldBombMotions.TryGetValue(message.motion.bombId, out var previous) ||
                NetworkInputHistory.IsNewer(message.motion.revision, previous.motion.revision))
                pendingWorldBombMotions[message.motion.bombId] = message;
        }
        static bool ValidWorldBombPresenter(NetworkBombFx presenter, Scene scene) => presenter != null &&
            presenter.gameObject.scene == scene && presenter.TryGetComponent<NetworkIdentity>(out var identity) && identity.netId != 0 &&
            NetworkClient.spawned.TryGetValue(identity.netId, out var current) && current == identity;

        bool PresentWorldBombMotion(WorldBombMotion message)
        {
            if (!NetworkClient.spawned.TryGetValue(message.motion.bombId, out var identity) || !identity.TryGetComponent<Bomb>(out var target) || identity.gameObject.scene != SceneManager.GetActiveScene()) return false;
            var presenter = target.ClientBombMotionPresenter;
            if (!ValidWorldBombPresenter(presenter, target.gameObject.scene)) presenter = null;
            if (presenter == null && NetworkClient.localPlayer != null)
            {
                var local = NetworkClient.localPlayer.GetComponent<NetworkBombFx>();
                if (ValidWorldBombPresenter(local, target.gameObject.scene)) presenter = local;
            }
            if (presenter == null)
                foreach (var actor in NetworkClient.spawned.Values)
                    if (actor != null && actor.TryGetComponent<NetworkBombFx>(out var candidate) && ValidWorldBombPresenter(candidate, target.gameObject.scene))
                    { presenter = candidate; break; }
            if (presenter == null) return false;
            presenter.ReceiveKickMotion(message.motion); return true;
        }
        void TickWorldBombMotions()
        {
            if (!NetSync.IsOnline) { worldBombStates.Clear(); pendingWorldBombMotions.Clear(); worldBombScene = null; return; }
            EnsureWorldBombScene();
            if (Time.unscaledTime < worldBombNextTick) return;
            worldBombNextTick = Time.unscaledTime + .1f;
            expiredWorldBombMotions.Clear();
            if (NetworkServer.active)
            {
                foreach (var pair in worldBombStates)
                    if (!NetworkServer.spawned.ContainsKey(pair.Key)) expiredWorldBombMotions.Add(pair.Key);
                    else SendWorldBombState(pair.Value);
                foreach (uint id in expiredWorldBombMotions) worldBombStates.Remove(id);
            }
            else
            {
                foreach (var pair in pendingWorldBombMotions)
                    if (NetworkBombFx.KickServerNow - pair.Value.motion.serverTime > 2 || PresentWorldBombMotion(pair.Value)) expiredWorldBombMotions.Add(pair.Key);
                foreach (uint id in expiredWorldBombMotions) pendingWorldBombMotions.Remove(id);
            }
        }
    }
}
