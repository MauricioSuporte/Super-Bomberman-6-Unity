using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bomb))]
public sealed class SearchBomb : MonoBehaviour
{
    [SerializeField, Range(1, 3)] private int detectionRangeTiles = 3;
    [SerializeField, Min(0.01f)] private float scanInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 0.3f;

    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private readonly Queue<Vector2Int> frontier = new();
    private readonly Dictionary<Vector2Int, Vector2Int> firstSteps = new();
    private readonly Dictionary<Vector2Int, Vector2Int> previousCells = new();
    private readonly Queue<Vector2Int> targetTrail = new();
    private Vector2Int lastTargetCell;
    private Bomb bomb;
    private Collider2D target;
    private bool pursuitSuspended;
    private float nextScanTime;

    private void Awake() => bomb = GetComponent<Bomb>();

    // External movement owns the bomb until it finishes. Clear the old target
    // now, then allow a fresh search when all movement/holding states end.
    public void SuspendPursuit()
    {
        if (pursuitSuspended)
            return;
        pursuitSuspended = true;
        target = null;
        targetTrail.Clear();
    }

    private void Update()
    {
        if (GamePauseController.IsPaused || bomb.HasExploded)
            return;
        var owner = bomb.Owner;
        var movement = owner != null ? owner.GetComponent<MovementController>() : null;
        float tileSize = movement != null ? Mathf.Max(0.0001f, movement.tileSize) : 1f;
        Vector2Int origin = GetCell(transform.position, tileSize);
        if (target != null)
        {
            Vector2Int currentTargetCell = GetCell(target.transform.position, tileSize);
            if (!IsValidTarget(target) || !IsWithinDetectionRange(origin, currentTargetCell))
            {
                target = null;
                targetTrail.Clear();
            }
            else
                RecordTargetTile(currentTargetCell);
        }
        if (bomb.IsBeingMagnetPulled || !bomb.CanBeMagnetPulled)
        {
            return;
        }
        if (pursuitSuspended)
        {
            pursuitSuspended = false;
            nextScanTime = Time.time;
        }
        if (bomb.Owner == null)
        {
            return;
        }
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanInterval;
        if (!IsValidTarget(target))
        {
            target = FindTarget(origin, tileSize);
            if (target != null)
            {
                lastTargetCell = GetCell(target.transform.position, tileSize);
                SeedTargetTrail(origin, lastTargetCell, tileSize);
            }
        }
        if (target == null)
            return;

        while (targetTrail.Count > 0 && targetTrail.Peek() == origin)
            targetTrail.Dequeue();
        if (targetTrail.Count == 0)
            return;
        Vector2Int destination = targetTrail.Peek();
        Vector2Int step = destination - origin;
        if (Mathf.Abs(step.x) + Mathf.Abs(step.y) != 1)
        {
            // A displaced bomb needs a new approach; never skip the queued turns.
            target = null;
            targetTrail.Clear();
            return;
        }
        Vector2 direction = step;

        LayerMask obstacles = LayerMask.GetMask("Stage", "Bomb", "Player", "Enemy", "Louie");
        bomb.StartMagnetPull(direction, tileSize, 1, obstacles, owner.destructibleTiles,
            movementSpeedMultiplier, obstacles, 0.6f, 0.9f, true, searchMovement: true);
    }

    private void RecordTargetTile(Vector2Int cell)
    {
        if (cell == lastTargetCell)
            return;
        Vector2Int delta = cell - lastTargetCell;
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        // Fill skipped samples only along a straight line. Diagonal jumps or
        // teleports do not prove which tiles the target actually traversed.
        if ((delta.x != 0 && delta.y != 0) || distance > 8 || targetTrail.Count + distance > 64)
        {
            target = null;
            targetTrail.Clear();
            return;
        }
        Vector2Int step = new(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
        for (int i = 0; i < distance; i++)
        {
            lastTargetCell += step;
            targetTrail.Enqueue(lastTargetCell);
        }
    }

    private void SeedTargetTrail(Vector2Int origin, Vector2Int destination, float tileSize)
    {
        targetTrail.Clear();
        if (!TryFindDirection(origin, destination, tileSize, out _))
            return;
        var route = new List<Vector2Int>();
        Vector2Int cell = destination;
        while (cell != origin)
        {
            route.Add(cell);
            cell = previousCells[cell];
        }
        for (int i = route.Count - 1; i >= 0; i--)
            targetTrail.Enqueue(route[i]);
    }

    private bool IsWithinDetectionRange(Vector2Int origin, Vector2Int destination) =>
        Mathf.Abs(destination.x - origin.x) + Mathf.Abs(destination.y - origin.y) <= Mathf.Min(3, detectionRangeTiles);

    private Collider2D FindTarget(Vector2Int origin, float tileSize)
    {
        bool enemyOwner = bomb.Owner != null && bomb.Owner.gameObject.layer == LayerMask.NameToLayer("Enemy");
        int mask = enemyOwner ? LayerMask.GetMask("Player") : BattleModeRules.Instance != null
            ? LayerMask.GetMask("Enemy", "Player") : LayerMask.GetMask("Enemy");
        Vector2 center = (Vector2)origin * tileSize;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center,
            Vector2.one * ((detectionRangeTiles * 2 + 1) * tileSize), 0f, mask);
        Collider2D nearest = null;
        int bestDistance = detectionRangeTiles + 1;
        foreach (Collider2D hit in hits)
        {
            bool valid = IsValidTarget(hit);
            Vector2Int cell = GetCell(hit.transform.position, tileSize);
            int distance = Mathf.Abs(cell.x - origin.x) + Mathf.Abs(cell.y - origin.y);
            bool inRange = IsWithinDetectionRange(origin, cell);
            bool hasRoute = valid && inRange && TryFindDirection(origin, cell, tileSize, out _);
            if (!valid || distance >= bestDistance || !hasRoute)
                continue;
            nearest = hit;
            bestDistance = distance;
        }
        return nearest;
    }

    private bool IsValidTarget(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            return false;
        BombController owner = bomb.Owner;
        if (owner == null || candidate.transform == owner.transform || candidate.transform.IsChildOf(owner.transform))
            return false;
        if (owner.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            return candidate.gameObject.layer == LayerMask.NameToLayer("Player");
        if (candidate.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            return true;
        BattleModeRules rules = BattleModeRules.Instance;
        if (rules == null || candidate.gameObject.layer != LayerMask.NameToLayer("Player"))
            return false;
        BombController opponent = candidate.GetComponentInParent<BombController>();
        PlayerIdentity identity = opponent == null ? candidate.GetComponentInParent<PlayerIdentity>() : null;
        int opponentId = opponent != null ? opponent.PlayerId : identity != null ? identity.playerId : 0;
        if (!GameSession.IsValidPlayerId(opponentId) || opponentId == owner.PlayerId)
            return false;
        return !rules.UsesTeams || rules.GetTeamForPlayer(opponentId) != rules.GetTeamForPlayer(owner.PlayerId);
    }

    private bool TryFindDirection(Vector2Int origin, Vector2Int destination, float tileSize, out Vector2 direction)
    {
        direction = Vector2.zero;
        if (origin == destination)
            return false;
        frontier.Clear();
        firstSteps.Clear();
        previousCells.Clear();
        frontier.Enqueue(origin);
        firstSteps[origin] = Vector2Int.zero;
        // Bounded routing avoids scanning the entire stage for an unreachable
        // target, while permitting turns around nearby obstacles.
        while (frontier.Count > 0 && firstSteps.Count < 256)
        {
            Vector2Int cell = frontier.Dequeue();
            foreach (Vector2Int step in Directions)
            {
                Vector2Int next = cell + step;
                if (firstSteps.ContainsKey(next))
                    continue;
                Vector2Int first = cell == origin ? step : firstSteps[cell];
                if (next == destination)
                {
                    previousCells[next] = cell;
                    direction = first;
                    return true;
                }
                if (Mathf.Abs(next.x - origin.x) + Mathf.Abs(next.y - origin.y) > 12 ||
                    !bomb.CanSearchMoveTo((Vector2)next * tileSize, tileSize))
                    continue;
                previousCells[next] = cell;
                firstSteps[next] = first;
                frontier.Enqueue(next);
            }
        }
        return false;
    }

    private static Vector2Int GetCell(Vector2 position, float tileSize) =>
        new(Mathf.RoundToInt(position.x / tileSize), Mathf.RoundToInt(position.y / tileSize));
}
