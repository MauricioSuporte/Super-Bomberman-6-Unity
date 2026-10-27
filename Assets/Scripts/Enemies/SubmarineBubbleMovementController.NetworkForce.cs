using Assets.Scripts.Netcode;
using UnityEngine;

public sealed partial class SubmarineBubbleMovementController
{
    public NetworkEnemyForceState CaptureNetworkForce(MovementController actor, NetworkEnemyForceState receipt)
    {
        if (!NetSync.IsServer || isDead || rb == null || !pushedPlayers.Contains(actor) || receipt.kind != NetworkEnemyForceKind.Bubble) return default;
        receipt.anchor = rb.position; receipt.speed = Mathf.Max(0.1f, playerPushSpeed);
        receipt.remaining = Mathf.Min(NetworkEnemyForceState.MaximumSeconds, receipt.remainingDistance / receipt.speed);
        receipt.alignment = 0; receipt.range = Mathf.Max(tileSize, 0.01f);
        return receipt;
    }
}
