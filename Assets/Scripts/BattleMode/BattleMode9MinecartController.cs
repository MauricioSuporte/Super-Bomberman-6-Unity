using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BattleMode9MinecartController : MonoBehaviour
{
    const string BattleMode9SceneName = "BattleMode_9";
    const string BattleMode12SceneName = "BattleMode_12";
    const string DefaultEnterSfxResourcesPath = "Sounds/start";
    const string DefaultExitSfxResourcesPath = "Sounds/start";

    [System.Serializable]
    struct RailSegment
    {
        public Vector2Int startCell;
        public Vector2Int endCell;
    }

    [System.Serializable]
    struct PortalRailRoute
    {
        public Vector2Int startCell;
        public Vector2Int cornerCell;
        public Vector2Int portalCell;
    }

    [Header("Rail")]
    [SerializeField] private Vector2Int stationCell = new(-1, -4);
    [SerializeField] private Vector2Int exitCellOffset = new(1, 0);
    [SerializeField]
    private RailSegment[] railSegments =
    {
        new() { startCell = new Vector2Int(-1, -4), endCell = new Vector2Int(3, -4) },
        new() { startCell = new Vector2Int(3, -3), endCell = new Vector2Int(3, 2) },
        new() { startCell = new Vector2Int(2, 2), endCell = new Vector2Int(-5, 2) },
        new() { startCell = new Vector2Int(-5, 1), endCell = new Vector2Int(-5, -4) },
        new() { startCell = new Vector2Int(-4, -4), endCell = new Vector2Int(-1, -4) },
    };

    [Header("Battle Mode 12 Portal Routes")]
    [SerializeField]
    private PortalRailRoute[] battleMode12Routes =
    {
        new() { startCell = new Vector2Int(-5, 1), cornerCell = new Vector2Int(-5, -2), portalCell = new Vector2Int(-3, -2) },
        new() { startCell = new Vector2Int(-3, -4), cornerCell = new Vector2Int(1, -4), portalCell = new Vector2Int(1, -2) },
        new() { startCell = new Vector2Int(3, -3), cornerCell = new Vector2Int(3, 0), portalCell = new Vector2Int(1, 0) },
        new() { startCell = new Vector2Int(1, 2), cornerCell = new Vector2Int(-3, 2), portalCell = new Vector2Int(-3, 0) },
    };

    [Header("Battle Mode 12 Teleport")]
    [SerializeField, Min(0.01f)] private float teleportSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float portalSinkSeconds = 0.1f;
    [SerializeField] private bool spawnTeleportStars = true;
    [SerializeField] private Sprite[] teleportStarSprites;
    [SerializeField, Min(0)] private int teleportStarCount = 32;
    [SerializeField, Min(0.01f)] private float teleportStarLifetime = 0.5f;
    [SerializeField] private Vector2 teleportStarScaleRange = new(0.24f, 0.42f);
    [SerializeField] private Vector2 teleportStarDriftRange = new(0.16f, 0.42f);
    [SerializeField] private Vector2 teleportStarSpinRange = new(-220f, 220f);
    [SerializeField, Range(0f, 1f)] private float teleportStarPathJitter = 0.22f;
    [SerializeField, Min(0.01f)] private float teleportStarAnimationFrameTime = 0.1f;
    [SerializeField] private int teleportStarSortingOrder = 90;

    [Header("Motion")]
    [SerializeField, Min(0.05f)] private float tilesPerSecond = 5f;
    [SerializeField, Min(0.01f)] private float enterAnimationSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float exitAnimationSeconds = 0.5f;
    [SerializeField, Min(0f)] private float visualHopHeightTiles = 1.75f;
    [SerializeField, Min(0f)] private float retriggerGraceSeconds = 0.08f;

    [Header("Pixel Perfect")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [SerializeField] private bool useIntegerPixelSteps = true;

    [Header("Cart Visuals")]
    [SerializeField] private GameObject minecartPrefab;
    [SerializeField] private AnimatedSpriteRenderer down;
    [SerializeField] private AnimatedSpriteRenderer up;
    [SerializeField] private AnimatedSpriteRenderer left;
    [SerializeField] private AnimatedSpriteRenderer right;
    [SerializeField] private bool hideCartWhenIdleAtStation = false;
    [SerializeField] private int cartSortingOrder = 4;

    [Header("Rider HeadOnly (child names)")]
    [SerializeField] private string headOnlyUpName = "HeadOnlyUp";
    [SerializeField] private string headOnlyDownName = "HeadOnlyDown";
    [SerializeField] private string headOnlyLeftName = "HeadOnlyLeft";
    [SerializeField] private string headOnlyRightName = "HeadOnlyRight";
    [SerializeField] private Vector2 headOnlyUpOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyDownOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyLeftOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyRightOffset = Vector2.zero;

    [Header("Mount HeadOnly Offsets (local, per direction)")]
    [SerializeField] private Vector2 mountHeadOnlyUpOffset = Vector2.zero;
    [SerializeField] private Vector2 mountHeadOnlyDownOffset = Vector2.zero;
    [SerializeField] private Vector2 mountHeadOnlyLeftOffset = Vector2.zero;
    [SerializeField] private Vector2 mountHeadOnlyRightOffset = Vector2.zero;

    [Header("Cart Hitbox")]
    [SerializeField] private Vector2 hitboxSize = new(0.8f, 0.8f);
    [SerializeField] private LayerMask collisionMask = 0;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private bool spawnDestructibleBreakPrefab = true;
    [SerializeField] private bool spawnHiddenObjectFromDestroyedBlock = true;

    [Header("SFX")]
    [SerializeField] private AudioClip enterSfx;
    [SerializeField] private AudioClip portalEnterSfx;
    [SerializeField] private AudioClip exitSfx;
    [SerializeField] private AudioClip rideLoopSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float portalEnterSfxVolume = 1f;

    Tilemap groundTilemap;
    Tilemap destructibleTilemap;
    Tilemap indestructibleTilemap;
    AudioSource audioSource;
    BombController destructibleClearer;
    BattleSuddenDeathController suddenDeathController;

    readonly List<Vector3Int> railPath = new();
    readonly HashSet<MovementController> activeRiders = new();
    readonly HashSet<MovementController> waitingForStationExit = new();
    readonly Dictionary<MovementController, RideState> activeStates = new();
    readonly Collider2D[] hitBuffer = new Collider2D[32];
    readonly HashSet<GameObject> processedThisFrame = new();
    readonly List<AnimatedSpriteRenderer> riderRenderers = new();
    readonly List<GameObject> activeTeleportStars = new();
    readonly List<SpriteRenderer> portalVisualRenderers = new();

    Transform cartVisualRoot;
    Vector2 currentCartDirection = Vector2.right;
    MovementController currentRider;
    float cartMovePixelAccumulator;
    bool rideLoopPausedForGamePause;
    bool cartDestroyedBySuddenDeath;
    int battleMode12CurrentRouteIndex;

    sealed class RideState
    {
        public bool prevInputLocked;
        public bool prevPlayerExplosionInvulnerable;
        public bool prevMountExplosionInvulnerable;
        public bool prevBombEnabled;
        public bool hadBombController;
        public bool prevColliderEnabled;
        public BombController bombController;
        public Collider2D playerCollider;
        public MountMovementController mountMovement;
        public CharacterHealth[] healths;
        public PlayerMountCompanion mountCompanion;
        public MountVisualController mountVisual;
        public MountEggQueue eggQueue;
        public IPinkLouieJumpExternalAnimator pinkJumpAnimator;
        public AnimatedSpriteRenderer headUp;
        public AnimatedSpriteRenderer headDown;
        public AnimatedSpriteRenderer headLeft;
        public AnimatedSpriteRenderer headRight;
        public Vector2 startFacing;
        public Dictionary<SpriteRenderer, int> previousSortingOrders;
        public bool forcedExit;
        public Vector2 forcedExitWorld;
        public Vector2 forcedExitFacing;
        public Vector3Int forcedExitCell;
        public bool forcedExitOnSuddenDeathIndestructible;
        public bool cartDestroyedBySuddenDeath;
    }

    readonly struct PortalVisualSnapshot
    {
        public readonly Transform transform;
        public readonly Vector3 localScale;
        public readonly Vector3 localPosition;
        public readonly bool rendererEnabled;

        public PortalVisualSnapshot(SpriteRenderer renderer)
        {
            transform = renderer != null ? renderer.transform : null;
            localScale = transform != null ? transform.localScale : Vector3.one;
            localPosition = transform != null ? transform.localPosition : Vector3.zero;
            rendererEnabled = renderer != null && renderer.enabled;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapOnInitialScene()
    {
        EnsureForActiveScene();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    static void EnsureForActiveScene()
    {
        if (!Application.isPlaying)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsSupportedScene(activeScene.name))
            return;

        if (FindAnyObjectByType<BattleMode9MinecartController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode9MinecartController));
        host.AddComponent<BattleMode9MinecartController>();
    }

    void Awake()
    {
        if (!IsSupportedSceneActive())
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        EnsureAudioSource();
        LoadDefaultSfxIfNeeded();
        EnsureCartVisuals();
        ConfigureForActiveScene();
        BuildRailPath();
        SnapCartToStation();
        currentCartDirection = GetIdleDirection();
        UpdateCartVisual(currentCartDirection, moving: false);
        SetCartVisible(!cartDestroyedBySuddenDeath && !hideCartWhenIdleAtStation);
    }

    void OnDisable()
    {
        foreach (var pair in activeStates)
            RestoreRideState(pair.Key, pair.Value);

        ClearActiveTeleportStars();
        activeStates.Clear();
        activeRiders.Clear();
        waitingForStationExit.Clear();
        currentRider = null;
        StopRideLoopSfx();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveCartVisualReferences(FindExistingCartVisualRoot() ?? transform);
            currentCartDirection = GetIdleDirection();
            ForceOnlyCartNoEnsure(PickCartRenderer(currentCartDirection));
        }
    }

    void Update()
    {
        if (!IsSupportedSceneActive())
            return;

        ResolveReferences();
        ConfigureForActiveScene();
        SyncRideLoopSfxWithPause();

        if (cartDestroyedBySuddenDeath)
        {
            SetCartVisible(false);
            return;
        }

        if (railPath.Count == 0)
            BuildRailPath();

        if (currentRider != null)
            return;

        Vector3Int station = GetActiveStationCell();
        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            if (TryHandlePlayerAtStation(players[i], station))
                break;
        }
    }

    bool TryHandlePlayerAtStation(MovementController mover, Vector3Int station)
    {
        if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
            return false;

        if (mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
            return false;

        Vector3Int currentCell = WorldToCell(mover.Rigidbody.position);

        if (waitingForStationExit.Contains(mover))
        {
            if (currentCell == station)
                return false;

            waitingForStationExit.Remove(mover);
            return false;
        }

        if (activeRiders.Contains(mover) || currentCell != station)
            return false;

        StartCoroutine(RideRoutine(mover));
        return true;
    }

    IEnumerator RideRoutine(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        activeRiders.Add(mover);
        currentRider = mover;

        bool battleMode12 = IsBattleMode12Active();
        int startRouteIndex = GetValidBattleMode12RouteIndex(battleMode12CurrentRouteIndex);
        int destinationRouteIndex = startRouteIndex;
        Vector3Int station = battleMode12 ? ToCell(battleMode12Routes[startRouteIndex].startCell) : ToCell(stationCell);
        Vector2 stationWorld = GetCellCenter(station);
        Vector3Int exitCell = battleMode12 ? station : station + new Vector3Int(exitCellOffset.x, exitCellOffset.y, 0);
        Vector2 exitWorld = GetCellCenter(exitCell);
        Vector2 startFacing = battleMode12 ? GetRouteStartDirection(startRouteIndex) : Vector2.right;
        Vector2 exitFacing = battleMode12 ? startFacing : Cardinalize(exitWorld - stationWorld);
        if (exitFacing == Vector2.zero)
            exitFacing = Vector2.right;
        Vector2 enterFacing = ResolveEnterHopFacing(mover, exitFacing);

        CancelActiveMountMovementAbilities(mover);
        RideState state = CaptureAndApplyRideState(mover);
        activeStates[mover] = state;

        SetCartVisible(true);
        SetCartWorldPosition(stationWorld);
        currentCartDirection = startFacing != Vector2.zero ? startFacing : Vector2.right;
        UpdateCartVisual(currentCartDirection, moving: false);

        PlayOneShot(enterSfx);

        try
        {
            yield return PlayEnterExitVisualRoutine(mover, state, stationWorld, stationWorld, enterFacing, enterAnimationSeconds, "Enter");
            SetRideCompanionsVisible(state, false);
            ShowRiderHeadOnly(state, currentCartDirection);
            PlayRideLoopSfx();

            if (battleMode12)
            {
                yield return MoveCartThroughBattleMode12Route(mover, state, startRouteIndex, route => destinationRouteIndex = route);
                exitFacing = GetRouteExitDirection(destinationRouteIndex);
                if (exitFacing == Vector2.zero)
                    exitFacing = GetRouteStartDirection(destinationRouteIndex);

                Vector3Int routeStartCell = ToCell(battleMode12Routes[destinationRouteIndex].startCell);
                exitCell = ResolveBattleMode12ExitCell(routeStartCell, ref exitFacing);
                exitWorld = GetCellCenter(exitCell);
            }
            else
            {
                yield return MoveCartAroundRail(mover, state);
            }

            Vector2 actualExitWorld = state.forcedExit ? state.forcedExitWorld : exitWorld;
            Vector2 actualExitFacing = state.forcedExit ? state.forcedExitFacing : exitFacing;
            Vector2 exitStartWorld = (state.forcedExit || battleMode12) && mover != null && mover.Rigidbody != null
                ? mover.Rigidbody.position
                : stationWorld;

            StopRideLoopSfx();
            HideRiderHeadOnly(state);
            SetRideCompanionsVisible(state, true);

            if (!state.cartDestroyedBySuddenDeath)
            {
                PlayOneShot(exitSfx);
                yield return PlayEnterExitVisualRoutine(mover, state, exitStartWorld, actualExitWorld, actualExitFacing, exitAnimationSeconds, "Exit");
            }
            else
            {
                SetCartVisible(false);
            }

            if (mover != null)
            {
                mover.SnapToWorldPoint(actualExitWorld, roundToGrid: false);
                TryResolvePlayerBounceAfterExit(mover, actualExitFacing, actualExitWorld);
            }

            if (state.eggQueue != null)
                state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
        }
        finally
        {
            StopRideLoopSfx();
            RestoreRideState(mover, state);
            if (mover != null)
            {
                Vector2 finalFacing = state.forcedExit ? state.forcedExitFacing : exitFacing;
                mover.ForceFacingDirection(finalFacing);
                ForceMountedVisualFacing(mover, finalFacing);
                RestoreMountedHeadOnlyOffsets(state);
            }
            activeStates.Remove(mover);
            currentRider = null;
            if (!cartDestroyedBySuddenDeath)
            {
                if (battleMode12)
                {
                    battleMode12CurrentRouteIndex = destinationRouteIndex;
                    Vector2 idlePosition = GetCellCenter(ToCell(battleMode12Routes[battleMode12CurrentRouteIndex].startCell));
                    Vector2 idleDirection = GetRouteStartDirection(battleMode12CurrentRouteIndex);
                    SetCartWorldPosition(idlePosition);
                    UpdateCartVisual(idleDirection, moving: false);
                }
                else
                {
                    SetCartWorldPosition(stationWorld);
                    UpdateCartVisual(Vector2.right, moving: false);
                }

                SetCartVisible(!hideCartWhenIdleAtStation);
            }
            else
            {
                SetCartVisible(false);
            }
            StartCoroutine(ReleaseRiderAfterGrace(mover));
        }
    }

    void TryResolvePlayerBounceAfterExit(MovementController mover, Vector2 exitFacing, Vector2 exitWorld)
    {
        if (mover == null)
            return;

        Vector2 dir = Cardinalize(exitFacing);
        if (dir == Vector2.zero)
            dir = Vector2.right;

        if (!mover.TryGetComponent<PlayerPushedOutOfInvalidTile>(out var resolver) || resolver == null)
            return;

        resolver.NotifyExternalPushed(dir);
    }

    void LateUpdate()
    {
        if (!IsSupportedSceneActive())
            return;

        SyncRideLoopSfxWithPause();
    }

    void ForceMountedVisualFacing(MovementController mover, Vector2 facing)
    {
        if (mover == null || !mover.IsMounted)
            return;

        var mountVisual = mover.GetComponentInChildren<MountVisualController>(true);
        if (mountVisual == null)
            return;

        if (!mountVisual.enabled)
            mountVisual.enabled = true;

        mountVisual.SetJumpVisual(false, facing);
    }

    void RestoreMountedHeadOnlyOffsets(RideState state)
    {
        if (state?.mountVisual == null || !state.mountVisual.useHeadOnlyPlayerVisual)
            return;

        ClearHeadOffsets(state);
        state.mountVisual.RefreshHeadOnlyPlayerOffsets();
    }

    IEnumerator MoveCartAroundRail(MovementController mover, RideState state)
    {
        yield return MoveCartAlongPath(mover, state, railPath);
    }

    IEnumerator MoveCartThroughBattleMode12Route(
        MovementController mover,
        RideState state,
        int sourceRouteIndex,
        System.Action<int> setDestinationRouteIndex)
    {
        if (!HasUsableBattleMode12Routes())
            yield break;

        sourceRouteIndex = GetValidBattleMode12RouteIndex(sourceRouteIndex);
        int destinationRouteIndex = GetRandomOtherBattleMode12RouteIndex(sourceRouteIndex);
        setDestinationRouteIndex?.Invoke(destinationRouteIndex);

        List<Vector3Int> sourcePath = BuildBattleMode12PathToPortal(sourceRouteIndex);
        yield return MoveCartAlongPath(mover, state, sourcePath);

        if (state != null && state.cartDestroyedBySuddenDeath)
            yield break;

        Vector3Int sourcePortal = ToCell(battleMode12Routes[sourceRouteIndex].portalCell);
        Vector3Int destinationPortal = ToCell(battleMode12Routes[destinationRouteIndex].portalCell);
        Vector2 destinationPortalFacing = GetRoutePortalExitDirection(destinationRouteIndex);
        yield return TeleportCartBetweenPortals(mover, state, sourcePortal, destinationPortal, destinationPortalFacing);

        if (state != null && state.cartDestroyedBySuddenDeath)
            yield break;

        List<Vector3Int> destinationPath = BuildBattleMode12PathFromPortalToStart(destinationRouteIndex);
        yield return MoveCartAlongPath(mover, state, destinationPath);
    }

    IEnumerator MoveCartAlongPath(MovementController mover, RideState state, List<Vector3Int> path)
    {
        if (path == null || path.Count == 0)
            yield break;

        float tileSize = Mathf.Max(0.0001f, mover != null ? mover.tileSize : 1f);
        float speed = Mathf.Max(0.05f, tilesPerSecond) * tileSize;
        var fixedWait = new WaitForFixedUpdate();

        for (int i = 1; i < path.Count; i++)
        {
            if (state != null && state.cartDestroyedBySuddenDeath)
                yield break;

            Vector2 start = QuantizeToPixelGrid(GetCellCenter(path[i - 1]));
            Vector2 end = QuantizeToPixelGrid(GetCellCenter(path[i]));
            Vector2 dir = Cardinalize(end - start);
            if (dir == Vector2.zero)
                dir = currentCartDirection;

            if (TryForceExitBeforeIndestructibleRailCell(state, path[i], end, dir, "segment-start"))
                yield break;

            currentCartDirection = dir;
            UpdateCartVisual(dir, moving: true);
            ShowRiderHeadOnly(state, dir);

            Vector2 position = start;
            ApplyCartRidePosition(mover, state, position, dir);
            ResetCartPixelAccumulator();

            while (Vector2.Distance(position, end) > PixelWorldStep * 0.5f)
            {
                if (state != null && state.cartDestroyedBySuddenDeath)
                    yield break;

                if (GamePauseController.IsPaused)
                {
                    yield return fixedWait;
                    continue;
                }

                if (TryForceExitBeforeIndestructibleRailCell(state, path[i], end, dir, "segment-moving"))
                    yield break;

                float moveWorld = GetQuantizedCartMoveWorld(speed * Time.fixedDeltaTime);
                if (moveWorld > 0f)
                {
                    position = Vector2.MoveTowards(position, end, moveWorld);
                    position = QuantizeToPixelGrid(position);

                    ApplyCartRidePosition(mover, state, position, dir);
                    HandleCartCollisions(position, dir, mover);
                }

                yield return fixedWait;
            }

            ApplyCartRidePosition(mover, state, end, dir);
            HandleCartCollisions(end, dir, mover);
        }

        UpdateCartVisual(currentCartDirection, moving: false);
    }

    IEnumerator TeleportCartBetweenPortals(
        MovementController mover,
        RideState state,
        Vector3Int sourcePortal,
        Vector3Int destinationPortal,
        Vector2 destinationFacing)
    {
        Vector2 source = QuantizeToPixelGrid(GetCellCenter(sourcePortal));
        Vector2 destination = QuantizeToPixelGrid(GetCellCenter(destinationPortal));
        float duration = Mathf.Max(0.01f, teleportSeconds);
        float elapsed = 0f;
        int spawnedStars = 0;

        ClearActiveTeleportStars();
        PlayOneShot(portalEnterSfx, portalEnterSfxVolume);
        yield return AnimatePortalSinkRise(mover, state, source, appearing: false);

        SetCartVisible(false);
        SetTeleportRiderVisualsVisible(state, false);
        MoveRiderWithCart(mover, source);

        if (spawnTeleportStars && teleportStarCount > 0)
        {
            SpawnTeleportStar(source);
            spawnedStars = 1;
        }

        while (elapsed < duration)
        {
            if (state != null && state.cartDestroyedBySuddenDeath)
            {
                ClearActiveTeleportStars();
                yield break;
            }

            if (mover == null || mover.Rigidbody == null)
            {
                ClearActiveTeleportStars();
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float nextElapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = Mathf.Clamp01(nextElapsed / duration);
            Vector2 position = QuantizeToPixelGrid(Vector2.Lerp(source, destination, SmoothTeleportT(t)));

            MoveRiderWithCart(mover, position);
            spawnedStars = SpawnTeleportStarsAlongPath(source, destination, t, spawnedStars);

            elapsed = nextElapsed;
            yield return null;
        }

        ClearActiveTeleportStars();
        Vector2 facing = Cardinalize(destinationFacing);
        if (facing == Vector2.zero)
            facing = currentCartDirection != Vector2.zero ? currentCartDirection : Vector2.right;

        currentCartDirection = facing;
        UpdateCartVisual(facing, moving: false);
        ApplyCartRidePosition(mover, state, destination, facing);
        SetCartVisible(!cartDestroyedBySuddenDeath);
        SetTeleportRiderVisualsVisible(state, true);
        yield return AnimatePortalSinkRise(mover, state, destination, appearing: true);
    }

    void SetTeleportRiderVisualsVisible(RideState state, bool visible)
    {
        if (visible)
        {
            SetRideCompanionsVisible(state, false);
            ShowRiderHeadOnly(state, currentCartDirection);
            return;
        }

        HideRiderHeadOnly(state);

        if (state?.mountVisual != null)
        {
            state.mountVisual.SetCartHeadOnlyVisual(false, currentCartDirection);
            state.mountVisual.ClearCartHeadOnlyOffsets();
        }

        if (state?.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(false);

        if (state?.eggQueue != null)
            state.eggQueue.ForceVisible(false);
    }

    IEnumerator AnimatePortalSinkRise(MovementController mover, RideState state, Vector2 portalWorld, bool appearing)
    {
        ApplyCartRidePosition(mover, state, portalWorld, currentCartDirection);
        SetCartVisible(!cartDestroyedBySuddenDeath);
        ShowRiderHeadOnly(state, currentCartDirection);

        List<PortalVisualSnapshot> snapshots = CapturePortalVisualSnapshots(mover);
        if (snapshots.Count == 0)
            yield break;

        float duration = Mathf.Max(0.01f, portalSinkSeconds);
        float elapsed = 0f;
        float initialProgress = appearing ? 0f : 1f;

        ApplyPortalSinkWorldOffset(mover, state, portalWorld, initialProgress);
        ApplyPortalVisualProgress(snapshots, initialProgress);

        while (elapsed < duration)
        {
            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float progress = appearing ? t : 1f - t;
            ApplyPortalSinkWorldOffset(mover, state, portalWorld, progress);
            ApplyPortalVisualProgress(snapshots, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        float finalProgress = appearing ? 1f : 0f;
        ApplyPortalSinkWorldOffset(mover, state, portalWorld, finalProgress);
        ApplyPortalVisualProgress(snapshots, finalProgress);
        RestorePortalVisualSnapshots(snapshots);
        ApplyCartRidePosition(mover, state, portalWorld, currentCartDirection);
    }

    List<PortalVisualSnapshot> CapturePortalVisualSnapshots(MovementController mover)
    {
        portalVisualRenderers.Clear();

        if (cartVisualRoot != null)
            AddPortalVisualRenderers(cartVisualRoot);

        var snapshots = new List<PortalVisualSnapshot>(portalVisualRenderers.Count);
        var seen = new HashSet<SpriteRenderer>();
        for (int i = 0; i < portalVisualRenderers.Count; i++)
        {
            SpriteRenderer renderer = portalVisualRenderers[i];
            if (renderer == null || !seen.Add(renderer) || !renderer.enabled || renderer.sprite == null)
                continue;

            snapshots.Add(new PortalVisualSnapshot(renderer));
        }

        return snapshots;
    }

    void AddPortalVisualRenderers(Transform root)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                portalVisualRenderers.Add(renderers[i]);
        }
    }

    void ApplyPortalVisualProgress(List<PortalVisualSnapshot> snapshots, float visibleProgress)
    {
        visibleProgress = Mathf.Clamp01(visibleProgress);
        float sinkOffset = (1f - visibleProgress) * -0.25f;

        for (int i = 0; snapshots != null && i < snapshots.Count; i++)
        {
            PortalVisualSnapshot snapshot = snapshots[i];
            if (snapshot.transform == null)
                continue;

            Vector3 scale = snapshot.localScale;
            scale.y = snapshot.localScale.y * visibleProgress;
            snapshot.transform.localScale = scale;

            Vector3 position = snapshot.localPosition;
            position.y = snapshot.localPosition.y + sinkOffset;
            snapshot.transform.localPosition = position;
        }
    }

    void ApplyPortalSinkWorldOffset(MovementController mover, RideState state, Vector2 portalWorld, float visibleProgress)
    {
        float tileSize = Mathf.Max(0.0001f, mover != null ? mover.tileSize : 1f);
        float sinkWorldOffset = (1f - Mathf.Clamp01(visibleProgress)) * -(tileSize * 0.5f);
        Vector2 position = portalWorld + Vector2.up * sinkWorldOffset;
        ApplyCartRidePosition(mover, state, position, currentCartDirection);
    }

    void RestorePortalVisualSnapshots(List<PortalVisualSnapshot> snapshots)
    {
        for (int i = 0; snapshots != null && i < snapshots.Count; i++)
        {
            PortalVisualSnapshot snapshot = snapshots[i];
            if (snapshot.transform == null)
                continue;

            snapshot.transform.localScale = snapshot.localScale;
            snapshot.transform.localPosition = snapshot.localPosition;
        }
    }

    int SpawnTeleportStarsAlongPath(Vector2 source, Vector2 destination, float normalizedTime, int alreadySpawned)
    {
        if (!spawnTeleportStars || teleportStarSprites == null || teleportStarSprites.Length == 0)
            return alreadySpawned;

        int targetCount = Mathf.FloorToInt(Mathf.Clamp01(normalizedTime) * teleportStarCount);
        while (alreadySpawned < targetCount)
        {
            float pathT = teleportStarCount <= 1 ? 1f : alreadySpawned / (float)(teleportStarCount - 1);
            Vector2 anchor = Vector2.Lerp(source, destination, SmoothTeleportT(pathT));
            Vector2 jitter = Random.insideUnitCircle * teleportStarPathJitter;
            SpawnTeleportStar(anchor + jitter);
            alreadySpawned++;
        }

        return alreadySpawned;
    }

    void SpawnTeleportStar(Vector2 position)
    {
        Sprite sprite = GetRandomTeleportStarSprite();
        if (sprite == null)
            return;

        var star = new GameObject("BattleMode12TeleportStar");
        activeTeleportStars.Add(star);
        star.transform.SetParent(transform, worldPositionStays: true);
        star.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

        float minScale = Mathf.Min(teleportStarScaleRange.x, teleportStarScaleRange.y);
        float maxScale = Mathf.Max(teleportStarScaleRange.x, teleportStarScaleRange.y);
        star.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);

        var renderer = star.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = teleportStarSortingOrder;
        renderer.color = Color.white;

        float minDrift = Mathf.Min(teleportStarDriftRange.x, teleportStarDriftRange.y);
        float maxDrift = Mathf.Max(teleportStarDriftRange.x, teleportStarDriftRange.y);
        Vector2 driftDirection = Random.insideUnitCircle.normalized;
        if (driftDirection.sqrMagnitude <= 0.001f)
            driftDirection = Vector2.up;

        Vector2 drift = driftDirection * Random.Range(minDrift, maxDrift);
        float spin = Random.Range(teleportStarSpinRange.x, teleportStarSpinRange.y);
        StartCoroutine(AnimateTeleportStar(star.transform, renderer, drift, spin));
    }

    void ClearActiveTeleportStars()
    {
        for (int i = activeTeleportStars.Count - 1; i >= 0; i--)
        {
            GameObject star = activeTeleportStars[i];
            if (star != null)
                Destroy(star);
        }

        activeTeleportStars.Clear();
    }

    IEnumerator AnimateTeleportStar(Transform star, SpriteRenderer renderer, Vector2 drift, float spin)
    {
        float duration = Mathf.Max(0.01f, teleportStarLifetime);
        float elapsed = 0f;
        Vector3 startScale = star != null ? star.localScale : Vector3.one;

        while (elapsed < duration)
        {
            if (star == null || renderer == null)
                yield break;

            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            star.position += (Vector3)(drift * Time.deltaTime);
            star.Rotate(0f, 0f, spin * Time.deltaTime);
            star.localScale = Vector3.Lerp(startScale, startScale * 0.45f, t);

            Color color = renderer.color;
            color.a = 1f - t;
            renderer.color = color;

            UpdateTeleportStarSprite(renderer, elapsed);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (star != null)
            Destroy(star.gameObject);
    }

    Sprite GetRandomTeleportStarSprite()
    {
        if (teleportStarSprites == null || teleportStarSprites.Length == 0)
            return null;

        for (int attempts = 0; attempts < teleportStarSprites.Length; attempts++)
        {
            Sprite sprite = teleportStarSprites[Random.Range(0, teleportStarSprites.Length)];
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    void UpdateTeleportStarSprite(SpriteRenderer renderer, float elapsed)
    {
        if (renderer == null || teleportStarSprites == null || teleportStarSprites.Length <= 1)
            return;

        int frame = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, teleportStarAnimationFrameTime)) % teleportStarSprites.Length;
        Sprite sprite = teleportStarSprites[frame];
        if (sprite != null)
            renderer.sprite = sprite;
    }

    bool TryForceExitBeforeIndestructibleRailCell(
        RideState state,
        Vector3Int blockedCell,
        Vector2 exitWorld,
        Vector2 exitFacing,
        string reason)
    {
        if (state == null || !HasIndestructibleAt(blockedCell))
            return false;

        Vector2 facing = Cardinalize(exitFacing);
        if (facing == Vector2.zero)
            facing = currentCartDirection != Vector2.zero ? currentCartDirection : Vector2.right;

        bool suddenDeathIndestructible = IsActiveSuddenDeathIndestructibleCell(blockedCell);
        state.forcedExit = true;
        state.forcedExitCell = blockedCell;
        state.forcedExitWorld = exitWorld;
        state.forcedExitFacing = facing;
        state.forcedExitOnSuddenDeathIndestructible = suddenDeathIndestructible;

        currentCartDirection = facing;
        UpdateCartVisual(facing, moving: false);

        return true;
    }

    public void OnSuddenDeathIndestructiblePlaced(Vector3Int cell)
    {
        if (cartDestroyedBySuddenDeath)
            return;

        ResolveReferences();

        Vector3Int cartCell = WorldToCell(transform.position);
        if (cartCell != cell)
            return;

        cartDestroyedBySuddenDeath = true;
        StopRideLoopSfx();
        SetCartVisible(false);

        if (currentRider == null || !activeStates.TryGetValue(currentRider, out RideState state) || state == null)
            return;

        Vector2 exitWorld = GetCellCenter(cell);
        Vector2 facing = currentCartDirection != Vector2.zero ? Cardinalize(currentCartDirection) : Vector2.right;

        state.forcedExit = true;
        state.forcedExitCell = cell;
        state.forcedExitWorld = exitWorld;
        state.forcedExitFacing = facing;
        state.forcedExitOnSuddenDeathIndestructible = true;
        state.cartDestroyedBySuddenDeath = true;

        MoveRiderWithCart(currentRider, exitWorld);
    }

    RideState CaptureAndApplyRideState(MovementController mover)
    {
        var state = new RideState
        {
            prevInputLocked = mover.InputLocked,
            prevPlayerExplosionInvulnerable = mover.explosionInvulnerable,
            playerCollider = mover.GetComponent<Collider2D>(),
            mountMovement = mover.GetComponentInChildren<MountMovementController>(true),
            healths = mover.GetComponentsInChildren<CharacterHealth>(true),
            mountCompanion = mover.GetComponent<PlayerMountCompanion>(),
            mountVisual = mover.GetComponentInChildren<MountVisualController>(true),
            eggQueue = mover.GetComponentInChildren<MountEggQueue>(true),
            pinkJumpAnimator = mover.GetComponentInChildren<IPinkLouieJumpExternalAnimator>(true),
            startFacing = mover.FacingDirection != Vector2.zero ? Cardinalize(mover.FacingDirection) : Vector2.down,
        };

        state.prevColliderEnabled = state.playerCollider != null && state.playerCollider.enabled;
        state.prevMountExplosionInvulnerable = state.mountMovement != null && state.mountMovement.explosionInvulnerable;
        state.hadBombController = mover.TryGetComponent(out state.bombController) && state.bombController != null;
        state.prevBombEnabled = state.hadBombController && state.bombController.enabled;

        CacheHeadOnlyRenderers(mover, state);

        mover.SetInputLocked(true, forceIdle: false);
        mover.SetExternalMovementOverride(true);
        mover.SetVisualOverrideActive(true);
        mover.SetAllSpritesVisible(false);
        mover.SetExplosionInvulnerable(true);

        if (state.bombController != null)
            state.bombController.enabled = false;

        if (state.playerCollider != null)
            state.playerCollider.enabled = false;

        if (state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(true);

        SetHealthInvulnerability(state.healths, true);
        ApplyHeadOffsets(state, "CaptureAndApplyRideState");

        return state;
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

        var pinkJump = mover.GetComponent<PinkLouieJumpAbility>();
        if (pinkJump != null && pinkJump.JumpActive)
            pinkJump.CancelJumpForExternalInterruption();
    }

    void RestoreRideState(MovementController mover, RideState state)
    {
        if (state == null)
            return;

        HideRiderHeadOnly(state);
        ClearHeadOffsets(state);
        RestoreCartRiderSorting(state);
        state.pinkJumpAnimator?.Stop();

        if (mover != null)
        {
            mover.SetExternalMovementOverride(false);
            mover.SetInputLocked(state.prevInputLocked, forceIdle: false);
            mover.SetVisualOverrideActive(false);
            mover.EnableExclusiveFromState();
            mover.SetExplosionInvulnerable(state.prevPlayerExplosionInvulnerable);

            if (mover.Rigidbody != null)
                mover.Rigidbody.linearVelocity = Vector2.zero;
        }

        if (state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(state.prevMountExplosionInvulnerable);

        if (state.mountVisual != null)
        {
            state.mountVisual.SetCartHeadOnlyVisual(false, Vector2.down);
            state.mountVisual.ClearCartHeadOnlyOffsets();
        }

        if (state.bombController != null)
            state.bombController.enabled = state.prevBombEnabled;

        if (state.playerCollider != null)
            state.playerCollider.enabled = state.prevColliderEnabled;

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(true);

        if (state.eggQueue != null)
        {
            state.eggQueue.ForceVisible(true);
            state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
        }

        SetHealthInvulnerability(state.healths, false);
    }

    void SetRideCompanionsVisible(RideState state, bool visible)
    {
        if (state == null)
            return;

        bool hasMountHeadOnly = state.mountVisual != null && state.mountVisual.HasHeadOnlyVisuals();

        if (state.mountVisual != null && !visible)
        {
            ApplyMountHeadOnlyOffsets(state.mountVisual);
            state.mountVisual.SetCartHeadOnlyVisual(true, currentCartDirection);
        }
        else if (state.mountVisual != null)
        {
            state.mountVisual.SetCartHeadOnlyVisual(false, currentCartDirection);
            state.mountVisual.ClearCartHeadOnlyOffsets();
        }

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(visible || hasMountHeadOnly);

        if (state.eggQueue != null)
        {
            state.eggQueue.ForceVisible(visible);

            if (visible)
                state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
        }
    }

    IEnumerator PlayEnterExitVisualRoutine(
        MovementController mover,
        RideState state,
        Vector2 start,
        Vector2 end,
        Vector2 faceDir,
        float animationSeconds,
        string phaseName)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        float duration = Mathf.Max(0.01f, animationSeconds);
        float half = duration * 0.5f;
        float height = Mathf.Max(0f, visualHopHeightTiles) * Mathf.Max(0.0001f, mover.tileSize);
        var riding = mover.GetComponent<PlayerRidingController>();
        var mountVisual = mover.GetComponentInChildren<MountVisualController>(true);
        Vector2 baseMountOffset = mountVisual != null ? mountVisual.localOffset : Vector2.zero;
        bool mounted = mover.IsMounted;
        Vector2 mountedPlayerFacing = Cardinalize(faceDir);
        if (mountedPlayerFacing == Vector2.zero)
            mountedPlayerFacing = Vector2.down;
        bool usePinkJumpAnimator = mounted && state?.pinkJumpAnimator != null;
        bool useMountedHeadOnly = mounted && mountVisual != null && mountVisual.useHeadOnlyPlayerVisual;

        if (mounted)
            mover.ForceFacingDirection(mountedPlayerFacing);

        if (useMountedHeadOnly)
            RestoreMountedHeadOnlyOffsets(state);

        if (usePinkJumpAnimator)
        {
            state.pinkJumpAnimator.Play(mountedPlayerFacing);
        }
        else if (mounted && mountVisual != null && mountVisual.HasJumpVisuals())
        {
            mountVisual.SetJumpVisual(true, faceDir, descending: false);
        }

        try
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (GamePauseController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 ground = Vector2.Lerp(start, end, t);

                bool descending = elapsed >= half;
                float phaseT = half > 0f
                    ? (descending ? Mathf.Clamp01((elapsed - half) / half) : Mathf.Clamp01(elapsed / half))
                    : 1f;
                float visualHeight = descending
                    ? Mathf.Lerp(height, 0f, phaseT)
                    : Mathf.Lerp(0f, height, phaseT);

                if (mounted && mountVisual != null)
                {
                    mover.Rigidbody.position = ground + Vector2.up * visualHeight;

                    if (!usePinkJumpAnimator && mountVisual.HasJumpVisuals())
                        mountVisual.SetJumpPhase(descending);

                    mountVisual.localOffset = baseMountOffset;
                    ApplyMountedPlayerHopSprite(mover, state, mountedPlayerFacing, useMountedHeadOnly);
                }
                else
                {
                    mover.Rigidbody.position = ground;
                    ApplyUnmountedSpringSprite(riding, faceDir, !descending, visualHeight);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            mover.Rigidbody.position = end;
        }
        finally
        {
            if (usePinkJumpAnimator)
            {
                state.pinkJumpAnimator.Stop();
            }

            if (mounted && mountVisual != null)
            {
                mountVisual.localOffset = baseMountOffset;
                if (!usePinkJumpAnimator && mountVisual.HasJumpVisuals())
                {
                    mountVisual.SetJumpVisual(false, faceDir);
                }

                if (useMountedHeadOnly)
                    RestoreMountedHeadOnlyOffsets(state);

                ClearMountedPlayerHopSprites(mover);
            }
            else
            {
                ClearUnmountedSpringSprites(riding);
            }
        }
    }

    void HandleCartCollisions(Vector2 position, Vector2 direction, MovementController rider)
    {
        processedThisFrame.Clear();
        int mask = collisionMask.value != 0 ? collisionMask.value : Physics2D.DefaultRaycastLayers;
        ContactFilter2D filter = new();
        filter.SetLayerMask(mask);
        filter.useTriggers = true;

        int count = Physics2D.OverlapBox(position, hitboxSize, 0f, filter, hitBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            hitBuffer[i] = null;

            if (hit == null)
                continue;

            GameObject root = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
            if (root == null || !processedThisFrame.Add(root))
                continue;

            if (rider != null && (root == rider.gameObject || hit.transform.IsChildOf(rider.transform)))
                continue;

            TryDamagePlayer(hit);
            TryDamageWorldMount(hit);
            TryDestroyItem(hit, direction);
            TryExplodeBomb(hit, position);
        }

        TryClearDestructibleAt(position);
    }

    void TryDamagePlayer(Collider2D hit)
    {
        MovementController target = hit.GetComponent<MovementController>() ?? hit.GetComponentInParent<MovementController>();
        if (target == null || !target.CompareTag("Player") || target.isDead)
            return;

        if (target.TryGetComponent<CharacterHealth>(out var health) && health != null && health.IsInvulnerable)
            return;

        if (target.IsRidingPlaying() && target.TryGetComponent<PlayerMountCompanion>(out var ridingCompanion) && ridingCompanion != null)
        {
            ridingCompanion.HandleDamageWhileMounting(damage);
            return;
        }

        if (target.IsMounted && target.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
        {
            companion.OnMountedLouieHit(damage, fromExplosion: false);
            return;
        }

        if (health != null)
            health.TakeDamage(damage, fromExplosion: false);
        else
            target.Kill();
    }

    void TryDamageWorldMount(Collider2D hit)
    {
        MountWorldPickup pickup =
            hit.GetComponent<MountWorldPickup>() ??
            hit.GetComponentInParent<MountWorldPickup>() ??
            hit.GetComponentInChildren<MountWorldPickup>();

        if (pickup == null)
            return;

        CharacterHealth health =
            pickup.GetComponent<CharacterHealth>() ??
            pickup.GetComponentInParent<CharacterHealth>() ??
            pickup.GetComponentInChildren<CharacterHealth>();

        if (health == null || health.life <= 0 || health.IsInvulnerable)
            return;

        health.TakeDamage(damage, fromExplosion: false);
    }

    void TryDestroyItem(Collider2D hit, Vector2 direction)
    {
        ItemPickup item = hit.GetComponent<ItemPickup>() ?? hit.GetComponentInParent<ItemPickup>();
        if (item == null)
            return;

        if (item.TryBounceSkull(direction, 1f, hit, 0.25f))
            return;

        item.DestroyWithAnimation();
    }

    void TryExplodeBomb(Collider2D hit, Vector2 position)
    {
        Bomb bomb = hit.GetComponent<Bomb>() ?? hit.GetComponentInParent<Bomb>();
        if (bomb == null || bomb.HasExploded)
            return;

        GameObject bombGo = bomb.gameObject;
        if (bomb.Owner != null)
            bomb.Owner.ExplodeBombChained(bombGo, position);
        else if (destructibleClearer != null)
            destructibleClearer.ExplodeBombChained(bombGo, position);
    }

    void TryClearDestructibleAt(Vector2 position)
    {
        if (destructibleTilemap == null)
            return;

        Vector3Int cell = destructibleTilemap.WorldToCell(position);
        if (destructibleTilemap.GetTile(cell) == null)
            return;

        if (destructibleClearer == null)
            destructibleClearer = FindAnyObjectByType<BombController>();

        if (destructibleClearer != null)
        {
            destructibleClearer.ClearDestructibleForEffect(
                position,
                spawnDestructibleBreakPrefab,
                spawnHiddenObjectFromDestroyedBlock);
            return;
        }

        destructibleTilemap.SetTile(cell, null);
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.OnDestructibleDestroyed(cell);
    }

    void MoveRiderWithCart(MovementController mover, Vector2 position)
    {
        if (mover == null || mover.Rigidbody == null)
            return;

        position = QuantizeToPixelGrid(position);
        mover.Rigidbody.linearVelocity = Vector2.zero;
        mover.Rigidbody.MovePosition(position);

        Vector3 transformPosition = mover.transform.position;
        transformPosition.x = position.x;
        transformPosition.y = position.y;
        mover.transform.position = transformPosition;
    }

    void ApplyCartRidePosition(MovementController mover, RideState state, Vector2 position, Vector2 direction)
    {
        position = QuantizeToPixelGrid(position);

        SetCartWorldPosition(position);
        MoveRiderWithCart(mover, position);

        if (state?.mountVisual != null && !state.mountVisual.gameObject.activeInHierarchy)
            return;

        if (state?.mountVisual != null)
        {
            ApplyMountHeadOnlyOffsets(state.mountVisual);
            state.mountVisual.SetCartHeadOnlyVisual(true, direction);
            state.mountVisual.transform.localPosition = state.mountVisual.localOffset;
        }
    }

    IEnumerator ReleaseRiderAfterGrace(MovementController mover)
    {
        if (retriggerGraceSeconds > 0f)
            yield return new WaitForSeconds(retriggerGraceSeconds);

        activeRiders.Remove(mover);

        if (mover != null)
            waitingForStationExit.Add(mover);
    }

    void BuildRailPath()
    {
        railPath.Clear();

        for (int i = 0; railSegments != null && i < railSegments.Length; i++)
        {
            Vector3Int start = ToCell(railSegments[i].startCell);
            Vector3Int end = ToCell(railSegments[i].endCell);
            Vector3Int delta = end - start;
            Vector3Int step = Vector3Int.zero;

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && delta.x != 0)
                step = new Vector3Int(delta.x > 0 ? 1 : -1, 0, 0);
            else if (delta.y != 0)
                step = new Vector3Int(0, delta.y > 0 ? 1 : -1, 0);
            else
                step = Vector3Int.zero;

            Vector3Int cell = start;
            AddRailCell(cell);

            int guard = 0;
            while (cell != end && guard++ < 128)
            {
                cell += step;
                AddRailCell(cell);
            }
        }

        if (railPath.Count == 0)
            AddRailCell(ToCell(stationCell));
    }

    List<Vector3Int> BuildBattleMode12PathToPortal(int routeIndex)
    {
        var path = new List<Vector3Int>();
        if (!HasUsableBattleMode12Routes())
            return path;

        PortalRailRoute route = battleMode12Routes[GetValidBattleMode12RouteIndex(routeIndex)];
        AppendCellsBetween(path, ToCell(route.startCell), ToCell(route.cornerCell));
        AppendCellsBetween(path, ToCell(route.cornerCell), ToCell(route.portalCell));
        return path;
    }

    List<Vector3Int> BuildBattleMode12PathFromPortalToStart(int routeIndex)
    {
        var path = new List<Vector3Int>();
        if (!HasUsableBattleMode12Routes())
            return path;

        PortalRailRoute route = battleMode12Routes[GetValidBattleMode12RouteIndex(routeIndex)];
        AppendCellsBetween(path, ToCell(route.portalCell), ToCell(route.cornerCell));
        AppendCellsBetween(path, ToCell(route.cornerCell), ToCell(route.startCell));
        return path;
    }

    void AppendCellsBetween(List<Vector3Int> path, Vector3Int start, Vector3Int end)
    {
        if (path == null)
            return;

        Vector3Int delta = end - start;
        Vector3Int step = Vector3Int.zero;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) && delta.x != 0)
            step = new Vector3Int(delta.x > 0 ? 1 : -1, 0, 0);
        else if (delta.y != 0)
            step = new Vector3Int(0, delta.y > 0 ? 1 : -1, 0);

        Vector3Int cell = start;
        AddPathCell(path, cell);

        int guard = 0;
        while (cell != end && step != Vector3Int.zero && guard++ < 128)
        {
            cell += step;
            AddPathCell(path, cell);
        }
    }

    static void AddPathCell(List<Vector3Int> path, Vector3Int cell)
    {
        if (path.Count > 0 && path[path.Count - 1] == cell)
            return;

        path.Add(cell);
    }

    void AddRailCell(Vector3Int cell)
    {
        if (railPath.Count > 0 && railPath[railPath.Count - 1] == cell)
            return;

        railPath.Add(cell);
    }

    void ResolveReferences()
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            if (groundTilemap == null)
                groundTilemap = gm.groundTilemap;
            if (destructibleTilemap == null)
                destructibleTilemap = gm.destructibleTilemap;
        }

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("ground");
        if (destructibleTilemap == null)
            destructibleTilemap = FindTilemapByName("destruct");
        if (indestructibleTilemap == null)
            indestructibleTilemap = FindIndestructibleTilemap();
        if (destructibleClearer == null)
            destructibleClearer = FindAnyObjectByType<BombController>();
        if (suddenDeathController == null)
            suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();
    }

    Tilemap FindIndestructibleTilemap()
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm == null)
                continue;

            if (tm.CompareTag("Indestructibles"))
                return tm;

            string lowerName = tm.name.ToLowerInvariant();
            if (lowerName.Contains("indestruct"))
                return tm;
        }

        return null;
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

    bool HasIndestructibleAt(Vector3Int cell)
    {
        if (indestructibleTilemap == null)
            indestructibleTilemap = FindIndestructibleTilemap();

        return indestructibleTilemap != null && indestructibleTilemap.HasTile(cell);
    }

    bool HasDestructibleAt(Vector3Int cell)
    {
        if (destructibleTilemap == null)
            destructibleTilemap = FindTilemapByName("destruct");

        return destructibleTilemap != null && destructibleTilemap.HasTile(cell);
    }

    Vector3Int ResolveBattleMode12ExitCell(Vector3Int cartCell, ref Vector2 exitFacing)
    {
        Vector3Int forwardOffset = ToCellOffset(exitFacing);
        Vector3Int forwardCell = cartCell + forwardOffset;
        if (!HasDestructibleAt(forwardCell))
            return forwardCell;

        exitFacing = -Cardinalize(exitFacing);
        if (exitFacing == Vector2.zero)
            exitFacing = Vector2.left;

        return cartCell - forwardOffset;
    }

    bool IsActiveSuddenDeathIndestructibleCell(Vector3Int cell)
    {
        if (suddenDeathController == null)
            suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();

        return suddenDeathController != null && suddenDeathController.IsActiveSuddenDeathCell(cell);
    }

    void CacheHeadOnlyRenderers(MovementController mover, RideState state)
    {
        riderRenderers.Clear();
        mover.GetComponentsInChildren(true, riderRenderers);

        state.headUp = FindChildAnimByName(mover.transform, headOnlyUpName);
        state.headDown = FindChildAnimByName(mover.transform, headOnlyDownName);
        state.headLeft = FindChildAnimByName(mover.transform, headOnlyLeftName);
        state.headRight = FindChildAnimByName(mover.transform, headOnlyRightName);
    }

    void ShowRiderHeadOnly(RideState state, Vector2 dir)
    {
        if (state == null)
            return;

        ApplyHeadOffsets(state, "ShowRiderHeadOnly");

        AnimatedSpriteRenderer target = PickHeadRenderer(state, dir);
        for (int i = 0; i < riderRenderers.Count; i++)
        {
            AnimatedSpriteRenderer r = riderRenderers[i];
            if (r == null)
                continue;

            bool enable = r == target;
            r.enabled = enable;
            if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
                sr.enabled = enable;
        }

        if (target != null)
        {
            target.idle = true;
            target.RefreshFrame();
        }

        if (state.mountVisual != null && state.mountVisual.HasHeadOnlyVisuals())
        {
            ApplyMountHeadOnlyOffsets(state.mountVisual);
            state.mountVisual.SetCartHeadOnlyVisual(true, dir);
        }

        ApplyCartRiderSorting(state, dir, target);
    }

    void HideRiderHeadOnly(RideState state)
    {
        if (state == null)
            return;

        SetAnimEnabled(state.headUp, false);
        SetAnimEnabled(state.headDown, false);
        SetAnimEnabled(state.headLeft, false);
        SetAnimEnabled(state.headRight, false);

        if (state.mountVisual != null)
        {
            state.mountVisual.SetCartHeadOnlyVisual(false, currentCartDirection);
            state.mountVisual.ClearCartHeadOnlyOffsets();
        }

        RestoreCartRiderSorting(state);
    }

    void ApplyCartRiderSorting(RideState state, Vector2 dir, AnimatedSpriteRenderer playerHeadRenderer)
    {
        if (state == null)
            return;

        Vector2 facing = Cardinalize(dir);
        int cartOrder = cartSortingOrder;
        int playerOrder = facing == Vector2.down ? cartOrder + 1 : cartOrder + 2;
        int mountOrder = facing == Vector2.down ? cartOrder + 2 : cartOrder + 1;

        SetRendererBranchSorting(state, playerHeadRenderer, playerOrder);

        if (state.mountVisual != null)
            SetTransformBranchSorting(state, state.mountVisual.transform, mountOrder);

        ApplyCartSorting(down);
        ApplyCartSorting(up);
        ApplyCartSorting(left);
        ApplyCartSorting(right);
    }

    void SetRendererBranchSorting(RideState state, AnimatedSpriteRenderer renderer, int order)
    {
        if (renderer == null)
            return;

        var srs = renderer.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            SetSortingOrderCached(state, srs[i], order);
    }

    void SetTransformBranchSorting(RideState state, Transform root, int order)
    {
        if (root == null)
            return;

        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            SetSortingOrderCached(state, srs[i], order);
    }

    void SetSortingOrderCached(RideState state, SpriteRenderer sr, int order)
    {
        if (state == null || sr == null)
            return;

        state.previousSortingOrders ??= new Dictionary<SpriteRenderer, int>();

        if (!state.previousSortingOrders.ContainsKey(sr))
            state.previousSortingOrders.Add(sr, sr.sortingOrder);

        sr.sortingOrder = order;
    }

    void RestoreCartRiderSorting(RideState state)
    {
        if (state?.previousSortingOrders == null)
            return;

        foreach (var pair in state.previousSortingOrders)
        {
            if (pair.Key != null)
                pair.Key.sortingOrder = pair.Value;
        }

        state.previousSortingOrders.Clear();
    }

    void ApplyMountHeadOnlyOffsets(MountVisualController mountVisual)
    {
        if (mountVisual == null)
            return;

        mountVisual.SetCartHeadOnlyOffsets(
            mountHeadOnlyUpOffset,
            mountHeadOnlyDownOffset,
            mountHeadOnlyLeftOffset,
            mountHeadOnlyRightOffset);
    }

    AnimatedSpriteRenderer PickHeadRenderer(RideState state, Vector2 dir)
    {
        Vector2 f = Cardinalize(dir);
        if (f == Vector2.up) return state.headUp != null ? state.headUp : state.headDown;
        if (f == Vector2.left) return state.headLeft != null ? state.headLeft : state.headDown;
        if (f == Vector2.right) return state.headRight != null ? state.headRight : state.headDown;
        return state.headDown;
    }

    void ApplyHeadOffsets(RideState state, string phase)
    {
        ApplyHeadOffset(state.headUp, headOnlyUpOffset);
        ApplyHeadOffset(state.headDown, headOnlyDownOffset);
        ApplyHeadOffset(state.headLeft, headOnlyLeftOffset);
        ApplyHeadOffset(state.headRight, headOnlyRightOffset);
    }

    void ClearHeadOffsets(RideState state)
    {
        ClearHeadOffset(state.headUp);
        ClearHeadOffset(state.headDown);
        ClearHeadOffset(state.headLeft);
        ClearHeadOffset(state.headRight);
    }

    static void ApplyHeadOffset(AnimatedSpriteRenderer r, Vector2 offset)
    {
        if (r == null)
            return;

        r.SetRuntimeBaseLocalX(offset.x);
        r.SetRuntimeBaseLocalY(offset.y);
    }

    static void ClearHeadOffset(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        r.ClearRuntimeBaseOffset();
    }

    static AnimatedSpriteRenderer FindChildAnimByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] != null && trs[i].name == childName)
                return trs[i].GetComponent<AnimatedSpriteRenderer>();
        }

        return null;
    }

    void UpdateCartVisual(Vector2 dir, bool moving)
    {
        EnsureCartVisuals();

        AnimatedSpriteRenderer target = PickCartRenderer(dir);
        if (target == null)
            target = down;

        ForceOnlyCart(target);
        SetIdle(down, !moving);
        SetIdle(up, !moving);
        SetIdle(left, !moving);
        SetIdle(right, !moving);
    }

    AnimatedSpriteRenderer PickCartRenderer(Vector2 dir)
    {
        Vector2 f = Cardinalize(dir);
        if (f == Vector2.up) return up != null ? up : down;
        if (f == Vector2.left) return left != null ? left : down;
        if (f == Vector2.right) return right != null ? right : down;
        return down;
    }

    void ForceOnlyCart(AnimatedSpriteRenderer target)
    {
        EnsureCartVisuals();
        ForceOnlyCartNoEnsure(target);
    }

    void ForceOnlyCartNoEnsure(AnimatedSpriteRenderer target)
    {
        SetAnimEnabled(down, target == down);
        SetAnimEnabled(up, target == up);
        SetAnimEnabled(left, target == left);
        SetAnimEnabled(right, target == right);
        ApplyCartSorting(down);
        ApplyCartSorting(up);
        ApplyCartSorting(left);
        ApplyCartSorting(right);
    }

    void SetCartVisible(bool visible)
    {
        SetBranchVisible(down, visible && down != null && down.enabled);
        SetBranchVisible(up, visible && up != null && up.enabled);
        SetBranchVisible(left, visible && left != null && left.enabled);
        SetBranchVisible(right, visible && right != null && right.enabled);
    }

    void ApplyCartSorting(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.sortingOrder = cartSortingOrder;
    }

    void SnapCartToStation()
    {
        SetCartWorldPosition(GetCellCenter(GetActiveStationCell()));
    }

    void ConfigureForActiveScene()
    {
        if (!IsBattleMode12Active())
            return;

        if (!HasUsableBattleMode12Routes())
            return;

        battleMode12CurrentRouteIndex = GetValidBattleMode12RouteIndex(battleMode12CurrentRouteIndex);
        stationCell = battleMode12Routes[battleMode12CurrentRouteIndex].startCell;
    }

    Vector3Int GetActiveStationCell()
    {
        if (IsBattleMode12Active() && HasUsableBattleMode12Routes())
            return ToCell(battleMode12Routes[GetValidBattleMode12RouteIndex(battleMode12CurrentRouteIndex)].startCell);

        return ToCell(stationCell);
    }

    Vector2 GetIdleDirection()
    {
        if (IsBattleMode12Active() && HasUsableBattleMode12Routes())
            return GetRouteStartDirection(GetValidBattleMode12RouteIndex(battleMode12CurrentRouteIndex));

        return Vector2.right;
    }

    bool HasUsableBattleMode12Routes()
        => battleMode12Routes != null && battleMode12Routes.Length >= 2;

    int GetValidBattleMode12RouteIndex(int routeIndex)
    {
        if (!HasUsableBattleMode12Routes())
            return 0;

        if (routeIndex < 0)
            return 0;

        if (routeIndex >= battleMode12Routes.Length)
            return routeIndex % battleMode12Routes.Length;

        return routeIndex;
    }

    int GetRandomOtherBattleMode12RouteIndex(int sourceRouteIndex)
    {
        if (!HasUsableBattleMode12Routes())
            return 0;

        sourceRouteIndex = GetValidBattleMode12RouteIndex(sourceRouteIndex);
        int destination = Random.Range(0, battleMode12Routes.Length - 1);
        if (destination >= sourceRouteIndex)
            destination++;

        return destination;
    }

    Vector2 GetRouteStartDirection(int routeIndex)
    {
        if (!HasUsableBattleMode12Routes())
            return Vector2.right;

        PortalRailRoute route = battleMode12Routes[GetValidBattleMode12RouteIndex(routeIndex)];
        Vector3Int delta = ToCell(route.cornerCell) - ToCell(route.startCell);
        return Cardinalize(new Vector2(delta.x, delta.y));
    }

    Vector2 GetRouteExitDirection(int routeIndex)
    {
        if (!HasUsableBattleMode12Routes())
            return Vector2.right;

        PortalRailRoute route = battleMode12Routes[GetValidBattleMode12RouteIndex(routeIndex)];
        Vector3Int delta = ToCell(route.startCell) - ToCell(route.cornerCell);
        return Cardinalize(new Vector2(delta.x, delta.y));
    }

    Vector2 GetRoutePortalExitDirection(int routeIndex)
    {
        if (!HasUsableBattleMode12Routes())
            return Vector2.right;

        PortalRailRoute route = battleMode12Routes[GetValidBattleMode12RouteIndex(routeIndex)];
        Vector3Int delta = ToCell(route.cornerCell) - ToCell(route.portalCell);
        return Cardinalize(new Vector2(delta.x, delta.y));
    }

    void EnsureCartVisuals()
    {
        if (cartVisualRoot == null)
        {
            cartVisualRoot = FindExistingCartVisualRoot();

            if (cartVisualRoot == null && minecartPrefab != null && !HasCartVisualReferences())
            {
                GameObject visual = Instantiate(minecartPrefab, transform);
                visual.name = minecartPrefab.name;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                cartVisualRoot = visual.transform;
                DisableCartPrefabColliders(cartVisualRoot);
            }
            else if (cartVisualRoot == null)
            {
                cartVisualRoot = transform;
            }
        }

        ResolveCartVisualReferences(cartVisualRoot);

        if (!HasCartVisualReferences() && cartVisualRoot != transform)
            ResolveCartVisualReferences(transform);
    }

    bool HasCartVisualReferences()
        => down != null || up != null || left != null || right != null;

    void ResolveCartVisualReferences(Transform root)
    {
        if (root == null)
            return;

        if (down == null)
            down = FindCartVisual(root, "Down");
        if (up == null)
            up = FindCartVisual(root, "Up");
        if (left == null)
            left = FindCartVisual(root, "Left");
        if (right == null)
            right = FindCartVisual(root, "Right", "Rigth");
    }

    Transform FindExistingCartVisualRoot()
    {
        AnimatedSpriteRenderer existingDown = FindCartVisual(transform, "Down");
        AnimatedSpriteRenderer existingUp = FindCartVisual(transform, "Up");
        AnimatedSpriteRenderer existingLeft = FindCartVisual(transform, "Left");
        AnimatedSpriteRenderer existingRight = FindCartVisual(transform, "Right", "Rigth");

        AnimatedSpriteRenderer any = existingDown != null
            ? existingDown
            : existingUp != null
                ? existingUp
                : existingLeft != null
                    ? existingLeft
                    : existingRight;

        if (any == null)
            return null;

        Transform root = any.transform;
        while (root.parent != null && root.parent != transform)
            root = root.parent;

        return root.parent == transform ? root : transform;
    }

    static AnimatedSpriteRenderer FindCartVisual(Transform root, params string[] childNames)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
                continue;

            for (int n = 0; childNames != null && n < childNames.Length; n++)
            {
                if (string.Equals(child.name, childNames[n], System.StringComparison.Ordinal))
                    return child.GetComponent<AnimatedSpriteRenderer>();
            }
        }

        return null;
    }

    static void DisableCartPrefabColliders(Transform root)
    {
        if (root == null)
            return;

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    void SetCartWorldPosition(Vector2 position)
    {
        position = QuantizeToPixelGrid(position);

        Vector3 p = transform.position;
        p.x = position.x;
        p.y = position.y;
        transform.position = p;
    }

    float PixelWorldStep => useIntegerPixelSteps && pixelsPerUnit > 0 ? 1f / pixelsPerUnit : 0.0001f;

    Vector2 QuantizeToPixelGrid(Vector2 world)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return world;

        float ppu = pixelsPerUnit;
        return new Vector2(
            Mathf.Round(world.x * ppu) / ppu,
            Mathf.Round(world.y * ppu) / ppu);
    }

    void ResetCartPixelAccumulator()
    {
        cartMovePixelAccumulator = 0f;
    }

    float GetQuantizedCartMoveWorld(float rawWorldStep)
    {
        if (!useIntegerPixelSteps || pixelsPerUnit <= 0)
            return rawWorldStep;

        cartMovePixelAccumulator += rawWorldStep * pixelsPerUnit;

        int wholePixels = (int)cartMovePixelAccumulator;
        cartMovePixelAccumulator -= wholePixels;

        return wholePixels * PixelWorldStep;
    }

    void EnsureAudioSource()
    {
        if (audioSource != null)
            return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    void LoadDefaultSfxIfNeeded()
    {
        if (enterSfx == null)
            enterSfx = Resources.Load<AudioClip>(DefaultEnterSfxResourcesPath);

        if (portalEnterSfx == null)
            portalEnterSfx = Resources.Load<AudioClip>(DefaultEnterSfxResourcesPath);

        if (exitSfx == null)
            exitSfx = Resources.Load<AudioClip>(DefaultExitSfxResourcesPath);
    }

    void PlayOneShot(AudioClip clip)
        => PlayOneShot(clip, sfxVolume);

    void PlayOneShot(AudioClip clip, float volume)
    {
        EnsureAudioSource();
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    void PlayRideLoopSfx()
    {
        EnsureAudioSource();
        if (rideLoopSfx == null || audioSource == null)
            return;

        audioSource.clip = rideLoopSfx;
        audioSource.loop = true;
        audioSource.volume = sfxVolume;
        audioSource.Play();
        rideLoopPausedForGamePause = false;
        SyncRideLoopSfxWithPause();
    }

    void StopRideLoopSfx()
    {
        if (audioSource == null)
            return;

        if (audioSource.loop)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }

        rideLoopPausedForGamePause = false;
    }

    void SyncRideLoopSfxWithPause()
    {
        if (audioSource == null ||
            rideLoopSfx == null ||
            audioSource.clip != rideLoopSfx ||
            !audioSource.loop)
        {
            rideLoopPausedForGamePause = false;
            return;
        }

        if (GamePauseController.IsPaused)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Pause();
                rideLoopPausedForGamePause = true;
            }

            return;
        }

        if (rideLoopPausedForGamePause)
        {
            audioSource.UnPause();
            rideLoopPausedForGamePause = false;
        }
    }

    Vector3Int WorldToCell(Vector2 worldPos)
    {
        if (groundTilemap != null)
            return groundTilemap.WorldToCell(worldPos);

        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), 0);
    }

    Vector2 GetCellCenter(Vector3Int cell)
    {
        if (groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell);

        return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }

    static void SetHealthInvulnerability(CharacterHealth[] healths, bool enabled)
    {
        if (healths == null)
            return;

        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] != null)
                healths[i].SetExternalInvulnerability(enabled);
        }
    }

    static Vector3Int ToCell(Vector2Int cell)
        => new(cell.x, cell.y, 0);

    static Vector3Int ToCellOffset(Vector2 direction)
    {
        Vector2 facing = Cardinalize(direction);
        if (facing == Vector2.up)
            return new Vector3Int(0, 1, 0);
        if (facing == Vector2.down)
            return new Vector3Int(0, -1, 0);
        if (facing == Vector2.left)
            return new Vector3Int(-1, 0, 0);

        return new Vector3Int(1, 0, 0);
    }

    static Vector2 Cardinalize(Vector2 v)
    {
        if (v.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    static float SmoothTeleportT(float t)
        => t * t * (3f - 2f * t);

    static void SetAnimEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;
        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = on;
    }

    static void SetBranchVisible(AnimatedSpriteRenderer r, bool visible)
    {
        if (r == null)
            return;

        if (r.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = visible;

        var children = r.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
                children[i].enabled = visible;
        }
    }

    static void SetIdle(AnimatedSpriteRenderer r, bool idle)
    {
        if (r == null)
            return;

        r.idle = idle;
        r.RefreshFrame();
    }

    static void ApplyUnmountedSpringSprite(PlayerRidingController riding, Vector2 facing, bool ascending, float visualHeight)
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

    static AnimatedSpriteRenderer PickUnmountedSpringRenderer(PlayerRidingController riding, Vector2 facing, bool ascending)
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

    static void SetExclusiveUnmountedSpringRenderer(PlayerRidingController riding, AnimatedSpriteRenderer target)
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

    static void ClearUnmountedSpringSprites(PlayerRidingController riding)
    {
        if (riding == null)
            return;

        SetExclusiveUnmountedSpringRenderer(riding, null);
    }

    static void ClearRuntimeOffsetIfNot(AnimatedSpriteRenderer keep, AnimatedSpriteRenderer current)
    {
        if (current == null || current == keep)
            return;

        current.ClearRuntimeBaseOffset();
    }

    static Vector2 ResolveEnterHopFacing(MovementController mover, Vector2 exitFacing)
    {
        if (mover != null)
        {
            if (mover.Direction != Vector2.zero)
                return Cardinalize(mover.Direction);

            if (mover.FacingDirection != Vector2.zero)
                return Cardinalize(mover.FacingDirection);
        }

        Vector2 oppositeExit = -Cardinalize(exitFacing);
        return oppositeExit != Vector2.zero ? oppositeExit : Vector2.left;
    }

    static void ApplyMountedPlayerHopSprite(MovementController mover, RideState state, Vector2 facing, bool preferHeadOnly)
    {
        if (mover == null)
            return;

        AnimatedSpriteRenderer target = PickMountedPlayerRenderer(mover, state, facing, preferHeadOnly);
        SetExclusiveMountedPlayerRenderer(mover, state, target);

        if (target != null)
        {
            target.idle = true;
            target.RefreshFrame();
        }
    }

    static AnimatedSpriteRenderer PickMountedPlayerRenderer(MovementController mover, RideState state, Vector2 facing, bool preferHeadOnly = false)
    {
        if (mover == null)
            return null;

        Vector2 f = Cardinalize(facing);
        if (preferHeadOnly)
        {
            if (f == Vector2.up && state?.headUp != null) return state.headUp;
            if (f == Vector2.left && state?.headLeft != null) return state.headLeft;
            if (f == Vector2.right && state?.headRight != null) return state.headRight;
            if (state?.headDown != null) return state.headDown;
        }

        if (f == Vector2.up) return mover.mountedSpriteUp != null ? mover.mountedSpriteUp : mover.spriteRendererUp;
        if (f == Vector2.left) return mover.mountedSpriteLeft != null ? mover.mountedSpriteLeft : mover.spriteRendererLeft;
        if (f == Vector2.right) return mover.mountedSpriteRight != null ? mover.mountedSpriteRight : mover.spriteRendererRight;
        return mover.mountedSpriteDown != null ? mover.mountedSpriteDown : mover.spriteRendererDown;
    }

    static void SetExclusiveMountedPlayerRenderer(MovementController mover, RideState state, AnimatedSpriteRenderer target)
    {
        if (mover == null)
            return;

        SetAnimEnabled(mover.mountedSpriteUp, target == mover.mountedSpriteUp);
        SetAnimEnabled(mover.mountedSpriteDown, target == mover.mountedSpriteDown);
        SetAnimEnabled(mover.mountedSpriteLeft, target == mover.mountedSpriteLeft);
        SetAnimEnabled(mover.mountedSpriteRight, target == mover.mountedSpriteRight);
        SetAnimEnabled(state?.headUp, target == state?.headUp);
        SetAnimEnabled(state?.headDown, target == state?.headDown);
        SetAnimEnabled(state?.headLeft, target == state?.headLeft);
        SetAnimEnabled(state?.headRight, target == state?.headRight);
    }

    static void ClearMountedPlayerHopSprites(MovementController mover)
    {
        if (mover == null)
            return;

        SetExclusiveMountedPlayerRenderer(mover, null, null);
    }

    static bool IsBattleMode12Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode12SceneName, System.StringComparison.Ordinal);

    static bool IsSupportedSceneActive()
        => IsSupportedScene(SceneManager.GetActiveScene().name);

    static bool IsSupportedScene(string sceneName)
        => string.Equals(sceneName, BattleMode9SceneName, System.StringComparison.Ordinal) ||
           string.Equals(sceneName, BattleMode12SceneName, System.StringComparison.Ordinal);
}
