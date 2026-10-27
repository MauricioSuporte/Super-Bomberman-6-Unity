using Assets.Scripts.Netcode;
using UnityEngine;

public sealed partial class HurricaneMovementController
{
    public NetworkEnemyForceState CaptureNetworkForce(MovementController actor, NetworkEnemyForceState receipt)
    {
        if (!NetSync.IsServer || isDead || rb == null || !IsPullFrame() || receipt.kind != NetworkEnemyForceKind.Hurricane) return default;
        float budget = activeSprite.RemainingCurrentFrameSeconds;
        // Adjacent active frames are one already-known pull phase. Do not invent
        // a new cycle, ping-pong turn or another AI decision beyond that phase.
        if (!activeSprite.pingPong && activeSprite.animationSprite != null)
            for (int frame = activeSprite.CurrentFrame + 1; frame < activeSprite.animationSprite.Length && IsPullFrame(frame) && budget < NetworkEnemyForceState.MaximumSeconds; frame++)
                budget += activeSprite.FrameDurationAt(frame);
        receipt.anchor = rb.position; receipt.speed = pullSpeedTilesPerSecond * tileSize;
        receipt.remaining = Mathf.Min(NetworkEnemyForceState.MaximumSeconds, budget);
        receipt.alignment = tileSize * 0.15f; receipt.range = visionTiles * tileSize;
        return receipt;
    }
}
