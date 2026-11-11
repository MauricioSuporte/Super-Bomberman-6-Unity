using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class BombermanNetworkManager
    {
        sealed class PresentationMetric
        {
            public int frames, changes, payloadBytes, motionEntries, motionAnchors, motionBytes, repositions;
            public double cpuMs, motionCpuMs;
        }
        readonly Dictionary<int, string> coopMetricFamilies = new();
        readonly Dictionary<string, PresentationMetric> coopPresentationMetrics = new();
        double coopMetricNextFlush;

        string CoopPresentationFamily(SpriteRenderer renderer)
        {
            int id = renderer.GetEntityId().GetHashCode();
            if (coopMetricFamilies.TryGetValue(id, out string family)) return family;
            family = ClassifyCoopPresentationFamily(renderer);
            coopMetricFamilies[id] = family;
            if (CoopShapeDiagnostics) coopNewFamilyIds.Add((id, family));
            return family;
        }

        static string ClassifyCoopPresentationFamily(SpriteRenderer renderer)
        {
            string environment = EnvironmentFamily(renderer);
            if (environment.Length != 0) return environment;
            if (renderer.TryGetComponent<CoopProjectileVisual>(out var projectile)) return projectile.Family;
            var enemy = renderer.GetComponentInParent<EnemyMovementController>(true);
            if (enemy != null) return enemy.GetType().Name;
            for (var current = renderer.transform; current != null; current = current.parent)
                foreach (var behaviour in current.GetComponents<MonoBehaviour>())
                    if (behaviour != null && behaviour is not AnimatedSpriteRenderer && behaviour is not CharacterHealth &&
                        behaviour.GetType().Assembly == typeof(GameManager).Assembly)
                        return behaviour.GetType().Name;
            return "UnclassifiedWorldVisual";
        }

        void RecordCoopPresentationMetric(SpriteRenderer renderer, CoopSpriteFrame frame, bool changed, long started)
        {
            if (!GameplayLatencyDiagnostics.Enabled || started == 0) return;
            // Core capture time excludes the diagnostic lookup/extra serialization below.
            double cpu = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            string family = CoopPresentationFamily(renderer);
            if (!coopPresentationMetrics.TryGetValue(family, out var metric))
                coopPresentationMetrics[family] = metric = new PresentationMetric();
            metric.frames++; metric.cpuMs += cpu;
            if (!changed) return;
            metric.changes++;
            using var writer = NetworkWriterPool.Get(); writer.Write(frame);
            metric.payloadBytes += writer.Position;
        }

        void FlushCoopPresentationMetrics()
        {
            if (!GameplayLatencyDiagnostics.Enabled) { ClearCoopPresentationMetrics(); return; }
            FlushCoopFamilyIds();
            if (Time.realtimeSinceStartupAsDouble < coopMetricNextFlush) return;
            coopMetricNextFlush = Time.realtimeSinceStartupAsDouble + 1;
            foreach (var pair in coopPresentationMetrics)
            {
                var metric = pair.Value;
                var entry = GameplayLatencyDiagnostics.For("coop_family_cost", null,
                    detail: "family=" + pair.Key + ";sampled=" + metric.frames + ";changed=" + metric.changes +
                        ";frame_payload_bytes=" + metric.payloadBytes + ";capture_cpu_ms=" + metric.cpuMs.ToString("R", CultureInfo.InvariantCulture) +
                        ";excludes_envelopes_asset_definitions_peer_fanout_and_diagnostic_overhead" +
                        ";motion_entries=" + metric.motionEntries + ";motion_anchors=" + metric.motionAnchors + ";motion_bytes=" + metric.motionBytes +
                        ";motion_cpu_ms=" + metric.motionCpuMs.ToString("R", CultureInfo.InvariantCulture) + ";repositions=" + metric.repositions);
                GameplayLatencyDiagnostics.Write(entry);
            }
            coopPresentationMetrics.Clear();
            FlushCoopShapeMetrics();
        }

        void ClearCoopPresentationMetrics()
        {
            coopMetricFamilies.Clear(); coopPresentationMetrics.Clear(); coopMetricNextFlush = 0;
            coopTruth.Clear(); coopShapeLastSent.Clear(); coopShapeMetrics.Clear(); coopNewFamilyIds.Clear(); coopNativeTracks.Clear(); coopShapePending.Clear();
            coopPresentStats.Clear(); coopPresentNextFlush = 0;
        }

        // ---- R02: shape of the reconstructed world stream (server, Development capture only) ----
        // The authority runs the same native scene code as offline. Its per-frame pose is the reference;
        // the frames actually sent are reconstructed exactly like PresentOnlineSprites (linear between two
        // visible samples, a cut for an enemy discontinuity, otherwise hold) and compared at each native frame.
        struct CoopTruthSample { public double time; public Vector3 position; public bool visible; }
        sealed class ShapeMetric
        {
            public int frames, over1, over2, holdStarts, holdFrames, extraFrames, nativeMoving, nativeStalls;
            public double sum, holdSum, nativeStepSum;
            public float max, holdMax, nativeStepMax;
            public readonly int[] bins = new int[81]; // 0.1 px bins; the last bin collects >= 8 px
        }
        readonly Dictionary<int, List<CoopTruthSample>> coopTruth = new();
        readonly Dictionary<int, CoopVisualSample> coopShapeLastSent = new();
        sealed class NativeTrack { public bool has, movedBefore; public int zeroRun; public Vector3 last; public double scoredTo = double.MinValue; }
        readonly Dictionary<int, NativeTrack> coopNativeTracks = new();
        readonly Dictionary<string, ShapeMetric> coopShapeMetrics = new();
        readonly List<(int id, string family)> coopNewFamilyIds = new();
        readonly List<CoopVisualSample> coopShapeSent = new();
        readonly Dictionary<int, List<CoopVisualSample>> coopShapePending = new();

        // Position-only samples sent between full frames, evaluated with the next full-frame tick.
        void RecordCoopMotionMetric(SpriteRenderer renderer, int id, in CoopSpriteFrame approved, bool anchor, Vector3 anchorPosition,
            double anchorTime, bool entry, Vector3 position, double time, long started)
        {
            double cpu = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            var metric = CoopFamilyMetric(renderer);
            if (entry) metric.motionEntries++;
            metric.motionCpuMs += cpu;
            using (var writer = NetworkWriterPool.Get())
            {
                writer.Write(new CoopMotionEntry { id = id, position = position });
                metric.motionBytes += writer.Position * ((anchor ? 1 : 0) + (entry ? 1 : 0));
            }
            var pending = CoopShapePendingList(id);
            var base_ = CoopVisualSample.Full(approved);
            if (anchor) { metric.motionAnchors++; pending.Add(base_.At(anchorTime, anchorPosition)); }
            if (entry) pending.Add(base_.At(time, position));
        }

        // Screen-space overlay reposition: approved end of the old segment, then an explicit cut to the new pose.
        void RecordCoopRepositionMetric(SpriteRenderer renderer, int id, in CoopSpriteFrame approved, Vector3 anchorPosition, double anchorTime,
            Vector3 position, double time)
        {
            var metric = CoopFamilyMetric(renderer);
            metric.repositions++;
            using (var writer = NetworkWriterPool.Get())
            {
                writer.Write(new CoopMotionEntry { id = id, position = position });
                metric.motionBytes += writer.Position * 2;
            }
            var pending = CoopShapePendingList(id);
            var base_ = CoopVisualSample.Full(approved);
            pending.Add(base_.At(anchorTime, anchorPosition));
            pending.Add(base_.At(time, position, true));
        }

        PresentationMetric CoopFamilyMetric(SpriteRenderer renderer)
        {
            string family = CoopPresentationFamily(renderer);
            if (!coopPresentationMetrics.TryGetValue(family, out var metric))
                coopPresentationMetrics[family] = metric = new PresentationMetric();
            return metric;
        }

        List<CoopVisualSample> CoopShapePendingList(int id)
        {
            if (!coopShapePending.TryGetValue(id, out var pending)) coopShapePending[id] = pending = new List<CoopVisualSample>(4);
            while (pending.Count >= 16) pending.RemoveAt(0);
            return pending;
        }

        void TakeCoopShapePending(int id, List<CoopVisualSample> sent)
        {
            sent.Clear();
            if (!coopShapePending.TryGetValue(id, out var pending)) return;
            sent.AddRange(pending); pending.Clear();
        }
        const float CoopPixelsPerUnit = 16f;

        static bool CoopShapeDiagnostics => UnityEngine.Debug.isDebugBuild && GameplayLatencyDiagnostics.Enabled;

        internal static Vector3 CoopBasePosition(SpriteRenderer renderer, out AnimatedSpriteRenderer animation, out Vector3 offsetRight, out Vector3 offsetUp)
        {
            Vector3 position = renderer.transform.position;
            offsetRight = offsetUp = Vector3.zero;
            if (renderer.TryGetComponent(out animation) && animation.isActiveAndEnabled)
            {
                // Remove the sampled jump offset; the client reapplies the offset for its rendered animation frame.
                offsetRight = animation.FrameOffsetToWorld(Vector2.right);
                offsetUp = animation.FrameOffsetToWorld(Vector2.up);
                Vector2 offset = animation.CurrentSpriteOffset;
                position -= offsetRight * offset.x + offsetUp * offset.y;
            }
            else animation = null;
            return position;
        }

        void SampleCoopTruth()
        {
            double now = NetworkTime.localTime;
            foreach (var renderer in coopRenderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                int id = renderer.GetEntityId().GetHashCode();
                if (!coopTruth.TryGetValue(id, out var samples)) coopTruth[id] = samples = new List<CoopTruthSample>(8);
                if (samples.Count >= 32) samples.RemoveAt(0);
                samples.Add(new CoopTruthSample { time = now, position = CoopBasePosition(renderer, out _, out _, out _),
                    visible = renderer.enabled && !renderer.forceRenderingOff });
            }
        }

        // ---- R02: capture-phase probe (server, Development capture only) ----
        // On every long frame (> 40 ms) and once per second, the overlay poses seen in the manager's LateUpdate (order -150)
        // are compared with the poses after every LateUpdate (the rendered ones), plus the camera move in between.
        readonly Dictionary<int, Vector3> coopPhaseEarly = new();
        Vector3 coopPhaseEarlyCamera;
        bool coopPhaseArmed;
        double coopPhaseNextPeriodic;

        void ProbeCoopPhaseEarly()
        {
            coopPhaseArmed = false;
            bool periodic = Time.realtimeSinceStartupAsDouble >= coopPhaseNextPeriodic;
            if (Time.unscaledDeltaTime <= .04f && !periodic) return;
            if (periodic) coopPhaseNextPeriodic = Time.realtimeSinceStartupAsDouble + 1;
            coopPhaseEarly.Clear();
            foreach (var renderer in coopOverlayRenderers)
                if (renderer != null && renderer.gameObject.activeInHierarchy)
                    coopPhaseEarly[renderer.GetEntityId().GetHashCode()] = CoopBasePosition(renderer, out _, out _, out _);
            var camera = Camera.main;
            coopPhaseEarlyCamera = camera != null ? camera.transform.position : Vector3.zero;
            coopPhaseArmed = true;
        }

        void ProbeCoopPhaseLate()
        {
            if (!coopPhaseArmed) return;
            coopPhaseArmed = false;
            float max = 0, sum = 0;
            int count = 0, wrapped = 0;
            foreach (var renderer in coopOverlayRenderers)
            {
                if (renderer == null || !coopPhaseEarly.TryGetValue(renderer.GetEntityId().GetHashCode(), out var early)) continue;
                float delta = Vector3.Distance(early, CoopBasePosition(renderer, out _, out _, out _)) * CoopPixelsPerUnit;
                if (delta > CoopRepositionJump * CoopPixelsPerUnit) { wrapped++; continue; }
                max = Mathf.Max(max, delta); sum += delta; count++;
            }
            var camera = Camera.main;
            float cameraMove = camera != null ? Vector3.Distance(coopPhaseEarlyCamera, camera.transform.position) * CoopPixelsPerUnit : 0;
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("coop_phase_probe", null,
                detail: "dt_ms=" + (Time.unscaledDeltaTime * 1000).ToString("0.###", CultureInfo.InvariantCulture) + ";overlays=" + count +
                    ";early_late_mean_px=" + (count == 0 ? "0" : (sum / count).ToString("0.###", CultureInfo.InvariantCulture)) +
                    ";early_late_max_px=" + max.ToString("0.###", CultureInfo.InvariantCulture) + ";wrapped_in_frame=" + wrapped +
                    ";camera_px=" + cameraMove.ToString("0.###", CultureInfo.InvariantCulture) + ";late_capture=" + (CoopLateCapture ? 1 : 0)));
        }

        // Same segment rule as PresentOnlineSprites (CoopVisualSample.Interpolates).
        static Vector3 ReconstructCoopPosition(in CoopVisualSample from, in CoopVisualSample to, double time)
        {
            if (time >= to.frame.time) return to.frame.position;
            if (!CoopVisualSample.Interpolates(from, to)) return from.frame.position;
            return Vector3.Lerp(from.frame.position, to.frame.position, Mathf.Clamp01((float)((time - from.frame.time) / (to.frame.time - from.frame.time))));
        }

        // sent: frames published for this renderer in this tick, in order (empty when unchanged).
        void EvaluateCoopShape(SpriteRenderer renderer, int id, List<CoopVisualSample> sent, double tickTime, int extraFrames)
        {
            if (!coopTruth.TryGetValue(id, out var truth) || truth.Count == 0) return;
            string family = CoopPresentationFamily(renderer);
            if (!coopShapeMetrics.TryGetValue(family, out var metric)) coopShapeMetrics[family] = metric = new ShapeMetric();
            metric.extraFrames += extraFrames;
            bool hasFrom = coopShapeLastSent.TryGetValue(id, out var from);
            if (!coopNativeTracks.TryGetValue(id, out var track)) coopNativeTracks[id] = track = new NativeTrack();
            int index = 0;
            while (index < truth.Count && truth[index].time <= track.scoredTo) index++;
            for (int s = 0; s <= sent.Count; s++)
            {
                bool hold = s == sent.Count;
                double end = hold ? tickTime : sent[s].frame.time;
                bool holdStart = !hold && hasFrom && sent[s].frame.time - from.frame.time > .08 && sent[s].frame.position != from.frame.position && !sent[s].cut;
                if (holdStart) metric.holdStarts++;
                for (; index < truth.Count && truth[index].time <= end; index++)
                {
                    var native = truth[index];
                    track.scoredTo = native.time;
                    bool nativeMoved = track.has && native.visible && native.position != track.last;
                    if (nativeMoved)
                    {
                        float step = Vector3.Distance(native.position, track.last) * CoopPixelsPerUnit;
                        metric.nativeMoving++; metric.nativeStepSum += step; metric.nativeStepMax = Mathf.Max(metric.nativeStepMax, step);
                        if (track.movedBefore && track.zeroRun > 0 && track.zeroRun <= 3) metric.nativeStalls += track.zeroRun;
                        track.zeroRun = 0; track.movedBefore = true;
                    }
                    else if (track.has && native.visible && ++track.zeroRun > 3) track.movedBefore = false;
                    if (!native.visible) track.movedBefore = false;
                    track.last = native.position; track.has = native.visible;
                    if (!hasFrom || !native.visible || !from.frame.visible) continue;
                    Vector3 presented = hold ? from.frame.position : ReconstructCoopPosition(from, sent[s], native.time);
                    float error = Vector3.Distance(presented, native.position) * CoopPixelsPerUnit;
                    if (!nativeMoved && error < .001f) continue; // stationary frames would dilute the distribution
                    metric.frames++; metric.sum += error; metric.max = Mathf.Max(metric.max, error);
                    if (error > 1) metric.over1++;
                    if (error > 2) metric.over2++;
                    metric.bins[Mathf.Min(80, (int)(error * 10))]++;
                    if (holdStart) { metric.holdFrames++; metric.holdSum += error; metric.holdMax = Mathf.Max(metric.holdMax, error); }
                }
                if (hold) break;
                from = sent[s]; hasFrom = true;
            }
            if (hasFrom) coopShapeLastSent[id] = from;
            if (index > 0) truth.RemoveRange(0, index);
        }

        void ForgetCoopShape(int id)
        {
            coopTruth.Remove(id); coopShapeLastSent.Remove(id); coopNativeTracks.Remove(id); coopShapePending.Remove(id);
        }

        void FlushCoopFamilyIds()
        {
            if (coopNewFamilyIds.Count == 0) return;
            var builder = new StringBuilder("ids=");
            for (int i = 0; i < coopNewFamilyIds.Count; i++)
            {
                if (i > 0) builder.Append('|');
                builder.Append(coopNewFamilyIds[i].id.ToString(CultureInfo.InvariantCulture)).Append(':').Append(coopNewFamilyIds[i].family);
            }
            coopNewFamilyIds.Clear();
            GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("coop_family_ids", null, detail: builder.ToString()));
        }

        void FlushCoopShapeMetrics()
        {
            foreach (var pair in coopShapeMetrics)
            {
                var m = pair.Value;
                if (m.frames == 0 && m.holdStarts == 0 && m.nativeMoving == 0 && m.extraFrames == 0) continue;
                var builder = new StringBuilder();
                builder.Append("family=").Append(pair.Key).Append(";frames=").Append(m.frames)
                    .Append(";error_sum_px=").Append(m.sum.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";error_max_px=").Append(m.max.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";over1=").Append(m.over1).Append(";over2=").Append(m.over2)
                    .Append(";hold_starts=").Append(m.holdStarts).Append(";hold_frames=").Append(m.holdFrames)
                    .Append(";hold_sum_px=").Append(m.holdSum.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";hold_max_px=").Append(m.holdMax.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";extra_frames=").Append(m.extraFrames)
                    .Append(";native_moving=").Append(m.nativeMoving)
                    .Append(";native_step_sum_px=").Append(m.nativeStepSum.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";native_step_max_px=").Append(m.nativeStepMax.ToString("R", CultureInfo.InvariantCulture))
                    .Append(";native_stalls=").Append(m.nativeStalls)
                    .Append(";bins=");
                int last = 80;
                while (last > 0 && m.bins[last] == 0) last--;
                for (int i = 0; i <= last; i++) { if (i > 0) builder.Append(','); builder.Append(m.bins[i]); }
                GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("coop_family_shape", null, detail: builder.ToString()));
            }
            coopShapeMetrics.Clear();
        }

        // ---- R02: client presentation continuity, delay and cost per sprite id (Development capture only) ----
        sealed class PresentStat
        {
            public int presented, interpolated, moving, stalls, zeroRun, arrivals, continuous, starved, late, pausedHeld, pausedMoving, moveContinuous, moveStarved;
            public int approvedHolds;
            public bool movedBefore, hasLast;
            public Vector3 last;
            public double stepSum, delaySum, starvedMs, marginSum, cpuUs, moveStarvedMs, approvedHoldMs, lagSum, holdSum, ageSum;
            public float stepMax, delayMax, lagMax, holdMax, ageMax;
            public double marginMin = double.MaxValue;
            // Delay histogram of moving presented frames: 10 ms bins, the last collects >= 600 ms.
            public int[] delayBins;
        }
        const int CoopDelayBinMs = 10, CoopDelayBins = 61;
        readonly Dictionary<int, PresentStat> coopPresentStats = new();
        readonly List<int> coopPresentIdle = new();
        double coopPresentNextFlush;

        PresentStat CoopPresentStat(int id)
        {
            if (!coopPresentStats.TryGetValue(id, out var stat)) coopPresentStats[id] = stat = new PresentStat();
            return stat;
        }

        // Called before a received frame is appended: the previous newest sample tells whether the
        // presentation clock had already run past the stream before this sample arrived.
        void RecordCoopArrival(int id, List<CoopVisualSample> samples, in CoopVisualSample sample)
        {
            var frame = sample.frame;
            var stat = CoopPresentStat(id);
            stat.arrivals++;
            // Network age of the approved state when it reaches the client (server-time estimate minus state time).
            float age = (float)((NetworkTime.time + NetworkClient.bufferTime + NetworkTime.rtt * .5 - frame.time) * 1000);
            stat.ageSum += age; stat.ageMax = Mathf.Max(stat.ageMax, age);
            double clock = CoopCarrierClock(id, CoopVisualClock);
            double margin = frame.time - clock;
            stat.marginSum += margin; stat.marginMin = System.Math.Min(stat.marginMin, margin);
            if (frame.time < clock) stat.late++;
            if (samples == null || samples.Count == 0) return;
            double newest = samples[^1].frame.time;
            if (frame.time <= newest || frame.time - newest > .1) return;
            stat.continuous++;
            if (clock > newest) { stat.starved++; stat.starvedMs += (clock - newest) * 1000; }
            // Positional subset: only a sample that moves the pose can make the presentation hold and then jump;
            // animation-only full frames keep playing locally from their playback origin.
            // A sample with the same pose proves the native object stayed there: holding it was the approved
            // presentation (native stop or rest), not a missing sample.
            if (frame.position == samples[^1].frame.position)
            {
                if (clock > newest) { stat.approvedHolds++; stat.approvedHoldMs += (clock - newest) * 1000; }
                return;
            }
            // The pose changed after `newest`: the native object was moving while the client held it (real starvation).
            stat.moveContinuous++;
            if (clock > newest) { stat.moveStarved++; stat.moveStarvedMs += (clock - newest) * 1000; }
        }

        void RecordCoopPresented(int id, in CoopSpriteFrame frame, bool interpolated, double stateTime, double clock, long started)
        {
            var stat = CoopPresentStat(id);
            stat.cpuUs += (Stopwatch.GetTimestamp() - started) * 1e6 / Stopwatch.Frequency;
            if (!frame.visible) { stat.hasLast = false; return; }
            stat.presented++;
            if (interpolated) stat.interpolated++;
            if (stat.hasLast)
            {
                float step = Vector3.Distance(frame.position, stat.last) * CoopPixelsPerUnit;
                if (step > .0001f)
                {
                    stat.moving++; stat.stepSum += step; stat.stepMax = Mathf.Max(stat.stepMax, step);
                    if (GamePauseController.IsPaused) stat.pausedMoving++;
                    if (stat.movedBefore && stat.zeroRun > 0 && stat.zeroRun <= 3) stat.stalls += stat.zeroRun;
                    stat.zeroRun = 0; stat.movedBefore = true;
                    float delay = (float)((NetworkTime.time + NetworkClient.bufferTime - stateTime + NetworkTime.rtt * .5) * 1000);
                    stat.delaySum += delay; stat.delayMax = Mathf.Max(stat.delayMax, delay);
                    // delay = network/timeline lag (buffer + half RTT + visual offset) + presentation hold of an old state.
                    float hold = (float)((clock - stateTime) * 1000), lag = delay - hold;
                    stat.holdSum += hold; stat.holdMax = Mathf.Max(stat.holdMax, hold);
                    stat.lagSum += lag; stat.lagMax = Mathf.Max(stat.lagMax, lag);
                    stat.delayBins ??= new int[CoopDelayBins];
                    stat.delayBins[Mathf.Clamp((int)(delay / CoopDelayBinMs), 0, CoopDelayBins - 1)]++;
                }
                else if (++stat.zeroRun > 3) stat.movedBefore = false;
            }
            stat.last = frame.position; stat.hasLast = true;
        }

        void FlushCoopPresentStats()
        {
            if (Time.realtimeSinceStartupAsDouble < coopPresentNextFlush) return;
            coopPresentNextFlush = Time.realtimeSinceStartupAsDouble + 1;
            var builder = new StringBuilder("v=4;paused=");
            builder.Append(GamePauseController.IsPaused ? 1 : 0).Append(";offset_ms=");
            builder.Append((TransformVisualOffset(NetworkClient.localPlayer) * 1000).ToString("0.###", CultureInfo.InvariantCulture)).Append(";ids=");
            bool first = true;
            foreach (var pair in coopPresentStats)
            {
                var s = pair.Value;
                if (s.presented == 0 && s.arrivals == 0 && s.pausedHeld == 0) continue;
                if (!first) builder.Append('|');
                first = false;
                builder.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s.presented).Append(':').Append(s.interpolated)
                    .Append(':').Append(s.moving).Append(':').Append(s.stalls)
                    .Append(':').Append(s.stepSum.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.stepMax.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.delaySum.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.delayMax.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.cpuUs.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.arrivals).Append(':').Append(s.continuous).Append(':').Append(s.starved)
                    .Append(':').Append(s.starvedMs.ToString("0.#", CultureInfo.InvariantCulture)).Append(':').Append(s.late)
                    .Append(':').Append((s.marginSum * 1000).ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.arrivals == 0 ? "0" : (s.marginMin * 1000).ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.pausedHeld).Append(':').Append(s.pausedMoving)
                    .Append(':').Append(s.moveContinuous).Append(':').Append(s.moveStarved)
                    .Append(':').Append(s.moveStarvedMs.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.approvedHolds).Append(':').Append(s.approvedHoldMs.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.lagSum.ToString("0.#", CultureInfo.InvariantCulture)).Append(':').Append(s.lagMax.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.holdSum.ToString("0.#", CultureInfo.InvariantCulture)).Append(':').Append(s.holdMax.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':').Append(s.ageSum.ToString("0.#", CultureInfo.InvariantCulture)).Append(':').Append(s.ageMax.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(':');
                // Sparse delay histogram: bin=count pairs separated by '/'.
                bool firstBin = true;
                if (s.delayBins != null)
                    for (int bin = 0; bin < CoopDelayBins; bin++)
                    {
                        if (s.delayBins[bin] == 0) continue;
                        if (!firstBin) builder.Append('/');
                        firstBin = false;
                        builder.Append(bin).Append('=').Append(s.delayBins[bin]);
                        s.delayBins[bin] = 0;
                    }
                s.presented = s.interpolated = s.moving = s.stalls = s.arrivals = s.continuous = s.starved = s.late = s.pausedHeld = s.pausedMoving = 0;
                s.moveContinuous = s.moveStarved = s.approvedHolds = 0;
                s.stepSum = s.delaySum = s.starvedMs = s.marginSum = s.cpuUs = s.moveStarvedMs = s.approvedHoldMs = s.lagSum = s.holdSum = s.ageSum = 0;
                s.stepMax = s.delayMax = s.lagMax = s.holdMax = s.ageMax = 0; s.marginMin = double.MaxValue;
            }
            if (!first) GameplayLatencyDiagnostics.Write(GameplayLatencyDiagnostics.For("coop_present_window", null, detail: builder.ToString()));
            coopPresentIdle.Clear();
            foreach (var pair in coopPresentStats) if (!pair.Value.hasLast && pair.Value.zeroRun == 0 && !pair.Value.movedBefore) coopPresentIdle.Add(pair.Key);
            foreach (int id in coopPresentIdle) coopPresentStats.Remove(id);
        }
    }
}
