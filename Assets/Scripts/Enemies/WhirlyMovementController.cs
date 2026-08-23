using UnityEngine;

/// <summary>
/// Turns at junctions like a normal JunctionTurning enemy. When the tile ahead
/// is blocked by Stage geometry and Whirly has to turn away, it triggers the
/// same destructible-tile destruction flow used by bombs, without an explosion.
/// </summary>
public sealed class WhirlyMovementController : JunctionTurningEnemyMovementController
{
    [SerializeField] private LayerMask stageLayerMask;

    private BombController bombController;
    protected override void Awake()
    {
        base.Awake();

        if (stageLayerMask.value == 0)
            stageLayerMask = LayerMask.GetMask("Stage");

    }

    protected override void DecideNextTile()
    {
        Vector2 forwardTile = rb.position + direction * tileSize;
        bool blockedAhead = IsTileBlocked(forwardTile);
        Vector2 previousDirection = direction;

        base.DecideNextTile();

        bool forcedToTurn = blockedAhead && direction != previousDirection && targetTile != rb.position;
        if (forcedToTurn && HasStageTileAt(forwardTile))
            TriggerTileEffect(forwardTile);
    }

    private bool HasStageTileAt(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * tileSize * 0.8f;
        return Physics2D.OverlapBox(worldPosition, size, 0f, stageLayerMask) != null;
    }

    private void TriggerTileEffect(Vector2 blockedTile)
    {
        if (bombController == null)
        {
            BombController[] controllers = FindObjectsByType<BombController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].CompareTag("Player"))
                {
                    bombController = controllers[i];
                    break;
                }
            }
        }

        if (bombController == null)
            return;

        bombController.TriggerDestructibleTileEffectWithoutExplosion(blockedTile);
    }
}
