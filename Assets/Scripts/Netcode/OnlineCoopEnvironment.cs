using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct CoopEnvironmentLoopFrame
    {
        public int id, clip;
        public float phase, gain;
        public bool looping, paused;
    }
    public struct CoopEnvironmentLoops : NetworkMessage
    {
        public string scene;
        public uint revision;
        public double time;
        public CoopEnvironmentLoopFrame[] frames;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<int, string> coopEnvironmentFamilies = new();
        internal static string EnvironmentFamily(SpriteRenderer renderer)
        {
            if (renderer.GetComponentInParent<global::StageAssets.RocketPenguinController>(true) != null) return "RocketPenguin";
            if (renderer.GetComponentInParent<global::StageAssets.DestructibleRocketController>(true) != null) return "DestructibleRocket";
            if (renderer.GetComponentInParent<global::StageAssets.SnowBallTrap>(true) != null) return "SnowBall";
            if (renderer.GetComponentInParent<global::StageAssets.BarrelPillarTrap>(true) != null) return "Barrel";
            if (renderer.GetComponentInParent<global::StageAssets.Stage32VolcanoEyesAnimator>(true) != null) return "Volcano";
            if (renderer.GetComponentInParent<global::StageAssets.Stage32VulcanFlameLeap>(true) != null) return "Vulcan";
            if (renderer.GetComponentInParent<global::StageAssets.BubbleChipFloatBobber>(true) != null ||
                renderer.GetComponentInParent<global::StageAssets.BubbleChipFloatAnimator>(true) != null) return "BubbleFloat";
            if (renderer.GetComponentInParent<global::StageAssets.World3GateOpenedSequenceController>(true) != null) return "GateBubble";
            if (renderer.GetComponentInParent<global::FloatingPlatform>(true) != null) return "Platform";
            if (renderer.name == "AmbientOceanBubble") return "AmbientBubble";
            return renderer.name.StartsWith("OceanBubble_", StringComparison.Ordinal) ? "BurstBubble" : "";
        }
        bool IsCoopEnvironmentRenderer(SpriteRenderer renderer)
        {
            int id = renderer.GetEntityId().GetHashCode();
            if (!coopEnvironmentFamilies.TryGetValue(id, out string family))
                coopEnvironmentFamilies[id] = family = EnvironmentFamily(renderer);
            return family.Length != 0;
        }

        sealed class EnvironmentLoopSource
        {
            public AudioSource source;
            public AudioClip clip;
            public float phase, gain;
            public bool looping = true;
        }
        sealed class EnvironmentLoopView
        {
            public AudioSource source;
            public AudioClip ownedClip;
            public int clip;
            public bool paused;
        }
        readonly Dictionary<int, EnvironmentLoopSource> coopLoopSources = new();
        readonly Dictionary<int, EnvironmentLoopView> coopLoopViews = new();
        readonly List<int> coopExpiredLoops = new();
        uint coopLoopRevision, coopReceivedLoopRevision;
        float coopNextLoops;
        bool coopLoopsPaused;

        public static bool CoopStartEnvironmentLoop(AudioSource source, AudioClip clip, float gain)
        {
            if (!NetworkServer.active || !IsCoopStage || source == null || clip == null ||
                singleton is not BombermanNetworkManager manager || manager == null) return false;
            if (!float.IsFinite(gain) || clip.length <= 0 || manager.coopLoopSources.Count >= 32) return false;
            int id = source.GetEntityId().GetHashCode();
            manager.coopLoopSources[id] = new EnvironmentLoopSource { source = source, clip = clip, gain = Mathf.Clamp(gain, 1, 16) };
            manager.PublishEnvironmentLoops();
            return true;
        }
        public static void CoopEndEnvironmentLoop(AudioSource source, bool tail)
        {
            if (!NetworkServer.active || source == null || singleton is not BombermanNetworkManager manager || manager == null) return;
            int id = source.GetEntityId().GetHashCode();
            if (tail && manager.coopLoopSources.TryGetValue(id, out var state)) state.looping = false;
            else manager.coopLoopSources.Remove(id);
            manager.PublishEnvironmentLoops();
        }
        public static bool CoopEnvironmentLoopPending(AudioSource source) => NetworkServer.active && source != null &&
            singleton is BombermanNetworkManager manager && manager != null && manager.coopLoopSources.ContainsKey(source.GetEntityId().GetHashCode());

        internal static float AdvanceEnvironmentLoop(float phase, float delta, float length, bool looping, bool paused, out bool finished)
        {
            if (!paused) phase += Mathf.Max(0, delta);
            finished = !looping && phase >= length;
            return looping && length > 0 ? phase % length : phase;
        }

        void TickCoopEnvironmentAudio()
        {
            if (NetworkServer.active)
            {
                bool paused = GamePauseController.IsPaused || Time.timeScale <= 0;
                coopExpiredLoops.Clear();
                foreach (var pair in coopLoopSources)
                {
                    var state = pair.Value;
                    if (state.source == null || state.clip == null) { coopExpiredLoops.Add(pair.Key); continue; }
                    state.phase = AdvanceEnvironmentLoop(state.phase, Time.unscaledDeltaTime, state.clip.length, state.looping, paused, out bool finished);
                    if (finished) coopExpiredLoops.Add(pair.Key);
                }
                foreach (int id in coopExpiredLoops) coopLoopSources.Remove(id);
                if (coopLoopSources.Count > 0 || coopExpiredLoops.Count > 0)
                    if (Time.unscaledTime >= coopNextLoops || coopExpiredLoops.Count > 0 || paused != coopLoopsPaused) PublishEnvironmentLoops();
            }
            else
                foreach (var state in coopLoopViews.Values)
                    if (state.source != null) state.source.volume = GameAudioSettings.ApplySfxVolume(1f);
        }
        void PublishEnvironmentLoops()
        {
            if (!IsCoopStage || !NetworkServer.active) return;
            coopNextLoops = Time.unscaledTime + .25f;
            coopLoopsPaused = GamePauseController.IsPaused || Time.timeScale <= 0;
            var frames = new List<CoopEnvironmentLoopFrame>();
            foreach (var pair in coopLoopSources)
            {
                var source = pair.Value;
                int clip = CoopSoundId(source.clip);
                if (clip < 0) continue;
                frames.Add(new CoopEnvironmentLoopFrame { id = pair.Key, clip = clip, phase = source.phase,
                    gain = source.gain, looping = source.looping, paused = GamePauseController.IsPaused || Time.timeScale <= 0 });
            }
            NetworkServer.SendToReady(new CoopEnvironmentLoops { scene = coopScene, time = NetworkTime.localTime,
                revision = ++coopLoopRevision, frames = frames.ToArray() });
            TraceEnvironmentLoop("r08_loop_snapshot_native", coopLoopRevision, 0, frames.Count, 0, false, false, false);
            foreach (var frame in frames) TraceEnvironmentLoop("r08_loop_native", coopLoopRevision, frame.id, frames.Count, frame.phase, frame.looping, frame.paused, true);
        }
        internal static bool ValidEnvironmentLoop(CoopEnvironmentLoopFrame frame) =>
            frame.clip >= 0 && float.IsFinite(frame.phase) && frame.phase >= 0 && float.IsFinite(frame.gain) && frame.gain >= 1 && frame.gain <= 16;

        void ApplyCoopEnvironmentLoops(CoopEnvironmentLoops message)
        {
            if (NetworkServer.active || !CanApplyCoop(message.scene) || message.frames == null ||
                message.revision <= coopReceivedLoopRevision || !double.IsFinite(message.time) || message.frames.Length > 32) return;
            foreach (var frame in message.frames) if (!ValidEnvironmentLoop(frame)) return;
            coopReceivedLoopRevision = message.revision;
            TraceEnvironmentLoop("r08_loop_snapshot_view", message.revision, 0, message.frames.Length, 0, false, false, false);
            var alive = new HashSet<int>();
            foreach (var frame in message.frames)
            {
                var clip = SoundCatalog == null ? null : At(SoundCatalog.sounds, frame.clip);
                if (clip == null || clip.length <= 0) continue;
                alive.Add(frame.id);
                if (coopLoopViews.TryGetValue(frame.id, out var old) && old.clip != frame.clip)
                { DestroyEnvironmentLoopView(old); coopLoopViews.Remove(frame.id); }
                bool fresh = !coopLoopViews.TryGetValue(frame.id, out var view);
                if (fresh)
                {
                    var go = new GameObject("CoopEnvironmentLoop", typeof(AudioSource));
                    view = new EnvironmentLoopView { source = go.GetComponent<AudioSource>(), clip = frame.clip };
                    view.source.playOnAwake = false; view.source.ignoreListenerPause = true;
                    view.source.clip = clip;
                    if (frame.gain > 1)
                    {
                        float[] samples = new float[clip.samples * clip.channels];
                        if (clip.GetData(samples, 0))
                        {
                            float peak = 0;
                            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                            float gain = peak > 0 ? Mathf.Min(frame.gain, .98f / peak) : 1;
                            gain = Mathf.Max(1, gain);
                            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
                            view.ownedClip = AudioClip.Create("ApprovedSnowBall", clip.samples, clip.channels, clip.frequency, false);
                            view.ownedClip.SetData(samples, 0); view.source.clip = view.ownedClip;
                        }
                    }
                    coopLoopViews[frame.id] = view;
                }
                float phase = frame.phase + (frame.paused ? 0 : (float)Math.Max(0, NetworkTime.time - message.time));
                if (frame.looping) phase %= clip.length;
                phase = Mathf.Clamp(phase, 0, Mathf.Max(0, clip.length - .001f));
                view.source.loop = frame.looping;
                if (fresh || Mathf.Abs(view.source.time - phase) > .25f) view.source.time = phase;
                if (fresh) view.source.Play();
                if (frame.paused && (!view.paused || fresh)) view.source.Pause();
                if (!frame.paused && view.paused) view.source.UnPause();
                view.paused = frame.paused;
                TraceEnvironmentLoop("r08_loop_view", message.revision, frame.id, message.frames.Length, view.source.time,
                    view.source.loop, view.paused, view.source.clip != null);
            }
            coopExpiredLoops.Clear();
            foreach (var pair in coopLoopViews) if (!alive.Contains(pair.Key)) coopExpiredLoops.Add(pair.Key);
            foreach (int id in coopExpiredLoops) { DestroyEnvironmentLoopView(coopLoopViews[id]); coopLoopViews.Remove(id); }
        }
        void DestroyEnvironmentLoopView(EnvironmentLoopView view)
        {
            if (view.source != null) { view.source.Stop(); DestroyEnvironmentAsset(view.source.gameObject); }
            if (view.ownedClip != null) DestroyEnvironmentAsset(view.ownedClip);
        }
        static void DestroyEnvironmentAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEBUG")]
        static void TraceEnvironmentLoop(string kind, uint revision, int id, int count, float phase, bool looping, bool paused, bool clip)
        {
#if UNITY_EDITOR || DEBUG
            if (!R08Probe || !GameplayLatencyDiagnostics.Enabled) return;
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For(kind, detail: "rev=" + revision + ";id=" + id +
                ";count=" + count + ";phase=" + phase.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ";loop=" + (looping ? 1 : 0) + ";paused=" + (paused ? 1 : 0) + ";clip=" + (clip ? 1 : 0)));
#endif
        }
        void ClearCoopEnvironment()
        {
            foreach (var view in coopLoopViews.Values) DestroyEnvironmentLoopView(view);
            coopLoopViews.Clear(); coopLoopSources.Clear(); coopEnvironmentFamilies.Clear();
            coopLoopRevision = coopReceivedLoopRevision = 0; coopNextLoops = 0;
        }
    }
}
