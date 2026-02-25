using UnityEngine;
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

    [Header("Cooldown After Stop")]
    [SerializeField, Min(0f)] private float reattractDelaySeconds = 1f;

    [Header("Detection / Blocking")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string stageLayerName = "Stage";
    [SerializeField] private string bombLayerName = "Bomb";

    [Header("Pull Speed")]
    [SerializeField, Min(0.1f)]
    private float magnetPullSpeedMultiplier = 1f;

    [SerializeField, Range(0.2f, 0.95f)] private float tileCheckBoxSize = 0.60f;

    private Bomb bomb;

    private float nextScanTime;
    private bool waitingMovementToEnd;
    private float reattractAllowedAt;

    private int playerMask;
    private int blockMask;

    private static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    private void Awake()
    {
        bomb = GetComponent<Bomb>();

        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        int stageLayer = LayerMask.NameToLayer(stageLayerName);
        int bombLayer = LayerMask.NameToLayer(bombLayerName);

        playerMask = (playerLayer >= 0) ? (1 << playerLayer) : LayerMask.GetMask("Player");

        int m = 0;
        if (stageLayer >= 0) m |= (1 << stageLayer);
        if (bombLayer >= 0) m |= (1 << bombLayer);
        if (playerLayer >= 0) m |= (1 << playerLayer);

        blockMask = (m != 0) ? m : LayerMask.GetMask("Stage", "Bomb", "Player");

        reattractAllowedAt = 0f;
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

        Vector2 origin = SnapToGrid(transform.position, tileSize);

        if (!TryFindPlayerInLine(origin, tileSize, out Vector2 dir, out int stepsToTarget))
            return;

        if (stepsToTarget <= 1)
            return;

        if (bomb.StartMagnetPull(dir, tileSize, 0, obstacleMask, destructibleTilemap, magnetPullSpeedMultiplier))
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

    private bool TryFindPlayerInLine(Vector2 origin, float tileSize, out Vector2 bestDir, out int bestSteps)
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

    private bool TryScanDirection(Vector2 origin, Vector2 dir, float tileSize, out int stepsToPlayer)
    {
        stepsToPlayer = 0;

        float s = Mathf.Max(0.2f, tileCheckBoxSize);
        Vector2 box = Vector2.one * (tileSize * s);

        for (int i = 1; i <= Mathf.Max(1, scanMaxDistanceTiles); i++)
        {
            Vector2 cur = origin + dir * (tileSize * i);

            Collider2D pl = Physics2D.OverlapBox(cur, box, 0f, playerMask);
            if (pl != null)
            {
                stepsToPlayer = i;
                return true;
            }

            Collider2D block = Physics2D.OverlapBox(cur, box, 0f, blockMask);
            if (block != null)
                return false;
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float s = Mathf.Max(0.2f, tileCheckBoxSize);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(transform.position, Vector3.one * s);
    }
#endif
}