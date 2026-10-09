using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escapes pursuit by simulating the real tile trail up to the remaining fuse.
/// Uses the shared COM walkability, explosion and timed danger contracts.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class BattleModeComSearchBombAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    private static readonly Vector2Int[] Directions =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private const int MaximumNodes = 192;
    private const int MaximumDepth = 10;
    private struct Threat
    {
        public Bomb Bomb;
        public SearchBomb Search;
        public Vector2Int Origin;
        public Vector2Int[] Trail;
        public float Fuse;
        public float StepSeconds;
        public int Radius;
    }
    private struct Node
    {
        public Vector2Int Tile;
        public int Parent;
        public int Depth;
        public Vector2Int FirstStep;
    }
    private MovementController movement;
    private readonly Collider2D[] predictionHits = new Collider2D[24];
    private ContactFilter2D predictionFilter;
    private readonly List<Threat> threats = new(4);
    private readonly List<Vector2Int> trail = new(64);
    private readonly List<Vector2Int> route = new(MaximumDepth);
    private readonly List<Vector2Int> prediction = new(80);
    private readonly List<Node> nodes = new(MaximumNodes);
    // Keep alternative first directions: the same destination can have a different trail.
    private readonly HashSet<(Vector2Int, Vector2Int)> visited = new();
    private Vector2Int previousStep;
    private Vector2Int previousTile;
    private float stuckSince;
    public string DiagnosticName => "SearchBombAwareness";
    public string LastDecisionTrace { get; private set; } = "not evaluated";
    public bool IsAvailable => isActiveAndEnabled && movement != null && !movement.isDead;
    private void Awake()
    {
        movement = GetComponent<MovementController>();
        predictionFilter = new ContactFilter2D { useTriggers = true };
        predictionFilter.SetLayerMask(LayerMask.GetMask("Stage", "Bomb", "Player", "Enemy", "Louie", "Item"));
    }
    private void OnDisable()
    {
        threats.Clear();
        previousStep = Vector2Int.zero;
    }

    public bool TryBuildEmergencyDecision(BattleModeComDifficultySettings settings,
        BattleModeComController controller, Vector2Int myTile, float currentDangerSeconds,
        out BattleModeComAbilityDecision decision) =>
        TryBuildEscape(settings, controller, myTile, out decision);

    public bool TryBuildCandidateDecision(BattleModeComDifficultySettings settings,
        BattleModeComController controller, Vector2Int myTile,
        out BattleModeComAbilityDecision decision) =>
        TryBuildEscape(settings, controller, myTile, out decision);

    private bool TryBuildEscape(BattleModeComDifficultySettings settings,
        BattleModeComController controller, Vector2Int start, out BattleModeComAbilityDecision decision)
    {
        decision = default;
        threats.Clear();
        if (!IsAvailable || controller == null)
        {
            LastDecisionTrace = "unavailable";
            return false;
        }
        float size = Mathf.Max(0.01f, movement.tileSize);
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.TryGetComponent(out SearchBomb search) ||
                !search.CopyPursuitTrail(transform, size, trail))
                continue;
            threats.Add(new Threat
            {
                Bomb = bomb, Search = search, Origin = ToTile(bomb.GetLogicalPosition(), size),
                Trail = trail.ToArray(), Fuse = bomb.RemainingFuseSeconds,
                StepSeconds = 1f / search.PursuitTilesPerSecond(size),
                Radius = bomb.Owner != null ? bomb.Owner.GetPredictedBlastRadius(bomb) : 2
            });
        }
        if (threats.Count == 0)
        {
            previousStep = Vector2Int.zero;
            LastDecisionTrace = "no pursuing search bomb";
            return false;
        }
        if (start != previousTile || previousStep == Vector2Int.zero)
            stuckSince = Time.time;
        bool stuck = Time.time - stuckSince > Mathf.Max(0.4f, 1.5f / Mathf.Max(1f, movement.speed));
        previousTile = start;
        nodes.Clear();
        visited.Clear();
        nodes.Add(new Node { Tile = start, Parent = -1 });
        float bestScore = float.NegativeInfinity;
        int best = -1;
        for (int index = 0; index < nodes.Count && nodes.Count < MaximumNodes; index++)
        {
            Node node = nodes[index];
            if (node.Depth >= MaximumDepth) continue;
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = node.Tile + direction;
                Vector2Int first = index == 0 ? direction : node.FirstStep;
                if ((stuck && first == previousStep) || visited.Contains((next, first)) ||
                    !controller.IsAbilityTileWalkable(next, start)) continue;
                float firstSeconds = controller.GetAbilityFirstMoveTraversalSeconds(first);
                float arrival = firstSeconds + node.Depth / Mathf.Max(0.01f, movement.speed);
                if (controller.IsAbilityTileDangerousAt(next, arrival, settings)) continue;
                route.Clear();
                route.Add(next);
                for (int parent = index; parent > 0; parent = nodes[parent].Parent)
                    route.Add(nodes[parent].Tile);
                route.Reverse();
                if (!EvaluateRoute(controller, start, firstSeconds, arrival, out float clearance)) continue;
                visited.Add((next, first));
                int nextIndex = nodes.Count;
                nodes.Add(new Node { Tile = next, Parent = index, Depth = node.Depth + 1, FirstStep = first });
                // Prefer safe final positions, separation and short routes. A bend
                // shields the blast only when the shared explosion model confirms it.
                if (clearance > 0f && float.IsInfinity(controller.GetAbilityDangerSeconds(next)))
                {
                    float score = Mathf.Min(8f, clearance) * 10f - route.Count * 2f;
                    if (first == previousStep) score += 1f;
                    if (score > bestScore) { bestScore = score; best = nextIndex; }
                }
                if (nodes.Count >= MaximumNodes) break;
            }
        }
        if (best < 0)
        {
            LastDecisionTrace = $"search threats:{threats.Count} no safe predicted route";
            return false; // Existing emergency/mount abilities can still escape.
        }
        Node chosen = nodes[best];
        previousStep = chosen.FirstStep;
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition, Weight = 4000,
            FirstMove = chosen.FirstStep, TargetTile = chosen.Tile, HasTarget = true,
            Reason = "search bomb escape predicted pursuit and blast",
            InputDescription = $"Move {chosen.FirstStep}"
        };
        LastDecisionTrace = $"threats:{threats.Count} target:{chosen.Tile} depth:{chosen.Depth} clearance:{bestScore:F1}";
        return true;
    }

    private bool EvaluateRoute(BattleModeComController controller, Vector2Int start,
        float firstSeconds, float arrival, out float clearance)
    {
        clearance = float.PositiveInfinity;
        foreach (Threat threat in threats)
        {
            prediction.Clear();
            prediction.AddRange(threat.Trail);
            Vector2Int last = prediction.Count > 0 ? prediction[prediction.Count - 1] : start;
            if (last != start) prediction.Add(start);
            prediction.AddRange(route);
            Vector2Int bombTile = threat.Origin;
            int cursor = 0;
            // Allow for a tile movement already in progress when Think runs.
            float nextMove = threat.StepSeconds * 0.5f;
            bool lostTarget = false;
            // Recheck each waypoint before advancing the bomb. This retains turns
            // and avoids assuming a search bomb heads straight to the destination.
            for (int i = 0; i < route.Count; i++)
            {
                float time = firstSeconds + i / Mathf.Max(0.01f, movement.speed);
                AdvancePrediction(threat, prediction, ref cursor, ref bombTile,
                    ref nextMove, Mathf.Min(time, threat.Fuse), lostTarget);
                Vector2Int player = route[i];
                if (Manhattan(player, bombTile) > threat.Search.DetectionRangeTiles + 1)
                    lostTarget = true;
                if (player == bombTile) return false;
                if (threat.Fuse <= time + 0.12f)
                {
                    // At detonation the player may still be between two tiles.
                    Vector2Int before = i == 0 ? start : route[i - 1];
                    if (controller.DoesBombBlastReachTileWithRadius(threat.Bomb, bombTile, player, threat.Radius) ||
                        controller.DoesBombBlastReachTileWithRadius(threat.Bomb, bombTile, before, threat.Radius))
                        return false;
                }
            }
            // A last in-flight step can reacquire a player that stopped near
            // the detection boundary; keep following the trail in that case.
            if (Manhattan(route[route.Count - 1], bombTile) <= threat.Search.DetectionRangeTiles + 1)
                lostTarget = false;
            AdvancePrediction(threat, prediction, ref cursor, ref bombTile,
                ref nextMove, threat.Fuse, lostTarget);
            Vector2Int goal = route[route.Count - 1];
            bool inBlast = controller.DoesBombBlastReachTileWithRadius(threat.Bomb, bombTile, goal, threat.Radius);
            // Unsafe stopping points may be traversed while there is fuse time.
            if (inBlast) clearance = Mathf.Min(clearance, -1f);
            else clearance = Mathf.Min(clearance, Manhattan(goal, bombTile));
            if (inBlast && threat.Fuse <= arrival + 0.15f) return false;
        }
        return true;
    }

    private void AdvancePrediction(Threat threat, List<Vector2Int> cells, ref int cursor,
        ref Vector2Int origin, ref float nextMove, float until, bool stopped)
    {
        float size = Mathf.Max(0.01f, movement.tileSize);
        while (!stopped && cursor < cells.Count && nextMove <= until)
        {
            Vector2Int next = cells[cursor];
            if (next == origin) { cursor++; continue; }
            if (Manhattan(next, origin) != 1 ||
                !CanPredictBombEnter(threat.Bomb, next, size)) break;
            cursor++;
            origin = next;
            nextMove += threat.StepSeconds;
        }
    }
    private bool CanPredictBombEnter(Bomb bomb, Vector2Int tile, float size)
    {
        Vector2 world = (Vector2)tile * size;
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            if (manager.groundTilemap != null &&
                !manager.groundTilemap.HasTile(manager.groundTilemap.WorldToCell(world))) return false;
            if (manager.indestructibleTilemap != null &&
                manager.indestructibleTilemap.HasTile(manager.indestructibleTilemap.WorldToCell(world))) return false;
            if (manager.destructibleTilemap != null &&
                manager.destructibleTilemap.HasTile(manager.destructibleTilemap.WorldToCell(world))) return false;
        }
        // The escaping player's CURRENT collider must not permanently block
        // a tile that it will have vacated by the predicted arrival time.
        int count = Physics2D.OverlapBox(world, Vector2.one * (size * 0.6f), 0f,
            predictionFilter, predictionHits);
        if (count >= predictionHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = predictionHits[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform) ||
                hit.GetComponentInParent<Bomb>() == bomb) continue;
            return false;
        }
        return true;
    }
    private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private static Vector2Int ToTile(Vector2 position, float size) =>
        new(Mathf.RoundToInt(position.x / size), Mathf.RoundToInt(position.y / size));
}
