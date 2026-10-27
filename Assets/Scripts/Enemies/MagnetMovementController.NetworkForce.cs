using Assets.Scripts.Netcode;
using UnityEngine;

public sealed partial class MagnetMovementController
{
    public NetworkEnemyForceState CaptureNetworkForce(MovementController actor, NetworkEnemyForceState receipt)
    {
        if (!NetSync.IsServer || isDead || rb == null || !_isMagnetActive || receipt.kind != NetworkEnemyForceKind.Magnet) return default;
        receipt.anchor = new Vector2(Mathf.Round(rb.position.x / tileSize) * tileSize, Mathf.Round(rb.position.y / tileSize) * tileSize);
        receipt.speed = pullSpeed; receipt.remaining = NetworkEnemyForceState.MaximumSeconds;
        // Stay strictly inside the server's tile-scan corridor; leaving it needs
        // a new approved target/direction rather than choosing another ray locally.
        receipt.alignment = tileSize * 0.15f; receipt.range = Mathf.Max(1, visionTiles) * tileSize;
        return receipt;
    }
}
