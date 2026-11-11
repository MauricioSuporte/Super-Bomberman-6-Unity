using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Render-only values from the authoritative absorption effect. No client attack simulation.
    public sealed class CoopAbsorptionLight : IEquatable<CoopAbsorptionLight>
    {
        public float amount;
        public bool IsValid() => !float.IsNaN(amount) && !float.IsInfinity(amount) && amount >= 0 && amount <= 1;
        public bool Equals(CoopAbsorptionLight other) => other != null && amount == other.amount;
    }

    public partial class BombermanNetworkManager
    {
        Material coopAbsorptionTemplate;
        MaterialPropertyBlock coopAbsorptionProperties;
        readonly HashSet<SpriteRenderer> coopAbsorptionViews = new();
        readonly Dictionary<int, float> coopAbsorptionSources = new();
        static readonly int CoopLightAmount = Shader.PropertyToID("_LightAmount");

        internal static CoopAbsorptionLight CaptureCoopAbsorption(SpriteRenderer renderer)
        {
            if (singleton is BombermanNetworkManager manager &&
                manager.coopAbsorptionSources.TryGetValue(renderer.GetEntityId().GetHashCode(), out float amount))
                return new CoopAbsorptionLight { amount = amount };
            var material = renderer.sharedMaterial;
            if (material == null || material.shader == null ||
                material.shader.name != "SuperBomberman/Freezer Venus Absorption") return null;
            return new CoopAbsorptionLight { amount = material.GetFloat(CoopLightAmount) };
        }

        // Dedicated-server builds can strip shaders. Publish producer data, not GPU state.
        public static void CoopSetAbsorptionLight(SpriteRenderer renderer, float? amount)
        {
            if (!Mirror.NetworkServer.active || !IsCoopStage || renderer == null ||
                singleton is not BombermanNetworkManager manager) return;
            int id = renderer.GetEntityId().GetHashCode();
            if (amount.HasValue && !float.IsNaN(amount.Value) && !float.IsInfinity(amount.Value) && amount.Value >= 0 && amount.Value <= 1)
                manager.coopAbsorptionSources[id] = amount.Value;
            else manager.coopAbsorptionSources.Remove(id);
        }

        internal bool ApplyCoopAbsorption(SpriteRenderer view, CoopAbsorptionLight light)
        {
            if (!light.IsValid()) return false;
            if (coopAbsorptionTemplate == null)
            {
                var shader = Resources.Load<Shader>("StageAssets/FreezerVenusAbsorption");
                if (shader == null) return false;
                coopAbsorptionTemplate = new Material(shader) { name = "Approved absorption light" };
            }
            view.sharedMaterial = coopAbsorptionTemplate;
            coopAbsorptionProperties ??= new MaterialPropertyBlock();
            coopAbsorptionProperties.Clear();
            coopAbsorptionProperties.SetFloat(CoopLightAmount, light.amount);
            view.SetPropertyBlock(coopAbsorptionProperties);
            coopAbsorptionViews.Add(view);
            return true;
        }

        void ReleaseCoopAbsorption(SpriteRenderer view)
        {
            if (coopAbsorptionViews.Remove(view) && view != null) view.SetPropertyBlock(null);
        }

        void ClearCoopAbsorption()
        {
            foreach (var view in coopAbsorptionViews) if (view != null) view.SetPropertyBlock(null);
            coopAbsorptionViews.Clear(); coopAbsorptionSources.Clear();
#if UNITY_EDITOR || DEBUG
            r06SampleTimes.Clear();
#endif
            if (coopAbsorptionTemplate != null) DestroyCoopMaskMaterial(coopAbsorptionTemplate);
            coopAbsorptionTemplate = null; coopAbsorptionProperties = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEBUG")]
        void RecordR06Boss(string kind, SpriteRenderer renderer, CoopSpriteFrame frame, double clock, double stateTime, double newest)
        {
#if UNITY_EDITOR || DEBUG
            if (!R06Probe || !GameplayLatencyDiagnostics.Enabled || frame.enemyStepLimit <= 0) return;
            if (kind == "r06_view" && frame.mask == (byte)SpriteMaskInteraction.VisibleInsideMask)
                LocalBossSmoke.ObserveFloorReveal();
            bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff;
            double now = Time.realtimeSinceStartupAsDouble;
            int sprite = R03SpriteKey(renderer.sprite);
            float light = frame.absorption?.amount ?? -1;
            if (r06SampleTimes.TryGetValue(frame.id, out var previous) && previous.visible == visible &&
                (!visible || (previous.sprite == sprite && previous.mask == frame.mask && previous.light == light && now < previous.next))) return;
            r06SampleTimes[frame.id] = (now + .05, visible, sprite, frame.mask, light);
            var row = GameplayLatencyDiagnostics.For(kind, detail: "id=" + frame.id +
                ";ptime=" + clock.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ";state=" + stateTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ";newest=" + newest.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ";stateMeasured=1;animating=" + (frame.animation != 0 && frame.playback.speed > 0 && !frame.playback.idle && !frame.playback.frozen ? 1 : 0) +
                ";branch=" + renderer.name + ";spriteName=" + (renderer.sprite != null ? renderer.sprite.name : "none") +
                ";sprite=" + R03SpriteKey(renderer.sprite) + ";mask=" + (byte)renderer.maskInteraction +
                ";visible=" + (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff ? 1 : 0) +
                ";light=" + (frame.absorption?.amount ?? -1).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ";paused=" + (GamePauseController.IsPaused ? 1 : 0));
            row.position = renderer.transform.position;
            GameplayLatencyDiagnostics.Write(row);
#endif
        }
#if UNITY_EDITOR || DEBUG
        readonly Dictionary<int, (double next, bool visible, int sprite, byte mask, float light)> r06SampleTimes = new();
        static readonly bool R06Probe = Array.IndexOf(Environment.GetCommandLineArgs(), "-r06-bosses") >= 0;
#endif

        internal static bool IsCoopBossRenderer(SpriteRenderer renderer)
        {
            if (renderer.GetComponentInParent<SunMaskBoss>(true) != null ||
                renderer.GetComponentInParent<ClownMaskBoss>(true) != null ||
                renderer.GetComponentInParent<FreezerVenusBoss>(true) != null) return true;
            var movement = renderer.GetComponentInParent<MovementControllerAI>(true);
            return movement != null && movement.isBoss;
        }
    }
}
