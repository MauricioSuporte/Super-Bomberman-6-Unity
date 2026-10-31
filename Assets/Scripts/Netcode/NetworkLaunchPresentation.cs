using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class NetworkPlayerInput
    {
        public struct LaunchState
        {
            public uint sequence, revision;
            public PlayerLaunchArc arc;
            public double time;
            public float elapsed;
            public bool active, paused;
        }
        [SyncVar(hook = nameof(OnLaunchState))] LaunchState serverLaunch;
        PlayerLaunchArc nativeLaunchArc;
        float nativeLaunchElapsed, nextLaunchSample;
        uint launchSequence, launchRevision, receivedLaunchRevision, cancelledLaunchSequence;
        readonly List<LaunchState> launchHistory = new(64);
        readonly Queue<NetworkBombFx.PlayerBounceMotion> launchLandingBounces = new();
        bool launchPresentationActive, hasLaunchMovement, launchPositionValid, launchOwnerTimeline, launchUsesBufferedTimeline;
        uint presentedLaunchSequence, ropePlaybackSequence;
        double ropePlaybackSourceTime, ropePlaybackLastAt, ropePlaybackFreshAt;
        float ropePlaybackPhase;
        int ropePlaybackFrame = -1;
        bool ropePlaybackStarted, ropeEndpointPresented, ropePlaybackFinished;
        MovementController.NetworkMovementState launchMovement;
        Vector3 launchPosition;
        public bool ApprovedLaunchActive => launchPresentationActive;
        public LaunchState ApprovedLaunchSnapshot => serverLaunch;
        SpriteRenderer launchShadow;
        Sprite launchShadowSprite;
        float nextLaunchDiagnostic, presentedLaunchElapsed;
        double LaunchTimeline => launchUsesBufferedTimeline && launchPresentationActive ? ropePlaybackSourceTime : isLocalPlayer ? NetworkTime.predictedTime : NetworkTime.time;
        public double AuthorityPresentationTime => launchUsesBufferedTimeline && launchPresentationActive ? ropePlaybackSourceTime : isLocalPlayer && launchOwnerTimeline ? NetworkTime.predictedTime : NetworkTime.time;

        public readonly struct LaunchScope : System.IDisposable
        {
            readonly NetworkPlayerInput input;
            readonly uint sequence;
            internal LaunchScope(NetworkPlayerInput input, uint sequence) { this.input = input; this.sequence = sequence; }
            public void Progress(PlayerLaunchArc arc, float elapsed, bool paused)
            {
                if (input != null && input.serverLaunch.active && input.serverLaunch.sequence == sequence)
                    input.ReportLaunch(arc, elapsed, paused);
            }
            public void Dispose()
            {
                if (input == null || !input.serverLaunch.active || input.serverLaunch.sequence != sequence) return;
                var state = input.serverLaunch;
                state.active = false; state.paused = false; state.time = NetworkTime.localTime;
                state.elapsed = input.nativeLaunchElapsed; state.revision = ++input.launchRevision;
                input.serverLaunch = state;
                input.PublishRopeLaunchEvent(state);
                input.TraceLaunch("launch_recipe_publish", state);
            }
        }
        public static LaunchScope PresentApprovedLaunch(MovementController actor, PlayerLaunchArc arc)
        {
            if (!NetSync.IsServer || actor == null || !actor.TryGetComponent<NetworkPlayerInput>(out var input) ||
                !input.isServer || input.netId == 0) return default;
            uint sequence = ++input.launchSequence;
            if (sequence == 0) sequence = ++input.launchSequence;
            input.nativeLaunchArc = arc; input.nativeLaunchElapsed = 0;
            input.serverLaunch = new LaunchState { sequence = sequence, revision = ++input.launchRevision, arc = arc,
                time = NetworkTime.localTime, active = true, paused = GamePauseController.IsPaused };
            input.PublishRopeLaunchEvent(input.serverLaunch);
            input.TraceLaunch("launch_recipe_publish", input.serverLaunch);
            input.nextLaunchSample = Time.unscaledTime + .05f;
            return new LaunchScope(input, sequence);
        }
        void PublishRopeLaunchEvent(LaunchState state)
        {
            // A blocked rope can begin and stop between two SyncVar deliveries.
            // Preserve those edges on the same reliable actor transport.
            if (state.arc.kind == PlayerLaunchArc.LaunchKind.Rope && NetworkServer.active && isServer && netId != 0)
                RpcRopeLaunchState(state);
        }
        [ClientRpc]
        void RpcRopeLaunchState(LaunchState state)
        {
            if (!isServer) ReceiveLaunchState(state);
        }
        void ReportLaunch(PlayerLaunchArc arc, float elapsed, bool paused)
        {
            nativeLaunchArc = arc; nativeLaunchElapsed = elapsed;
            if (serverLaunch.paused == paused && (paused || Time.unscaledTime < nextLaunchSample)) return;
            nextLaunchSample = Time.unscaledTime + .05f;
            var state = serverLaunch; state.arc = arc; state.elapsed = elapsed; state.paused = paused;
            state.time = NetworkTime.localTime; state.revision = ++launchRevision; serverLaunch = state;
            TraceLaunch("launch_recipe_publish", state);
        }
        public bool CaptureLaunchLift(SpriteRenderer sprite, out uint sequence, out float lift, out sbyte mode)
        {
            sequence = 0; lift = 0; mode = 0;
            if (!isServer || !serverLaunch.active || sprite == null || !sprite.enabled || sprite.transform == transform) return false;
            var shadow = sprite.GetComponentInParent<PinkLouieShadowController>(true);
            if (nativeLaunchArc.rootCarriesHeight)
            {
                if (shadow == null || !shadow.HasGroundedJump) return false;
                mode = -1; lift = -nativeLaunchArc.Height(nativeLaunchElapsed);
            }
            else
            {
                if (shadow != null) return false;
                mode = 1; lift = nativeLaunchArc.VisualHeight(nativeLaunchElapsed);
            }
            sequence = serverLaunch.sequence;
            return true;
        }
        void OnLaunchState(LaunchState previous, LaunchState current) => ReceiveLaunchState(current);
        public void ReceiveLaunchState(LaunchState state)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer ||
                !NetworkInputHistory.IsNewer(state.revision, receivedLaunchRevision) || !ValidLaunch(state)) return;
            receivedLaunchRevision = state.revision;
            if (!gameObject.activeInHierarchy || movement != null && (movement.isDead || movement.IsEndingStage))
            { cancelledLaunchSequence = state.sequence; return; }
            authorityPositionSync = authorityPositionSync != null ? authorityPositionSync : GetComponent<NetworkTransformReliable>();
            double adjustment = authorityPositionSync != null ? NetworkClient.sendInterval * (authorityPositionSync.sendIntervalMultiplier - 1 +
                (authorityPositionSync.timelineOffset ? authorityPositionSync.sendIntervalMultiplier : 0)) : 0;
            state.time += adjustment;
            launchUsesBufferedTimeline = state.arc.kind == PlayerLaunchArc.LaunchKind.Rope;
            if (launchUsesBufferedTimeline) ropePlaybackFreshAt = Time.realtimeSinceStartupAsDouble;
            else if (ropePlaybackStarted)
            {
                if (NetworkInputHistory.IsNewer(ropePlaybackSequence, cancelledLaunchSequence)) cancelledLaunchSequence = ropePlaybackSequence;
                ropePlaybackStarted = ropeEndpointPresented = ropePlaybackFinished = false;
            }
            launchHistory.Add(state);
            if (launchHistory.Count > 64) launchHistory.RemoveAt(0);
            if (launchUsesBufferedTimeline && state.active && NetworkInputHistory.IsNewer(state.sequence, cancelledLaunchSequence))
            {
                // Claim presentation before a later motor acknowledgement can
                // release control. Playback begins in the actual render frame.
                if (!launchPresentationActive)
                {
                    ropePlaybackSequence = state.sequence; ropePlaybackSourceTime = state.time;
                    ropePlaybackPhase = state.elapsed; ropePlaybackStarted = false;
                }
                launchPresentationActive = true; launchOwnerTimeline = false;
                if (isLocalPlayer) { movement?.SetPredictLocally(false); SetOwnerAuthorityPresentation(true); }
            }
            else if (!launchUsesBufferedTimeline && TrySampleApprovedLaunch(LaunchTimeline, out _, out _) && GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true)
            { launchPresentationActive = true; launchOwnerTimeline = isLocalPlayer; }
            TraceLaunch("launch_recipe_receive", state);
        }
        public static bool ValidLaunch(LaunchState state) => (uint)state.arc.kind <= (uint)PlayerLaunchArc.LaunchKind.Rope && state.sequence != 0 && double.IsFinite(state.time) &&
            float.IsFinite(state.elapsed) && state.elapsed >= 0 && float.IsFinite(state.arc.duration) && state.arc.duration >= .01f &&
            state.arc.duration <= 60 && Finite(state.arc.start) && Finite(state.arc.end) && float.IsFinite(state.arc.height) &&
            Mathf.Abs(state.arc.height) <= 128 && float.IsFinite(state.arc.ascend) && float.IsFinite(state.arc.descend) &&
            float.IsFinite(state.arc.tileSize) && (!state.arc.groundShadow || (Finite(state.arc.shadowScale) &&
            float.IsFinite(state.arc.shadowColor.r) && float.IsFinite(state.arc.shadowColor.g) && float.IsFinite(state.arc.shadowColor.b) && float.IsFinite(state.arc.shadowColor.a))) && (!state.arc.bounded || (float.IsFinite(state.arc.room.x) && float.IsFinite(state.arc.room.y) &&
            float.IsFinite(state.arc.room.z) && float.IsFinite(state.arc.room.w) && state.arc.room.z > state.arc.room.x && state.arc.room.w > state.arc.room.y));
        static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);

        void TickRopePlayback()
        {
            if (!launchUsesBufferedTimeline || ropePlaybackFrame == Time.frameCount) return;
            ropePlaybackFrame = Time.frameCount;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!ropePlaybackStarted)
            {
                foreach (var sample in launchHistory)
                    if (sample.active && sample.arc.kind == PlayerLaunchArc.LaunchKind.Rope && NetworkInputHistory.IsNewer(sample.sequence, cancelledLaunchSequence))
                    {
                        ropePlaybackSequence = sample.sequence; ropePlaybackPhase = sample.elapsed;
                        ropePlaybackSourceTime = sample.time; ropePlaybackLastAt = now;
                        ropePlaybackStarted = true; ropeEndpointPresented = ropePlaybackFinished = false;
                        return;
                    }
                return;
            }
            float delta = Mathf.Clamp((float)(now - ropePlaybackLastAt), 0, .05f);
            ropePlaybackLastAt = now;
            if (!TryRopePlaybackState(out var latest)) return;
            if (GamePauseController.IsPaused) ropePlaybackFreshAt = now;
            if (!GamePauseController.IsPaused && latest.active && !latest.paused && now - ropePlaybackFreshAt > Mathf.Max(1, latest.arc.duration + .5f))
            { ropePlaybackFinished = true; TraceLaunch("launch_recipe_timeout", latest); return; }
            if (ropeEndpointPresented && !latest.active && !GamePauseController.IsPaused)
            { ropePlaybackFinished = true; return; }
            if (!GamePauseController.IsPaused && !latest.paused)
                ropePlaybackPhase = Mathf.Min(ropePlaybackPhase + delta, latest.elapsed);
            // Map the local approved phase back to the matching native pose
            // clock, skipping an already-observed pause rather than replaying it.
            float selectedPhase = -1;
            foreach (var sample in launchHistory)
                if (sample.sequence == ropePlaybackSequence && sample.elapsed <= ropePlaybackPhase + .00001f && sample.elapsed >= selectedPhase)
                { selectedPhase = sample.elapsed; ropePlaybackSourceTime = sample.time + ropePlaybackPhase - sample.elapsed; }
        }
        bool TryRopePlaybackState(out LaunchState state)
        {
            state = default;
            for (int i = launchHistory.Count - 1; i >= 0; i--)
                if (launchHistory[i].sequence == ropePlaybackSequence)
                { state = launchHistory[i]; return true; }
            return false;
        }
        bool HasPendingRopePlayback()
        {
            foreach (var sample in launchHistory)
                if (sample.active && sample.arc.kind == PlayerLaunchArc.LaunchKind.Rope && NetworkInputHistory.IsNewer(sample.sequence, cancelledLaunchSequence)) return true;
            return false;
        }
        public bool TrySampleApprovedLaunch(double timeline, out LaunchState state, out float elapsed)
        {
            state = default; elapsed = 0;
            if (ropePlaybackStarted && launchUsesBufferedTimeline)
            {
                if (!TryRopePlaybackState(out state) || ropePlaybackFinished) return false;
                elapsed = ropePlaybackPhase;
                // A stop confirms the prefix; finish presenting it before handoff.
                state.active = true;
                return true;
            }
            int index = -1;
            for (int i = 0; i < launchHistory.Count; i++)
                if (launchHistory[i].time <= timeline) index = i;
            if (index < 0) return false;
            state = launchHistory[index];
            if (!state.active || !NetworkInputHistory.IsNewer(state.sequence, cancelledLaunchSequence) ||
                !state.paused && timeline - state.time > Mathf.Max(1, state.arc.duration + .5f)) return false;
            elapsed = Mathf.Clamp(state.elapsed + (state.paused ? 0 : Mathf.Max(0, (float)(timeline - state.time))), 0, state.arc.duration);
            // Rope checks moving blockers at every native physics step. A complete
            // descriptor cannot authorize travelling beyond its confirmed prefix.
            if (state.arc.kind == PlayerLaunchArc.LaunchKind.Rope)
            {
                float approved = state.elapsed;
                foreach (var sample in launchHistory)
                    if (sample.sequence == state.sequence) approved = Mathf.Max(approved, sample.elapsed);
                elapsed = Mathf.Min(elapsed, approved);
            }
            return true;
        }
        public bool TryApprovedLaunchLift(uint sequence, sbyte mode, out float lift)
        {
            lift = 0;
            if (GamePauseController.IsPaused && launchPresentationActive)
            {
                // The pose caller already retains its last frame during pause.
                return false;
            }
            if (!TrySampleApprovedLaunch(LaunchTimeline, out var state, out float elapsed) || state.sequence != sequence) return false;
            elapsed = PresentedLaunchElapsed(state, elapsed);
            lift = mode < 0 ? -state.arc.Height(elapsed) : state.arc.VisualHeight(elapsed); return true;
        }
        float PresentedLaunchElapsed(LaunchState state, float elapsed) => launchPositionValid && presentedLaunchSequence == state.sequence
            ? Mathf.Max(presentedLaunchElapsed, elapsed) : elapsed;
        void ReconcileApprovedLaunch(MovementController.NetworkMovementState state)
        {
            if (!launchPresentationActive) return;
            launchMovement = state; hasLaunchMovement = true;
            if (!launchUsesBufferedTimeline && state.canPredict && TrySampleApprovedLaunch(LaunchTimeline, out var launch, out _) &&
                state.serverTime >= launch.time + Mathf.Max(0, launch.arc.duration - launch.elapsed) - .02)
                CancelApprovedLaunch(false);
        }
        public void RestoreApprovedLaunchPosition()
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || isServer || movement == null) return;
            if (movement.isDead || movement.IsEndingStage) { CancelApprovedLaunch(false); return; }
            if (GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive == true)
            { if (launchPositionValid) CancelApprovedLaunch(false); else launchPresentationActive = false; return; }
            TickRopePlayback();
            if (GamePauseController.IsPaused && launchPresentationActive && launchPositionValid) { ApplyLaunchPosition(); return; }
            if (!TrySampleApprovedLaunch(LaunchTimeline, out var state, out float elapsed))
            {
                if (launchLandingBounces.Count > 0) ReleaseLaunchBounces();
                else if (launchPresentationActive) CancelApprovedLaunch(true);
                return;
            }
            elapsed = PresentedLaunchElapsed(state, elapsed);
            bool began = presentedLaunchSequence != state.sequence;
            presentedLaunchElapsed = elapsed;
            presentedLaunchSequence = state.sequence;
            launchPresentationActive = true; launchOwnerTimeline = isLocalPlayer && !launchUsesBufferedTimeline;
            if (isLocalPlayer) { movement.SetPredictLocally(false); SetOwnerAuthorityPresentation(true); }
            launchPosition = state.arc.Position(elapsed); launchPosition.z = transform.position.z;
            launchPositionValid = true; ApplyLaunchPosition();
            if (launchUsesBufferedTimeline && !ropeEndpointPresented && TryRopePlaybackState(out var terminal) && !terminal.active && elapsed >= terminal.elapsed)
            {
                ropeEndpointPresented = true;
                state.elapsed = elapsed; TraceLaunch("launch_recipe_endpoint", state);
            }
            PresentLaunchShadow(state.arc, elapsed);
            if (elapsed >= state.arc.duration && launchLandingBounces.Count > 0) { ReleaseLaunchBounces(); return; }
            if (began) TraceLaunch("launch_recipe_begin", state);
            if (GameplayLatencyDiagnostics.Enabled && Time.unscaledTime >= nextLaunchDiagnostic)
            { nextLaunchDiagnostic = Time.unscaledTime + .05f; state.elapsed = elapsed; TraceLaunch("launch_recipe_sample", state); }
        }
        void ApplyLaunchPosition()
        {
            if (movement.Rigidbody != null) movement.Rigidbody.position = launchPosition;
            transform.position = launchPosition;
        }
        void CancelApprovedLaunch(bool replay)
        {
            bool wasActive = launchPresentationActive;
            TrySampleApprovedLaunch(LaunchTimeline, out var state, out _);
            if (state.sequence == 0 && launchHistory.Count > 0) state = launchHistory[launchHistory.Count - 1];
            if (NetworkInputHistory.IsNewer(state.sequence, cancelledLaunchSequence)) cancelledLaunchSequence = state.sequence;
            bool pendingRope = launchUsesBufferedTimeline && HasPendingRopePlayback();
            launchPresentationActive = pendingRope; launchPositionValid = false;
            ropePlaybackStarted = ropeEndpointPresented = ropePlaybackFinished = false;
            launchLandingBounces.Clear();
            if (launchShadow != null) launchShadow.enabled = false;
            if (replay && wasActive && !pendingRope && isLocalPlayer && hasLaunchMovement && launchMovement.canPredict && !Blocked &&
                GetComponent<NetworkBombFx>()?.ClientPlayerMotionActive != true)
            {
                movement.RestoreNetworkMovementState(launchMovement);
                SetOwnerAuthorityPresentation(false);
                bool complete = pending.Count == 0 || pending[0].tick == unchecked(lastLaunchAck + 1);
                movement.SetPredictLocally(complete && movement.CanPredictNetworkMovement);
                if (complete) ReplayMovementHistory();
            }
            hasLaunchMovement = false;
            if (wasActive) TraceLaunch("launch_recipe_end", state);
        }
        uint lastLaunchAck;
        void ClearApprovedLaunchHistory()
        {
            launchPresentationActive = hasLaunchMovement = launchPositionValid = false;
            launchOwnerTimeline = false;
            launchLandingBounces.Clear();
            launchUsesBufferedTimeline = false; ropePlaybackSequence = 0;
            ropePlaybackSourceTime = ropePlaybackLastAt = ropePlaybackFreshAt = 0; ropePlaybackPhase = 0; ropePlaybackFrame = -1;
            ropePlaybackStarted = ropeEndpointPresented = ropePlaybackFinished = false;
            launchHistory.Clear(); presentedLaunchSequence = cancelledLaunchSequence = receivedLaunchRevision = 0;
            if (launchShadow != null) ReleaseLaunchObject(launchShadow.gameObject);
            if (launchShadowSprite != null) { var texture = launchShadowSprite.texture; ReleaseLaunchObject(launchShadowSprite); ReleaseLaunchObject(texture); }
            launchShadow = null; launchShadowSprite = null;
        }
        public bool DeferBounceForApprovedLaunch(NetworkBombFx.PlayerBounceMotion motion)
        {
            if (!launchPresentationActive || (!motion.moving && launchLandingBounces.Count == 0 && GetComponent<NetworkBombFx>()?.PlayerBounceActive != true)) return false;
            if (!TrySampleApprovedLaunch(LaunchTimeline, out var state, out float elapsed) || elapsed >= state.arc.duration) return false;
            if (launchLandingBounces.Count >= 32) { CancelApprovedLaunch(false); return false; }
            launchLandingBounces.Enqueue(motion); return true;
        }
        void ReleaseLaunchBounces()
        {
            var bounces = launchLandingBounces.ToArray(); launchLandingBounces.Clear();
            CancelApprovedLaunch(false);
            var fx = GetComponent<NetworkBombFx>();
            if (fx != null) foreach (var bounce in bounces) fx.ReceivePlayerBounce(bounce);
        }
        void PresentLaunchShadow(PlayerLaunchArc arc, float elapsed)
        {
            if (!arc.groundShadow) { if (launchShadow != null) launchShadow.enabled = false; return; }
            if (launchShadow == null)
            {
                var go = new GameObject("ApprovedLaunchShadow");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                launchShadow = go.AddComponent<SpriteRenderer>();
                launchShadowSprite = BattleMode4SpringTileController.CreateLaunchShadowSprite();
                launchShadow.sprite = launchShadowSprite;
            }
            launchShadow.enabled = true; launchShadow.color = arc.shadowColor; launchShadow.sortingOrder = arc.shadowOrder;
            launchShadow.transform.position = arc.Ground(elapsed);
            launchShadow.transform.localScale = new Vector3(Mathf.Max(.01f, arc.shadowScale.x), Mathf.Max(.01f, arc.shadowScale.y), 1);
        }
        static void ReleaseLaunchObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        void TraceLaunch(string kind, LaunchState state)
        {
            if (!GameplayLatencyDiagnostics.Enabled) return;
            var entry = GameplayLatencyDiagnostics.For(kind, movement, detail: "sequence=" + state.sequence + ";approved_curve;kind=" + state.arc.kind + ";root_height=" + state.arc.rootCarriesHeight);
            entry.accepted = state.active;
            entry.durationMs = state.elapsed * 1000; entry.delta = state.arc.end - state.arc.start; GameplayLatencyDiagnostics.Write(entry);
        }
    }
}
