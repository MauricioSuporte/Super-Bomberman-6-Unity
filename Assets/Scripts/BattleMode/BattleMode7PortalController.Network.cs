using System.Collections.Generic;
using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

public sealed partial class BattleMode7PortalController
{
    readonly List<GameObject> activeNetworkPortalStars = new();
    uint portalStarRevision, receivedPortalStarRevision, clearedPortalStarRevision;
    string networkPortalPath;
    public string NetworkPortalPath
    {
        get
        {
            if (networkPortalPath != null) return networkPortalPath;
            networkPortalPath = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) networkPortalPath = parent.name + "/" + networkPortalPath;
            return networkPortalPath;
        }
    }

    void PublishNetworkPortalStar(Transform star, Sprite sprite, Vector2 drift, float spin, bool clear = false)
    {
        // Co-op already streams these world sprites. Battle needs the same
        // approved random choices without running the native portal on clients.
        if (!NetSync.IsServer || !NetworkServer.active || NetworkServer.isLoadingScene ||
            BombermanNetworkManager.IsCoopStage || !IsSupportedSceneActive()) return;
        NetworkServer.SendToReady(new PortalStarView { scene = gameObject.scene.name, path = NetworkPortalPath,
            revision = ++portalStarRevision, time = NetworkTime.localTime, clear = clear,
            position = star != null ? star.position : Vector3.zero, scale = star != null ? star.localScale : Vector3.one,
            angle = star != null ? star.eulerAngles.z : 0, drift = drift, spin = spin,
            life = teleportStarLifetime, frameSeconds = teleportStarAnimationFrameTime,
            sprite = teleportStarSprites != null ? System.Array.IndexOf(teleportStarSprites, sprite) : -1,
            sorting = teleportStarSortingOrder });
    }

    public void ReceiveNetworkPortalStar(PortalStarView view)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active ||
            BombermanNetworkManager.IsCoopStage || !isActiveAndEnabled || view.scene != gameObject.scene.name ||
            view.path != NetworkPortalPath || !NetworkInputHistory.IsNewer(view.revision, receivedPortalStarRevision)) return;
        receivedPortalStarRevision = view.revision;
        if (view.clear) { clearedPortalStarRevision = view.revision; ClearNetworkPortalStars(); return; }
        if ((float)(NetworkBombFx.KickServerNow - view.time) > Mathf.Max(1, view.life + 0.5f)) return;
        StartCoroutine(PresentNetworkPortalStar(view));
    }

    System.Collections.IEnumerator PresentNetworkPortalStar(PortalStarView view)
    {
        double offset = 0;
        if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<NetworkTransformReliable>(out var sync))
            offset = NetworkClient.sendInterval * (sync.sendIntervalMultiplier - 1 + (sync.timelineOffset ? sync.sendIntervalMultiplier : 0));
        float pausedWait = 0;
        while (GamePauseController.IsPaused || NetworkTime.time < view.time + offset)
        {
            if (!isActiveAndEnabled || !NetworkClient.active || !NetworkInputHistory.IsNewer(view.revision, clearedPortalStarRevision)) yield break;
            if (GamePauseController.IsPaused) pausedWait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!isActiveAndEnabled || !NetworkClient.active || !NetworkInputHistory.IsNewer(view.revision, clearedPortalStarRevision)) yield break;
        float age = Mathf.Clamp((float)(NetworkTime.time - view.time - offset) - pausedWait, 0, 0.15f);
        var star = CreateNetworkPortalStar(view, age);
        if (star != null) yield return AnimateTeleportStar(star.transform, star.GetComponent<SpriteRenderer>(), view.drift, view.spin,
            age, view.life, view.frameSeconds);
    }

    public GameObject CreateNetworkPortalStar(PortalStarView view, float age)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || NetworkServer.active || view.clear ||
            view.scene != gameObject.scene.name || view.path != NetworkPortalPath || teleportStarSprites == null ||
            view.sprite < 0 || view.sprite >= teleportStarSprites.Length || teleportStarSprites[view.sprite] == null) return null;
        var star = new GameObject("ApprovedPortalStar"); star.transform.SetParent(transform, worldPositionStays: true);
        star.transform.SetPositionAndRotation(view.position + (Vector3)view.drift * age, Quaternion.Euler(0, 0, view.angle + view.spin * age));
        star.transform.localScale = view.scale;
        var renderer = star.AddComponent<SpriteRenderer>(); renderer.sprite = teleportStarSprites[view.sprite]; renderer.sortingOrder = view.sorting;
        activeNetworkPortalStars.Add(star);
        return star;
    }

    void ClearNetworkPortalStars()
    {
        PublishNetworkPortalStar(null, null, Vector2.zero, 0, clear: true);
        foreach (var star in activeNetworkPortalStars)
        {
            if (star == null) continue;
            if (Application.isPlaying) Destroy(star); else DestroyImmediate(star);
        }
        activeNetworkPortalStars.Clear();
    }
}
