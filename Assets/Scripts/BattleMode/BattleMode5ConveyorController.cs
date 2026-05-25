using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GroundTileResolver))]
public sealed class BattleMode5ConveyorController : MonoBehaviour, IGroundTileHandler
{
    const string BattleMode5SceneName = "BattleMode_5";

    [System.Serializable]
    private struct HorizontalConveyorSegment
    {
        public Vector3Int startCell;
        public Vector3Int endCell;
    }

    [System.Serializable]
    private struct VerticalConveyorSegment
    {
        public Vector3Int startCell;
        public Vector3Int endCell;
    }

    private sealed class ConveyorAnimatedTileState
    {
        public AnimatedTile clockwiseSlow;
        public AnimatedTile clockwiseFast;
        public AnimatedTile counterClockwiseSlow;
        public AnimatedTile counterClockwiseFast;
    }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap destructibleTilemap;
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Conveyor Path")]
    [SerializeField]
    private HorizontalConveyorSegment[] horizontalSegments =
    {
        new() { startCell = new Vector3Int(-4, 2, 0), endCell = new Vector3Int(2, 2, 0) },
        new() { startCell = new Vector3Int(-4, -4, 0), endCell = new Vector3Int(2, -4, 0) },
    };

    [SerializeField]
    private VerticalConveyorSegment[] verticalSegments =
    {
        new() { startCell = new Vector3Int(-5, 1, 0), endCell = new Vector3Int(-5, -3, 0) },
        new() { startCell = new Vector3Int(3, 1, 0), endCell = new Vector3Int(3, -3, 0) },
    };

    [SerializeField]
    private Vector3Int[] cornerCells =
    {
        new(-5, 2, 0),
        new(3, 2, 0),
        new(3, -4, 0),
        new(-5, -4, 0),
    };

    [Tooltip("Opcional. Se vazio, todas as celulas configuradas acima funcionam como esteira.")]
    [SerializeField] private TileBase[] conveyorGroundTiles;

    [Header("Control Tiles")]
    [SerializeField] private TileBase[] reverseDirectionTiles;
    [SerializeField] private TileBase[] speedToggleTiles;

    [Header("Speed")]
    [SerializeField, Min(0.01f)] private float slowSpeedTilesPerSecond = 1.25f;
    [SerializeField, Min(0.01f)] private float fastSpeedTilesPerSecond = 2.5f;
    [SerializeField] private bool startFast;
    [SerializeField, Min(0.01f)] private float fastAnimatedTileSpeedMultiplier = 2f;

    [Header("Player Movement Influence")]
    [SerializeField, Min(0f)] private float playerWithSlowConveyorSpeedMultiplier = 4f / 3f;
    [SerializeField, Min(0f)] private float playerAgainstSlowConveyorSpeedMultiplier = 2f / 3f;
    [SerializeField, Min(0f)] private float playerWithFastConveyorSpeedMultiplier = 5f / 3f;
    [SerializeField, Min(0f)] private float playerAgainstFastConveyorSpeedMultiplier = 1f / 3f;
    [SerializeField, Min(0f)] private float playerSpeedMultiplierRefreshSeconds = 0.12f;

    [Header("Direction")]
    [SerializeField] private bool startClockwise = true;

    readonly HashSet<TileBase> registeredTiles = new();
    readonly HashSet<Vector3Int> conveyorCells = new();
    readonly Dictionary<Vector3Int, Vector3Int> clockwiseNextCell = new();
    readonly Dictionary<Vector3Int, Vector3Int> counterClockwiseNextCell = new();
    readonly HashSet<MovementController> reverseTilePlayers = new();
    readonly HashSet<MovementController> speedTilePlayers = new();
    readonly List<Vector3Int> orderedClockwiseCells = new(32);
    readonly List<Bomb> bombSnapshot = new(64);
    readonly Dictionary<int, Vector3Int> lockedTargetCellByObject = new();
    readonly Dictionary<AnimatedTile, ConveyorAnimatedTileState> conveyorAnimatedTileStates = new();
    readonly Dictionary<AnimatedTile, AnimatedTile> conveyorRuntimeTileOrigins = new();

    static FieldInfo bombLastPosField;
    static FieldInfo bombCurrentTileCenterField;

    bool clockwise;
    bool fast;

    void Awake()
    {
        EnsureBombFields();
        clockwise = startClockwise;
        fast = startFast;

        ResolveReferences();
        BuildConveyorPath();
        RegisterResolverTiles();
        RefreshControlTileVisuals();
        RefreshConveyorAnimatedTileVisuals();
    }

    void OnEnable()
    {
        ResolveReferences();
        BuildConveyorPath();
        RegisterResolverTiles();
        RefreshControlTileVisuals();
        RefreshConveyorAnimatedTileVisuals();
    }

    void OnDisable()
    {
        UnregisterResolverTiles();
        reverseTilePlayers.Clear();
        speedTilePlayers.Clear();
    }

    void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.ArenaUpdate.Auto();

        if (!IsBattleMode5Active())
            return;

        ResolveReferences();
        HandleControlTiles();
    }

    void FixedUpdate()
    {
        using var performanceSample = BattleModePerformanceMarkers.ArenaUpdate.Auto();

        if (!IsBattleMode5Active())
            return;

        ResolveReferences();
        BuildConveyorPath();

        if (groundTilemap == null || conveyorCells.Count == 0)
            return;

        float tileSize = ResolveTileSize();
        float speed = (fast ? fastSpeedTilesPerSecond : slowSpeedTilesPerSecond) * tileSize;
        float maxDistance = speed * Time.fixedDeltaTime;

        MovePlayers(maxDistance);
        MoveBombs(maxDistance);
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
        => false;

    void MovePlayers(float maxDistance)
    {
        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            MovementController mover = players[i];
            if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
                continue;

            if (mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
                continue;

            int objectKey = GetObjectKey(mover.gameObject);
            if (!TryGetConveyorTarget(
                    objectKey,
                    mover.Rigidbody.position,
                    out Vector2 target,
                    out Vector3Int targetCell,
                    out Vector3Int sourceCell,
                    out Vector2 unityCenter,
                    out Vector2 cellToWorld))
                continue;

            if (IsBlockedForConveyor(sourceCell, ignoredBomb: null, blockPlayers: false))
            {
                ClearObjectConveyorState(objectKey);
                continue;
            }

            ApplyPlayerMovementInfluence(mover, sourceCell);

            if (IsBlockedForConveyor(targetCell, ignoredBomb: null, blockPlayers: false))
            {
                ClearObjectConveyorState(objectKey);
                MoveRigidbodyTowardCellCenter(mover.Rigidbody, sourceCell, maxDistance, respectConveyorDirection: true);
                continue;
            }

            Vector2 next = Vector2.MoveTowards(mover.Rigidbody.position, target, maxDistance);
            mover.Rigidbody.MovePosition(next);
        }
    }

    void ApplyPlayerMovementInfluence(MovementController mover, Vector3Int sourceCell)
    {
        if (mover == null)
            return;

        Vector2 playerDirection = NormalizeCardinal(mover.Direction);
        Vector2 conveyorDirection = GetConveyorDirection(sourceCell);
        if (playerDirection == Vector2.zero || conveyorDirection == Vector2.zero)
            return;

        float dot = Vector2.Dot(playerDirection, conveyorDirection);
        if (dot > 0.5f)
        {
            mover.ApplyExternalMovementSpeedMultiplier(
                fast ? playerWithFastConveyorSpeedMultiplier : playerWithSlowConveyorSpeedMultiplier,
                playerSpeedMultiplierRefreshSeconds);
        }
        else if (dot < -0.5f)
        {
            mover.ApplyExternalMovementSpeedMultiplier(
                fast ? playerAgainstFastConveyorSpeedMultiplier : playerAgainstSlowConveyorSpeedMultiplier,
                playerSpeedMultiplierRefreshSeconds);
        }
    }

    void MoveBombs(float maxDistance)
    {
        bombSnapshot.Clear();
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb != null)
                bombSnapshot.Add(bomb);
        }

        for (int i = 0; i < bombSnapshot.Count; i++)
        {
            Bomb bomb = bombSnapshot[i];
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (bomb.IsBeingKicked || bomb.IsBeingPunched || bomb.IsBeingMagnetPulled)
                continue;

            if (!bomb.TryGetComponent(out Rigidbody2D rb) || rb == null)
                continue;

            int objectKey = GetObjectKey(bomb.gameObject);
            if (!TryGetConveyorTarget(
                    objectKey,
                    rb.position,
                    out Vector2 target,
                    out Vector3Int targetCell,
                    out Vector3Int sourceCell,
                    out Vector2 unityCenter,
                    out Vector2 cellToWorld))
                continue;

            if (IsBlockedForConveyor(sourceCell, ignoredBomb: bomb, blockPlayers: false))
            {
                ClearObjectConveyorState(objectKey);
                continue;
            }

            if (IsBlockedForConveyor(targetCell, ignoredBomb: bomb, blockPlayers: true))
            {
                ClearObjectConveyorState(objectKey);
                MoveBombTowardCellCenter(bomb, rb, sourceCell, maxDistance, respectConveyorDirection: true);
                continue;
            }

            Vector2 next = Vector2.MoveTowards(rb.position, target, maxDistance);
            rb.MovePosition(next);
            bomb.transform.position = next;
            SetBombLogicalPosition(bomb, next);
        }

        bombSnapshot.Clear();
    }

    void MoveRigidbodyTowardCellCenter(Rigidbody2D rb, Vector3Int cell, float maxDistance, bool respectConveyorDirection)
    {
        if (rb == null || groundTilemap == null || !IsActiveConveyorCell(cell))
            return;

        Vector2 center = GetCellCenterWorld(cell);
        if (Vector2.Distance(rb.position, center) <= 0.01f)
            return;

        if (respectConveyorDirection && WouldMoveBackwardAlongConveyor(rb.position, center, cell))
            return;

        Vector2 next = Vector2.MoveTowards(rb.position, center, maxDistance);
        rb.MovePosition(next);
    }

    void MoveBombTowardCellCenter(Bomb bomb, Rigidbody2D rb, Vector3Int cell, float maxDistance, bool respectConveyorDirection)
    {
        if (bomb == null || rb == null || groundTilemap == null || !IsActiveConveyorCell(cell))
            return;

        Vector2 center = GetCellCenterWorld(cell);
        if (Vector2.Distance(rb.position, center) <= 0.01f)
            return;

        if (respectConveyorDirection && WouldMoveBackwardAlongConveyor(rb.position, center, cell))
            return;

        Vector2 next = Vector2.MoveTowards(rb.position, center, maxDistance);
        rb.MovePosition(next);
        bomb.transform.position = next;
        SetBombLogicalPosition(bomb, next);
    }

    void ClearObjectConveyorState(int objectKey)
    {
        lockedTargetCellByObject.Remove(objectKey);
    }

    bool TryGetConveyorTarget(
        int objectKey,
        Vector2 worldPos,
        out Vector2 target,
        out Vector3Int targetCell,
        out Vector3Int sourceCell,
        out Vector2 unityCenter,
        out Vector2 cellToWorld)
    {
        sourceCell = groundTilemap.WorldToCell(worldPos);

        if (lockedTargetCellByObject.TryGetValue(objectKey, out Vector3Int lockedTargetCell))
        {
            bool sourceIsActive = IsActiveConveyorCell(sourceCell);
            if (!sourceIsActive && sourceCell != lockedTargetCell)
            {
                lockedTargetCellByObject.Remove(objectKey);
                target = default;
                targetCell = default;
                unityCenter = default;
                cellToWorld = default;
                return false;
            }

            if (!sourceIsActive || sourceCell == lockedTargetCell)
            {
                Vector2 lockedTarget = GetCellCenterWorld(lockedTargetCell);
                if (Vector2.Distance(worldPos, lockedTarget) > 0.01f && IsActiveConveyorCell(lockedTargetCell))
                {
                    target = lockedTarget;
                    targetCell = lockedTargetCell;
                    unityCenter = groundTilemap.GetCellCenterWorld(lockedTargetCell);
                    cellToWorld = groundTilemap.CellToWorld(lockedTargetCell);
                    return true;
                }
            }

            lockedTargetCellByObject.Remove(objectKey);
        }

        if (!IsActiveConveyorCell(sourceCell))
        {
            lockedTargetCellByObject.Remove(objectKey);
            target = default;
            targetCell = default;
            unityCenter = default;
            cellToWorld = default;
            return false;
        }

        Dictionary<Vector3Int, Vector3Int> map = clockwise ? clockwiseNextCell : counterClockwiseNextCell;
        if (!map.TryGetValue(sourceCell, out Vector3Int nextCell))
        {
            target = default;
            targetCell = default;
            unityCenter = default;
            cellToWorld = default;
            return false;
        }

        targetCell = nextCell;
        unityCenter = groundTilemap.GetCellCenterWorld(nextCell);
        cellToWorld = groundTilemap.CellToWorld(nextCell);
        target = GetCellCenterWorld(nextCell);
        lockedTargetCellByObject[objectKey] = targetCell;
        return true;
    }

    bool IsBlockedForConveyor(Vector3Int targetCell, Bomb ignoredBomb, bool blockPlayers)
    {
        if (destructibleTilemap != null && destructibleTilemap.GetTile(targetCell) != null)
            return true;

        if (indestructibleTilemap != null && indestructibleTilemap.GetTile(targetCell) != null)
            return true;

        if (HasBombAtCell(targetCell, ignoredBomb))
            return true;

        return blockPlayers && HasPlayerAtCell(targetCell);
    }

    bool HasBombAtCell(Vector3Int targetCell, Bomb ignoredBomb)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb == ignoredBomb || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector3Int bombCell = groundTilemap.WorldToCell(bomb.GetLogicalPosition());
            if (bombCell == targetCell)
                return true;
        }

        return false;
    }

    bool HasPlayerAtCell(Vector3Int targetCell)
    {
        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            MovementController mover = players[i];
            if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
                continue;

            if (mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
                continue;

            Vector3Int playerCell = groundTilemap.WorldToCell(mover.Rigidbody.position);
            if (playerCell == targetCell)
                return true;
        }

        return false;
    }

    void HandleControlTiles()
    {
        if (groundTilemap == null)
            return;

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            MovementController mover = players[i];
            if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
                continue;

            Vector3Int cell = groundTilemap.WorldToCell(mover.Rigidbody.position);
            TileBase tile = groundTilemap.GetTile(cell);

            HandleToggleTile(mover, tile, reverseDirectionTiles, reverseTilePlayers, ToggleDirection);
            HandleToggleTile(mover, tile, speedToggleTiles, speedTilePlayers, ToggleSpeed);
        }
    }

    void HandleToggleTile(
        MovementController mover,
        TileBase currentTile,
        TileBase[] triggerTiles,
        HashSet<MovementController> playersOnTile,
        System.Action toggle)
    {
        bool onTile = ContainsTile(triggerTiles, currentTile);
        if (!onTile)
        {
            playersOnTile.Remove(mover);
            return;
        }

        if (!playersOnTile.Add(mover))
            return;

        toggle?.Invoke();
    }

    void ToggleDirection()
    {
        clockwise = !clockwise;
        RefreshDirectionTileVisuals();
        RefreshConveyorAnimatedTileVisuals();
    }

    void ToggleSpeed()
    {
        fast = !fast;
        RefreshSpeedTileVisuals();
        RefreshConveyorAnimatedTileVisuals();
    }

    void RefreshControlTileVisuals()
    {
        RefreshDirectionTileVisuals();
        RefreshSpeedTileVisuals();
    }

    void RefreshDirectionTileVisuals()
    {
        int tileIndex = clockwise == startClockwise ? 0 : 1;
        ReplaceControlTiles(reverseDirectionTiles, tileIndex);
    }

    void RefreshSpeedTileVisuals()
    {
        int tileIndex = fast == startFast ? 0 : 1;
        ReplaceControlTiles(speedToggleTiles, tileIndex);
    }

    void ReplaceControlTiles(TileBase[] controlTiles, int targetIndex)
    {
        if (groundTilemap == null || controlTiles == null || controlTiles.Length == 0)
            return;

        int clampedIndex = Mathf.Clamp(targetIndex, 0, controlTiles.Length - 1);
        TileBase targetTile = controlTiles[clampedIndex];
        if (targetTile == null)
            return;

        BoundsInt bounds = groundTilemap.cellBounds;
        bool changed = false;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase currentTile = groundTilemap.GetTile(cell);
            if (!ContainsTile(controlTiles, currentTile) || currentTile == targetTile)
                continue;

            groundTilemap.SetTile(cell, targetTile);
            changed = true;
        }

        if (changed)
            groundTilemap.RefreshAllTiles();
    }

    void RefreshConveyorAnimatedTileVisuals()
    {
        if (groundTilemap == null)
            return;

        bool changed = false;
        foreach (Vector3Int cell in conveyorCells)
        {
            TileBase currentTile = groundTilemap.GetTile(cell);
            if (!TryGetOriginalConveyorAnimatedTile(currentTile, out AnimatedTile originalTile))
                continue;

            AnimatedTile stateTile = GetConveyorAnimatedTileForCurrentState(originalTile);
            if (stateTile == null || currentTile == stateTile)
                continue;

            groundTilemap.SetTile(cell, stateTile);
            changed = true;
        }

        if (changed)
            groundTilemap.RefreshAllTiles();
    }

    bool TryGetOriginalConveyorAnimatedTile(TileBase tile, out AnimatedTile originalTile)
    {
        originalTile = null;
        if (tile is not AnimatedTile animatedTile)
            return false;

        if (conveyorRuntimeTileOrigins.TryGetValue(animatedTile, out AnimatedTile origin))
        {
            originalTile = origin;
            return originalTile != null;
        }

        originalTile = animatedTile;
        return true;
    }

    AnimatedTile GetConveyorAnimatedTileForCurrentState(AnimatedTile originalTile)
    {
        if (originalTile == null)
            return null;

        if (!conveyorAnimatedTileStates.TryGetValue(originalTile, out ConveyorAnimatedTileState state))
        {
            state = new ConveyorAnimatedTileState
            {
                clockwiseSlow = CreateRuntimeConveyorAnimatedTile(originalTile, reverseSprites: false, speedMultiplier: 1f, "ClockwiseSlow"),
                clockwiseFast = CreateRuntimeConveyorAnimatedTile(originalTile, reverseSprites: false, speedMultiplier: fastAnimatedTileSpeedMultiplier, "ClockwiseFast"),
                counterClockwiseSlow = CreateRuntimeConveyorAnimatedTile(originalTile, reverseSprites: true, speedMultiplier: 1f, "CounterClockwiseSlow"),
                counterClockwiseFast = CreateRuntimeConveyorAnimatedTile(originalTile, reverseSprites: true, speedMultiplier: fastAnimatedTileSpeedMultiplier, "CounterClockwiseFast"),
            };

            RegisterRuntimeConveyorAnimatedTile(state.clockwiseSlow, originalTile);
            RegisterRuntimeConveyorAnimatedTile(state.clockwiseFast, originalTile);
            RegisterRuntimeConveyorAnimatedTile(state.counterClockwiseSlow, originalTile);
            RegisterRuntimeConveyorAnimatedTile(state.counterClockwiseFast, originalTile);
            conveyorAnimatedTileStates[originalTile] = state;
        }

        if (clockwise)
            return fast ? state.clockwiseFast : state.clockwiseSlow;

        return fast ? state.counterClockwiseFast : state.counterClockwiseSlow;
    }

    static AnimatedTile CreateRuntimeConveyorAnimatedTile(
        AnimatedTile source,
        bool reverseSprites,
        float speedMultiplier,
        string stateName)
    {
        if (source == null)
            return null;

        AnimatedTile clone = ScriptableObject.CreateInstance<AnimatedTile>();
        clone.name = $"{source.name}_{stateName}_Runtime";
        clone.hideFlags = HideFlags.DontSave;

        if (source.m_AnimatedSprites != null)
        {
            clone.m_AnimatedSprites = (Sprite[])source.m_AnimatedSprites.Clone();
            if (reverseSprites)
                System.Array.Reverse(clone.m_AnimatedSprites);
        }

        float multiplier = Mathf.Max(0.01f, speedMultiplier);
        clone.m_MinSpeed = source.m_MinSpeed * multiplier;
        clone.m_MaxSpeed = source.m_MaxSpeed * multiplier;
        clone.m_AnimationStartTime = source.m_AnimationStartTime;
        clone.UseSpecifiedTime = source.UseSpecifiedTime;
        clone.SpecifiedTime = source.UseSpecifiedTime
            ? Mathf.Max(0.01f, source.SpecifiedTime / multiplier)
            : source.SpecifiedTime;
        clone.m_TileColliderType = source.m_TileColliderType;

        return clone;
    }

    void RegisterRuntimeConveyorAnimatedTile(AnimatedTile runtimeTile, AnimatedTile originalTile)
    {
        if (runtimeTile == null || originalTile == null)
            return;

        conveyorRuntimeTileOrigins[runtimeTile] = originalTile;
    }

    void BuildConveyorPath()
    {
        conveyorCells.Clear();
        orderedClockwiseCells.Clear();
        clockwiseNextCell.Clear();
        counterClockwiseNextCell.Clear();

        AddHorizontalRange(new Vector3Int(-5, 2, 0), new Vector3Int(3, 2, 0), step: 1);
        AddVerticalRange(new Vector3Int(3, 1, 0), new Vector3Int(3, -4, 0), step: -1);
        AddHorizontalRange(new Vector3Int(2, -4, 0), new Vector3Int(-5, -4, 0), step: -1);
        AddVerticalRange(new Vector3Int(-5, -3, 0), new Vector3Int(-5, 1, 0), step: 1);

        AddConfiguredCells();

        for (int i = 0; i < orderedClockwiseCells.Count; i++)
        {
            Vector3Int current = orderedClockwiseCells[i];
            Vector3Int next = orderedClockwiseCells[(i + 1) % orderedClockwiseCells.Count];
            Vector3Int previous = orderedClockwiseCells[(i - 1 + orderedClockwiseCells.Count) % orderedClockwiseCells.Count];

            clockwiseNextCell[current] = next;
            counterClockwiseNextCell[current] = previous;
        }
    }

    void AddConfiguredCells()
    {
        if (horizontalSegments != null)
        {
            for (int i = 0; i < horizontalSegments.Length; i++)
                AddCellsFromSegment(horizontalSegments[i].startCell, horizontalSegments[i].endCell);
        }

        if (verticalSegments != null)
        {
            for (int i = 0; i < verticalSegments.Length; i++)
                AddCellsFromSegment(verticalSegments[i].startCell, verticalSegments[i].endCell);
        }

        if (cornerCells != null)
        {
            for (int i = 0; i < cornerCells.Length; i++)
                AddConveyorCell(cornerCells[i]);
        }
    }

    void AddCellsFromSegment(Vector3Int start, Vector3Int end)
    {
        if (start.x == end.x)
        {
            int step = start.y <= end.y ? 1 : -1;
            for (int y = start.y; y != end.y + step; y += step)
                AddConveyorCell(new Vector3Int(start.x, y, 0));
            return;
        }

        if (start.y == end.y)
        {
            int step = start.x <= end.x ? 1 : -1;
            for (int x = start.x; x != end.x + step; x += step)
                AddConveyorCell(new Vector3Int(x, start.y, 0));
        }
    }

    void AddHorizontalRange(Vector3Int start, Vector3Int end, int step)
    {
        for (int x = start.x; x != end.x + step; x += step)
            AddOrderedConveyorCell(new Vector3Int(x, start.y, 0));
    }

    void AddVerticalRange(Vector3Int start, Vector3Int end, int step)
    {
        for (int y = start.y; y != end.y + step; y += step)
            AddOrderedConveyorCell(new Vector3Int(start.x, y, 0));
    }

    void AddOrderedConveyorCell(Vector3Int cell)
    {
        if (conveyorCells.Add(cell))
            orderedClockwiseCells.Add(cell);
    }

    void AddConveyorCell(Vector3Int cell)
    {
        conveyorCells.Add(cell);
    }

    bool IsActiveConveyorCell(Vector3Int cell)
    {
        if (!conveyorCells.Contains(cell))
            return false;

        if (conveyorGroundTiles == null || conveyorGroundTiles.Length == 0)
            return true;

        TileBase tile = groundTilemap.GetTile(cell);
        if (ContainsTile(conveyorGroundTiles, tile))
            return true;

        if (tile is AnimatedTile animatedTile &&
            conveyorRuntimeTileOrigins.TryGetValue(animatedTile, out AnimatedTile originalTile))
            return ContainsTile(conveyorGroundTiles, originalTile);

        return false;
    }

    Vector2 GetConveyorDirection(Vector3Int sourceCell)
    {
        Dictionary<Vector3Int, Vector3Int> map = clockwise ? clockwiseNextCell : counterClockwiseNextCell;
        if (!map.TryGetValue(sourceCell, out Vector3Int targetCell))
            return Vector2.zero;

        Vector3Int delta = targetCell - sourceCell;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);

        if (delta.y != 0)
            return new Vector2(0f, Mathf.Sign(delta.y));

        return Vector2.zero;
    }

    bool WouldMoveBackwardAlongConveyor(Vector2 actorPosition, Vector2 center, Vector3Int sourceCell)
    {
        Vector2 conveyorDirection = GetConveyorDirection(sourceCell);
        if (conveyorDirection == Vector2.zero)
            return false;

        Vector2 toCenter = center - actorPosition;
        return Vector2.Dot(toCenter, conveyorDirection) < -0.001f;
    }

    static Vector2 NormalizeCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return Mathf.Abs(direction.x) > 0.01f ? new Vector2(Mathf.Sign(direction.x), 0f) : Vector2.zero;

        return Mathf.Abs(direction.y) > 0.01f ? new Vector2(0f, Mathf.Sign(direction.y)) : Vector2.zero;
    }

    void ResolveReferences()
    {
        if (groundTileResolver == null)
            groundTileResolver = GetComponent<GroundTileResolver>();

        if (groundTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                groundTilemap = gm.groundTilemap;
                if (destructibleTilemap == null)
                    destructibleTilemap = gm.destructibleTilemap;
                if (indestructibleTilemap == null)
                    indestructibleTilemap = gm.indestructibleTilemap;
            }
        }

        if ((destructibleTilemap == null || indestructibleTilemap == null) && FindAnyObjectByType<GameManager>() is GameManager gameManager)
        {
            if (destructibleTilemap == null)
                destructibleTilemap = gameManager.destructibleTilemap;
            if (indestructibleTilemap == null)
                indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("ground");

        if (destructibleTilemap == null)
            destructibleTilemap = FindTilemapByName("destruct");

        if (indestructibleTilemap == null)
            indestructibleTilemap = FindTilemapByName("indestruct");
    }

    Tilemap FindTilemapByName(string namePart)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm != null && tm.name.ToLowerInvariant().Contains(namePart))
                return tm;
        }

        return null;
    }

    void RegisterResolverTiles()
    {
        ResolveReferences();
        if (groundTileResolver == null)
            return;

        RegisterTiles(conveyorGroundTiles);
        RegisterTiles(reverseDirectionTiles);
        RegisterTiles(speedToggleTiles);
    }

    void RegisterTiles(IEnumerable<TileBase> tiles)
    {
        if (tiles == null)
            return;

        foreach (TileBase tile in tiles)
        {
            if (tile == null || registeredTiles.Contains(tile))
                continue;

            groundTileResolver.RegisterRuntimeHandler(tile, this);
            registeredTiles.Add(tile);
        }
    }

    void UnregisterResolverTiles()
    {
        if (groundTileResolver == null)
            return;

        foreach (TileBase tile in registeredTiles)
            groundTileResolver.UnregisterRuntimeHandler(tile, this);

        registeredTiles.Clear();
    }

    float ResolveTileSize()
    {
        if (groundTilemap != null && groundTilemap.layoutGrid != null)
            return Mathf.Max(0.0001f, groundTilemap.layoutGrid.cellSize.x);

        return 1f;
    }

    Vector2 GetCellCenterWorld(Vector3Int cell)
    {
        if (groundTilemap == null)
            return new Vector2(cell.x, cell.y);

        return groundTilemap.GetCellCenterWorld(cell);
    }

    static int GetObjectKey(GameObject go)
        => go != null ? go.GetHashCode() : 0;

    static bool ContainsTile(TileBase[] tiles, TileBase tile)
    {
        if (tile == null || tiles == null)
            return false;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == tile)
                return true;
        }

        return false;
    }

    static void EnsureBombFields()
    {
        if (bombLastPosField == null)
            bombLastPosField = typeof(Bomb).GetField("lastPos", BindingFlags.Instance | BindingFlags.NonPublic);

        if (bombCurrentTileCenterField == null)
            bombCurrentTileCenterField = typeof(Bomb).GetField("currentTileCenter", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    static void SetBombLogicalPosition(Bomb bomb, Vector2 position)
    {
        if (bomb == null)
            return;

        EnsureBombFields();
        bombLastPosField?.SetValue(bomb, position);
        bombCurrentTileCenterField?.SetValue(bomb, position);
    }

    static bool IsBattleMode5Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode5SceneName, System.StringComparison.Ordinal);
}
