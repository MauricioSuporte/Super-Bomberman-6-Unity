using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bomb))]
public sealed class SearchBomb : MonoBehaviour
{
    [SerializeField, Range(1, 3)] private int detectionRangeTiles = 3;
    [SerializeField, Min(0.01f)] private float scanInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 0.3f;

    [SerializeField] private Sprite shadowSprite;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer floatingRenderer;
    private SpriteRenderer shadowRenderer;
    private float hoverTime;

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

    public int DetectionRangeTiles => Mathf.Min(3, detectionRangeTiles);
    public float PursuitTilesPerSecond(float tileSize) =>
        bomb.SearchMovementSpeed * Mathf.Max(0.05f, movementSpeedMultiplier) / Mathf.Max(0.0001f, tileSize);

    // Copy the real queued turns without exposing or changing the live pursuit.
    public bool CopyPursuitTrail(Transform candidate, float tileSize, List<Vector2Int> destination)
    {
        destination.Clear();
        if (bomb == null || bomb.HasExploded || pursuitSuspended || bomb.Owner == null ||
            bomb.IsBeingHeldByPowerGlove || candidate == null)
            return false;
        Collider2D collider = candidate.GetComponentInChildren<Collider2D>();
        if (!IsValidTarget(collider))
            return false;
        Vector2Int origin = GetCell(bomb.GetLogicalPosition(), tileSize);
        Vector2Int goal = GetTargetCell(collider, origin, tileSize);
        if (!IsWithinDetectionRange(origin, goal))
            return false;
        if (target != null)
        {
            if (target.transform != candidate && !target.transform.IsChildOf(candidate))
                return false;
            destination.AddRange(targetTrail);
            return true;
        }
        if (!TryFindDirection(origin, goal, tileSize, out _))
            return false;
        Vector2Int cell = goal;
        while (cell != origin)
        {
            destination.Add(cell);
            cell = previousCells[cell];
        }
        destination.Reverse();
        return true;
    }

    private void Awake()
    {
        bomb = GetComponent<Bomb>();
        sourceRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (sourceRenderer == null)
            return;
        if (floatingRenderer == null)
        {
            floatingRenderer = CreateVisualRenderer("FloatingVisual");
            shadowRenderer = CreateVisualRenderer("GroundShadow");
            shadowRenderer.sprite = shadowSprite != null ? shadowSprite :
                Resources.Load<Sprite>("Bombs/SearchBombShadow");
        }
        sourceRenderer.forceRenderingOff = true;
    }

    private SpriteRenderer CreateVisualRenderer(string objectName)
    {
        var visual = new GameObject(objectName);
        visual.layer = gameObject.layer;
        visual.transform.SetParent(transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.maskInteraction = sourceRenderer.maskInteraction;
        return renderer;
    }

    private void LateUpdate()
    {
        if (floatingRenderer == null)
            return;
        if (!GamePauseController.IsPaused)
            hoverTime += Time.deltaTime;

        // Only the presentation rises: physics, pursuit and explosion stay on the ground.
        const float pixelsPerUnit = 16f;
        // A centered 16px bomb extends 8px below its pivot. Keep its bottom
        // above the shadow, whose opaque rows sit around the ground footpoint.
        float heightPixels = 6f + Mathf.Round(Mathf.Sin(hoverTime * Mathf.PI * 2f / 1.2f));
        Vector3 position = transform.position;
        position.x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
        position.y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
        floatingRenderer.transform.position = position + Vector3.up * (heightPixels / pixelsPerUnit);
        floatingRenderer.sprite = sourceRenderer.sprite;
        floatingRenderer.color = sourceRenderer.color;
        floatingRenderer.flipX = sourceRenderer.flipX;
        floatingRenderer.flipY = sourceRenderer.flipY;
        floatingRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        floatingRenderer.sortingOrder = sourceRenderer.sortingOrder;
        floatingRenderer.enabled = sourceRenderer.enabled && !bomb.HasExploded;

        Vector2 groundPosition = bomb.GetLogicalPosition();
        shadowRenderer.transform.position = new Vector3(
            Mathf.Round(groundPosition.x * pixelsPerUnit) / pixelsPerUnit,
            (Mathf.Round(groundPosition.y * pixelsPerUnit) - 4f) / pixelsPerUnit, position.z);
        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        shadowRenderer.enabled = floatingRenderer.enabled && !bomb.IsBeingHeldByPowerGlove;
    }

    private void OnDisable()
    {
        if (sourceRenderer != null)
            sourceRenderer.forceRenderingOff = false;
        if (floatingRenderer != null)
            floatingRenderer.enabled = false;
        if (shadowRenderer != null)
            shadowRenderer.enabled = false;
    }

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
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
            return;

        if (GamePauseController.IsPaused || bomb.HasExploded)
            return;
        var owner = bomb.Owner;
        var movement = owner != null ? owner.GetComponent<MovementController>() : null;
        float tileSize = movement != null ? Mathf.Max(0.0001f, movement.tileSize) : 1f;
        Vector2Int origin = GetCell(transform.position, tileSize);
        if (target != null)
        {
            Vector2Int currentTargetCell = GetTargetCell(target, origin, tileSize);
            if (!IsValidTarget(target) || !IsWithinDetectionRange(origin, currentTargetCell))
            {
                target = null;
                targetTrail.Clear();
            }
            else if (!IsBossTarget(target))
                RecordTargetTile(currentTargetCell);
        }
        if (bomb.IsBeingMagnetPulled || !bomb.CanSearchMove)
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
                lastTargetCell = GetTargetCell(target, origin, tileSize);
                SeedTargetTrail(origin, lastTargetCell, tileSize);
            }
        }
        if (target == null)
            return;

        // Large boss hitboxes can cover several tiles and move diagonally.
        // Rebuild the approach to their nearest edge instead of following their pivot trail.
        if (IsBossTarget(target))
            SeedTargetTrail(origin, GetTargetCell(target, origin, tileSize), tileSize);

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
            Vector2Int cell = GetTargetCell(hit, origin, tileSize);
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

    private static bool IsBossTarget(Collider2D candidate) =>
        candidate.GetComponentInParent<FreezerVenusBoss>() != null ||
        candidate.GetComponentInParent<SunMaskBoss>() != null ||
        candidate.GetComponentInParent<ClownMaskBoss>() != null;

    private static Vector2Int GetTargetCell(Collider2D candidate, Vector2Int origin, float tileSize)
    {
        Vector2 position = IsBossTarget(candidate)
            ? candidate.ClosestPoint((Vector2)origin * tileSize)
            : (Vector2)candidate.transform.position;
        return GetCell(position, tileSize);
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
