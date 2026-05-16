using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleRevengeSystem : MonoBehaviour
{
    private const string SwapLogPrefix = "[BattleRevenge][Swap]";

    [SerializeField] private BattleRevengeController cartPrefab;
    private readonly float cartBombCooldownSeconds = 2f;
    [SerializeField, Min(1)] private int cartBombDistanceTiles = 3;
    [SerializeField, Min(1)] private int revengeBombRadius = 2;
    [SerializeField, Min(0.01f)] private float revengeLaunchMuzzleVfxDuration = 0.16f;
    [SerializeField] private int revengeLaunchMuzzleVfxOrderInLayer = 11;
    [SerializeField] private Vector2 revengeLaunchMuzzleVfxOffsetLeft = new(-0.5f, 0f);
    [SerializeField] private Vector2 revengeLaunchMuzzleVfxOffsetRight = new(0.5f, 0f);
    [SerializeField] private Vector2 revengeLaunchMuzzleVfxOffsetTop = new(0f, 0.5f);
    [SerializeField] private Vector2 revengeLaunchMuzzleVfxOffsetBottom = new(0f, -0.5f);
    [SerializeField, Min(0.1f)] private float respawnInvulnerabilitySeconds = 2f;
    [SerializeField, Min(0.01f)] private float respawnBlinkInterval = 0.08f;

    private readonly Dictionary<int, BattleRevengeController> activeCartsByOwner = new();
    private readonly Stack<BattleRevengeController> cartPool = new();
    private readonly Dictionary<int, PlayerPersistentStats.PlayerState> baseLoadoutsByPlayer = new();
    private readonly Dictionary<int, MovementController> playersById = new();
    private float nextLandingValidationDebugAt;

    private float minEdgeX;
    private float maxEdgeX;
    private float minEdgeY;
    private float maxEdgeY;

    private bool baseSnapshotsCaptured;

    public static BattleRevengeSystem Instance { get; private set; }

    public float CartBombCooldownSeconds => cartBombCooldownSeconds;

    public bool IsRuntimeEnabled =>
        Application.isPlaying &&
        IsBattleModeScene() &&
        BattleModeRules.Instance != null &&
        BattleModeRules.Instance.EnableRevengeBomber &&
        !BattleRevengeBomberBlocker.PreventNewRevengeBombers;

    void Awake()
    {
        Instance = this;
        RefreshLaneBounds();
        RefreshPlayerCache(includeInactive: true);
    }

    void OnEnable()
    {
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(CaptureBaseLoadoutsNextFrame());
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    IEnumerator CaptureBaseLoadoutsNextFrame()
    {
        yield return null;
        CaptureBaseLoadouts();
    }

    public void HandlePlayerDeathCompleted(MovementController deadPlayer)
    {
        if (BattleRevengeBomberBlocker.PreventNewRevengeBombers)
            return;

        if (!IsRuntimeEnabled || deadPlayer == null || !deadPlayer.CompareTag("Player"))
            return;

        CleanupDestroyedActiveCarts();
        EnsureBattleSetupCached();

        if (activeCartsByOwner.ContainsKey(deadPlayer.PlayerId))
            return;

        BattleRevengeController cart = AcquireCart();
        if (cart == null)
            return;

        Vector2 startPosition = GetCartPositionForDeath(deadPlayer.transform.position, out Vector2 inwardDirection);

        cart.ConfigureBounds(minEdgeX, maxEdgeX, minEdgeY, maxEdgeY);
        cart.Activate(this, deadPlayer.PlayerId, deadPlayer, startPosition, inwardDirection);

        activeCartsByOwner[deadPlayer.PlayerId] = cart;
    }

    public bool TryHandleLethalRevengeHit(MovementController victim, Collider2D hazard)
    {
        if (BattleRevengeBomberBlocker.PreventNewRevengeBombers)
        {
            Debug.Log($"{SwapLogPrefix} ignored reason=Blocked victim={DescribePlayer(victim)} hazard={DescribeCollider(hazard)}");
            return false;
        }

        if (!IsRuntimeEnabled || victim == null || hazard == null || !victim.CompareTag("Player"))
        {
            Debug.Log(
                $"{SwapLogPrefix} ignored reason=InvalidRuntimeOrVictim " +
                $"runtime={IsRuntimeEnabled} victim={DescribePlayer(victim)} hazard={DescribeCollider(hazard)}");
            return false;
        }

        CleanupDestroyedActiveCarts();

        BombExplosion explosion =
            hazard.GetComponent<BombExplosion>() ??
            hazard.GetComponentInParent<BombExplosion>() ??
            hazard.GetComponentInChildren<BombExplosion>();

        if (explosion == null || !explosion.IsRevengeBomb)
        {
            Debug.Log(
                $"{SwapLogPrefix} ignored reason=NotRevengeExplosion " +
                $"victim={DescribePlayer(victim)} hazard={DescribeCollider(hazard)} explosion={DescribeExplosion(explosion)}");
            return false;
        }

        int respawnPlayerId = Mathf.Clamp(explosion.OwnerPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        if (respawnPlayerId == victim.PlayerId)
        {
            Debug.Log(
                $"{SwapLogPrefix} ignored reason=SelfHit victim={DescribePlayer(victim)} " +
                $"explosion={DescribeExplosion(explosion)} respawnPlayer={respawnPlayerId}");
            return false;
        }

        if (!activeCartsByOwner.TryGetValue(respawnPlayerId, out BattleRevengeController cart) || cart == null)
        {
            Debug.LogWarning(
                $"{SwapLogPrefix} aborted reason=MissingActiveCart victim={DescribePlayer(victim)} " +
                $"explosion={DescribeExplosion(explosion)} respawnPlayer={respawnPlayerId} activeCarts={DescribeActiveCartOwners()}");
            return false;
        }

        EnsureBattleSetupCached();

        if (!playersById.TryGetValue(respawnPlayerId, out MovementController respawningPlayer) || respawningPlayer == null)
        {
            Debug.LogWarning(
                $"{SwapLogPrefix} aborted reason=MissingRespawningPlayer victim={DescribePlayer(victim)} " +
                $"explosion={DescribeExplosion(explosion)} respawnPlayer={respawnPlayerId} players={DescribeCachedPlayerIds()}");
            return false;
        }

        int victimPlayerId = victim.PlayerId;
        Vector2 respawnPosition = GetArenaSnapPosition(victim.transform.position);
        Vector2 newCartPosition = GetCartPositionForDeath(victim.transform.position, out Vector2 newInwardDirection);

        PlayerPersistentStats.ApplyBattleRevengeRespawnLoadout(respawnPlayerId);

        bool victimDeathFinished = false;
        bool revengeJumpFinished = false;
        bool respawningPlayerReleased = false;
        bool swapFinalized = false;

        Vector3 jumpStartWorld = cart.transform.position;
        Vector3 jumpTargetWorld = new Vector3(respawnPosition.x, respawnPosition.y, cart.transform.position.z);
        Vector2 jumpFacing = ResolveJumpFacing(jumpStartWorld, jumpTargetWorld);

        Debug.Log(
            $"{SwapLogPrefix} begin victim={DescribePlayer(victim)} respawning={DescribePlayer(respawningPlayer)} " +
            $"explosion={DescribeExplosion(explosion)} cart={DescribeCart(cart)} " +
            $"respawnPos={respawnPosition} newCartPos={newCartPosition} newInward={newInwardDirection} " +
            $"jumpStart={jumpStartWorld} jumpTarget={jumpTargetWorld} jumpFacing={jumpFacing}");

        void TryFinalizeSwap()
        {
            if (swapFinalized)
            {
                Debug.Log($"{SwapLogPrefix} finalize skipped reason=AlreadyFinalized victim={victimPlayerId} respawn={respawnPlayerId}");
                return;
            }

            if (BattleRevengeBomberBlocker.PreventNewRevengeBombers)
            {
                Debug.LogWarning($"{SwapLogPrefix} finalize blocked reason=BlockerEnabled victim={victimPlayerId} respawn={respawnPlayerId}");
                return;
            }

            if (!victimDeathFinished || !revengeJumpFinished)
            {
                Debug.Log(
                    $"{SwapLogPrefix} finalize waiting victimDeathFinished={victimDeathFinished} " +
                    $"revengeJumpFinished={revengeJumpFinished} victim={victimPlayerId} respawn={respawnPlayerId}");
                return;
            }

            swapFinalized = true;

            Debug.Log(
                $"{SwapLogPrefix} finalize-start victim={DescribePlayer(victim)} respawning={DescribePlayer(respawningPlayer)} " +
                $"cart={DescribeCart(cart)} respawningReleased={respawningPlayerReleased}");

            victim.RemoveForBattleRevengeSwap();

            activeCartsByOwner.Remove(respawnPlayerId);
            activeCartsByOwner[victimPlayerId] = cart;

            if (!respawningPlayerReleased)
            {
                respawningPlayer.RespawnFromBattleRevenge(
                    respawnPosition,
                    respawnInvulnerabilitySeconds,
                    respawnBlinkInterval);

                respawningPlayerReleased = true;

                Debug.Log($"{SwapLogPrefix} respawned-during-finalize respawning={DescribePlayer(respawningPlayer)}");
            }

            cart.ReenterAs(
                victimPlayerId,
                victim,
                newCartPosition,
                newInwardDirection);

            Debug.Log(
                $"{SwapLogPrefix} finalize-complete victim={DescribePlayer(victim)} respawning={DescribePlayer(respawningPlayer)} " +
                $"cart={DescribeCart(cart)} activeCarts={DescribeActiveCartOwners()}");

            GameManager.Instance?.CheckWinState();
        }

        victim.PlayBattleRevengeSwapDeathSequence(() =>
        {
            Debug.Log($"{SwapLogPrefix} victim-death-complete victim={DescribePlayer(victim)}");
            victimDeathFinished = true;
            TryFinalizeSwap();
        });

        StartCoroutine(PlayRevengeReplacementJump(
            cart,
            respawningPlayer,
            jumpStartWorld,
            jumpTargetWorld,
            jumpFacing,
            () =>
            {
                if (BattleRevengeBomberBlocker.PreventNewRevengeBombers)
                {
                    Debug.LogWarning($"{SwapLogPrefix} jump-callback ignored reason=BlockerEnabled respawning={DescribePlayer(respawningPlayer)}");
                    return;
                }

                if (!respawningPlayerReleased)
                {
                    respawningPlayer.RespawnFromBattleRevenge(
                        respawnPosition,
                        respawnInvulnerabilitySeconds,
                        respawnBlinkInterval);

                    respawningPlayerReleased = true;

                    Debug.Log($"{SwapLogPrefix} respawned-after-jump respawning={DescribePlayer(respawningPlayer)}");
                }

                revengeJumpFinished = true;
                Debug.Log($"{SwapLogPrefix} jump-complete respawning={DescribePlayer(respawningPlayer)}");
                TryFinalizeSwap();
            }));

        return true;
    }

    private IEnumerator PlayRevengeReplacementJump(
        BattleRevengeController cart,
        MovementController respawningPlayer,
        Vector3 startWorldPos,
        Vector3 targetWorldPos,
        Vector2 facing,
        Action onFinished)
    {
        Debug.Log(
            $"{SwapLogPrefix} jump-routine-start cart={DescribeCart(cart)} respawning={DescribePlayer(respawningPlayer)} " +
            $"start={startWorldPos} target={targetWorldPos} facing={facing}");

        if (cart != null)
            cart.HideImmediately();

        if (respawningPlayer == null)
        {
            Debug.LogWarning($"{SwapLogPrefix} jump-fallback reason=MissingRespawningPlayer");
            onFinished?.Invoke();
            yield break;
        }

        PlayerRidingController riding = respawningPlayer.GetComponent<PlayerRidingController>();
        if (riding == null)
        {
            Debug.LogWarning($"{SwapLogPrefix} jump-fallback reason=MissingPlayerRidingController respawning={DescribePlayer(respawningPlayer)}");

            if (!respawningPlayer.gameObject.activeSelf)
                respawningPlayer.gameObject.SetActive(true);

            respawningPlayer.transform.position = startWorldPos;
            onFinished?.Invoke();
            yield break;
        }

        if (riding.IsPlaying)
        {
            Debug.LogWarning(
                $"{SwapLogPrefix} jump-reset-stale-riding respawning={DescribePlayer(respawningPlayer)} " +
                $"ridingSprites={DescribeRidingSprites(riding)}");
            riding.ResetStaleRidingState();
        }

        if (!respawningPlayer.gameObject.activeSelf)
            respawningPlayer.gameObject.SetActive(true);

        respawningPlayer.transform.position = startWorldPos;

        if (respawningPlayer.Rigidbody != null)
        {
            respawningPlayer.Rigidbody.simulated = false;
            respawningPlayer.Rigidbody.linearVelocity = Vector2.zero;
            respawningPlayer.Rigidbody.angularVelocity = 0f;
        }

        if (respawningPlayer.TryGetComponent(out Collider2D col) && col != null)
            col.enabled = false;

        respawningPlayer.SetExplosionInvulnerable(true);
        respawningPlayer.SetBattleRevengeJumpInvulnerabilityNoBlink(true);
        respawningPlayer.SetExternalVisualSuppressed(false);
        respawningPlayer.SetAllSpritesVisible(false);

        bool finished = false;

        Debug.Log(
            $"{SwapLogPrefix} jump-before-arc respawning={DescribePlayer(respawningPlayer)} " +
            $"ridingPlaying={riding.IsPlaying} ridingSprites={DescribeRidingSprites(riding)}");

        if (!riding.TryPlayMountArc(
                facing,
                startWorldPos,
                targetWorldPos,
                onComplete: () => finished = true))
        {
            Debug.LogWarning(
                $"{SwapLogPrefix} jump-fallback reason=TryPlayMountArcReturnedFalse respawning={DescribePlayer(respawningPlayer)} " +
                $"ridingPlaying={riding.IsPlaying} ridingSprites={DescribeRidingSprites(riding)}");
            finished = true;
        }

        while (!finished)
            yield return null;

        respawningPlayer.SetBattleRevengeJumpInvulnerabilityNoBlink(false);
        Debug.Log($"{SwapLogPrefix} jump-routine-finished respawning={DescribePlayer(respawningPlayer)}");
        onFinished?.Invoke();
    }

    private static string DescribePlayer(MovementController player)
    {
        if (player == null)
            return "null";

        Rigidbody2D rb = player.Rigidbody;
        Collider2D col = player.GetComponent<Collider2D>();
        CharacterHealth health = player.GetComponent<CharacterHealth>();

        return
            $"P{player.PlayerId} name={player.name} activeSelf={player.gameObject.activeSelf} " +
            $"activeInHierarchy={player.gameObject.activeInHierarchy} isDead={player.isDead} " +
            $"ending={player.IsEndingStage} pos={player.transform.position} " +
            $"rbSim={(rb != null ? rb.simulated.ToString() : "null")} " +
            $"colEnabled={(col != null ? col.enabled.ToString() : "null")} " +
            $"life={(health != null ? health.life.ToString() : "null")}";
    }

    private static string DescribeCollider(Collider2D collider)
    {
        if (collider == null)
            return "null";

        return
            $"name={collider.name} layer={LayerMask.LayerToName(collider.gameObject.layer)} " +
            $"enabled={collider.enabled} active={collider.gameObject.activeInHierarchy} pos={collider.transform.position}";
    }

    private static string DescribeExplosion(BombExplosion explosion)
    {
        if (explosion == null)
            return "null";

        return
            $"owner={explosion.OwnerPlayerId} revenge={explosion.IsRevengeBomb} " +
            $"part={explosion.CurrentPart} origin={explosion.Origin} pos={explosion.transform.position}";
    }

    private static string DescribeCart(BattleRevengeController cart)
    {
        if (cart == null)
            return "null";

        return
            $"owner={cart.OwnerPlayerId} active={cart.gameObject.activeInHierarchy} pos={cart.transform.position} " +
            $"launchable={cart.IsInLaunchableSegment} launchStart={cart.LaunchStartWorldPosition} launchDir={cart.LaunchDirection}";
    }

    private static string DescribeRidingSprites(PlayerRidingController riding)
    {
        if (riding == null)
            return "null";

        return
            $"ascU={riding.mountAscendUp != null} ascD={riding.mountAscendDown != null} " +
            $"ascL={riding.mountAscendLeft != null} ascR={riding.mountAscendRight != null} " +
            $"descU={riding.mountDescendUp != null} descD={riding.mountDescendDown != null} " +
            $"descL={riding.mountDescendLeft != null} descR={riding.mountDescendRight != null}";
    }

    private string DescribeActiveCartOwners()
    {
        if (activeCartsByOwner.Count == 0)
            return "none";

        List<string> owners = new();
        foreach (KeyValuePair<int, BattleRevengeController> pair in activeCartsByOwner)
            owners.Add($"{pair.Key}:{(pair.Value != null ? pair.Value.name : "null")}");

        return string.Join(",", owners);
    }

    private string DescribeCachedPlayerIds()
    {
        if (playersById.Count == 0)
            return "none";

        List<string> players = new();
        foreach (KeyValuePair<int, MovementController> pair in playersById)
            players.Add($"{pair.Key}:{(pair.Value != null ? pair.Value.name : "null")}");

        return string.Join(",", players);
    }

    private static Vector2 ResolveJumpFacing(Vector3 startWorldPos, Vector3 targetWorldPos)
    {
        Vector2 delta = targetWorldPos - startWorldPos;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0f ? Vector2.right : Vector2.left;

        return delta.y >= 0f ? Vector2.up : Vector2.down;
    }

    public bool TryLaunchBombFromCart(BattleRevengeController cart)
    {
        return TryLaunchBombFromCart(cart, cartBombDistanceTiles);
    }

    public bool TryLaunchBombFromCart(BattleRevengeController cart, int distanceTiles)
    {
        if (!IsRuntimeEnabled || cart == null)
            return false;

        EnsureBattleSetupCached();

        if (!playersById.TryGetValue(cart.OwnerPlayerId, out MovementController ownerMovement) || ownerMovement == null)
            return false;

        if (!ownerMovement.TryGetComponent<BombController>(out var bombController))
            return false;

        if (!cart.IsInLaunchableSegment)
            return false;

        Vector2 snappedPos = GetArenaSnapPosition(cart.LaunchStartWorldPosition);
        Vector2 launchDirection = cart.LaunchDirection.normalized;

        Vector2 finalStart = snappedPos;

        int clampedDistance = Mathf.Clamp(distanceTiles, 3, 7);
        Vector2 landingPosition = finalStart + (launchDirection * clampedDistance);

        if (!IsValidRevengeBombLandingPosition(landingPosition, cart, clampedDistance, "Launch"))
            return false;

        bool launched = bombController.LaunchRevengeBomb(
            finalStart,
            launchDirection,
            clampedDistance,
            revengeBombRadius);

        bool muzzleVfxSpawned = false;
        if (launched)
            muzzleVfxSpawned = bombController.SpawnRevengeLaunchMuzzleVisual(
                cart.LaunchStartWorldPosition + GetRevengeLaunchMuzzleVfxOffset(launchDirection),
                launchDirection,
                revengeLaunchMuzzleVfxDuration,
                revengeLaunchMuzzleVfxOrderInLayer,
                cart.LaunchMuzzleVfxParent);

        DebugLaunchResult(
            cart,
            finalStart,
            launchDirection,
            clampedDistance,
            landingPosition,
            launched,
            muzzleVfxSpawned);

        return launched;
    }

    public bool IsPredictedLandingPositionValid(BattleRevengeController cart, int distanceTiles)
    {
        return TryGetPredictedLandingPosition(cart, distanceTiles, out _);
    }

    public bool TryGetPredictedLandingPosition(BattleRevengeController cart, int distanceTiles, out Vector2 landingPosition)
    {
        landingPosition = Vector2.zero;

        if (cart == null || !cart.IsInLaunchableSegment)
            return false;

        Vector2 start = GetArenaSnapPosition(cart.LaunchStartWorldPosition);
        Vector2 direction = cart.LaunchDirection.normalized;
        int clampedDistance = Mathf.Clamp(distanceTiles, 3, 7);

        landingPosition = start + (direction * clampedDistance);
        return IsValidRevengeBombLandingPosition(landingPosition, cart, clampedDistance, "Predict");
    }

    public Vector2 GetPredictedLandingPosition(BattleRevengeController cart, int distanceTiles)
    {
        return TryGetPredictedLandingPosition(cart, distanceTiles, out Vector2 landingPosition)
            ? landingPosition
            : Vector2.zero;
    }

    private bool IsValidRevengeBombLandingPosition(
        Vector2 worldPosition,
        BattleRevengeController cart = null,
        int distanceTiles = 0,
        string phase = null)
    {
        if (GameManager.Instance == null || GameManager.Instance.groundTilemap == null)
        {
            DebugLandingValidation(
                phase,
                cart,
                distanceTiles,
                worldPosition,
                Vector3Int.zero,
                false,
                false,
                false,
                false,
                default,
                "MissingGroundTilemap");

            return false;
        }

        Tilemap groundTilemap = GameManager.Instance.groundTilemap;
        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        bool hasGround = groundTilemap.HasTile(cell);
        bool hasIndestructible =
            GameManager.Instance.indestructibleTilemap != null &&
            GameManager.Instance.indestructibleTilemap.HasTile(cell);
        bool hasDestructible =
            GameManager.Instance.destructibleTilemap != null &&
            GameManager.Instance.destructibleTilemap.HasTile(cell);
        bool insideArena = IsCellInsideArenaBounds(cell, out BoundsInt arenaBounds);

        if (!insideArena)
        {
            DebugLandingValidation(
                phase,
                cart,
                distanceTiles,
                worldPosition,
                cell,
                hasGround,
                hasIndestructible,
                hasDestructible,
                insideArena,
                arenaBounds,
                "OutOfArena");

            return false;
        }

        DebugLandingValidation(
            phase,
            cart,
            distanceTiles,
            worldPosition,
            cell,
            hasGround,
            hasIndestructible,
            hasDestructible,
            insideArena,
            arenaBounds,
            "Valid");

        return true;
    }

    private bool IsCellInsideArenaBounds(Vector3Int cell, out BoundsInt arenaBounds)
    {
        arenaBounds = default;

        bool hasBounds = false;

        IncludeTilemapBounds(GameManager.Instance?.groundTilemap, ref arenaBounds, ref hasBounds);
        IncludeTilemapBounds(GameManager.Instance?.indestructibleTilemap, ref arenaBounds, ref hasBounds);
        IncludeTilemapBounds(GameManager.Instance?.destructibleTilemap, ref arenaBounds, ref hasBounds);

        return
            hasBounds &&
            cell.x >= arenaBounds.xMin &&
            cell.x < arenaBounds.xMax &&
            cell.y >= arenaBounds.yMin &&
            cell.y < arenaBounds.yMax;
    }

    private static void IncludeTilemapBounds(Tilemap tilemap, ref BoundsInt arenaBounds, ref bool hasBounds)
    {
        if (tilemap == null)
            return;

        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
            return;

        if (!hasBounds)
        {
            arenaBounds = bounds;
            hasBounds = true;
            return;
        }

        int xMin = Mathf.Min(arenaBounds.xMin, bounds.xMin);
        int yMin = Mathf.Min(arenaBounds.yMin, bounds.yMin);
        int xMax = Mathf.Max(arenaBounds.xMax, bounds.xMax);
        int yMax = Mathf.Max(arenaBounds.yMax, bounds.yMax);

        arenaBounds.SetMinMax(
            new Vector3Int(xMin, yMin, 0),
            new Vector3Int(xMax, yMax, 1));
    }

    private void DebugLandingValidation(
        string phase,
        BattleRevengeController cart,
        int distanceTiles,
        Vector2 landingPosition,
        Vector3Int cell,
        bool hasGround,
        bool hasIndestructible,
        bool hasDestructible,
        bool insideArena,
        BoundsInt arenaBounds,
        string result)
    {
        if (cart == null || !cart.DebugActionALaunchEnabled)
            return;

        if (Time.unscaledTime < nextLandingValidationDebugAt && phase != "Launch" && result == "Valid")
            return;

        nextLandingValidationDebugAt = Time.unscaledTime + cart.DebugActionAInterval;

        Debug.Log(
            $"[BattleRevenge][LandingValidation:{phase}] " +
            $"owner={cart.OwnerPlayerId} result={result} " +
            $"cartPos={cart.transform.position} launchStart={cart.LaunchStartWorldPosition} " +
            $"direction={cart.LaunchDirection} distance={distanceTiles} landing={landingPosition} cell={cell} " +
            $"insideArena={insideArena} arenaBounds=({arenaBounds.xMin},{arenaBounds.yMin})..({arenaBounds.xMax},{arenaBounds.yMax}) " +
            $"hasGround={hasGround} hasIndestructible={hasIndestructible} hasDestructible={hasDestructible}");
    }

    private void DebugLaunchResult(
        BattleRevengeController cart,
        Vector2 start,
        Vector2 direction,
        int distanceTiles,
        Vector2 predictedLanding,
        bool launched,
        bool muzzleVfxSpawned)
    {
        if (cart == null || !cart.DebugActionALaunchEnabled)
            return;

        Debug.Log(
            $"[BattleRevenge][ActionA:LaunchResult] " +
            $"owner={cart.OwnerPlayerId} launched={launched} muzzleVfx={muzzleVfxSpawned} " +
            $"cartPos={cart.transform.position} launchStart={cart.LaunchStartWorldPosition} " +
            $"snappedStart={start} direction={direction} distance={distanceTiles} predictedLanding={predictedLanding}");
    }

    private Vector2 GetRevengeLaunchMuzzleVfxOffset(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x < 0f
                ? revengeLaunchMuzzleVfxOffsetLeft
                : revengeLaunchMuzzleVfxOffsetRight;

        return direction.y < 0f
            ? revengeLaunchMuzzleVfxOffsetBottom
            : revengeLaunchMuzzleVfxOffsetTop;
    }

    private void EnsureBattleSetupCached()
    {
        RefreshLaneBounds();
        RefreshPlayerCache(includeInactive: true);

        if (!baseSnapshotsCaptured)
            CaptureBaseLoadouts();
    }

    private void CaptureBaseLoadouts()
    {
        baseLoadoutsByPlayer.Clear();

        for (int playerId = GameSession.MinPlayerId; playerId <= GameSession.MaxPlayerId; playerId++)
        {
            if (!IsConfiguredActivePlayer(playerId))
                continue;

            PlayerPersistentStats.PlayerState snapshot = PlayerPersistentStats.CreateLoadoutSnapshot(playerId);
            if (snapshot != null)
                baseLoadoutsByPlayer[playerId] = snapshot;
        }

        baseSnapshotsCaptured = true;
    }

    private void RefreshPlayerCache(bool includeInactive)
    {
        playersById.Clear();

        FindObjectsInactive inactiveMode = includeInactive
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        MovementController[] movements = FindObjectsByType<MovementController>(inactiveMode);
        for (int i = 0; i < movements.Length; i++)
        {
            MovementController movement = movements[i];
            if (movement == null || !movement.CompareTag("Player"))
                continue;

            if (!IsConfiguredActivePlayer(movement.PlayerId))
                continue;

            playersById[movement.PlayerId] = movement;
        }
    }

    private void RefreshLaneBounds()
    {
        Tilemap laneTilemap = null;

        if (GameManager.Instance != null)
            laneTilemap = GameManager.Instance.indestructibleTilemap != null
                ? GameManager.Instance.indestructibleTilemap
                : GameManager.Instance.groundTilemap;

        if (laneTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null)
                    continue;

                if (tilemap.name.IndexOf("Indestruct", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    laneTilemap = tilemap;
                    break;
                }
            }
        }

        if (laneTilemap == null)
            return;

        laneTilemap.CompressBounds();
        BoundsInt bounds = laneTilemap.cellBounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
            return;

        Vector3 leftWorld = laneTilemap.GetCellCenterWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
        Vector3 rightWorld = laneTilemap.GetCellCenterWorld(new Vector3Int(bounds.xMax - 1, bounds.yMin, 0));
        Vector3 bottomWorld = laneTilemap.GetCellCenterWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
        Vector3 topWorld = laneTilemap.GetCellCenterWorld(new Vector3Int(bounds.xMin, bounds.yMax - 1, 0));

        minEdgeX = leftWorld.x;
        maxEdgeX = rightWorld.x;
        minEdgeY = bottomWorld.y;
        maxEdgeY = topWorld.y;
    }

    private Vector2 GetArenaSnapPosition(Vector3 worldPosition)
    {
        Tilemap snapTilemap = GameManager.Instance != null && GameManager.Instance.groundTilemap != null
            ? GameManager.Instance.groundTilemap
            : GameManager.Instance != null
                ? GameManager.Instance.indestructibleTilemap
                : null;

        if (snapTilemap == null)
            return new Vector2(Mathf.Round(worldPosition.x), Mathf.Round(worldPosition.y));

        Vector3Int cell = snapTilemap.WorldToCell(worldPosition);
        Vector3 center = snapTilemap.GetCellCenterWorld(cell);
        return new Vector2(center.x, center.y);
    }

    private Vector2 GetCartPositionForDeath(Vector3 deathWorldPosition, out Vector2 inwardDirection)
    {
        Vector2 snapped = GetArenaSnapPosition(deathWorldPosition);

        float distLeft = Mathf.Abs(snapped.x - minEdgeX);
        float distRight = Mathf.Abs(snapped.x - maxEdgeX);
        float distBottom = Mathf.Abs(snapped.y - minEdgeY);
        float distTop = Mathf.Abs(snapped.y - maxEdgeY);

        float best = distLeft;
        inwardDirection = Vector2.right;
        Vector2 result = new Vector2(minEdgeX, Mathf.Clamp(snapped.y, minEdgeY, maxEdgeY));

        if (distRight < best)
        {
            best = distRight;
            inwardDirection = Vector2.left;
            result = new Vector2(maxEdgeX, Mathf.Clamp(snapped.y, minEdgeY, maxEdgeY));
        }

        if (distTop < best)
        {
            best = distTop;
            inwardDirection = Vector2.down;
            result = new Vector2(Mathf.Clamp(snapped.x, minEdgeX, maxEdgeX), maxEdgeY);
        }

        if (distBottom < best)
        {
            inwardDirection = Vector2.up;
            result = new Vector2(Mathf.Clamp(snapped.x, minEdgeX, maxEdgeX), minEdgeY);
        }

        return result;
    }

    private BattleRevengeController AcquireCart()
    {
        if (BattleRevengeBomberBlocker.PreventNewRevengeBombers)
            return null;

        while (cartPool.Count > 0)
        {
            BattleRevengeController pooledCart = cartPool.Pop();
            if (pooledCart != null)
                return PrepareCartForWorldSpace(pooledCart);
        }

        if (cartPrefab == null)
            return null;

        return PrepareCartForWorldSpace(Instantiate(cartPrefab));
    }

    private BattleRevengeController PrepareCartForWorldSpace(BattleRevengeController cart)
    {
        if (cart == null)
            return null;

        cart.transform.SetParent(null, true);
        return cart;
    }

    private void CleanupDestroyedActiveCarts()
    {
        if (activeCartsByOwner.Count == 0)
            return;

        List<int> invalidKeys = null;

        foreach (KeyValuePair<int, BattleRevengeController> pair in activeCartsByOwner)
        {
            if (pair.Value != null)
                continue;

            invalidKeys ??= new List<int>();
            invalidKeys.Add(pair.Key);
        }

        if (invalidKeys == null)
            return;

        for (int i = 0; i < invalidKeys.Count; i++)
            activeCartsByOwner.Remove(invalidKeys[i]);
    }

    private bool IsConfiguredActivePlayer(int playerId)
    {
        playerId = Mathf.Clamp(playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        if (GameSession.Instance != null)
            return GameSession.Instance.IsPlayerActive(playerId);

        return true;
    }

    private static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }
}
