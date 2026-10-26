using Assets.Scripts.Netcode;
using Mirror;
using UnityEngine;

namespace Assets.Scripts.Netcode
{
    public struct FallingBombTarget : NetworkMessage
    {
        public string scene;
        public uint revision;
        public double time;
        public Vector2 position;
        public float remaining, fuse;
        public int radius;
        public bool active, paused;
    }
}

public sealed partial class BattleMode2FallingBombController
{
    uint targetRevision, receivedTargetRevision;
    double receivedTargetTime = double.NegativeInfinity;
    float nextTargetSample, clientTargetRemaining, clientTargetAge;
    bool clientTargetPaused;

    void PublishTarget(bool changed = false)
    {
        if (!NetworkServer.active || !NetSync.IsServer || NetworkServer.isLoadingScene || !IsBattleMode2Active()) return;
        if (changed) ++targetRevision;
        else if (Time.unscaledTime < nextTargetSample) return;
        nextTargetSample = Time.unscaledTime + 0.1f;
        NetworkServer.SendToReady(new FallingBombTarget { scene = gameObject.scene.name, revision = targetRevision,
            time = NetworkTime.localTime, position = targetWarningWorldPosition, remaining = targetWarningSecondsRemaining,
            fuse = bombFuseSeconds, radius = bombFireLevel, active = targetWarningActive, paused = GamePauseController.IsPaused });
    }

    public void ReceiveNetworkTarget(FallingBombTarget message)
    {
        if (NetSync.Mode != NetSync.NetMode.Client || NetworkServer.active || !NetworkClient.active ||
            message.scene != gameObject.scene.name || message.scene != BattleMode2SceneName || !isActiveAndEnabled ||
            (message.revision != receivedTargetRevision && !NetworkInputHistory.IsNewer(message.revision, receivedTargetRevision)) ||
            (message.revision == receivedTargetRevision && message.time <= receivedTargetTime)) return;
        receivedTargetRevision = message.revision; receivedTargetTime = message.time;
        targetWarningActive = message.active; targetWarningWorldPosition = message.position;
        clientTargetPaused = message.paused;
        float age = message.paused || GamePauseController.IsPaused ? 0 : Mathf.Max(0, (float)(NetworkBombFx.KickServerNow - message.time));
        clientTargetRemaining = Mathf.Max(0, message.remaining - age); clientTargetAge = 0;
        targetWarningSecondsRemaining = clientTargetRemaining;
        bombFuseSeconds = Mathf.Max(0.01f, message.fuse); bombFireLevel = Mathf.Max(1, message.radius);
        EnsureTargetRenderer();
        if (targetRenderer != null)
        {
            targetRenderer.transform.position = new Vector3(message.position.x, message.position.y, targetRenderer.transform.position.z);
            targetRenderer.enabled = message.active;
        }
    }

    void Update()
    {
        if (NetSync.Mode != NetSync.NetMode.Client || !NetworkClient.active || !targetWarningActive) return;
        if (GamePauseController.IsPaused || clientTargetPaused) return;
        clientTargetAge += Time.unscaledDeltaTime;
        targetWarningSecondsRemaining = Mathf.Max(0, clientTargetRemaining - clientTargetAge);
        if (targetRenderer != null)
        {
            Color color = targetRenderer.color;
            color.a = Mathf.Lerp(targetMinAlpha, targetMaxAlpha, Mathf.PingPong(Time.time * targetBlinkSpeed, 1));
            targetRenderer.color = color;
        }
        // Reaching the display deadline never launches a client bomb. Only the
        // server's inactive event removes the warning; spawn remains authoritative.
    }
}
