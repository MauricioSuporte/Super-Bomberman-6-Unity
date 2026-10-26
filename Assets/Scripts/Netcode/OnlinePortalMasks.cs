using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Netcode
{
    public struct PortalMaskView
    {
        public int id, backLayer, frontLayer, backOrder, frontOrder;
        public Vector3 position, scale;
        public Quaternion rotation;
        public float cutoff;
        public bool custom;
    }
    public struct PortalMaskSnapshot : NetworkMessage
    {
        public string scene;
        public uint revision;
        public double time;
        public PortalMaskView[] masks;
    }

    public partial class BombermanNetworkManager
    {
        readonly Dictionary<int, SpriteMask> portalMaskViews = new();
        readonly List<PortalMaskSnapshot> portalMaskHistory = new(64);
        readonly List<PortalMaskView> portalMaskSend = new(8);
        readonly HashSet<int> portalMaskCurrent = new();
        readonly List<int> portalMaskRemoved = new();
        GameObject portalMaskRoot;
        Sprite portalMaskSprite;
        uint portalMaskServerRevision, portalMaskReceivedRevision;
        float nextPortalMaskSample;
        bool portalMasksWereActive;

        public static void NotifyPortalMasksChanged()
        {
            if (NetworkServer.active && singleton is BombermanNetworkManager manager) manager.PublishPortalMasks(true);
        }

        void PublishPortalMasks(bool force = false)
        {
            if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene ||
                (!IsCoopStage && !IsBattleArena)) return;
            if (!force && Time.unscaledTime < nextPortalMaskSample) return;
            nextPortalMaskSample = Time.unscaledTime + 0.1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Stage_3-4" && scene.name != "BattleMode_7") return;
            portalMaskSend.Clear();
            foreach (var mask in FindObjectsByType<SpriteMask>())
            {
                if (!mask.enabled || mask.sprite == null || mask.gameObject.scene != scene || mask.name != "Stage34PortalFixedMask") continue;
                portalMaskSend.Add(new PortalMaskView { id = mask.GetEntityId().GetHashCode(), position = mask.transform.position,
                    rotation = mask.transform.rotation, scale = mask.transform.lossyScale, cutoff = mask.alphaCutoff,
                    custom = mask.isCustomRangeActive, backLayer = mask.backSortingLayerID, frontLayer = mask.frontSortingLayerID,
                    backOrder = mask.backSortingOrder, frontOrder = mask.frontSortingOrder });
                if (portalMaskSend.Count == 64) break;
            }
            if (!force && portalMaskSend.Count == 0 && !portalMasksWereActive) return;
            portalMasksWereActive = portalMaskSend.Count > 0;
            NetworkServer.SendToReady(new PortalMaskSnapshot { scene = scene.name, revision = ++portalMaskServerRevision,
                time = NetworkTime.localTime, masks = portalMaskSend.ToArray() });
        }

        public void ApplyPortalMasks(PortalMaskSnapshot message)
        {
            if (NetSync.Mode != NetSync.NetMode.Client || NetworkServer.active || !NetworkClient.active ||
                message.scene != SceneManager.GetActiveScene().name || message.masks == null || message.masks.Length > 64 ||
                !NetworkInputHistory.IsNewer(message.revision, portalMaskReceivedRevision)) return;
            portalMaskReceivedRevision = message.revision;
            if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
                message.time += NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
            portalMaskHistory.Add(message);
            if (portalMaskHistory.Count > 64) portalMaskHistory.RemoveAt(0);
        }

        void PresentPortalMaskViews()
        {
            if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
                GamePauseController.IsPaused || portalMaskHistory.Count == 0) return;
            double timeline = NetworkTime.time;
            if (portalMaskHistory[0].time > timeline) return;
            while (portalMaskHistory.Count > 1 && portalMaskHistory[1].time <= timeline) portalMaskHistory.RemoveAt(0);
            if (portalMaskRoot == null)
            {
                portalMaskViews.Clear(); portalMaskRoot = new GameObject("ApprovedPortalMasks");
                SceneManager.MoveGameObjectToScene(portalMaskRoot, SceneManager.GetActiveScene());
            }
            if (portalMaskSprite == null)
                portalMaskSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            portalMaskCurrent.Clear();
            foreach (var frame in portalMaskHistory[0].masks)
            {
                portalMaskCurrent.Add(frame.id);
                if (!portalMaskViews.TryGetValue(frame.id, out var mask) || mask == null)
                {
                    var go = new GameObject("ApprovedPortalMask " + frame.id); go.transform.SetParent(portalMaskRoot.transform, false);
                    mask = go.AddComponent<SpriteMask>(); portalMaskViews[frame.id] = mask;
                }
                mask.transform.SetPositionAndRotation(frame.position, frame.rotation); mask.transform.localScale = frame.scale;
                mask.sprite = portalMaskSprite; mask.alphaCutoff = frame.cutoff; mask.isCustomRangeActive = frame.custom;
                mask.backSortingLayerID = frame.backLayer; mask.frontSortingLayerID = frame.frontLayer;
                mask.backSortingOrder = frame.backOrder; mask.frontSortingOrder = frame.frontOrder;
            }
            portalMaskRemoved.Clear();
            foreach (var pair in portalMaskViews)
                if (!portalMaskCurrent.Contains(pair.Key)) portalMaskRemoved.Add(pair.Key);
            foreach (int id in portalMaskRemoved)
            {
                if (portalMaskViews[id] != null) DestroyPortalMaskObject(portalMaskViews[id].gameObject);
                portalMaskViews.Remove(id);
            }
        }

        static void DestroyPortalMaskObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        void ClearPortalMaskViews()
        {
            if (portalMaskRoot != null) DestroyPortalMaskObject(portalMaskRoot);
            if (portalMaskSprite != null) DestroyPortalMaskObject(portalMaskSprite);
            portalMaskRoot = null; portalMaskSprite = null; portalMaskViews.Clear(); portalMaskHistory.Clear();
            portalMaskSend.Clear(); portalMaskCurrent.Clear(); portalMaskRemoved.Clear();
            portalMaskReceivedRevision = portalMaskServerRevision = 0; nextPortalMaskSample = 0; portalMasksWereActive = false;
        }
    }
}
