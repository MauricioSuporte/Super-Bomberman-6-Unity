using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // A renderer pose relative to its replicated actor, never a gameplay transform.
    public struct NetworkVisualPose : System.IEquatable<NetworkVisualPose>
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public byte mask;
        public bool valid;
        public float offsetLimit;

        public bool Equals(NetworkVisualPose other) => valid == other.valid && mask == other.mask &&
            position.Equals(other.position) && scale.Equals(other.scale) && rotation.Equals(other.rotation) && offsetLimit.Equals(other.offsetLimit);

        static Vector3 Divide(Vector3 value, Vector3 divisor) => new(
            Mathf.Abs(divisor.x) > 0.000001f ? value.x / divisor.x : 0,
            Mathf.Abs(divisor.y) > 0.000001f ? value.y / divisor.y : 0,
            Mathf.Abs(divisor.z) > 0.000001f ? value.z / divisor.z : 0);

        public static NetworkVisualPose Capture(Transform actor, SpriteRenderer renderer)
        {
            if (actor == null || renderer == null) return default;
            return new NetworkVisualPose { valid = true, position = actor.InverseTransformPoint(renderer.transform.position),
                rotation = Quaternion.Inverse(actor.rotation) * renderer.transform.rotation,
                scale = Divide(renderer.transform.lossyScale, actor.lossyScale), mask = (byte)renderer.maskInteraction,
                offsetLimit = actor.TryGetComponent<NetworkPlayerInput>(out var input) ? input.AuthorityVisualOffsetLimit : 4f };
        }

        public static float ApprovedOffsetLimit(NetworkVisualPose from, NetworkVisualPose to)
        {
            // The authority can approve a large, rapid lift of presentation children.
            // Root teleports still use their separate discontinuity rule. Retain the
            // boundary's approval when interpolating the final return to rest.
            float a = from.valid && !float.IsNaN(from.offsetLimit) && !float.IsInfinity(from.offsetLimit) ? from.offsetLimit : 4f;
            float b = to.valid && !float.IsNaN(to.offsetLimit) && !float.IsInfinity(to.offsetLimit) ? to.offsetLimit : 4f;
            return Mathf.Clamp(Mathf.Max(a, b), 4f, 128f);
        }

        public static NetworkVisualPose Interpolate(NetworkVisualPose from, NetworkVisualPose to, float t)
        {
            if (!from.valid || !to.valid) return from;
            from.position = Vector3.Lerp(from.position, to.position, t);
            from.scale = Vector3.Lerp(from.scale, to.scale, t);
            from.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
            return from; // The mask mode is discrete, just like the selected frame.
        }

        public void Apply(Transform actor, SpriteRenderer renderer)
        {
            if (!valid || actor == null || renderer == null) return;
            renderer.maskInteraction = (SpriteMaskInteraction)mask;
            if (renderer.transform == actor) return; // Never move/scale a gameplay root.
            renderer.transform.SetPositionAndRotation(actor.TransformPoint(position), actor.rotation * rotation);
            var parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
            renderer.transform.localScale = Divide(Vector3.Scale(actor.lossyScale, scale), parentScale);
        }

        public struct Baseline
        {
            public AnimatedSpriteRenderer renderer;
            public Vector3 position, scale, offset;
            public Quaternion rotation;
            public SpriteMaskInteraction mask;
            public bool gameplayRoot;
        }

        public static Baseline[] CaptureBaseline(AnimatedSpriteRenderer[] renderers, Transform actor)
        {
            if (renderers == null) return null;
            var result = new Baseline[renderers.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                result[i] = new Baseline { renderer = renderer, gameplayRoot = renderer.transform == actor, position = renderer.transform.localPosition,
                    scale = renderer.transform.localScale, rotation = renderer.transform.localRotation,
                    offset = renderer.RuntimeBaseLocalOffset,
                    mask = renderer.TryGetComponent<SpriteRenderer>(out var sprite) ? sprite.maskInteraction : SpriteMaskInteraction.None };
            }
            return result;
        }

        public static void RestoreBaseline(Baseline[] baseline)
        {
            if (baseline == null) return;
            foreach (var state in baseline)
            {
                if (state.renderer == null) continue;
                if (!state.gameplayRoot)
                {
                    state.renderer.SetRuntimeBaseLocalOffset(state.offset);
                    state.renderer.transform.SetLocalPositionAndRotation(state.position, state.rotation);
                    state.renderer.transform.localScale = state.scale;
                }
                if (state.renderer.TryGetComponent<SpriteRenderer>(out var sprite)) sprite.maskInteraction = state.mask;
            }
        }
    }
}
