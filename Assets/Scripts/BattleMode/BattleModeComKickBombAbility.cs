using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComKickBombAbility : MonoBehaviour, IBattleModeComAbility
{
    private const float OffensiveSequenceCooldownSeconds = 1.1f;
    private const float ActionRStopCooldownSeconds = 0.7f;
    private const int MaxOffensiveKickDistance = 8;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private enum SequenceState
    {
        None,
        RetreatAfterPlant,
        ReturnToKick
    }

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BombKickAbility kickAbility;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private int explosionMask;
    private float tileSize = 1f;
    private float nextOffensiveSequenceTime;
    private float lastActionRStopTime = -10f;
    private SequenceState sequenceState;
    private Vector2Int sequenceBombTile;
    private Vector2Int sequenceRetreatTile;
    private Vector2Int sequenceKickDirection;
    private string lastDecisionTrace = "not evaluated";

    public string DiagnosticName => "KickBomb";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return kickAbility != null && kickAbility.IsEnabled;
        }
    }

    private void Awake()
    {
        CacheReferences();
        LogKick("awake/cache");
    }

    private void OnEnable()
    {
        CacheReferences();
        LogKick("enabled");
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (kickAbility == null)
            TryGetComponent(out kickAbility);

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        explosionMask = LayerMask.GetMask("Explosion");
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency start";

        if (!IsAvailable)
        {
            lastDecisionTrace = BuildAvailabilityTrace("emergency unavailable");
            LogKick(lastDecisionTrace);
            return false;
        }

        string rejectedDirections = string.Empty;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Bomb bomb = FindBombAt(myTile + dir);
            if (!CanKick(bomb))
            {
                Bomb nearBomb = FindBombAt(myTile + dir * 2);
                if (CanKick(nearBomb) &&
                    IsWalkableTile(myTile + dir, myTile) &&
                    IsKickLaneOpen(myTile + dir * 2, dir, 1))
                {
                    Vector2Int standTile = myTile + dir;
                    Vector2Int bombTile = myTile + dir * 2;
                    Vector2Int approachBombDestination = myTile + dir * 3;
                    decision = new BattleModeComAbilityDecision
                    {
                        Action = BattleModeComActionType.KickBomb,
                        Weight = 200 + DifficultyWeight(settings),
                        TargetTile = bombTile,
                        HasTarget = true,
                        FirstMove = TileDirectionToVector(dir),
                        Reason = $"approach kick bomb at {bombTile} toward {approachBombDestination}",
                        InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
                    };
                    lastDecisionTrace = $"emergency approach selected dir {dir} stand {standTile} bomb {bombTile} dest {approachBombDestination}";
                    LogKick(lastDecisionTrace);
                    return true;
                }

                AppendTracePart(ref rejectedDirections, $"{dir}:no adjacent kickable bomb");
                continue;
            }

            Vector2Int bombDestination = myTile + dir * 2;
            if (!IsKickLaneOpen(myTile + dir, dir, 1))
            {
                AppendTracePart(ref rejectedDirections, $"{dir}:lane blocked");
                continue;
            }

            Vector2Int escapeTile = myTile - dir;
            if (!IsSafeWalkable(escapeTile, myTile, settings, 1))
                escapeTile = FindBestEscapeAfterKick(myTile, dir, settings);

            bool escapesThroughVacatedBombTile = false;
            if (escapeTile == myTile && IsSafeAfterKickingBomb(myTile + dir, bomb, settings, 1))
            {
                escapeTile = myTile + dir;
                escapesThroughVacatedBombTile = true;
            }

            if (escapeTile == myTile)
            {
                AppendTracePart(ref rejectedDirections, $"{dir}:no escape after kick");
                continue;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 220 + DifficultyWeight(settings),
                TargetTile = bombDestination,
                HasTarget = true,
                FirstMove = TileDirectionToVector(dir),
                Reason = $"kick escape bomb toward {bombDestination}",
                InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
            };
            lastDecisionTrace =
                $"emergency selected dir {dir} bomb {myTile + dir} dest {bombDestination} escape {escapeTile}" +
                (escapesThroughVacatedBombTile ? " via vacated bomb tile" : string.Empty);
            LogKick(lastDecisionTrace);
            return true;
        }

        lastDecisionTrace = string.IsNullOrEmpty(rejectedDirections)
            ? $"emergency no adjacent kick options nearby:{DescribeNearbyBombs(myTile)}"
            : $"emergency rejected {rejectedDirections} nearby:{DescribeNearbyBombs(myTile)}";
        LogKick(lastDecisionTrace);
        return false;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "candidate start";

        if (!IsAvailable)
        {
            lastDecisionTrace = BuildAvailabilityTrace("candidate unavailable");
            LogKick(lastDecisionTrace);
            return false;
        }

        if (TryBuildActionRStopDecision(settings, myTile, out decision))
            return true;

        if (TryContinueOffensiveSequence(settings, myTile, out decision))
            return true;

        if (Time.time < nextOffensiveSequenceTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextOffensiveSequenceTime - Time.time):F2}s";
            LogKick(lastDecisionTrace);
            return false;
        }

        float chance = DifficultyChance(settings, 0.08f, 0.22f, 0.48f);
        if (Random.value > chance)
        {
            lastDecisionTrace = $"candidate chance failed chance {chance:F2}";
            LogKick(lastDecisionTrace);
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = $"candidate no bombs remaining bc:{(bombController != null)}";
            LogKick(lastDecisionTrace);
            return false;
        }

        if (!TryFindNearestEnemyAligned(myTile, out PlayerIdentity target, out Vector2Int targetTile, out Vector2Int kickDir))
        {
            lastDecisionTrace = "candidate no aligned target with open lane";
            LogKick(lastDecisionTrace);
            return false;
        }

        if (!CanPlantBombForKick(myTile, kickDir, settings, out Vector2Int retreatTile))
        {
            lastDecisionTrace = $"candidate cannot plant/retreat dir {kickDir} target P{target.playerId}@{targetTile}";
            LogKick(lastDecisionTrace);
            return false;
        }

        sequenceState = SequenceState.RetreatAfterPlant;
        sequenceBombTile = myTile;
        sequenceRetreatTile = retreatTile;
        sequenceKickDirection = kickDir;
        nextOffensiveSequenceTime = Time.time + OffensiveSequenceCooldownSeconds;

        Vector2 retreatMove = TileDirectionToVector(retreatTile - myTile);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = Mathf.Max(1, settings.combatPlantWeight + 35 + DifficultyWeight(settings)),
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = retreatMove,
            Reason = $"plant kick bomb toward P{target.playerId}",
            InputDescription = AppendInput("ActionA", FirstMoveDescription(retreatMove)),
            TapBomb = true
        };
        lastDecisionTrace = $"candidate selected plant target P{target.playerId}@{targetTile} dir {kickDir} retreat {retreatTile}";
        LogKick(lastDecisionTrace);
        return true;
    }

    private bool TryContinueOffensiveSequence(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceState == SequenceState.None)
        {
            lastDecisionTrace = "sequence none";
            return false;
        }

        Bomb bomb = FindBombAt(sequenceBombTile);
        if (bomb == null || bomb.HasExploded || bomb.Owner != bombController)
        {
            lastDecisionTrace = $"sequence cancelled bomb missing/exploded/owner tile {sequenceBombTile}";
            sequenceState = SequenceState.None;
            return false;
        }

        if (sequenceState == SequenceState.RetreatAfterPlant)
        {
            if (myTile != sequenceRetreatTile)
            {
                Vector2Int dir = StepToward(myTile, sequenceRetreatTile);
                if (dir == Vector2Int.zero || !IsSafeWalkable(myTile + dir, myTile, settings, 1))
                {
                    lastDecisionTrace = $"sequence retreat blocked from {myTile} to {sequenceRetreatTile}";
                    sequenceState = SequenceState.None;
                    return false;
                }

                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.KickBomb,
                    Weight = 170 + DifficultyWeight(settings),
                    TargetTile = sequenceRetreatTile,
                    HasTarget = true,
                    FirstMove = TileDirectionToVector(dir),
                    Reason = "retreat before kick",
                    InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
                };
                lastDecisionTrace = $"sequence retreat toward {sequenceRetreatTile} move {dir}";
                return true;
            }

            sequenceState = SequenceState.ReturnToKick;
        }

        if (sequenceState == SequenceState.ReturnToKick)
        {
            Vector2Int kickStandTile = sequenceBombTile - sequenceKickDirection;
            Vector2Int dir = myTile == kickStandTile
                ? sequenceKickDirection
                : StepToward(myTile, kickStandTile);

            if (dir == Vector2Int.zero)
            {
                lastDecisionTrace = $"sequence return no dir from {myTile} to {kickStandTile}";
                sequenceState = SequenceState.None;
                return false;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 190 + DifficultyWeight(settings),
                TargetTile = sequenceBombTile,
                HasTarget = true,
                FirstMove = TileDirectionToVector(dir),
                Reason = "return and kick bomb",
                InputDescription = FirstMoveDescription(TileDirectionToVector(dir))
            };

            if (myTile == kickStandTile)
                sequenceState = SequenceState.None;

            lastDecisionTrace = $"sequence return/kick stand {kickStandTile} move {dir}";
            return true;
        }

        lastDecisionTrace = $"sequence unknown state {sequenceState}";
        return false;
    }

    private bool TryBuildActionRStopDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (Time.time - lastActionRStopTime < ActionRStopCooldownSeconds)
        {
            lastDecisionTrace = $"actionR cooldown {ActionRStopCooldownSeconds - (Time.time - lastActionRStopTime):F2}s";
            return false;
        }

        float chance = DifficultyChance(settings, 0.04f, 0.16f, 0.35f);
        if (Random.value > chance)
        {
            lastDecisionTrace = $"actionR chance failed chance {chance:F2}";
            return false;
        }

        int movingOwnBombs = 0;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.IsBeingKicked || bomb.Owner != bombController)
                continue;

            movingOwnBombs++;
            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            if (!TryFindNearestEnemyAligned(bombTile, out PlayerIdentity target, out Vector2Int targetTile, out _))
                continue;

            int distance = Manhattan(bombTile, targetTile);
            if (distance < 1 || distance > 3)
                continue;

            lastActionRStopTime = Time.time;
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 160 + DifficultyWeight(settings),
                TargetTile = targetTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = $"stop kicked bomb near P{target.playerId}",
                InputDescription = "ActionR",
                TapActionR = true
            };
            lastDecisionTrace = $"actionR selected bomb {bombTile} target P{target.playerId}@{targetTile} distance {distance}";
            return true;
        }

        lastDecisionTrace = $"actionR no stop target movingOwnBombs {movingOwnBombs}";
        return false;
    }

    private bool CanPlantBombForKick(
        Vector2Int myTile,
        Vector2Int kickDir,
        BattleModeComDifficultySettings settings,
        out Vector2Int retreatTile)
    {
        retreatTile = myTile - kickDir;
        if (!IsSafeWalkable(retreatTile, myTile, settings, 1))
        {
            Vector2Int sideA = new(-kickDir.y, kickDir.x);
            Vector2Int sideB = new(kickDir.y, -kickDir.x);
            if (IsSafeWalkable(myTile + sideA, myTile, settings, 1))
                retreatTile = myTile + sideA;
            else if (IsSafeWalkable(myTile + sideB, myTile, settings, 1))
                retreatTile = myTile + sideB;
            else
                return false;
        }

        return IsKickLaneOpen(myTile + kickDir, kickDir, 2);
    }

    private Vector2Int FindBestEscapeAfterKick(
        Vector2Int myTile,
        Vector2Int kickDir,
        BattleModeComDifficultySettings settings)
    {
        Vector2Int best = myTile;
        int bestOpen = -1;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            if (dir == kickDir)
                continue;

            Vector2Int tile = myTile + dir;
            if (!IsSafeWalkable(tile, myTile, settings, 1))
                continue;

            int open = CountOpenNeighbors(tile);
            if (open > bestOpen)
            {
                bestOpen = open;
                best = tile;
            }
        }

        return best;
    }

    private bool TryFindNearestEnemyAligned(
        Vector2Int origin,
        out PlayerIdentity target,
        out Vector2Int targetTile,
        out Vector2Int direction)
    {
        target = null;
        targetTile = origin;
        direction = Vector2Int.zero;
        int bestDistance = int.MaxValue;

        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerIdentity player = players[i];
            if (player == null || player == identity)
                continue;

            if (player.TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)
                continue;

            if (identity != null &&
                BattleModeRules.Instance != null &&
                BattleModeRules.Instance.GetTeamForPlayer(player.playerId) ==
                BattleModeRules.Instance.GetTeamForPlayer(identity.playerId))
                continue;

            Vector2Int tile = WorldToTile(player.transform.position);
            Vector2Int delta = tile - origin;
            bool aligned = delta.x == 0 ^ delta.y == 0;
            if (!aligned)
                continue;

            int distance = Manhattan(origin, tile);
            if (distance <= 1 || distance > MaxOffensiveKickDistance || distance >= bestDistance)
                continue;

            Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
            if (!IsKickLaneOpen(origin, dir, distance - 1))
                continue;

            bestDistance = distance;
            target = player;
            targetTile = tile;
            direction = dir;
        }

        return target != null;
    }

    private bool IsKickLaneOpen(Vector2Int start, Vector2Int dir, int minOpenTiles)
    {
        int open = 0;
        for (int step = 1; step <= Mathf.Max(1, minOpenTiles); step++)
        {
            Vector2Int tile = start + dir * step;
            if (!HasGroundTile(tile) || HasIndestructibleTile(tile) || HasDestructibleTile(tile))
                return false;

            Bomb bomb = FindBombAt(tile);
            if (bomb != null && step > 1)
                return false;

            open++;
        }

        return open >= minOpenTiles;
    }

    private bool IsSafeWalkable(
        Vector2Int tile,
        Vector2Int startTile,
        BattleModeComDifficultySettings settings,
        int depth)
    {
        if (!IsWalkableTile(tile, startTile))
            return false;

        float eta = EstimateTraversalSeconds(depth);
        return !IsDangerousAt(tile, eta, settings);
    }

    private bool IsSafeAfterKickingBomb(
        Vector2Int vacatedBombTile,
        Bomb kickedBomb,
        BattleModeComDifficultySettings settings,
        int depth)
    {
        if (!HasGroundTile(vacatedBombTile))
            return false;

        if (HasIndestructibleTile(vacatedBombTile) || HasDestructibleTile(vacatedBombTile))
            return false;

        float eta = EstimateTraversalSeconds(depth);
        return !IsDangerousAt(vacatedBombTile, eta, settings, kickedBomb);
    }

    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return false;

        if (FindBombAt(tile) != null && tile != startTile)
            return false;

        if (movement != null && movement.obstacleMask.value != 0)
        {
            Vector2 center = TileToWorld(tile);
            Vector2 size = Vector2.one * (tileSize * 0.55f);
            int hitCount = Physics2D.OverlapBox(center, size, 0f, obstacleFilter, obstacleHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = obstacleHits[i];
                if (hit == null || IsOwnCollider(hit))
                    continue;

                if (hit.GetComponentInParent<ItemPickup>() != null)
                    continue;

                if (hit.GetComponentInParent<PlayerIdentity>() != null)
                    continue;

                if (tile == startTile && hit.GetComponentInParent<Bomb>() != null)
                    continue;

                return false;
            }
        }

        return true;
    }

    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings,
        Bomb ignoredBomb = null)
    {
        float dangerSeconds = GetDangerSeconds(tile, ignoredBomb);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private float GetDangerSeconds(Vector2Int tile, Bomb ignoredBomb = null)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (bomb == ignoredBomb)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.explosionRadius) : 2;
            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
        if (tile == origin)
            return true;

        Vector2Int delta = tile - origin;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;
        if (!sameColumn && !sameRow)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + dir * step;
            if (HasIndestructibleTile(check) || HasDestructibleTile(check) || FindBombAt(check) != null)
                return false;
        }

        return true;
    }

    private Bomb FindBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return bomb;
        }

        return null;
    }

    private bool CanKick(Bomb bomb)
    {
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               (bomb.CanBeKicked || bomb.CanBeKickedEarly);
    }

    private bool HasGroundTile(Vector2Int tile)
    {
        if (groundTilemap == null)
            return true;

        return groundTilemap.HasTile(WorldToCell(groundTilemap, tile));
    }

    private bool HasDestructibleTile(Vector2Int tile)
    {
        return destructibleTilemap != null && destructibleTilemap.HasTile(WorldToCell(destructibleTilemap, tile));
    }

    private bool HasIndestructibleTile(Vector2Int tile)
    {
        return indestructibleTilemap != null && indestructibleTilemap.HasTile(WorldToCell(indestructibleTilemap, tile));
    }

    private int CountOpenNeighbors(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (IsWalkableTile(tile + CardinalTiles[i], tile))
                count++;
        }

        return count;
    }

    private bool IsOwnCollider(Collider2D colliderToCheck)
    {
        if (ownColliders == null)
            return false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == colliderToCheck)
                return true;
        }

        return false;
    }

    private Vector3Int WorldToCell(Tilemap tilemap, Vector2Int tile)
    {
        return tilemap.WorldToCell(TileToWorld(tile));
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector2 TileToWorld(Vector2Int tile)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2(tile.x * size, tile.y * size);
    }

    private float EstimateTraversalSeconds(int depth)
    {
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static Vector2Int StepToward(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && delta.x != 0)
            return new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0);

        if (delta.y != 0)
            return new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));

        return Vector2Int.zero;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int DifficultyWeight(BattleModeComDifficultySettings settings)
    {
        return settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0,
            BattleModeComputerLevel.Hard => 80,
            _ => 35
        };
    }

    private static float DifficultyChance(
        BattleModeComDifficultySettings settings,
        float easy,
        float normal,
        float hard)
    {
        return settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => easy,
            BattleModeComputerLevel.Hard => hard,
            _ => normal
        };
    }

    private static Vector2 TileDirectionToVector(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return Vector2.up;

        if (direction == Vector2Int.down)
            return Vector2.down;

        if (direction == Vector2Int.left)
            return Vector2.left;

        if (direction == Vector2Int.right)
            return Vector2.right;

        return Vector2.zero;
    }

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.up)
            return "MoveUp";

        if (move == Vector2.down)
            return "MoveDown";

        if (move == Vector2.left)
            return "MoveLeft";

        if (move == Vector2.right)
            return "MoveRight";

        return "none";
    }

    private static string AppendInput(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next) || next == "none")
            return string.IsNullOrWhiteSpace(current) ? "none" : current;

        if (string.IsNullOrWhiteSpace(current) || current == "none")
            return next;

        if (current.Contains(next, System.StringComparison.Ordinal))
            return current;

        return current + "+" + next;
    }

    private string BuildAvailabilityTrace(string prefix)
    {
        return $"{prefix} kick:{(kickAbility != null)} kickEnabled:{(kickAbility != null && kickAbility.IsEnabled)} " +
               $"movement:{(movement != null)} bombController:{(bombController != null)}";
    }

    private static void AppendTracePart(ref string trace, string part)
    {
        if (string.IsNullOrEmpty(trace))
            trace = part;
        else
            trace += "; " + part;
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}";
    }

    private string DescribeNearbyBombs(Vector2Int myTile)
    {
        string result = string.Empty;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int tile = WorldToTile(bomb.GetLogicalPosition());
            int distance = Manhattan(myTile, tile);
            if (distance > 2)
                continue;

            AppendTracePart(
                ref result,
                $"{tile}/d{distance}/solid:{bomb.IsSolid}/can:{bomb.CanBeKicked}/early:{bomb.CanBeKickedEarly}/moving:{bomb.IsBeingKicked}");
        }

        return string.IsNullOrEmpty(result) ? "none" : result;
    }

    private void LogKick(string message)
    {
        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.Log($"[BattleCOMKick][P{id}] tile:{tile} {message}", this);
    }
}
