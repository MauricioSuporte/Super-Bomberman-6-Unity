using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GroundTileResolver))]
public sealed class BattleMode4SpringTileController : MonoBehaviour, IGroundTileHandler
{
    const string BattleMode4SceneName = "BattleMode_4";

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap destructibleTilemap;
    [SerializeField] private Tilemap indestructibleTilemap;
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Spring Tiles")]
    [SerializeField] private TileBase[] springGroundTiles;

    [Header("Spring Ground Swap")]
    [SerializeField, Min(0.01f)] private float springSwapSeconds = 0.5f;
    [SerializeField] private Sprite[] springSwapSprites;

    [Header("Jump")]
    [SerializeField, Min(0.05f)] private float jumpSeconds = 0.8f;
    [SerializeField, Min(0f)] private float ascendSeconds = 0.1f;
    [SerializeField, Min(0f)] private float descendSeconds = 0.1f;
    [SerializeField, Min(0f)] private float heightTiles = 20f;
    [SerializeField, Min(0)] private int maxLandingTiles = 3;
    [SerializeField, Min(0f)] private float retriggerGraceSeconds = 0.08f;

    [Header("Shadow")]
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.75f, 0.32f);
    [SerializeField] private int shadowSortingOrder = -1;

    [Header("SFX")]
    [SerializeField] private AudioClip springSfx;

    readonly HashSet<TileBase> registeredTiles = new();
    readonly HashSet<MovementController> activeJumpers = new();
    readonly HashSet<MovementController> waitingForSpringExit = new();
    readonly Dictionary<MovementController, Vector3Int> occupiedSpringCells = new();
    readonly Dictionary<Vector3Int, Coroutine> swapRoutines = new();
    readonly List<Vector3Int> landingCandidates = new(32);
    readonly List<Vector3Int> safeComLandingCandidates = new(32);
    readonly Queue<Vector3Int> bfsQueue = new();
    readonly Dictionary<Vector3Int, int> bfsDistance = new();

    Tile[] springSwapTiles;
    Sprite generatedShadowSprite;

    public float JumpDurationSeconds => Mathf.Max(0.05f, jumpSeconds);

    void Awake()
    {
        ResolveReferences();
        BuildSpringSwapTiles();
        RegisterResolverTiles();
    }

    void OnEnable()
    {
        ResolveReferences();
        BuildSpringSwapTiles();
        RegisterResolverTiles();
    }

    void OnDisable()
    {
        UnregisterResolverTiles();

        foreach (var routine in swapRoutines.Values)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        swapRoutines.Clear();
        activeJumpers.Clear();
        waitingForSpringExit.Clear();
        occupiedSpringCells.Clear();
    }

    void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.ArenaUpdate.Auto();

        if (!IsBattleMode4Active())
            return;

        ResolveReferences();

        if (groundTilemap == null || springGroundTiles == null || springGroundTiles.Length == 0)
            return;

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
            TryLaunchIfStandingOnSpring(players[i]);
    }

    public bool TryModifyExplosion(
        BombController source,
        Vector2 worldPos,
        TileBase groundTile,
        ref int radius,
        ref bool pierce)
        => false;

    void TryLaunchIfStandingOnSpring(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null || activeJumpers.Contains(mover))
            return;

        if (!mover.CompareTag("Player") || mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
            return;

        Vector3Int springCell = groundTilemap.WorldToCell(mover.Rigidbody.position);
        TileBase groundTile = groundTilemap.GetTile(springCell);
        if (!IsSpringTile(groundTile))
        {
            waitingForSpringExit.Remove(mover);
            occupiedSpringCells.Remove(mover);
            return;
        }

        if (waitingForSpringExit.Contains(mover))
            return;

        if (occupiedSpringCells.TryGetValue(mover, out Vector3Int occupiedSpringCell) &&
            occupiedSpringCell == springCell)
        {
            return;
        }

        var pinkJump = mover.GetComponent<PinkLouieJumpAbility>();
        if (pinkJump != null && pinkJump.JumpActive)
            return;

        bool hasSpringAbility =
            mover.TryGetComponent(out BattleModeComStage4SpringEscapeAbility springAbility);
        if (!TryPickLandingCell(mover, springCell, out Vector3Int landingCell))
        {
            if (hasSpringAbility)
            {
                springAbility.LogSpringLaunchBlocked(GetCellCenter(springCell));
                return;
            }

            landingCell = springCell;
        }

        if (hasSpringAbility)
        {
            springAbility.LogSpringLaunch(
                GetCellCenter(springCell),
                GetCellCenter(landingCell));
        }

        occupiedSpringCells[mover] = springCell;
        StartCoroutine(SpringJumpRoutine(mover, springCell, landingCell));
    }

    IEnumerator SpringJumpRoutine(MovementController mover, Vector3Int springCell, Vector3Int landingCell)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        activeJumpers.Add(mover);
        StartSpringTileSwap(springCell);

        Rigidbody2D rb = mover.Rigidbody;
        float tileSize = Mathf.Max(0.0001f, mover.tileSize);
        Vector2 start = GetCellCenter(springCell);
        Vector2 end = GetCellCenter(landingCell);
        Vector2 faceDir = ResolveFacing(mover, start, end);

        ThrowHeldPowerGloveBomb(mover);
        CancelActiveMountMovementAbilities(mover);

        bool prevInputLocked = mover.InputLocked;
        bool prevPlayerExplosionInvulnerable = mover.explosionInvulnerable;
        bool prevBombEnabled = false;
        bool hadBombController = mover.TryGetComponent(out BombController bombController) && bombController != null;
        if (hadBombController)
            prevBombEnabled = bombController.enabled;

        Collider2D playerCollider = mover.GetComponent<Collider2D>();
        bool prevColliderEnabled = playerCollider != null && playerCollider.enabled;

        PlayerInputManager inputManager = PlayerInputManager.Instance;
        int playerId = mover.PlayerId;

        MountMovementController mountMovement = mover.GetComponentInChildren<MountMovementController>(true);
        bool prevMountExplosionInvulnerable = mountMovement != null && mountMovement.explosionInvulnerable;

        CharacterHealth[] affectedHealth = mover.GetComponentsInChildren<CharacterHealth>(true);
        var riding = mover.GetComponent<PlayerRidingController>();
        var mountVisual = mover.GetComponentInChildren<MountVisualController>(true);
        Vector2 prevMountLocalOffset = mountVisual != null ? mountVisual.localOffset : Vector2.zero;
        var shadow = CreateShadow(start);

        mover.SetInputLocked(true, forceIdle: false);
        mover.SetExternalMovementOverride(true);
        if (inputManager != null)
            inputManager.SetSpringLauncherInputGate(playerId, true);

        if (hadBombController)
            bombController.enabled = false;

        if (playerCollider != null)
            playerCollider.enabled = false;

        ApplyInvulnerability(mover, mountMovement, affectedHealth, true);
        PlaySpringSfx(mover);

        try
        {
            yield return PlayJumpVisualRoutine(
                mover,
                riding,
                mountVisual,
                rb,
                shadow,
                start,
                end,
                heightTiles * tileSize,
                faceDir);
        }
        finally
        {
            if (shadow != null)
                Destroy(shadow);

            if (mountVisual != null)
                mountVisual.localOffset = prevMountLocalOffset;

            ClearUnmountedSpringSprites(riding);

            if (mover != null)
            {
                rb.linearVelocity = Vector2.zero;
                mover.SetExternalMovementOverride(false);
                mover.SetVisualOverrideActive(false);

                if (!mover.IsEndingStage)
                {
                    rb.position = end;
                    mover.SetInputLocked(prevInputLocked, forceIdle: false);
                    mover.EnableExclusiveFromState();
                    mover.SetExplosionInvulnerable(prevPlayerExplosionInvulnerable);
                }
            }

            if (mover != null &&
                mover.TryGetComponent(out BattleModeComStage4SpringEscapeAbility springAbility))
            {
                springAbility.LogSpringLanding(start, end);
            }

            bool restoreGameplayState = mover == null || !mover.IsEndingStage;

            if (restoreGameplayState && mountMovement != null)
                mountMovement.SetExplosionInvulnerable(prevMountExplosionInvulnerable);

            if (restoreGameplayState)
                ApplyHealthInvulnerability(affectedHealth, false);

            if (restoreGameplayState && hadBombController && bombController != null)
                bombController.enabled = prevBombEnabled;

            if (restoreGameplayState && playerCollider != null)
                playerCollider.enabled = prevColliderEnabled;

            if (restoreGameplayState)
                TryResolvePlayerBounceAfterLanding(mover, faceDir);

            if (inputManager != null)
                inputManager.SetSpringLauncherInputGate(playerId, false);

            bool landedOnSpring = groundTilemap != null && IsSpringTile(groundTilemap.GetTile(landingCell));
            ReleaseJumperAfterGrace(mover, landedOnSpring);
        }
    }

    static void TryResolvePlayerBounceAfterLanding(MovementController mover, Vector2 landingFacing)
    {
        if (mover == null)
            return;

        Vector2 direction = Cardinalize(landingFacing);
        if (direction == Vector2.zero)
            direction = Vector2.right;

        if (mover.TryGetComponent(out PlayerPushedOutOfInvalidTile resolver) && resolver != null)
            resolver.NotifyExternalPushed(direction);
    }

    void CancelActiveMountMovementAbilities(MovementController mover)
    {
        if (mover == null)
            return;

        var greenDash = mover.GetComponent<GreenLouieDashAbility>();
        if (greenDash != null && greenDash.DashActive)
            greenDash.CancelDashForExternalInterruption();

        var blackDash = mover.GetComponent<BlackLouieDashPushAbility>();
        if (blackDash != null && blackDash.DashActive)
            blackDash.CancelDashForExternalInterruption();
    }

    static void ThrowHeldPowerGloveBomb(MovementController mover)
    {
        if (mover != null &&
            mover.TryGetComponent(out PowerGloveAbility powerGlove) &&
            powerGlove.IsHoldingBomb)
        {
            powerGlove.ThrowHeldBombForExternalTransition();
        }
    }

    void ReleaseJumperAfterGrace(MovementController mover, bool waitForSpringExit)
    {
        if (retriggerGraceSeconds <= 0f)
        {
            activeJumpers.Remove(mover);
            if (waitForSpringExit && mover != null)
                waitingForSpringExit.Add(mover);
            return;
        }

        StartCoroutine(ReleaseJumperAfterGraceRoutine(mover, waitForSpringExit));
    }

    IEnumerator ReleaseJumperAfterGraceRoutine(MovementController mover, bool waitForSpringExit)
    {
        yield return new WaitForSeconds(retriggerGraceSeconds);
        activeJumpers.Remove(mover);

        if (waitForSpringExit && mover != null)
            waitingForSpringExit.Add(mover);
    }

    IEnumerator PlayJumpVisualRoutine(
        MovementController mover,
        PlayerRidingController riding,
        MountVisualController mountVisual,
        Rigidbody2D rb,
        GameObject shadow,
        Vector2 start,
        Vector2 end,
        float heightWorld,
        Vector2 faceDir)
    {
        if (mover == null || rb == null)
            yield break;

        float total = Mathf.Max(0.05f, jumpSeconds);
        float up = Mathf.Clamp(ascendSeconds, 0f, total);
        float down = Mathf.Clamp(descendSeconds, 0f, total - up);
        float holdStart = up;
        float holdEnd = total - down;

        Vector2 baseMountOffset = mountVisual != null ? mountVisual.localOffset : Vector2.zero;
        bool mounted = mover.IsMounted;

        mover.SetVisualOverrideActive(true);
        mover.SetAllSpritesVisible(false);

        if (mounted && mountVisual != null && mountVisual.HasJumpVisuals())
            mountVisual.SetJumpVisual(true, faceDir, descending: false);

        float elapsed = 0f;
        while (elapsed < total)
        {
            if (mover.IsEndingStage)
                yield break;

            float t = Mathf.Clamp01(elapsed / total);
            Vector2 ground = Vector2.Lerp(start, end, t);
            rb.position = ground;

            if (shadow != null)
                shadow.transform.position = new Vector3(ground.x, ground.y, shadow.transform.position.z);

            float visualHeight = CalculateOffsetHeight(elapsed, up, holdStart, holdEnd, down, total, heightWorld);
            bool descending = elapsed >= holdEnd;

            if (mounted && mountVisual != null)
            {
                if (mountVisual.HasJumpVisuals())
                    mountVisual.SetJumpPhase(descending);

                mountVisual.localOffset = baseMountOffset + Vector2.up * visualHeight;
            }
            else
            {
                ApplyUnmountedSpringSprite(riding, faceDir, !descending, visualHeight);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = end;
        if (shadow != null)
            shadow.transform.position = new Vector3(end.x, end.y, shadow.transform.position.z);

        if (mounted && mountVisual != null)
        {
            mountVisual.localOffset = baseMountOffset;
            if (mountVisual.HasJumpVisuals())
                mountVisual.SetJumpVisual(false, faceDir);
        }
        else
        {
            ClearUnmountedSpringSprites(riding);
        }
    }

    float CalculateOffsetHeight(float elapsed, float up, float holdStart, float holdEnd, float down, float total, float heightWorld)
    {
        if (up > 0f && elapsed < holdStart)
            return Mathf.Lerp(0f, heightWorld, Mathf.Clamp01(elapsed / up));

        if (down > 0f && elapsed >= holdEnd)
            return Mathf.Lerp(heightWorld, 0f, Mathf.Clamp01((elapsed - holdEnd) / down));

        if (elapsed >= total)
            return 0f;

        return heightWorld;
    }

    void ApplyUnmountedSpringSprite(PlayerRidingController riding, Vector2 facing, bool ascending, float visualHeight)
    {
        if (riding == null)
            return;

        AnimatedSpriteRenderer target = PickUnmountedSpringRenderer(riding, facing, ascending);
        SetExclusiveUnmountedSpringRenderer(riding, target);

        if (target != null)
        {
            target.SetRuntimeBaseLocalY(visualHeight);
            target.RefreshFrame();
        }
    }

    AnimatedSpriteRenderer PickUnmountedSpringRenderer(PlayerRidingController riding, Vector2 facing, bool ascending)
    {
        Vector2 f = Cardinalize(facing);
        if (f == Vector2.zero)
            f = Vector2.down;

        if (ascending)
        {
            if (f == Vector2.up) return riding.mountAscendUp;
            if (f == Vector2.down) return riding.mountAscendDown;
            if (f == Vector2.left) return riding.mountAscendLeft;
            return riding.mountAscendRight;
        }

        if (f == Vector2.up) return riding.mountDescendUp;
        if (f == Vector2.down) return riding.mountDescendDown;
        if (f == Vector2.left) return riding.mountDescendLeft;
        return riding.mountDescendRight;
    }

    void SetExclusiveUnmountedSpringRenderer(PlayerRidingController riding, AnimatedSpriteRenderer target)
    {
        if (riding == null)
            return;

        SetAnimEnabled(riding.mountAscendUp, target == riding.mountAscendUp);
        SetAnimEnabled(riding.mountAscendDown, target == riding.mountAscendDown);
        SetAnimEnabled(riding.mountAscendLeft, target == riding.mountAscendLeft);
        SetAnimEnabled(riding.mountAscendRight, target == riding.mountAscendRight);
        SetAnimEnabled(riding.mountDescendUp, target == riding.mountDescendUp);
        SetAnimEnabled(riding.mountDescendDown, target == riding.mountDescendDown);
        SetAnimEnabled(riding.mountDescendLeft, target == riding.mountDescendLeft);
        SetAnimEnabled(riding.mountDescendRight, target == riding.mountDescendRight);

        ClearRuntimeOffsetIfNot(target, riding.mountAscendUp);
        ClearRuntimeOffsetIfNot(target, riding.mountAscendDown);
        ClearRuntimeOffsetIfNot(target, riding.mountAscendLeft);
        ClearRuntimeOffsetIfNot(target, riding.mountAscendRight);
        ClearRuntimeOffsetIfNot(target, riding.mountDescendUp);
        ClearRuntimeOffsetIfNot(target, riding.mountDescendDown);
        ClearRuntimeOffsetIfNot(target, riding.mountDescendLeft);
        ClearRuntimeOffsetIfNot(target, riding.mountDescendRight);
    }

    void ClearUnmountedSpringSprites(PlayerRidingController riding)
    {
        if (riding == null)
            return;

        SetExclusiveUnmountedSpringRenderer(riding, null);
    }

    bool TryPickLandingCell(
        MovementController mover,
        Vector3Int startCell,
        out Vector3Int landingCell)
    {
        BuildLandingCandidates(startCell);

        if (landingCandidates.Count <= 0)
        {
            landingCell = startCell;
            return false;
        }

        if (mover != null &&
            mover.TryGetComponent(out BattleModeComStage4SpringEscapeAbility springAbility))
        {
            safeComLandingCandidates.Clear();
            for (int i = 0; i < landingCandidates.Count; i++)
            {
                Vector3Int candidate = landingCandidates[i];
                if (springAbility.IsImmediateLandingSafe(GetCellCenter(candidate)))
                    safeComLandingCandidates.Add(candidate);
            }

            if (safeComLandingCandidates.Count <= 0)
            {
                landingCell = startCell;
                return false;
            }

            landingCell =
                safeComLandingCandidates[Random.Range(0, safeComLandingCandidates.Count)];
            return true;
        }

        landingCell = landingCandidates[Random.Range(0, landingCandidates.Count)];
        return true;
    }

    public void CopySpringWorldPositions(List<Vector2> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        ResolveReferences();
        if (groundTilemap == null)
            return;

        BoundsInt bounds = groundTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (IsSpringTile(groundTilemap.GetTile(cell)))
                destination.Add(GetCellCenter(cell));
        }
    }

    public bool CopyLandingWorldPositions(
        Vector2 springWorldPosition,
        List<Vector2> destination)
    {
        if (destination == null)
            return false;

        destination.Clear();
        ResolveReferences();
        if (groundTilemap == null)
            return false;

        Vector3Int springCell = groundTilemap.WorldToCell(springWorldPosition);
        if (!IsSpringTile(groundTilemap.GetTile(springCell)))
            return false;

        BuildLandingCandidates(springCell);
        for (int i = 0; i < landingCandidates.Count; i++)
            destination.Add(GetCellCenter(landingCandidates[i]));

        return destination.Count > 0;
    }

    public bool IsSpringWorldPosition(Vector2 worldPosition)
    {
        ResolveReferences();
        if (groundTilemap == null)
            return false;

        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        return IsSpringTile(groundTilemap.GetTile(cell));
    }

    void BuildLandingCandidates(Vector3Int startCell)
    {
        landingCandidates.Clear();
        bfsQueue.Clear();
        bfsDistance.Clear();

        bfsQueue.Enqueue(startCell);
        bfsDistance[startCell] = 0;

        while (bfsQueue.Count > 0)
        {
            Vector3Int current = bfsQueue.Dequeue();
            int distance = bfsDistance[current];

            if (distance > 0 && !IsCellBlocked(current))
                landingCandidates.Add(current);

            if (distance >= maxLandingTiles)
                continue;

            TryEnqueue(current + Vector3Int.up, distance + 1);
            TryEnqueue(current + Vector3Int.down, distance + 1);
            TryEnqueue(current + Vector3Int.left, distance + 1);
            TryEnqueue(current + Vector3Int.right, distance + 1);
        }
    }

    void TryEnqueue(Vector3Int next, int distance)
    {
        if (bfsDistance.ContainsKey(next))
            return;

        if (IsCellBlocked(next))
            return;

        bfsDistance[next] = distance;
        bfsQueue.Enqueue(next);
    }

    bool IsCellBlocked(Vector3Int cell)
    {
        if (destructibleTilemap != null && destructibleTilemap.GetTile(cell) != null)
            return true;

        if (indestructibleTilemap != null && indestructibleTilemap.GetTile(cell) != null)
            return true;

        if (groundTilemap == null || groundTilemap.GetTile(cell) == null)
            return true;

        return HasBombAt(GetCellCenter(cell));
    }

    bool HasBombAt(Vector2 world)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (Vector2.Distance(bomb.GetLogicalPosition(), world) <= 0.35f)
                return true;
        }

        return false;
    }

    void StartSpringTileSwap(Vector3Int cell)
    {
        if (groundTilemap == null || springSwapTiles == null || springSwapTiles.Length == 0)
            return;

        if (swapRoutines.TryGetValue(cell, out Coroutine running) && running != null)
            return;

        swapRoutines[cell] = StartCoroutine(SpringTileSwapRoutine(cell));
    }

    IEnumerator SpringTileSwapRoutine(Vector3Int cell)
    {
        TileBase originalTile = groundTilemap.GetTile(cell);
        Matrix4x4 originalTransform = groundTilemap.GetTransformMatrix(cell);

        float duration = Mathf.Max(0.01f, springSwapSeconds);
        float elapsed = 0f;
        int lastFrame = -1;

        while (elapsed < duration)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt((elapsed / duration) * springSwapTiles.Length), 0, springSwapTiles.Length - 1);
            if (frame != lastFrame)
            {
                groundTilemap.SetTile(cell, springSwapTiles[frame]);
                groundTilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                lastFrame = frame;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        groundTilemap.SetTile(cell, originalTile);
        groundTilemap.SetTransformMatrix(cell, originalTransform);
        swapRoutines.Remove(cell);
    }

    void ResolveReferences()
    {
        if (groundTileResolver == null)
            groundTileResolver = GetComponent<GroundTileResolver>();

        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            if (groundTilemap == null)
                groundTilemap = gm.groundTilemap;
            if (destructibleTilemap == null)
                destructibleTilemap = gm.destructibleTilemap;
            if (indestructibleTilemap == null)
                indestructibleTilemap = gm.indestructibleTilemap;
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

        RegisterTiles(springGroundTiles);
        RegisterTiles(springSwapTiles);
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

    void BuildSpringSwapTiles()
    {
        if (springSwapSprites == null || springSwapSprites.Length == 0)
        {
            springSwapTiles = null;
            return;
        }

        springSwapTiles = new Tile[springSwapSprites.Length];
        for (int i = 0; i < springSwapSprites.Length; i++)
        {
            if (springSwapSprites[i] == null)
                continue;

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"BattleMode4SpringSwap_{i:00}";
            tile.sprite = springSwapSprites[i];
            springSwapTiles[i] = tile;
        }
    }

    GameObject CreateShadow(Vector2 position)
    {
        var go = new GameObject("BattleMode4SpringShadow");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(Mathf.Max(0.01f, shadowScale.x), Mathf.Max(0.01f, shadowScale.y), 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetGeneratedShadowSprite();
        sr.color = shadowColor;
        sr.sortingOrder = shadowSortingOrder;

        return go;
    }

    Sprite GetGeneratedShadowSprite()
    {
        if (generatedShadowSprite != null)
            return generatedShadowSprite;

        Texture2D tex = new(16, 16, TextureFormat.RGBA32, false);
        tex.name = "BattleMode4GeneratedShadow";

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 p = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
                float alpha = p.sqrMagnitude <= 1f ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        generatedShadowSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        generatedShadowSprite.name = "BattleMode4GeneratedShadowSprite";
        return generatedShadowSprite;
    }

    void ApplyInvulnerability(MovementController mover, MountMovementController mountMovement, CharacterHealth[] healths, bool enabled)
    {
        if (mover != null)
            mover.SetExplosionInvulnerable(enabled);

        if (mountMovement != null)
            mountMovement.SetExplosionInvulnerable(enabled);

        ApplyHealthInvulnerability(healths, enabled);
    }

    void ApplyHealthInvulnerability(CharacterHealth[] healths, bool enabled)
    {
        if (healths == null)
            return;

        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] != null)
                healths[i].SetExternalInvulnerability(enabled);
        }
    }

    void PlaySpringSfx(MovementController mover)
    {
        if (springSfx == null || mover == null)
            return;

        if (mover.TryGetComponent(out AudioSource audio) && audio != null)
            audio.PlayOneShot(springSfx);
        else
            AudioSource.PlayClipAtPoint(springSfx, mover.transform.position);
    }

    bool IsSpringTile(TileBase tile)
    {
        if (tile == null)
            return false;

        return ContainsTile(springGroundTiles, tile) || ContainsTile(springSwapTiles, tile);
    }

    static bool ContainsTile(TileBase[] tiles, TileBase tile)
    {
        if (tiles == null || tile == null)
            return false;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == tile)
                return true;
        }

        return false;
    }

    Vector2 GetCellCenter(Vector3Int cell)
    {
        if (groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);

        return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }

    static Vector2 ResolveFacing(MovementController mover, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude > 0.0001f)
            return Cardinalize(delta);

        if (mover != null && mover.FacingDirection != Vector2.zero)
            return Cardinalize(mover.FacingDirection);

        return Vector2.down;
    }

    static Vector2 Cardinalize(Vector2 v)
    {
        if (v == Vector2.zero)
            return Vector2.zero;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;
        if (r.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            sr.enabled = on;
    }

    static void ClearRuntimeOffsetIfNot(AnimatedSpriteRenderer keep, AnimatedSpriteRenderer current)
    {
        if (current == null || current == keep)
            return;

        current.ClearRuntimeBaseOffset();
    }

    static bool IsBattleMode4Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode4SceneName, System.StringComparison.Ordinal);
}
