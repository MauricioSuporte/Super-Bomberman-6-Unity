using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public partial class BombermanNetworkManager
    {
        sealed class PresentationMetric
        {
            public int frames, changes, payloadBytes;
            public double cpuMs;
        }
        readonly Dictionary<int, string> coopMetricFamilies = new();
        readonly Dictionary<string, PresentationMetric> coopPresentationMetrics = new();
        double coopMetricNextFlush;

        string CoopPresentationFamily(SpriteRenderer renderer)
        {
            int id = renderer.GetEntityId().GetHashCode();
            if (coopMetricFamilies.TryGetValue(id, out string family)) return family;
            var enemy = renderer.GetComponentInParent<EnemyMovementController>(true);
            if (enemy != null) return coopMetricFamilies[id] = enemy.GetType().Name;
            for (var current = renderer.transform; current != null; current = current.parent)
                foreach (var behaviour in current.GetComponents<MonoBehaviour>())
                    if (behaviour != null && behaviour is not AnimatedSpriteRenderer && behaviour is not CharacterHealth &&
                        behaviour.GetType().Assembly == typeof(GameManager).Assembly)
                        return coopMetricFamilies[id] = behaviour.GetType().Name;
            return coopMetricFamilies[id] = "UnclassifiedWorldVisual";
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
            if (Time.realtimeSinceStartupAsDouble < coopMetricNextFlush) return;
            coopMetricNextFlush = Time.realtimeSinceStartupAsDouble + 1;
            foreach (var pair in coopPresentationMetrics)
            {
                var metric = pair.Value;
                var entry = GameplayLatencyDiagnostics.For("coop_family_cost", null,
                    detail: "family=" + pair.Key + ";sampled=" + metric.frames + ";changed=" + metric.changes +
                        ";frame_payload_bytes=" + metric.payloadBytes + ";capture_cpu_ms=" + metric.cpuMs.ToString("R", CultureInfo.InvariantCulture) +
                        ";excludes_envelopes_asset_definitions_peer_fanout_and_diagnostic_overhead");
                GameplayLatencyDiagnostics.Write(entry);
            }
            coopPresentationMetrics.Clear();
        }

        void ClearCoopPresentationMetrics()
        {
            coopMetricFamilies.Clear(); coopPresentationMetrics.Clear(); coopMetricNextFlush = 0;
        }
    }
}
