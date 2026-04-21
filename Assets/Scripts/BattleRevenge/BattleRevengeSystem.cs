using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleRevengeSystem : MonoBehaviour
{
    [SerializeField] private BattleRevengeController cartPrefab;
    private readonly float cartBombCooldownSeconds = 2f;
    [SerializeField, Min(1)] private int cartBombDistanceTiles = 3;
    [SerializeField, Min(1)] private int revengeBombRadius = 2;
    [SerializeField, Min(0.1f)] private float respawnInvulnerabilitySeconds = 2f;
    [SerializeField, Min(0.01f)] private float respawnBlinkInterval = 0.08f;

    private readonly Dictionary<int, BattleRevengeController> activeCartsByOwner = new();
    private readonly Stack<BattleRevengeController> cartPool = new();
    private readonly Dictionary<int, PlayerPersistentStats.PlayerState> baseLoadoutsByPlayer = new();
    private readonly Dictionary<int, MovementController> playersById = new();

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
        BattleModeRules.Instance.EnableRevengeBomber;

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
        if (!IsRuntimeEnabled || deadPlayer == null || !deadPlayer.CompareTag("Player"))
            return;

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
        if (!IsRuntimeEnabled || victim == null || hazard == null || !victim.CompareTag("Player"))
            return false;

        BombExplosion explosion =
            hazard.GetComponent<BombExplosion>() ??
            hazard.GetComponentInParent<BombExplosion>() ??
            hazard.GetComponentInChildren<BombExplosion>();

        if (explosion == null || !explosion.IsRevengeBomb)
            return false;

        int respawnPlayerId = Mathf.Clamp(explosion.OwnerPlayerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        if (respawnPlayerId == victim.PlayerId)
            return false;

        if (!activeCartsByOwner.TryGetValue(respawnPlayerId, out BattleRevengeController cart) || cart == null)
            return false;

        EnsureBattleSetupCached();

        if (!playersById.TryGetValue(respawnPlayerId, out MovementController respawningPlayer) || respawningPlayer == null)
            return false;

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

        void TryFinalizeSwap()
        {
            if (swapFinalized)
                return;

            if (!victimDeathFinished || !revengeJumpFinished)
                return;

            swapFinalized = true;

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
            }

            cart.ReenterAs(
                victimPlayerId,
                victim,
                newCartPosition,
                newInwardDirection);

            GameManager.Instance?.CheckWinState();
        }

        victim.PlayBattleRevengeSwapDeathSequence(() =>
        {
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
                if (!respawningPlayerReleased)
                {
                    respawningPlayer.RespawnFromBattleRevenge(
                        respawnPosition,
                        respawnInvulnerabilitySeconds,
                        respawnBlinkInterval);

                    respawningPlayerReleased = true;
                }

                revengeJumpFinished = true;
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
        if (cart != null)
            cart.HideImmediately();

        if (respawningPlayer == null)
        {
            onFinished?.Invoke();
            yield break;
        }

        PlayerRidingController riding = respawningPlayer.GetComponent<PlayerRidingController>();
        if (riding == null)
        {
            if (!respawningPlayer.gameObject.activeSelf)
                respawningPlayer.gameObject.SetActive(true);

            respawningPlayer.transform.position = startWorldPos;
            onFinished?.Invoke();
            yield break;
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
        respawningPlayer.SetExternalVisualSuppressed(false);
        respawningPlayer.SetAllSpritesVisible(false);

        bool finished = false;

        if (!riding.TryPlayMountArc(
                facing,
                startWorldPos,
                targetWorldPos,
                onComplete: () => finished = true))
        {
            finished = true;
        }

        while (!finished)
            yield return null;

        onFinished?.Invoke();
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

        Vector2 rawCartPos = cart.transform.position;
        Vector2 snappedPos = GetArenaSnapPosition(rawCartPos);
        Vector2 launchDirection = cart.LaunchDirection.normalized;

        Vector2 finalStart = snappedPos;

        int clampedDistance = Mathf.Clamp(distanceTiles, 3, 7);

        return bombController.LaunchRevengeBomb(
            finalStart,
            launchDirection,
            clampedDistance,
            revengeBombRadius);
    }

    public Vector2 GetPredictedLandingPosition(BattleRevengeController cart, int distanceTiles)
    {
        if (cart == null)
            return Vector2.zero;

        Vector2 start = GetArenaSnapPosition(cart.transform.position);
        Vector2 direction = cart.LaunchDirection.normalized;
        int clampedDistance = Mathf.Clamp(distanceTiles, 3, 7);

        return start + (direction * clampedDistance);
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
        while (cartPool.Count > 0)
        {
            BattleRevengeController pooledCart = cartPool.Pop();
            if (pooledCart != null)
                return pooledCart;
        }

        return Instantiate(cartPrefab, transform);
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