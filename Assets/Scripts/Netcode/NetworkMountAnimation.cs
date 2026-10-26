using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    // Replicate every animated branch, including simultaneous dash/shoot effects.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(NetworkIdentity), typeof(PlayerMountCompanion))]
    public class NetworkMountAnimation : NetworkBehaviour
    {
        public readonly SyncList<NetworkWorldMount.MountFrame> frames = new();
        PlayerMountCompanion companion;
        GameObject trackedMount;
        AnimatedSpriteRenderer[] renderers;
        SpriteRenderer[] sprites;
        float nextSample;
        GreenLouieDashAnimator greenDash;
        BlackLouieDashAnimator blackDash;
        MountVisualController ownerVisual;

        void Awake()
        {
            companion = GetComponent<PlayerMountCompanion>();
            syncInterval = 0.03f;
            syncDirection = SyncDirection.ServerToClient;
        }

        void LateUpdate()
        {
            if (!NetSync.IsOnline || netIdentity == null || (!isServer && !isClient)) return;
            var mount = companion != null ? companion.GetMountedLouieObject() : null;
            if (mount != trackedMount) Rebind(mount);
            if (renderers == null) return;
            if (isServer)
            {
                if (Time.unscaledTime < nextSample) return;
                nextSample = Time.unscaledTime + syncInterval;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var animation = renderers[i];
                    var sprite = sprites[i];
                    var frame = new NetworkWorldMount.MountFrame
                    {
                        visible = sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy,
                        idle = animation.idle, frame = animation.CurrentFrame,
                        flipX = sprite != null && sprite.flipX, flipY = sprite != null && sprite.flipY,
                        color = sprite != null ? sprite.color : Color.white,
                        localPosition = animation.transform.localPosition
                    };
                    if (i >= frames.Count) frames.Add(frame);
                    else if (!frames[i].Equals(frame)) frames[i] = frame;
                }
                return;
            }
            if (TryGetComponent<NetworkBombFx>(out var bombFx) && (bombFx.RidingArcActive || bombFx.PlayerBounceActive || bombFx.PinkJumpVisualActive || (isLocalPlayer && bombFx.HasLocalMountAbilityPose))) return;
            if (isLocalPlayer && TryGetComponent<MovementController>(out var movement) && movement.PredictLocally && movement.IsMounted && ownerVisual != null)
            {
                ownerVisual.PresentNetworkLocomotion();
                return;
            }
            for (int i = 0; i < renderers.Length && i < frames.Count; i++)
            {
                var animation = renderers[i];
                var sprite = sprites[i];
                var frame = frames[i];
                if (frame.visible)
                    for (var branch = animation.transform; branch != null && branch != trackedMount.transform; branch = branch.parent)
                        if (!branch.gameObject.activeSelf) branch.gameObject.SetActive(true);
                animation.idle = frame.idle;
                animation.CurrentFrame = frame.frame;
                animation.RefreshFrame();
                animation.transform.localPosition = frame.localPosition;
                if (sprite == null) continue;
                sprite.enabled = frame.visible;
                sprite.flipX = frame.flipX; sprite.flipY = frame.flipY;
                sprite.color = frame.color;
                if (frame.visible && greenDash != null) greenDash.PresentNetworkAfterimage(animation);
                if (frame.visible && blackDash != null) blackDash.PresentNetworkAfterimage(animation);
            }
        }

        void Rebind(GameObject mount)
        {
            trackedMount = mount;
            ownerVisual = mount != null ? mount.GetComponentInChildren<MountVisualController>(true) : null;
            if (!isServer && isLocalPlayer && ownerVisual != null && TryGetComponent<MovementController>(out var movement))
            {
                movement.SetUseHeadOnlyWhenMounted(ownerVisual.useHeadOnlyPlayerVisual);
                movement.SetMountedSpritesLocalYOverride(companion.GetMountedLouieType() == MountedType.Pink, movement.pinkMountedSpritesLocalY);
            }
            greenDash = mount != null ? mount.GetComponentInChildren<GreenLouieDashAnimator>(true) : null;
            blackDash = mount != null ? mount.GetComponentInChildren<BlackLouieDashAnimator>(true) : null;
            renderers = mount != null ? mount.GetComponentsInChildren<AnimatedSpriteRenderer>(true) : null;
            sprites = renderers != null ? new SpriteRenderer[renderers.Length] : null;
            if (isServer) frames.Clear();
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                sprites[i] = renderers[i].GetComponent<SpriteRenderer>();
                if (!isServer) renderers[i].SetManualAnimationUpdate(true);
            }
            if (!isServer)
                foreach (var behaviour in mount.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.StopAllCoroutines();
                    behaviour.enabled = false;
                }
            if (isLocalPlayer) GetComponent<NetworkBombFx>()?.ReapplyMountAbilityPose();
        }
    }
}
