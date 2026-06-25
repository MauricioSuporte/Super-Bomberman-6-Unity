using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bomb))]
public sealed class MagnetBomb : MonoBehaviour
{
    [Header("Scan")]
    [SerializeField, Min(0.01f)] private float scanInterval = 0.08f;
    [SerializeField, Min(1)] private int scanMaxDistanceTiles = 12;

    [Header("Pull")]
    [SerializeField, Min(1)] private int maxPullSteps = 12;
    [SerializeField, Min(1)] private int stopBeforeTargetTiles = 1;

    [Header("Cooldown After Stop")]
    [SerializeField, Min(0f)] private float reattractDelaySeconds = 1f;

    [Header("Detection / Blocking")]
    [FormerlySerializedAs("playerLayerName")]
    [SerializeField] private string targetLayerName = "Player";
    [SerializeField] private string stageLayerName = "Stage";
    [SerializeField] private string bombLayerName = "Bomb";

    [Header("Pull Speed")]
    [SerializeField, Min(0.1f)] private float magnetPullSpeedMultiplier = 1f;
    [SerializeField, Range(0.2f, 0.95f)] private float tileCheckBoxSize = 0.60f;

    [Header("Magnet Safety (like PushIndestructibleTileHandler)")]
    [SerializeField] private LayerMask blockMoveMask;
    [SerializeField, Range(0.1f, 1.5f)] private float overlapBoxSize = 0.60f;

    [Header("Origin Blocker (while moving)")]
    [SerializeField, Range(0.2f, 1.2f)] private float originBlockerSize = 0.90f;
    [SerializeField] private bool originBlockerUseTrigger = false;

    private Bomb bomb;

    private float nextScanTime;
    private bool waitingMovementToEnd;
    private float reattractAllowedAt;

    private int targetMask;
    private int blockMask;

    private static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    private void Awake()
    {
        bomb = GetComponent<Bomb>();
        RefreshLayerMasks();
        reattractAllowedAt = 0f;
    }

    public void SetTargetLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return;

        targetLayerName = layerName;
        RefreshLayerMasks();
    }

    private void RefreshLayerMasks()
    {
        int targetLayer = LayerMask.NameToLayer(targetLayerName);
        int stageLayer = LayerMask.NameToLayer(stageLayerName);
        int bombLayer = LayerMask.NameToLayer(bombLayerName);
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int louieLayer = LayerMask.NameToLayer("Louie");

        targetMask = (targetLayer >= 0) ? (1 << targetLayer) : LayerMask.GetMask(targetLayerName);

        int m = 0;
        if (stageLayer >= 0) m |= (1 << stageLayer);
        if (bombLayer >= 0) m |= (1 << bombLayer);
        if (playerLayer >= 0) m |= (1 << playerLayer);
        if (enemyLayer >= 0) m |= (1 << enemyLayer);
        if (louieLayer >= 0) m |= (1 << louieLayer);
        if (targetLayer >= 0) m |= (1 << targetLayer);

        blockMask = (m != 0)
            ? m
            : LayerMask.GetMask(stageLayerName, bombLayerName, "Player", "Enemy", "Louie", targetLayerName);

        if (blockMoveMask.value == 0)
            blockMoveMask = LayerMask.GetMask("Player", "Bomb", "Enemy", "Louie");
    }

    private void Update()
    {
        if (GamePauseController.IsPaused)
            return;

        if (bomb == null || bomb.HasExploded)
            return;

        if (waitingMovementToEnd)
        {
            if (!bomb.IsBeingMagnetPulled)
            {
                waitingMovementToEnd = false;
                reattractAllowedAt = Time.time + Mathf.Max(0f, reattractDelaySeconds);
            }

            return;
        }

        if (Time.time < reattractAllowedAt)
            return;

        if (!bomb.CanBeMagnetPulled)
            return;

        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + Mathf.Max(0.01f, scanInterval);

        if (!TryGetMagnetContext(out float tileSize, out LayerMask obstacleMask, out Tilemap destructibleTilemap))
            return;

        RefreshDynamicTargetAndBlockMasks();

        Vector2 origin = SnapToGrid(transform.position, tileSize);

        if (!TryFindTargetInLine(origin, tileSize, out Vector2 dir, out int stepsToTarget))
            return;

        int stopDistance = Mathf.Max(1, stopBeforeTargetTiles);
        if (stepsToTarget <= stopDistance)
            return;

        int steps = Mathf.Clamp(stepsToTarget - stopDistance, 1, Mathf.Max(1, maxPullSteps));

        if (bomb.StartMagnetPull(
                dir,
                tileSize,
                steps,
                obstacleMask,
                destructibleTilemap,
                magnetPullSpeedMultiplier,
                blockMoveMask,
                overlapBoxSize,
                originBlockerSize,
                originBlockerUseTrigger))
        {
            waitingMovementToEnd = true;
        }
    }

    private bool TryGetMagnetContext(out float tileSize, out LayerMask obstacleMask, out Tilemap destructibleTilemap)
    {
        tileSize = 1f;
        obstacleMask = default;
        destructibleTilemap = null;

        BombController owner = bomb != null ? bomb.Owner : null;
        if (owner != null)
        {
            if (owner.TryGetComponent<MovementController>(out var mv))
            {
                tileSize = Mathf.Max(0.0001f, mv.tileSize);
                obstacleMask = mv.obstacleMask;
            }
            else
            {
                tileSize = 1f;
                obstacleMask = LayerMask.GetMask("Stage", "Bomb", "Player");
            }

            destructibleTilemap = owner.destructibleTiles;
            return true;
        }

        var selfMv = GetComponentInParent<MovementController>();
        if (selfMv != null)
        {
            tileSize = Mathf.Max(0.0001f, selfMv.tileSize);
            obstacleMask = selfMv.obstacleMask;
            return true;
        }

        return false;
    }

    private bool TryFindTargetInLine(Vector2 origin, float tileSize, out Vector2 bestDir, out int bestSteps)
    {
        bestDir = Vector2.zero;
        bestSteps = 0;

        int best = int.MaxValue;

        for (int d = 0; d < Dirs.Length; d++)
        {
            Vector2 dir = Dirs[d];
            if (TryScanDirection(origin, dir, tileSize, out int steps))
            {
                if (steps < best)
                {
                    best = steps;
                    bestDir = dir;
                    bestSteps = steps;
                }
            }
        }

        return bestDir != Vector2.zero;
    }

    private bool TryScanDirection(Vector2 origin, Vector2 dir, float tileSize, out int stepsToTarget)
    {
        stepsToTarget = 0;

        float s = Mathf.Max(0.2f, tileCheckBoxSize);
        Vector2 box = Vector2.one * (tileSize * s);

        for (int i = 1; i <= Mathf.Max(1, scanMaxDistanceTiles); i++)
        {
            Vector2 cur = origin + dir * (tileSize * i);

            Collider2D target = FindValidTargetAt(cur, box);
            if (target != null)
            {
                stepsToTarget = i;
                return true;
            }

            if (IsScanBlockedAt(cur, box))
                return false;
        }

        return false;
    }

    private Collider2D FindValidTargetAt(Vector2 worldCenter, Vector2 boxSize)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, boxSize, 0f, targetMask);
        if (hits == null || hits.Length == 0)
            return null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (IsValidTarget(hit, out string rejectReason))
                return hit;
        }

        return null;
    }

    private bool IsValidTarget(Collider2D target, out string rejectReason)
    {
        rejectReason = string.Empty;

        if (target == null)
        {
            rejectReason = "collider is null";
            return false;
        }

        if (target.transform == transform || target.transform.IsChildOf(transform))
        {
            rejectReason = "candidate is this bomb or its child";
            return false;
        }

        if (!IsBattleModeActive())
            return true;

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0 || target.gameObject.layer != playerLayer)
            return true;

        BombController owner = bomb != null ? bomb.Owner : null;
        if (owner == null || !GameSession.IsValidPlayerId(owner.PlayerId))
            return true;

        if (!TryGetPlayerId(target, out int targetPlayerId))
        {
            rejectReason = "battle mode active, but candidate PlayerId could not be resolved";
            return false;
        }

        BattleModeRules rules = BattleModeRules.Instance;
        if (rules == null)
            return true;

        if (targetPlayerId == owner.PlayerId)
        {
            rejectReason = $"owner player ownerPlayer:{owner.PlayerId} targetPlayer:{targetPlayerId}";
            return false;
        }

        if (!rules.UsesTeams)
            return true;

        BattleModeRules.TeamId targetTeam = rules.GetTeamForPlayer(targetPlayerId);
        BattleModeRules.TeamId ownerTeam = rules.GetTeamForPlayer(owner.PlayerId);
        if (targetTeam == ownerTeam)
        {
            rejectReason = $"same team ownerPlayer:{owner.PlayerId}/{ownerTeam} targetPlayer:{targetPlayerId}/{targetTeam}";
            return false;
        }

        return true;
    }

    private static bool IsBattleModeActive()
    {
        return BattleModeRules.Instance != null;
    }

    private static bool TryGetPlayerId(Collider2D target, out int playerId)
    {
        playerId = 0;

        if (target == null)
            return false;

        BombController bombController = target.GetComponentInParent<BombController>();
        if (bombController != null && GameSession.IsValidPlayerId(bombController.PlayerId))
        {
            playerId = bombController.PlayerId;
            return true;
        }

        PlayerIdentity identity = target.GetComponentInParent<PlayerIdentity>();
        if (identity != null && GameSession.IsValidPlayerId(identity.playerId))
        {
            playerId = identity.playerId;
            return true;
        }

        return false;
    }

    private static Vector2 SnapToGrid(Vector2 worldPos, float tileSize)
    {
        float t = Mathf.Max(0.0001f, tileSize);
        worldPos.x = Mathf.Round(worldPos.x / t) * t;
        worldPos.y = Mathf.Round(worldPos.y / t) * t;
        return worldPos;
    }

    private void RefreshDynamicTargetAndBlockMasks()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int stageLayer = LayerMask.NameToLayer(stageLayerName);
        int louieLayer = LayerMask.NameToLayer("Louie");

        int resolvedTargetLayer = -1;

        BombController owner = bomb != null ? bomb.Owner : null;

        if (owner != null)
        {
            int ownerLayer = owner.gameObject.layer;

            if (ownerLayer == playerLayer && IsBattleModeActive())
            {
                resolvedTargetLayer = playerLayer;
            }
            else if (ownerLayer == playerLayer)
            {
                resolvedTargetLayer = enemyLayer;
            }
            else if (ownerLayer == enemyLayer)
            {
                resolvedTargetLayer = playerLayer;
            }
        }

        if (resolvedTargetLayer < 0)
            resolvedTargetLayer = LayerMask.NameToLayer(targetLayerName);

        targetMask = resolvedTargetLayer >= 0
            ? (1 << resolvedTargetLayer)
            : LayerMask.GetMask(targetLayerName);

        int m = 0;

        if (stageLayer >= 0) m |= (1 << stageLayer);
        if (bombLayer >= 0) m |= (1 << bombLayer);
        if (playerLayer >= 0) m |= (1 << playerLayer);
        if (enemyLayer >= 0) m |= (1 << enemyLayer);
        if (louieLayer >= 0) m |= (1 << louieLayer);

        if (resolvedTargetLayer >= 0) m |= (1 << resolvedTargetLayer);

        blockMask = (m != 0)
            ? m
            : LayerMask.GetMask(stageLayerName, bombLayerName, "Player", "Enemy", "Louie");

        if (blockMoveMask.value == 0)
            blockMoveMask = LayerMask.GetMask("Player", "Bomb", "Enemy", "Louie");
    }

    private bool IsScanBlockedAt(Vector2 worldCenter, Vector2 boxSize)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldCenter, boxSize, 0f, blockMask);
        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                if (hit.isTrigger)
                {
                    var pickup = hit.GetComponent<ItemPickup>() ?? hit.GetComponentInParent<ItemPickup>();
                    if (pickup != null)
                        return true;

                    continue;
                }

                return true;
            }
        }

        Collider2D[] allHits = Physics2D.OverlapBoxAll(worldCenter, boxSize, 0f);
        if (allHits != null && allHits.Length > 0)
        {
            for (int i = 0; i < allHits.Length; i++)
            {
                var hit = allHits[i];
                if (hit == null)
                    continue;

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                var pickup = hit.GetComponent<ItemPickup>() ?? hit.GetComponentInParent<ItemPickup>();
                if (pickup != null)
                    return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float s = Mathf.Max(0.2f, tileCheckBoxSize);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(transform.position, Vector3.one * s);
    }
#endif
}
