using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
public partial class BombController : MonoBehaviour
{
    private const int RevengeLaunchMinDistanceTiles = 3;
    private const int RevengeLaunchMaxDistanceTiles = 7;
    private const float RevengeLaunchMinArcHeightTiles = 2f;
    private const float RevengeLaunchMaxArcHeightTiles = 3f;
    private const float RevengeLaunchBounceArcHeightTiles = 1f;

    [Header("Player Id (only used if tagged Player)")]
    [SerializeField, Range(1, 6)] private int playerId = 1;
    public int PlayerId => playerId;

    [Header("Input")]
    public bool useAIInput = false;
    private bool bombRequested;

    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public GameObject pierceBombPrefab;
    public GameObject controlBombPrefab;
    public GameObject powerBombPrefab;
    public GameObject rubberBombPrefab;
    public GameObject magnetBombPrefab;
    public GameObject revengeBombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmout = 1;

    [Header("Chain Explosion")]
    [SerializeField] private float chainBombDelaySeconds = 0.1f;
    private readonly HashSet<GameObject> scheduledChainBombs = new();
    private readonly Dictionary<GameObject, Vector2> scheduledChainBombSnapPositions = new();
    private int _lastChainScheduleFrame = -1;
    private int _chainIndexThisFrame = 0;

    private int bombsRemaining = 0;
    public int BombsRemaining => bombsRemaining;

    private readonly HashSet<int> _activeSpotlightIds = new();
    private int _nextSpotlightBaseId = 1;

    [Header("Control Bomb")]
    private readonly List<GameObject> plantedBombs = new();

    [Header("Power Bomb")]
    [SerializeField, Min(1)] private int powerBombRadius = 15;
    private GameObject activePowerBomb;

    [Header("Explosion Settings")]
    private const string ExplosionPrefabResourcesPath = "Explosions/BombExplosion";
    private static BombExplosion cachedExplosionPrefab;
    public BombExplosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;

    [Header("Stage & Tiles")]
    public Tilemap groundTiles;
    public Tilemap stageBoundsTiles;

    [Header("Water (blocks explosion)")]
    [SerializeField] private Tilemap waterTiles;
    [SerializeField] private string waterTag = "Water";

    [Header("Hole (kills bomb)")]
    [SerializeField] private Tilemap holeTiles;
    [SerializeField] private string holeTag = "Hole";

    [Header("Destructible (resolved from GameManager)")]
    public Tilemap destructibleTiles;
    public Destructible destructiblePrefab;

    [Header("Destructible Tile Effects (Strategy B/2)")]
    [SerializeField] private DestructibleTileResolver destructibleTileResolver;

    [Header("Items")]
    public LayerMask itemLayerMask;

    [Header("SFX")]
    public AudioClip placeBombSfx;
    public AudioSource playerAudioSource;

    [Header("Water Destroy SFX")]
    [SerializeField] private AudioClip waterDestroySfx;
    [Range(0f, 1f)][SerializeField] private float waterDestroyVolume = 1f;

    [Header("Hole Destroy SFX")]
    [SerializeField] private AudioClip holeDestroySfx;
    [Range(0f, 1f)][SerializeField] private float holeDestroyVolume = 1f;

    [Header("Water Sink Animation")]
    private readonly float waterSinkSeconds = 0.55f;

    [Header("Hole Sink Animation")]
    private readonly float holeSinkSeconds = 0.55f;

    [Header("Water Sink Tint")]
    [SerializeField] private bool waterSinkApplyBlueTint = true;
    [SerializeField] private bool waterSinkTintAffectsChildren = true;
    [SerializeField] private Color waterSinkTint = new(0.45f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float waterSinkTargetAlpha = 0.25f;

    [Header("Hole Sink Tint (darken to solid black)")]
    [SerializeField] private bool holeSinkApplyBlackTint = true;
    [SerializeField] private bool holeSinkTintAffectsChildren = true;
    [SerializeField] private Color holeSinkTint = new(0f, 0f, 0f, 1f);

    [Header("Explosion SFX By Radius (1..9, >=10 = last)")]
    public AudioClip[] explosionSfxByRadius = new AudioClip[10];
    [Range(0f, 1f)] public float explosionSfxVolume = 1f;

    private const int ExplosionSfxRadiusCount = 10;
    private const string DefaultExplosionSfxResourcesPath = "Sounds/Explosions/BombExplosion";
    private const string PierceExplosionSfxResourcesPath = "Sounds/Explosions/PierceBombExplosion";
    private static AudioClip[] cachedDefaultExplosionSfx;
    private static AudioClip[] cachedPierceExplosionSfx;
    private static bool explosionSfxPreloaded;
    private static AudioSource currentExplosionAudio;

    private GameManager _gm;
    private AudioSource _localAudio;
    private MovementController cachedMovement;
    private PlayerRidingController cachedRidingController;
    private AbilitySystem cachedAbilitySystem;
    private GreenLouieDashAbility cachedGreenLouieDashAbility;
    private BombKickAbility cachedBombKickAbility;
    private YellowLouieKickAbility cachedYellowLouieKickAbility;
    private MagnetBombAbility cachedMagnetBombAbility;
    private int bombLayer = -1;
    private int bombMask;
    private ContactFilter2D bombContactFilter;
    private int explosionLayer = -1;
    private int explosionMask;
    private ContactFilter2D explosionContactFilter;
    private bool queryCachesInitialized;

    private static readonly Collider2D[] _bombOverlapBuffer = new Collider2D[16];
    private static readonly Collider2D[] _explosionOverlapBuffer = new Collider2D[24];
    private static readonly List<Bomb> _activeBombSnapshot = new(64);

    private GameObject lastPlacedBomb;
    public GameObject GetLastPlacedBomb() => lastPlacedBomb;
    private float skullBombFuseMultiplier = 1f;
    private float skullBombFuseMultiplierUntil;
    private int skullExplosionRadiusOverride;
    private float skullExplosionRadiusOverrideUntil;
    private float skullBombPlacementBlockedUntil;

    [Header("Ground Tile Effects")]
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Indestructible Tile Effects")]
    [SerializeField] private IndestructibleTileResolver indestructibleTileResolver;

    private readonly HashSet<GameObject> _removedBombs = new(128);

    private struct ExplosionLineResult
    {
        public int Reach;
        public List<(Vector2 position, BombExplosion.ExplosionPart part)> Explosions;
    }

    public void SetPlayerId(int id)
    {
        playerId = Mathf.Clamp(id, 1, 6);
    }

    private void Awake()
    {
        _localAudio = GetComponent<AudioSource>();
        if (playerAudioSource == null)
            playerAudioSource = _localAudio;

        CacheRuntimeReferences();
        EnsureQueryCaches();
        ResolveExplosionPrefab();

        ResolveTilemaps();
        ResolveDestructibleTileResolver();
        ResolveGroundTileResolver();
        ResolveIndestructibleTileResolver();
    }

    private void Start()
    {
        ResolveTilemaps();
        ResolveDestructibleTileResolver();
        ResolveGroundTileResolver();
        ResolveIndestructibleTileResolver();
    }

    private void CacheRuntimeReferences()
    {
        cachedMovement ??= GetComponent<MovementController>();
        cachedRidingController ??= GetComponent<PlayerRidingController>();
        _ = GetCachedComponent(ref cachedAbilitySystem);
        _ = GetCachedComponent(ref cachedGreenLouieDashAbility);
        _ = GetCachedComponent(ref cachedBombKickAbility);
        _ = GetCachedComponent(ref cachedYellowLouieKickAbility);
        _ = GetCachedComponent(ref cachedMagnetBombAbility);
    }

    private void EnsureQueryCaches()
    {
        if (queryCachesInitialized)
            return;

        bombLayer = LayerMask.NameToLayer("Bomb");
        bombMask = bombLayer >= 0 ? (1 << bombLayer) : 0;
        bombContactFilter = new ContactFilter2D { useLayerMask = true };
        bombContactFilter.SetLayerMask(bombMask);
        bombContactFilter.useTriggers = true;

        explosionLayer = LayerMask.NameToLayer("Explosion");
        explosionMask = explosionLayer >= 0 ? (1 << explosionLayer) : 0;
        explosionContactFilter = new ContactFilter2D { useLayerMask = true };
        explosionContactFilter.SetLayerMask(explosionMask);
        explosionContactFilter.useTriggers = true;

        queryCachesInitialized = true;
    }

    private T GetCachedComponent<T>(ref T component) where T : Component
    {
        if (component == null)
            TryGetComponent(out component);

        return component;
    }

    private MovementController GetMovement()
    {
        if (cachedMovement == null)
            cachedMovement = GetComponent<MovementController>();

        return cachedMovement;
    }

    private AbilitySystem GetAbilitySystem()
    {
        return GetCachedComponent(ref cachedAbilitySystem);
    }

    private bool IsDashActive()
    {
        var dashAbility = GetCachedComponent(ref cachedGreenLouieDashAbility);
        return dashAbility != null && dashAbility.DashActive;
    }

    private bool IsRidingTransitionActive()
    {
        return cachedRidingController != null && cachedRidingController.IsPlaying;
    }

    private static Vector2 GetBombPlantDirection(MovementController movement)
    {
        if (movement != null)
        {
            if (movement.Direction != Vector2.zero)
                return movement.Direction.normalized;

            if (movement.FacingDirection != Vector2.zero)
                return movement.FacingDirection.normalized;
        }

        return Vector2.down;
    }

    private void OnEnable()
    {
        CacheRuntimeReferences();
        EnsureQueryCaches();
        ResolveExplosionPrefab();
        bombAmout = Mathf.Min(bombAmout, PlayerPersistentStats.MaxBombAmount);
        bombsRemaining = bombAmout;
        lastPlacedBomb = null;
        activePowerBomb = null;
        _nextSpotlightBaseId = 1;
        _removedBombs.Clear();
        scheduledChainBombs.Clear();
        CleanupNullBombs();
    }

    public void ResetRuntimeStateAfterRespawn()
    {
        CacheRuntimeReferences();
        EnsureQueryCaches();
        ResolveExplosionPrefab();

        bombAmout = Mathf.Min(bombAmout, PlayerPersistentStats.MaxBombAmount);
        bombsRemaining = bombAmout;
        lastPlacedBomb = null;
        activePowerBomb = null;
        _nextSpotlightBaseId = 1;
        _removedBombs.Clear();
        scheduledChainBombs.Clear();
        plantedBombs.Clear();
        CleanupNullBombs();
    }

    private void Update()
    {
        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        var movement = GetMovement();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (IsRidingTransitionActive())
            return;

        if (IsDashActive())
            return;

        if (GamePauseController.IsPaused)
            return;

        if (!useAIInput && CompareTag("Player"))
        {
            var input = PlayerInputManager.Instance;
            if (input == null)
                return;

            if (bombsRemaining > 0 && input.GetDown(playerId, PlayerAction.ActionA))
                PlaceBomb();

            if (IsControlEnabled() && input.GetDown(playerId, PlayerAction.ActionB))
                TryExplodeOldestControlledBomb();
        }
        else
        {
            if (bombsRemaining <= 0)
                return;

            if (bombRequested)
            {
                PlaceBomb();
                bombRequested = false;
            }
        }
    }

    private void ResolveTilemaps()
    {
        if (_gm == null)
            _gm = FindAnyObjectByType<GameManager>();

        if (_gm != null)
        {
            if (groundTiles == null) groundTiles = _gm.groundTilemap;
            if (destructibleTiles == null) destructibleTiles = _gm.destructibleTilemap;

            if (stageBoundsTiles == null)
                stageBoundsTiles = _gm.indestructibleTilemap != null ? _gm.indestructibleTilemap : _gm.groundTilemap;

            destructiblePrefab = _gm.destructiblePrefab;
        }

        var tilemaps = FindObjectsByType<Tilemap>();

        if (groundTiles == null)
            groundTiles = FindTilemapByNameContains(tilemaps, "ground") ?? (tilemaps.Length > 0 ? tilemaps[0] : null);

        if (stageBoundsTiles == null)
            stageBoundsTiles = FindTilemapByNameContains(tilemaps, "indestruct") ?? groundTiles;

        if (destructibleTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.CompareTag("Destructibles"))
                {
                    destructibleTiles = tm;
                    break;
                }
            }

            if (destructibleTiles == null)
                destructibleTiles = FindTilemapByNameContains(tilemaps, "destruct");
        }

        if (waterTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.CompareTag(waterTag))
                {
                    waterTiles = tm;
                    break;
                }
            }

            if (waterTiles == null)
                waterTiles = FindTilemapByNameContains(tilemaps, "water");
        }

        if (holeTiles == null)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm != null && tm.CompareTag(holeTag))
                {
                    holeTiles = tm;
                    break;
                }
            }

            if (holeTiles == null)
                holeTiles = FindTilemapByNameContains(tilemaps, "hole");
        }
    }

    private Tilemap FindTilemapByNameContains(Tilemap[] tilemaps, string containsLower)
    {
        for (int i = 0; i < tilemaps.Length; i++)
        {
            var tm = tilemaps[i];
            if (tm == null) continue;

            string n = tm.name.ToLowerInvariant();
            if (n.Contains(containsLower))
                return tm;
        }
        return null;
    }

    private void ResolveDestructibleTileResolver()
    {
        if (destructibleTileResolver != null)
            return;

        destructibleTileResolver = FindAnyObjectByType<DestructibleTileResolver>();
        if (destructibleTileResolver != null)
            return;

        if (destructibleTiles != null)
            destructibleTileResolver = destructibleTiles.GetComponentInParent<DestructibleTileResolver>(true);

        if (destructibleTileResolver != null)
            return;

        var stage = GameObject.Find("Stage");
        if (stage != null)
            destructibleTileResolver = stage.GetComponentInChildren<DestructibleTileResolver>(true);
    }

    private void ResolveGroundTileResolver()
    {
        if (groundTileResolver != null)
            return;

        groundTileResolver = FindAnyObjectByType<GroundTileResolver>();
        if (groundTileResolver != null)
            return;

        if (groundTiles != null)
            groundTileResolver = groundTiles.GetComponentInParent<GroundTileResolver>(true);

        if (groundTileResolver != null)
            return;

        var stage = GameObject.Find("Stage");
        if (stage != null)
            groundTileResolver = stage.GetComponentInChildren<GroundTileResolver>(true);
    }

    private void ResolveIndestructibleTileResolver()
    {
        if (indestructibleTileResolver != null)
            return;

        indestructibleTileResolver = FindAnyObjectByType<IndestructibleTileResolver>();
        if (indestructibleTileResolver != null)
            return;

        if (stageBoundsTiles != null)
            indestructibleTileResolver = stageBoundsTiles.GetComponentInParent<IndestructibleTileResolver>(true);

        if (indestructibleTileResolver != null)
            return;

        var stage = GameObject.Find("Stage");
        if (stage != null)
            indestructibleTileResolver = stage.GetComponentInChildren<IndestructibleTileResolver>(true);
    }

    private void ResolveExplosionPrefab()
    {
        BombExplosion.PreloadPierceSprites();
        PreloadExplosionSfx();

        if (explosionPrefab != null)
            return;

        if (cachedExplosionPrefab == null)
            cachedExplosionPrefab = Resources.Load<BombExplosion>(ExplosionPrefabResourcesPath);

        explosionPrefab = cachedExplosionPrefab;
    }

    private void ApplyExplosionSource(BombExplosion explosion, Bomb sourceBomb)
    {
        if (explosion == null)
            return;

        BombController ownerController = this;
        int sourcePlayerId = 0;
        bool isRevengeBomb = false;

        if (sourceBomb != null)
        {
            if (sourceBomb.Owner != null)
            {
                ownerController = sourceBomb.Owner;
                sourcePlayerId = sourceBomb.Owner.PlayerId;
            }

            isRevengeBomb = sourceBomb.IsRevengeBomb;
        }

        explosion.SetSource(ownerController, sourcePlayerId, isRevengeBomb);
    }

    private BombExplosion SpawnExplosionVisual(
        Vector2 position,
        BombExplosion.ExplosionPart part,
        Vector2 direction,
        Vector2 origin,
        Bomb sourceBomb = null)
    {
        BombExplosion explosion = BombExplosion.Spawn(explosionPrefab, position, Quaternion.identity);
        ApplyExplosionSource(explosion, sourceBomb);
        explosion.Play(part, direction, 0f, explosionDuration, origin, sourceBomb != null && sourceBomb.IsPierceBomb);
        return explosion;
    }

    private BombExplosion SpawnExplosionDamageHitbox(
        Vector2 position,
        Vector2 origin,
        Bomb sourceBomb = null)
    {
        BombExplosion explosion = BombExplosion.Spawn(explosionPrefab, position, Quaternion.identity);
        if (explosion == null)
            return null;

        ApplyExplosionSource(explosion, sourceBomb);
        explosion.PlayDamageOnly(explosionDuration, origin);
        return explosion;
    }

    public void AddBomb()
    {
        if (bombAmout >= PlayerPersistentStats.MaxBombAmount)
            return;

        bombAmout++;
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    public void RefundBombSlot()
    {
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    private bool IsPierceEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(PierceBombAbility.AbilityId);
    }

    private bool IsControlEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(ControlBombAbility.AbilityId);
    }

    private bool IsRubberEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(RubberBombAbility.AbilityId);
    }

    private bool IsFullFireEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(FullFireAbility.AbilityId);
    }

    private bool IsMagnetBombEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(MagnetBombAbility.AbilityId);
    }

    private Tilemap GetSnapTilemapForGround()
    {
        return groundTiles != null ? groundTiles : stageBoundsTiles;
    }

    private Vector2 SnapToTileCenter(Tilemap tm, Vector2 worldPos, out Vector3Int cell, out Vector2 center)
    {
        if (tm == null)
        {
            cell = default;
            center = new Vector2(Mathf.Round(worldPos.x), Mathf.Round(worldPos.y));
            return center;
        }

        cell = tm.WorldToCell(worldPos);
        Vector3 c = tm.GetCellCenterWorld(cell);
        center = new Vector2(c.x, c.y);
        return center;
    }

    private Vector2 SnapToTileCenter(Tilemap tm, Vector2 worldPos)
    {
        return SnapToTileCenter(tm, worldPos, out _, out _);
    }

    private bool TryGetAnyBombColliderAt(Vector2 position, float size, out Collider2D bombCol)
    {
        EnsureQueryCaches();

        if (bombMask == 0)
        {
            bombCol = null;
            return false;
        }

        int count = Physics2D.OverlapBox(position, Vector2.one * size, 0f, bombContactFilter, _bombOverlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var c = _bombOverlapBuffer[i];
            _bombOverlapBuffer[i] = null;

            if (c == null)
                continue;

            bombCol = c;
            return true;
        }

        bombCol = null;
        return false;
    }

    private bool TileHasBomb(Vector2 position)
    {
        return TryGetAnyBombColliderAt(position, 0.6f, out _);
    }

    private bool HasActiveExplosionAt(Vector2 position)
    {
        EnsureQueryCaches();

        if (explosionMask == 0)
            return false;

        var hit = Physics2D.OverlapBox(
            position,
            Vector2.one * 0.6f,
            0f,
            explosionMask
        );

        return hit != null;
    }

    private void ExplodeAnyBombAt(Vector2 position)
    {
        EnsureQueryCaches();

        if (bombMask == 0)
            return;

        int count = Physics2D.OverlapBox(position, Vector2.one * 0.6f, 0f, bombContactFilter, _bombOverlapBuffer);
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var hit = _bombOverlapBuffer[i];
            _bombOverlapBuffer[i] = null;

            if (hit == null)
                continue;

            GameObject bombGo = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.gameObject
                : hit.gameObject;

            if (bombGo == null)
                continue;

            ExplodeBomb(bombGo);
        }
    }

    private bool HasIndestructibleAt(Vector2 worldPos)
    {
        if (stageBoundsTiles == null)
            return false;

        Vector3Int cell = stageBoundsTiles.WorldToCell(worldPos);
        return stageBoundsTiles.GetTile(cell) != null;
    }

    private bool HasWaterAt(Vector2 worldPos)
    {
        if (waterTiles == null)
        {
            ResolveTilemaps();
            if (waterTiles == null)
                return false;
        }

        Vector3Int cell = waterTiles.WorldToCell(worldPos);
        return waterTiles.GetTile(cell) != null;
    }

    private bool HasHoleAt(Vector2 worldPos)
    {
        if (holeTiles == null)
        {
            ResolveTilemaps();
            if (holeTiles == null)
                return false;
        }

        Vector3Int cell = holeTiles.WorldToCell(worldPos);
        return holeTiles.GetTile(cell) != null;
    }

    private bool HasDestructibleAt(Vector2 worldPos)
    {
        if (destructibleTiles == null)
            return false;

        Vector3Int cell = destructibleTiles.WorldToCell(worldPos);
        TileBase tile = destructibleTiles.GetTile(cell);
        if (tile == null)
            return false;

        ResolveDestructibleTileResolver();
        if (destructibleTileResolver != null &&
            !destructibleTileResolver.IsRegisteredDestructibleTile(tile))
            return false;

        return true;
    }

    private bool TryGetDestructibleTileAt(Vector2 worldPos, out Vector3Int cell, out TileBase tile)
    {
        cell = default;
        tile = null;

        if (destructibleTiles == null)
            return false;

        cell = destructibleTiles.WorldToCell(worldPos);
        tile = destructibleTiles.GetTile(cell);
        if (tile == null)
            return false;

        ResolveDestructibleTileResolver();
        if (destructibleTileResolver != null &&
            !destructibleTileResolver.IsRegisteredDestructibleTile(tile))
        {
            tile = null;
            return false;
        }

        return true;
    }

    private bool HasGroundAt(Vector2 worldPos)
    {
        if (groundTiles == null)
            return false;

        Vector3Int cell = groundTiles.WorldToCell(worldPos);
        return groundTiles.GetTile(cell) != null;
    }

    private bool HasDestroyingDestructibleAt(Vector2 worldPos)
    {
        int mask = LayerMask.GetMask("Stage");

        Collider2D hit = Physics2D.OverlapBox(
            worldPos,
            Vector2.one * 0.6f,
            0f,
            mask
        );

        if (hit == null)
            return false;

        return hit.GetComponent<Destructible>() != null;
    }

    public bool CanSpawnTileEffectAt(Vector2 worldPos)
    {
        if (!HasGroundAt(worldPos))
            return false;

        if (HasIndestructibleAt(worldPos))
            return false;

        return true;
    }

    private bool CanPlaceBombAt(Vector2 worldPos)
    {
        if (!HasGroundAt(worldPos))
            return false;

        if (HasIndestructibleAt(worldPos))
            return false;

        if (HasWaterAt(worldPos))
            return false;

        if (HasHoleAt(worldPos))
            return false;

        return true;
    }

    public void ApplyTemporarySkullBombFuseMultiplier(float multiplier, float durationSeconds)
    {
        skullBombFuseMultiplier = Mathf.Max(0.01f, multiplier);
        skullBombFuseMultiplierUntil = Time.time + Mathf.Max(0.01f, durationSeconds);
    }

    public void ApplyTemporarySkullExplosionRadiusOverride(int radius, float durationSeconds)
    {
        skullExplosionRadiusOverride = Mathf.Max(1, radius);
        skullExplosionRadiusOverrideUntil = Time.time + Mathf.Max(0.01f, durationSeconds);
    }

    public void ApplyTemporarySkullBombPlacementBlock(float durationSeconds)
    {
        skullBombPlacementBlockedUntil = Time.time + Mathf.Max(0.01f, durationSeconds);
    }

    public void ClearTemporarySkullBombModifiers()
    {
        skullBombFuseMultiplier = 1f;
        skullBombFuseMultiplierUntil = 0f;
        skullExplosionRadiusOverride = 0;
        skullExplosionRadiusOverrideUntil = 0f;
        skullBombPlacementBlockedUntil = 0f;
    }

    bool IsSkullBombPlacementBlocked()
    {
        return Time.time < skullBombPlacementBlockedUntil;
    }

    float GetSkullModifiedBombFuseTime(float baseFuseSeconds)
    {
        if (Time.time >= skullBombFuseMultiplierUntil)
            return baseFuseSeconds;

        return baseFuseSeconds * skullBombFuseMultiplier;
    }

    int GetSkullExplosionRadiusOverride()
    {
        if (Time.time >= skullExplosionRadiusOverrideUntil)
            return 0;

        return skullExplosionRadiusOverride;
    }

    private void PlaceBomb()
    {
        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        var movement = GetMovement();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (IsRidingTransitionActive())
            return;

        if (IsDashActive())
            return;

        if (GamePauseController.IsPaused)
            return;

        if (IsSkullBombPlacementBlocked())
            return;

        ResolveTilemaps();

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 position = SnapToTileCenter(snapTm, (Vector2)transform.position, out _, out _);

        if (!CanPlaceBombAt(position))
            return;

        bool explosionAlreadyHere = HasActiveExplosionAt(position);

        if (TileHasBomb(position))
        {
            if (!explosionAlreadyHere)
                return;

            ExplodeAnyBombAt(position);
        }

        if (HasDestructibleAt(position))
            return;

        bool canUsePowerNow = CanUsePowerBombNow();

        bool controlEnabled = !canUsePowerNow && IsControlEnabled();
        bool pierceEnabled = !canUsePowerNow && !controlEnabled && IsPierceEnabled();
        bool rubberEnabled = !canUsePowerNow && !controlEnabled && !pierceEnabled && IsRubberEnabled();
        bool shouldMakeFirstPlacedBombMagnetic = IsMagnetBombEnabled() && !HasAnyAliveBombOwnedByMe();

        GameObject prefabToUse =
            (shouldMakeFirstPlacedBombMagnetic && magnetBombPrefab != null) ? magnetBombPrefab :
            (canUsePowerNow && powerBombPrefab != null) ? powerBombPrefab :
            (controlEnabled && controlBombPrefab != null) ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            (rubberEnabled && rubberBombPrefab != null) ? rubberBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return;

        PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        lastPlacedBomb = bomb;
        bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPowerBomb = canUsePowerNow;
        bombComponent.IsControlBomb = controlEnabled;
        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsRubberBomb = rubberEnabled;

        int skullRadiusOverride = GetSkullExplosionRadiusOverride();
        if (skullRadiusOverride > 0)
            bombComponent.ExplosionRadiusOverride = skullRadiusOverride;

        if (canUsePowerNow)
            TrackNewActivePowerBomb(bomb);

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.SetFuseSeconds(GetSkullModifiedBombFuseTime(bombFuseTime));
        bombComponent.Initialize(this);

        if (shouldMakeFirstPlacedBombMagnetic)
        {
            if (!bomb.TryGetComponent<MagnetBomb>(out var magnetBomb) || magnetBomb == null)
                magnetBomb = bomb.AddComponent<MagnetBomb>();

            magnetBomb.SetTargetLayer("Enemy");
        }

        if (!bomb.TryGetComponent<BombAtGroundTileNotifier>(out var notifier))
            notifier = bomb.AddComponent<BombAtGroundTileNotifier>();

        notifier.Initialize(this);

        TryHandleGroundBombAt(position, bomb);

        if (TryDestroyBombIfOnWater(bomb, position, refund: true))
            return;

        if (TryDestroyBombIfOnHole(bomb, position, refund: true))
            return;

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            ExplodeBomb(bomb);
            return;
        }

        if (!controlEnabled)
            bombComponent.BeginFuse();

        Vector2 plantDir = GetBombPlantDirection(movement);

        if (bombComponent == null || !bombComponent.IsRevengeBomb)
        {
            movement?.NotifyBombPlanted(bombComponent, plantDir);

            var kickAbility = GetCachedComponent(ref cachedBombKickAbility);
            if (kickAbility != null)
                kickAbility.NotifyBombPlanted(bombComponent, plantDir);
        }
    }

    private bool TryDestroyBombIfOnWater(GameObject bombGo, Vector2 worldPos, bool refund)
    {
        if (bombGo == null)
            return false;

        if (_removedBombs.Contains(bombGo))
            return true;

        if (waterTiles == null)
        {
            ResolveTilemaps();
            if (waterTiles == null)
                return false;
        }

        Vector3Int waterCell = waterTiles.WorldToCell(worldPos);
        TileBase waterTile = waterTiles.GetTile(waterCell);
        if (waterTile == null)
            return false;

        _removedBombs.Add(bombGo);

        Vector3 wc = waterTiles.GetCellCenterWorld(waterCell);
        Vector2 sinkPos = new(Mathf.Round(wc.x), Mathf.Round(wc.y));

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
        {
            bomb.LockWorldPosition(sinkPos);
            bomb.ForceStopExternalMovementAndSnap(sinkPos);
            bomb.MarkAsExploded();
        }

        if (bombGo.TryGetComponent<Bomb>(out var b) && b != null && b.IsPowerBomb)
            ClearActivePowerBombIfMatches(bombGo);

        PlayWaterDestroySfx();
        UnregisterBomb(bombGo);

        if (refund)
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);

        StartSafeCoroutine(WaterSinkAndDestroyRoutine(bombGo, sinkPos));
        return true;
    }

    private bool TryDestroyBombIfOnHole(GameObject bombGo, Vector2 worldPos, bool refund)
    {
        if (bombGo == null)
            return false;

        if (_removedBombs.Contains(bombGo))
            return true;

        if (holeTiles == null)
        {
            ResolveTilemaps();
            if (holeTiles == null)
                return false;
        }

        Vector3Int holeCell = holeTiles.WorldToCell(worldPos);
        TileBase holeTile = holeTiles.GetTile(holeCell);
        if (holeTile == null)
            return false;

        _removedBombs.Add(bombGo);

        Vector3 hc = holeTiles.GetCellCenterWorld(holeCell);
        Vector2 sinkPos = new(Mathf.Round(hc.x), Mathf.Round(hc.y));

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
        {
            bomb.StopAllCoroutines();
            bomb.LockWorldPosition(sinkPos);
            bomb.ForceStopExternalMovementAndSnap(sinkPos);
        }

        if (bombGo.TryGetComponent<Bomb>(out var b) && b != null && b.IsPowerBomb)
            ClearActivePowerBombIfMatches(bombGo);

        PlayHoleDestroySfx();
        UnregisterBomb(bombGo);

        if (refund)
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);

        StartSafeCoroutine(HoleSinkAndDestroyRoutine(bombGo, sinkPos));
        return true;
    }

    private IEnumerator WaterSinkAndDestroyRoutine(GameObject bombGo, Vector2 sinkWorldPos)
    {
        if (bombGo == null)
            yield break;

        var t = bombGo.transform;

        if (bombGo.TryGetComponent<Bomb>(out var bombComp) && bombComp != null)
        {
            bombComp.LockWorldPosition(sinkWorldPos);
            bombComp.ForceStopExternalMovementAndSnap(sinkWorldPos);
        }

        if (t != null)
            t.position = new Vector3(sinkWorldPos.x, sinkWorldPos.y, t.position.z);

        if (bombGo.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.position = sinkWorldPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        Physics2D.SyncTransforms();

        if (bombGo.TryGetComponent<BombAtGroundTileNotifier>(out var notifier) && notifier != null)
            notifier.enabled = false;

        if (bombGo.TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = false;

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var ar) && ar != null)
        {
            ar.SetFrozen(true);
            ar.enabled = true;
        }

        SpriteRenderer[] srs = null;
        Color[] originalColors = null;

        if (waterSinkApplyBlueTint)
        {
            srs = waterSinkTintAffectsChildren
                ? bombGo.GetComponentsInChildren<SpriteRenderer>(true)
                : bombGo.GetComponents<SpriteRenderer>();

            if (srs != null && srs.Length > 0)
            {
                originalColors = new Color[srs.Length];
                for (int i = 0; i < srs.Length; i++)
                {
                    var sr = srs[i];
                    originalColors[i] = sr != null ? sr.color : Color.white;
                }
            }
        }

        Vector3 startScale = t != null ? t.localScale : Vector3.one;

        float elapsed = 0f;

        while (elapsed < waterSinkSeconds)
        {
            if (bombGo == null)
                yield break;

            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / waterSinkSeconds);

            if (t != null)
                t.localScale = Vector3.Lerp(startScale, Vector3.zero, a);

            if (waterSinkApplyBlueTint && srs != null && originalColors != null)
            {
                float alpha = Mathf.Lerp(1f, waterSinkTargetAlpha, a);

                Color target = waterSinkTint;
                target.a = alpha;

                for (int r = 0; r < srs.Length; r++)
                {
                    var sr = srs[r];
                    if (sr == null) continue;

                    sr.color = Color.Lerp(originalColors[r], target, a);
                }
            }

            yield return null;
        }

        if (t != null)
            t.localScale = Vector3.zero;

        if (waterSinkApplyBlueTint && srs != null && originalColors != null)
        {
            for (int r = 0; r < srs.Length; r++)
            {
                var sr = srs[r];
                if (sr == null) continue;

                Color final = waterSinkTint;
                final.a = waterSinkTargetAlpha;
                sr.color = final;
            }
        }

        DisableBombDrivers(bombGo);

        if (bombGo.TryGetComponent<Bomb>(out var bombScript) && bombScript != null)
            bombScript.enabled = false;

        if (bombGo != null)
            Destroy(bombGo);
    }

    private IEnumerator HoleSinkAndDestroyRoutine(GameObject bombGo, Vector2 sinkWorldPos)
    {
        if (bombGo == null)
            yield break;

        var t = bombGo.transform;

        if (bombGo.TryGetComponent<Bomb>(out var bombComp) && bombComp != null)
        {
            bombComp.LockWorldPosition(sinkWorldPos);
            bombComp.ForceStopExternalMovementAndSnap(sinkWorldPos);
        }

        if (t != null)
            t.position = new Vector3(sinkWorldPos.x, sinkWorldPos.y, t.position.z);

        if (bombGo.TryGetComponent<Rigidbody2D>(out var rb) && rb != null)
        {
            rb.position = sinkWorldPos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        Physics2D.SyncTransforms();

        if (bombGo.TryGetComponent<BombAtGroundTileNotifier>(out var notifier) && notifier != null)
            notifier.enabled = false;

        if (bombGo.TryGetComponent<Collider2D>(out var col) && col != null)
            col.enabled = false;

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var ar) && ar != null)
        {
            ar.SetFrozen(true);
            ar.enabled = true;
        }

        SpriteRenderer[] srs = null;
        Color[] originalColors = null;

        if (holeSinkApplyBlackTint)
        {
            srs = holeSinkTintAffectsChildren
                ? bombGo.GetComponentsInChildren<SpriteRenderer>(true)
                : bombGo.GetComponents<SpriteRenderer>();

            if (srs != null && srs.Length > 0)
            {
                originalColors = new Color[srs.Length];
                for (int i = 0; i < srs.Length; i++)
                {
                    var sr = srs[i];
                    originalColors[i] = sr != null ? sr.color : Color.white;
                }
            }
        }

        Vector3 startScale = t != null ? t.localScale : Vector3.one;

        float elapsed = 0f;

        while (elapsed < holeSinkSeconds)
        {
            if (bombGo == null)
                yield break;

            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / holeSinkSeconds);

            if (t != null)
                t.localScale = Vector3.Lerp(startScale, Vector3.zero, a);

            if (holeSinkApplyBlackTint && srs != null && originalColors != null)
            {
                for (int r = 0; r < srs.Length; r++)
                {
                    var sr = srs[r];
                    if (sr == null) continue;

                    sr.color = Color.Lerp(originalColors[r], holeSinkTint, a);
                }
            }

            yield return null;
        }

        if (t != null)
            t.localScale = Vector3.zero;

        if (holeSinkApplyBlackTint && srs != null && originalColors != null)
        {
            for (int r = 0; r < srs.Length; r++)
            {
                var sr = srs[r];
                if (sr == null) continue;

                sr.color = holeSinkTint;
            }
        }

        DisableBombDrivers(bombGo);

        if (bombGo.TryGetComponent<Bomb>(out var bombScript) && bombScript != null)
            bombScript.enabled = false;

        if (bombGo != null)
            Destroy(bombGo);
    }

    private void DisableBombDrivers(GameObject bombGo)
    {
        if (bombGo == null)
            return;

        if (bombGo.TryGetComponent<BombAtGroundTileNotifier>(out var notifier) && notifier != null)
            notifier.enabled = false;

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var anim) && anim != null)
            anim.enabled = false;
    }

    public void ExplodeBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        if (_removedBombs.Contains(bomb))
            return;

        bomb.TryGetComponent<Bomb>(out var bombComp);

        if (bombComp != null && bombComp.IsPowerBomb)
            ClearActivePowerBombIfMatches(bomb);

        BombController realOwner = bombComp != null ? bombComp.Owner : null;
        if (realOwner != null && realOwner != this)
        {
            realOwner.ExplodeBomb(bomb);
            return;
        }

        if (bombComp != null && bombComp.IsControlBomb && bombComp.IsBeingPunched)
            return;

        UnregisterBomb(bomb);

        Vector2 logicalPos = bombComp != null
            ? bombComp.GetLogicalPosition()
            : (Vector2)bomb.transform.position;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 snapped = SnapToTileCenter(snapTm, logicalPos, out _, out _);

        bomb.transform.position = snapped;

        if (bomb.TryGetComponent<Rigidbody2D>(out var rb))
            rb.position = snapped;

        if (bombComp != null)
        {
            if (bombComp.HasExploded)
                return;

            bombComp.MarkAsExploded();
            bombComp.ForceSetLogicalPosition(snapped);
        }

        int effectiveRadius;

        if (bombComp != null && bombComp.ExplosionRadiusOverride > 0)
            effectiveRadius = bombComp.ExplosionRadiusOverride;
        else if (bombComp != null && bombComp.IsPowerBomb)
            effectiveRadius = Mathf.Max(1, powerBombRadius);
        else
            effectiveRadius = IsFullFireEnabled()
                ? PlayerPersistentStats.MaxExplosionRadius
                : explosionRadius;

        bool pierce = bombComp != null && bombComp.IsPierceBomb;

        TryApplyGroundExplosionModifiers(snapped, ref effectiveRadius, ref pierce);

        HideBombVisuals(bomb);

        if (bomb.TryGetComponent<AudioSource>(out var explosionAudio))
        {
            if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
                currentExplosionAudio.Stop();

            currentExplosionAudio = explosionAudio;
            PlayExplosionSfx(currentExplosionAudio, effectiveRadius, pierce);
        }

        TryHandleGroundExplosionHit(snapped);

        ResolveExplosionPrefab();
        if (explosionPrefab == null)
        {
            Destroy(bomb);
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
            return;
        }

        int bombSpotlightId = AllocateSpotlightBaseId();

        SpawnExplosionVisual(
            snapped,
            BombExplosion.ExplosionPart.Start,
            Vector2.zero,
            snapped,
            bombComp);

        ExplosionLineResult up = ExplodeAndCollect(snapped, Vector2.up, effectiveRadius, pierce, bombComp);
        ExplosionLineResult down = ExplodeAndCollect(snapped, Vector2.down, effectiveRadius, pierce, bombComp);
        ExplosionLineResult left = ExplodeAndCollect(snapped, Vector2.left, effectiveRadius, pierce, bombComp);
        ExplosionLineResult right = ExplodeAndCollect(snapped, Vector2.right, effectiveRadius, pierce, bombComp);

        RegisterBlackoutSpotlightsForExplosion(
            bombSpotlightId,
            snapped,
            up.Reach,
            down.Reach,
            left.Reach,
            right.Reach);

        float spotlightDuration = Mathf.Max(0.01f, explosionDuration);
        StartSafeCoroutine(AnimateBlackoutSpotlights(bombSpotlightId, spotlightDuration));

        float destroyDelay = 0.1f;

        AudioClip destroyDelaySfx = GetExplosionSfx(effectiveRadius, pierce);
        if (destroyDelaySfx != null)
        {
            destroyDelay = destroyDelaySfx.length;
        }
        else if (explosionAudio != null && explosionAudio.clip != null)
        {
            destroyDelay = explosionAudio.clip.length;
        }

        Destroy(bomb, destroyDelay);
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    private void PreloadExplosionSfx()
    {
        if (explosionSfxPreloaded)
            return;

        cachedDefaultExplosionSfx = LoadExplosionSfxSet(DefaultExplosionSfxResourcesPath, "Explosion", "Exposion");
        cachedPierceExplosionSfx = LoadExplosionSfxSet(PierceExplosionSfxResourcesPath, "PierceExplosion", "PierceExposion");
        explosionSfxPreloaded = true;
    }

    private static AudioClip[] LoadExplosionSfxSet(string resourcesPath, string clipNamePrefix, string fallbackClipNamePrefix)
    {
        AudioClip[] clips = new AudioClip[ExplosionSfxRadiusCount];
        for (int i = 0; i < clips.Length; i++)
        {
            int radius = i + 1;
            clips[i] = Resources.Load<AudioClip>($"{resourcesPath}/{clipNamePrefix}{radius}");
            if (clips[i] == null && !string.Equals(clipNamePrefix, fallbackClipNamePrefix, System.StringComparison.Ordinal))
                clips[i] = Resources.Load<AudioClip>($"{resourcesPath}/{fallbackClipNamePrefix}{radius}");
        }

        return clips;
    }

    private AudioClip GetExplosionSfx(int radius, bool pierce)
    {
        PreloadExplosionSfx();

        AudioClip[] sfxSet = pierce ? cachedPierceExplosionSfx : cachedDefaultExplosionSfx;
        AudioClip clip = GetExplosionSfxFromSet(sfxSet, radius);
        if (clip != null)
            return clip;

        if (!pierce)
            return GetExplosionSfxFromSet(explosionSfxByRadius, radius);

        return null;
    }

    private static AudioClip GetExplosionSfxFromSet(AudioClip[] sfxSet, int radius)
    {
        if (sfxSet == null || sfxSet.Length == 0)
            return null;

        int index = Mathf.Clamp(radius - 1, 0, sfxSet.Length - 1);
        return sfxSet[index];
    }

    private void TryHandleItemHitByExplosion(Collider2D itemHit, Vector2 direction)
    {
        if (itemHit == null)
            return;

        var item = itemHit.GetComponent<ItemPickup>() ?? itemHit.GetComponentInParent<ItemPickup>();
        if (item == null)
            return;

        if (item.TryBounceSkull(direction, 1f, itemHit, explosionDuration + 0.05f))
            return;

        item.DestroyWithExplosionAnimation();
    }

    private ExplosionLineResult ExplodeAndCollect(Vector2 origin, Vector2 direction, int length, bool pierce, Bomb sourceBomb)
    {
        ExplosionLineResult result = new()
        {
            Reach = 0,
            Explosions = new List<(Vector2 position, BombExplosion.ExplosionPart part)>(length)
        };

        if (length <= 0)
            return result;

        Vector2 position = origin;
        Tilemap snapTm = GetSnapTilemapForGround();

        for (int i = 0; i < length; i++)
        {
            Vector2 nextPosition = position + direction;
            nextPosition = SnapToTileCenter(snapTm, nextPosition);

            if (HasStartExplosionAt(nextPosition))
                break;

            position = nextPosition;

            bool isMaxRangeTile = i == length - 1;

            if (HasWaterAt(position))
                break;

            if (HasHoleAt(position))
                break;

            if (HasIndestructibleAt(position))
            {
                TryHandleIndestructibleTileHit(position, origin);
                break;
            }

            var itemHit = Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, itemLayerMask);
            if (itemHit != null)
            {
                TryHandleItemHitByExplosion(itemHit, direction);

                if (pierce)
                {
                    result.Explosions.Add((position, BombExplosion.ExplosionPart.Middle));
                    result.Reach = Mathf.Max(result.Reach, i + 1);
                    continue;
                }

                break;
            }

            if (TryGetDestructibleTileAt(position, out var cell, out var tile))
            {
                SpawnExplosionDamageHitbox(position, origin, sourceBomb);
                result.Reach = Mathf.Max(result.Reach, i + 1);

                if (!TryHandleDestructibleTileEffect(position, cell, tile))
                    ClearDestructibleForEffect(position);

                if (!pierce)
                    break;

                continue;
            }

            if (HasDestroyingDestructibleAt(position))
            {
                SpawnExplosionDamageHitbox(position, origin, sourceBomb);
                result.Reach = Mathf.Max(result.Reach, i + 1);

                if (!pierce)
                    break;

                continue;
            }

            if (TryGetAnyBombColliderAt(position, 0.6f, out var bombHit))
            {
                GameObject otherBombGo = bombHit.attachedRigidbody != null
                    ? bombHit.attachedRigidbody.gameObject
                    : bombHit.gameObject;

                if (otherBombGo != null)
                {
                    if (otherBombGo.TryGetComponent<Bomb>(out var otherBomb) && otherBomb != null && otherBomb.Owner != null)
                        otherBomb.Owner.ExplodeBombChained(otherBombGo, position);
                    else
                        ExplodeBombChained(otherBombGo, position);

                    _removedBombs.Remove(otherBombGo);
                }

                break;
            }

            BombExplosion.ExplosionPart defaultPart =
                isMaxRangeTile
                    ? BombExplosion.ExplosionPart.End
                    : BombExplosion.ExplosionPart.Middle;

            result.Explosions.Add((position, defaultPart));
            result.Reach = Mathf.Max(result.Reach, i + 1);
        }

        for (int i = 0; i < result.Explosions.Count; i++)
        {
            Vector2 p = result.Explosions[i].position;
            BombExplosion.ExplosionPart part = result.Explosions[i].part;

            TryHandleGroundExplosionHit(p);

            SpawnExplosionVisual(p, part, direction, origin, sourceBomb);
        }

        return result;
    }

    public void ExplodeBombChained(GameObject bomb)
    {
        ExplodeBombChained(bomb, null);
    }

    public void ExplodeBombChained(GameObject bomb, Vector2 chainHitWorldPosition)
    {
        ExplodeBombChained(bomb, (Vector2?)chainHitWorldPosition);
    }

    private void ExplodeBombChained(GameObject bomb, Vector2? chainHitWorldPosition)
    {
        if (bomb == null)
            return;

        if (_removedBombs.Contains(bomb))
            return;

        Bomb bombComp = null;
        bomb.TryGetComponent(out bombComp);

        if (bombComp != null && bombComp.HasExploded)
            return;

        if (!scheduledChainBombs.Add(bomb))
            return;

        if (chainHitWorldPosition.HasValue && bombComp != null && bombComp.IsBeingKicked)
        {
            Tilemap snapTm = GetSnapTilemapForGround();
            scheduledChainBombSnapPositions[bomb] = SnapToTileCenter(snapTm, chainHitWorldPosition.Value);
        }

        if (Time.frameCount != _lastChainScheduleFrame)
        {
            _lastChainScheduleFrame = Time.frameCount;
            _chainIndexThisFrame = 0;
        }

        _chainIndexThisFrame++;

        float queuedDelay = Mathf.Max(0f, chainBombDelaySeconds) * _chainIndexThisFrame;

        float delay = queuedDelay;
        if (bombComp != null)
            delay = Mathf.Max(delay, Mathf.Max(0f, bombComp.chainStepDelay));

        StartSafeCoroutine(ExplodeBombAfterDelay(bomb, delay));
    }

    private IEnumerator ExplodeBombAfterDelay(GameObject bomb, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        scheduledChainBombs.Remove(bomb);
        bool hasSnapPosition = scheduledChainBombSnapPositions.TryGetValue(bomb, out var snapPosition);
        scheduledChainBombSnapPositions.Remove(bomb);

        if (bomb == null)
            yield break;

        if (_removedBombs.Contains(bomb))
            yield break;

        if (bomb.TryGetComponent<Bomb>(out var bombComp))
        {
            if (bombComp.HasExploded)
                yield break;

            if (bombComp.IsBeingKicked && hasSnapPosition)
                bombComp.ForceStopExternalMovementAndSnap(snapPosition);
        }

        ExplodeBomb(bomb);
    }

    private void Explode(Vector2 origin, Vector2 direction, int length, bool pierce)
    {
        if (length <= 0)
            return;

        List<(Vector2 position, BombExplosion.ExplosionPart part)> explosionsToSpawn = new(length);
        Vector2 position = origin;

        Tilemap snapTm = GetSnapTilemapForGround();

        for (int i = 0; i < length; i++)
        {
            position += direction;
            position = SnapToTileCenter(snapTm, position);

            bool isMaxRangeTile = i == length - 1;

            if (HasWaterAt(position))
                break;

            if (HasHoleAt(position))
                break;

            if (HasIndestructibleAt(position))
            {
                TryHandleIndestructibleTileHit(position, origin);
                break;
            }

            var itemHit = Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, itemLayerMask);
            if (itemHit != null)
            {
                TryHandleItemHitByExplosion(itemHit, direction);

                if (pierce)
                {
                    explosionsToSpawn.Add((position, BombExplosion.ExplosionPart.Middle));
                    continue;
                }

                break;
            }

            if (TryGetDestructibleTileAt(position, out var cell, out var tile))
            {
                SpawnExplosionDamageHitbox(position, origin);

                if (!TryHandleDestructibleTileEffect(position, cell, tile))
                    ClearDestructibleForEffect(position);

                if (!pierce)
                    break;

                continue;
            }

            if (HasDestroyingDestructibleAt(position))
            {
                SpawnExplosionDamageHitbox(position, origin);

                if (!pierce)
                    break;

                continue;
            }

            if (TryGetAnyBombColliderAt(position, 0.6f, out var bombHit))
            {
                GameObject otherBombGo = bombHit.attachedRigidbody != null
                    ? bombHit.attachedRigidbody.gameObject
                    : bombHit.gameObject;

                if (otherBombGo != null)
                {
                    BombExplosion.ExplosionPart part =
                        isMaxRangeTile
                            ? BombExplosion.ExplosionPart.End
                            : BombExplosion.ExplosionPart.Middle;

                    explosionsToSpawn.Add((position, part));

                    if (otherBombGo.TryGetComponent<Bomb>(out var otherBomb) && otherBomb != null && otherBomb.Owner != null)
                        otherBomb.Owner.ExplodeBombChained(otherBombGo, position);
                    else
                        ExplodeBombChained(otherBombGo, position);

                    _removedBombs.Remove(otherBombGo);
                }

                continue;
            }

            BombExplosion.ExplosionPart defaultPart =
                isMaxRangeTile
                    ? BombExplosion.ExplosionPart.End
                    : BombExplosion.ExplosionPart.Middle;

            explosionsToSpawn.Add((position, defaultPart));
        }

        for (int i = 0; i < explosionsToSpawn.Count; i++)
        {
            Vector2 p = explosionsToSpawn[i].position;
            BombExplosion.ExplosionPart part = explosionsToSpawn[i].part;

            TryHandleGroundExplosionHit(p);

            BombExplosion explosion = BombExplosion.Spawn(explosionPrefab, p, Quaternion.identity);
            explosion.Play(part, direction, 0f, explosionDuration, origin);
        }
    }

    private bool TryHandleDestructibleTileEffect(Vector2 worldPos, Vector3Int cell, TileBase tile)
    {
        ResolveDestructibleTileResolver();

        if (destructibleTileResolver == null)
            return false;

        if (!destructibleTileResolver.TryGetHandler(tile, out var handler))
            return false;

        return handler.HandleHit(this, worldPos, cell);
    }

    public void ClearDestructibleForEffect(
        Vector2 position,
        bool spawnDestructiblePrefab = true,
        bool spawnHiddenObject = true)
    {
        if (destructibleTiles == null)
            return;

        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile == null)
            return;

        if (_gm == null)
            _gm = FindAnyObjectByType<GameManager>();

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;
        Vector3 spawnWorldPosition = destructibleTiles.GetCellCenterWorld(cell);

        if (spawnDestructiblePrefab && destructiblePrefab != null)
        {
            if (parent != null)
                Instantiate(destructiblePrefab, spawnWorldPosition, Quaternion.identity, parent);
            else
                Instantiate(destructiblePrefab, spawnWorldPosition, Quaternion.identity);
        }

        if (spawnHiddenObject && _gm != null)
        {
            GameObject spawnPrefab = _gm.GetSpawnForDestroyedBlock(cell);
            if (spawnPrefab != null)
            {
                float delay = GetDestructibleDestroyTime();
                StartSafeCoroutine(SpawnHiddenObjectAfterDelay(spawnPrefab, cell, parent, delay));
            }
        }

        destructibleTiles.SetTile(cell, null);

        if (_gm != null)
            _gm.OnDestructibleDestroyed(cell);
    }

    private IEnumerator SpawnHiddenObjectAfterDelay(GameObject prefab, Vector3Int cell, Transform parent, float delay)
    {
        if (prefab == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (_gm == null)
            _gm = FindAnyObjectByType<GameManager>();

        if (destructibleTiles == null || _gm == null)
        {
            _gm?.ReleasePendingHiddenItemCell(cell);
            yield break;
        }

        Vector3 spawnWorldPosition = destructibleTiles.GetCellCenterWorld(cell);

        _gm.ReleasePendingHiddenItemCell(cell);

        if (!_gm.TryReserveItemSpawnCell(cell))
            yield break;

        GameObject spawned = parent != null
            ? Instantiate(prefab, spawnWorldPosition, Quaternion.identity, parent)
            : Instantiate(prefab, spawnWorldPosition, Quaternion.identity);

        _gm.PrepareSpawnedHiddenObject(spawned, prefab, spawnWorldPosition);
    }

    private float GetDestructibleDestroyTime()
    {
        if (destructiblePrefab != null)
            return Mathf.Max(0f, destructiblePrefab.destructionTime);

        return 0.5f;
    }

    private void HideBombVisuals(GameObject bomb)
    {
        if (bomb == null)
            return;

        if (bomb.TryGetComponent<SpriteRenderer>(out var sprite))
            sprite.enabled = false;

        if (bomb.TryGetComponent<Collider2D>(out var collider))
            collider.enabled = false;

        if (bomb.TryGetComponent<AnimatedSpriteRenderer>(out var anim))
            anim.enabled = false;

        if (bomb.TryGetComponent<Bomb>(out var bombScript))
            bombScript.enabled = false;

        var mbs = bomb.GetComponents<MonoBehaviour>();
        for (int i = 0; i < mbs.Length; i++)
        {
            var mb = mbs[i];
            if (mb == null) continue;
            mb.enabled = false;
        }
    }

    private void PlayExplosionSfx(AudioSource source, int radius, bool pierce = false)
    {
        if (source == null)
            return;

        AudioClip clip = GetExplosionSfx(radius, pierce);

        if (clip != null)
            source.PlayOneShot(clip, explosionSfxVolume);
    }

    public void PlayExplosionSfxExclusive(AudioSource source, int radius, bool pierce = false)
    {
        if (source == null)
            return;

        AudioClip clip = GetExplosionSfx(radius, pierce);
        if (clip == null)
            return;

        if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
            currentExplosionAudio.Stop();

        currentExplosionAudio = source;
        currentExplosionAudio.PlayOneShot(clip, explosionSfxVolume);
    }

    private void PlayPlaceBombSfx()
    {
        if (placeBombSfx == null)
            return;

        AudioSource src = playerAudioSource != null ? playerAudioSource : _localAudio;
        if (src == null)
            return;

        src.PlayOneShot(placeBombSfx);
    }

    private void PlayWaterDestroySfx()
    {
        if (waterDestroySfx == null)
            return;

        AudioSource src = playerAudioSource != null ? playerAudioSource : _localAudio;
        if (src == null)
            return;

        src.PlayOneShot(waterDestroySfx, waterDestroyVolume);
    }

    private void PlayHoleDestroySfx()
    {
        if (holeDestroySfx == null)
            return;

        AudioSource src = playerAudioSource != null ? playerAudioSource : _localAudio;
        if (src == null)
            return;

        src.PlayOneShot(holeDestroySfx, holeDestroyVolume);
    }

    private void TryApplyGroundExplosionModifiers(Vector2 worldPos, ref int radius, ref bool pierce)
    {
        ResolveGroundTileResolver();

        if (groundTileResolver == null)
            return;

        if (!TryGetGroundTileAt(worldPos, out _, out var groundTile))
            return;

        if (!groundTileResolver.TryGetHandler(groundTile, out var handler))
            return;

        handler.TryModifyExplosion(this, worldPos, groundTile, ref radius, ref pierce);
    }

    private bool TryGetGroundTileAt(Vector2 worldPos, out Vector3Int cell, out TileBase tile)
    {
        cell = default;
        tile = null;

        if (groundTiles == null)
            return false;

        cell = groundTiles.WorldToCell(worldPos);
        tile = groundTiles.GetTile(cell);
        return tile != null;
    }

    private bool TryGetIndestructibleTileAt(Vector2 worldPos, out Vector3Int cell, out TileBase tile)
    {
        cell = default;
        tile = null;

        if (stageBoundsTiles == null)
            return false;

        cell = stageBoundsTiles.WorldToCell(worldPos);
        tile = stageBoundsTiles.GetTile(cell);
        return tile != null;
    }

    private void TryHandleIndestructibleTileHit(Vector2 indestructibleWorldPos, Vector2 hitFromWorldPos)
    {
        ResolveIndestructibleTileResolver();

        if (indestructibleTileResolver == null)
            return;

        if (!TryGetIndestructibleTileAt(indestructibleWorldPos, out var cell, out var tile))
            return;

        if (!indestructibleTileResolver.TryGetHandler(tile, out var handler))
            return;

        handler.HandleExplosionHit(this, hitFromWorldPos, cell, tile);
    }

    private void TryHandleGroundBombAt(Vector2 worldPos, GameObject bomb)
    {
        ResolveGroundTileResolver();

        if (groundTileResolver == null || bomb == null)
            return;

        if (!TryGetGroundTileAt(worldPos, out var cell, out var groundTile))
            return;

        if (!groundTileResolver.TryGetHandler(groundTile, out var handler) || handler == null)
            return;

        if (handler is IGroundTileBombAtHandler bombAtHandler)
            bombAtHandler.OnBombAt(this, worldPos, cell, groundTile, bomb);
    }

    private void TryHandleGroundExplosionHit(Vector2 worldPos)
    {
        ResolveGroundTileResolver();

        if (groundTileResolver == null)
            return;

        if (!TryGetGroundTileAt(worldPos, out var cell, out var groundTile))
            return;

        if (!groundTileResolver.TryGetHandler(groundTile, out var handler) || handler == null)
            return;

        if (handler is IGroundTileExplosionHitHandler hitHandler)
            hitHandler.OnExplosionHit(this, worldPos, cell, groundTile);
    }

    private void RegisterBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        plantedBombs.Add(bomb);
    }

    public void UnregisterBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        plantedBombs.Remove(bomb);
    }

    private void CleanupNullBombs()
    {
        for (int i = plantedBombs.Count - 1; i >= 0; i--)
        {
            if (plantedBombs[i] == null)
                plantedBombs.RemoveAt(i);
        }
    }

    public bool TryExplodeOldestControlledBomb()
    {
        CleanupNullBombs();

        for (int i = 0; i < plantedBombs.Count; i++)
        {
            var b = plantedBombs[i];
            if (b == null)
            {
                plantedBombs.RemoveAt(i);
                i--;
                continue;
            }

            if (!b.TryGetComponent<Bomb>(out var bombComp) || bombComp == null || !bombComp.IsControlBomb)
            {
                plantedBombs.RemoveAt(i);
                i--;
                continue;
            }

            if (bombComp.IsBeingPunched)
                continue;

            plantedBombs.RemoveAt(i);
            ExplodeBomb(b);
            return true;
        }

        return false;
    }

    public void ClearPlantedBombsOnStageEnd(bool explodeInstead = false)
    {
        CleanupNullBombs();

        for (int i = 0; i < plantedBombs.Count; i++)
        {
            var b = plantedBombs[i];
            if (b == null)
                continue;

            if (explodeInstead)
                ExplodeBomb(b);
            else
                Destroy(b);
        }

        plantedBombs.Clear();
    }

    public static void ExplodeAllControlBombsInStage()
    {
        if (Bomb.ActiveBombs.Count == 0)
            return;

        _activeBombSnapshot.Clear();
        foreach (var bomb in Bomb.ActiveBombs)
        {
            if (bomb != null)
                _activeBombSnapshot.Add(bomb);
        }

        for (int i = 0; i < _activeBombSnapshot.Count; i++)
        {
            var b = _activeBombSnapshot[i];
            if (b == null)
                continue;

            if (!b.IsControlBomb)
                continue;

            if (b.HasExploded)
                continue;

            var owner = b.Owner;
            if (owner != null)
                owner.ExplodeBomb(b.gameObject);
            else
                Object.Destroy(b.gameObject);
        }

        _activeBombSnapshot.Clear();
    }

    public void NotifyBombAt(Vector2 worldPos, GameObject bombGo)
    {
        if (bombGo == null)
            return;

        if (_removedBombs.Contains(bombGo))
            return;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 snapped = SnapToTileCenter(snapTm, worldPos, out _, out _);

        TryHandleGroundBombAt(snapped, bombGo);

        if (TryDestroyBombIfOnWater(bombGo, snapped, refund: true))
            return;

        TryDestroyBombIfOnHole(bombGo, snapped, refund: true);
    }

    public void SpawnExplosionCrossForEffect(Vector2 origin, int radius, bool pierce)
    {
        ResolveExplosionPrefab();
        if (explosionPrefab == null)
            return;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 p = SnapToTileCenter(snapTm, origin, out _, out _);

        BombExplosion centerExp = BombExplosion.Spawn(explosionPrefab, p, Quaternion.identity);
        centerExp.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, radius, pierce);
        Explode(p, Vector2.down, radius, pierce);
        Explode(p, Vector2.left, radius, pierce);
        Explode(p, Vector2.right, radius, pierce);
    }

    public bool SpawnRevengeLaunchMuzzleVisual(
        Vector2 launchWorldPos,
        Vector2 direction,
        float duration,
        int orderInLayer,
        Transform parent)
    {
        ResolveExplosionPrefab();
        if (explosionPrefab == null)
            return false;

        direction = direction == Vector2.zero ? Vector2.right : direction.normalized;

        Vector2 position = launchWorldPos;

        BombExplosion flash = BombExplosion.Spawn(explosionPrefab, position, Quaternion.identity);
        if (flash == null)
            return false;

        if (parent != null)
            flash.transform.SetParent(parent, true);

        SetSortingOrder(flash, orderInLayer);
        flash.SetCollisionEnabled(false);

        flash.Play(
            BombExplosion.ExplosionPart.End,
            direction,
            0f,
            Mathf.Max(0.01f, duration),
            position);

        return true;
    }

    private static void SetSortingOrder(BombExplosion explosion, int orderInLayer)
    {
        if (explosion == null)
            return;

        SpriteRenderer[] renderers = explosion.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].sortingOrder = orderInLayer;
        }
    }

    public void SpawnExplosionCrossForEffectWithTileEffects(
        Vector2 origin,
        int radius,
        bool pierce,
        AudioSource sfxSource = null)
    {
        ResolveExplosionPrefab();
        if (explosionPrefab == null)
            return;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 p = SnapToTileCenter(snapTm, origin, out _, out _);

        int effectiveRadius = Mathf.Max(0, radius);
        bool effectivePierce = pierce;

        TryApplyGroundExplosionModifiers(p, ref effectiveRadius, ref effectivePierce);

        if (sfxSource != null)
            PlayExplosionSfxExclusive(sfxSource, effectiveRadius);

        BombExplosion centerExp = BombExplosion.Spawn(explosionPrefab, p, Quaternion.identity);
        centerExp.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, effectiveRadius, effectivePierce);
        Explode(p, Vector2.down, effectiveRadius, effectivePierce);
        Explode(p, Vector2.left, effectiveRadius, effectivePierce);
        Explode(p, Vector2.right, effectiveRadius, effectivePierce);
    }

    public bool LaunchRevengeBomb(Vector2 launchWorldPos, Vector2 direction, int distanceTiles, int forcedRadius)
    {
        ResolveTilemaps();

        GameObject prefabToUse = revengeBombPrefab != null
            ? revengeBombPrefab
            : bombPrefab;

        if (prefabToUse == null)
            return false;

        direction = direction == Vector2.zero ? Vector2.right : direction.normalized;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 position = SnapToTileCenter(snapTm, launchWorldPos, out _, out _);

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        if (bomb == null)
            return false;

        lastPlacedBomb = bomb;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPowerBomb = false;
        bombComponent.IsControlBomb = false;
        bombComponent.IsPierceBomb = false;
        bombComponent.IsRubberBomb = false;
        bombComponent.IsRevengeBomb = true;
        bombComponent.ExplosionRadiusOverride = Mathf.Max(1, forcedRadius);

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.SetFuseSeconds(bombFuseTime);
        bombComponent.Initialize(this);
        bombComponent.BeginFuse();

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider) && bombCollider != null)
            bombCollider.isTrigger = true;

        float launchTileSize = 1f;
        LayerMask launchObstacleMask = LayerMask.GetMask("Stage", "Bomb");

        if (cachedMovement == null)
            cachedMovement = GetComponent<MovementController>();

        if (cachedMovement != null)
        {
            launchTileSize = Mathf.Max(0.01f, cachedMovement.tileSize);
            launchObstacleMask = cachedMovement.obstacleMask;
        }

        int launchDistanceTiles = Mathf.Max(1, distanceTiles);
        float distanceT = Mathf.InverseLerp(
            RevengeLaunchMinDistanceTiles,
            RevengeLaunchMaxDistanceTiles,
            launchDistanceTiles);
        float launchArcHeight = Mathf.Lerp(
            RevengeLaunchMinArcHeightTiles,
            RevengeLaunchMaxArcHeightTiles,
            distanceT) * launchTileSize;
        float launchBounceArcHeight = RevengeLaunchBounceArcHeightTiles * launchTileSize;

        if (!bombComponent.StartPunch(
            direction,
            launchTileSize,
            launchDistanceTiles,
            launchObstacleMask,
            destructibleTiles,
            arcHeightOverride: launchArcHeight,
            bounceArcHeightOverride: launchBounceArcHeight))
        {
            Destroy(bomb);

            if (lastPlacedBomb == bomb)
                lastPlacedBomb = null;

            return false;
        }

        return true;
    }

    public bool TryPlaceBombAtIgnoringInputLock(Vector2 worldPos, out GameObject placedBomb, bool consumeBomb = true, bool playSfx = true)
    {
        placedBomb = null;

        if (ClownMaskBoss.BossIntroRunning)
            return false;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return false;

        if (GamePauseController.IsPaused)
            return false;

        var movement = GetMovement();
        if (movement != null && (movement.isDead || movement.IsEndingStage))
            return false;

        if (IsRidingTransitionActive())
            return false;

        if (IsDashActive())
            return false;

        if (consumeBomb && bombsRemaining <= 0)
            return false;

        ResolveTilemaps();

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 position = SnapToTileCenter(snapTm, worldPos, out _, out _);

        if (!CanPlaceBombAt(position))
            return false;

        bool explosionAlreadyHere = HasActiveExplosionAt(position);

        if (TileHasBomb(position))
        {
            if (!explosionAlreadyHere)
                return false;

            ExplodeAnyBombAt(position);
        }

        if (HasDestructibleAt(position))
            return false;

        bool canUsePowerNow = CanUsePowerBombNow();

        bool controlEnabled = !canUsePowerNow && IsControlEnabled();
        bool pierceEnabled = !canUsePowerNow && !controlEnabled && IsPierceEnabled();
        bool rubberEnabled = !canUsePowerNow && !controlEnabled && !pierceEnabled && IsRubberEnabled();
        bool shouldMakeFirstPlacedBombMagnetic = IsMagnetBombEnabled() && !HasAnyAliveBombOwnedByMe();

        GameObject prefabToUse =
            (shouldMakeFirstPlacedBombMagnetic && magnetBombPrefab != null) ? magnetBombPrefab :
            (canUsePowerNow && powerBombPrefab != null) ? powerBombPrefab :
            (controlEnabled && controlBombPrefab != null) ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            (rubberEnabled && rubberBombPrefab != null) ? rubberBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return false;

        if (playSfx)
            PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        placedBomb = bomb;
        lastPlacedBomb = bomb;

        if (consumeBomb)
            bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPowerBomb = canUsePowerNow;
        bombComponent.IsControlBomb = controlEnabled;
        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsRubberBomb = rubberEnabled;

        if (canUsePowerNow)
            TrackNewActivePowerBomb(bomb);

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.SetFuseSeconds(bombFuseTime);
        bombComponent.Initialize(this);

        if (shouldMakeFirstPlacedBombMagnetic)
        {
            var magnetAbility = GetCachedComponent(ref cachedMagnetBombAbility);
            if (magnetAbility != null)
                magnetAbility.ApplyToBomb(bomb);
            else
            {
                if (!bomb.TryGetComponent<MagnetBomb>(out var magnetBomb) || magnetBomb == null)
                    magnetBomb = bomb.AddComponent<MagnetBomb>();

                magnetBomb.SetTargetLayer("Enemy");
            }
        }

        if (!bomb.TryGetComponent<BombAtGroundTileNotifier>(out var notifier))
            notifier = bomb.AddComponent<BombAtGroundTileNotifier>();

        notifier.Initialize(this);

        TryHandleGroundBombAt(position, bomb);

        if (TryDestroyBombIfOnWater(bomb, position, refund: consumeBomb))
        {
            placedBomb = null;
            return false;
        }

        if (TryDestroyBombIfOnHole(bomb, position, refund: consumeBomb))
        {
            placedBomb = null;
            return false;
        }

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            ExplodeBomb(bomb);
            return true;
        }

        if (!controlEnabled)
            bombComponent.BeginFuse();

        Vector2 plantDir = GetBombPlantDirection(movement);
        movement?.NotifyBombPlanted(bombComponent, plantDir);

        var kickAbility = GetCachedComponent(ref cachedBombKickAbility);
        if (kickAbility != null)
            kickAbility.NotifyBombPlanted(bombComponent, plantDir);

        var yellowKickAbility = GetCachedComponent(ref cachedYellowLouieKickAbility);
        if (yellowKickAbility != null)
            yellowKickAbility.NotifyBombPlanted(bombComponent, plantDir);

        return true;
    }

    public bool TryPlaceBombAtIgnoringInputLock(Vector2 worldPos)
    {
        return TryPlaceBombAtIgnoringInputLock(worldPos, out _, consumeBomb: true);
    }

    public void NotifyBombAtHoleWithVisualOffset(Vector2 holeCheckWorldPos, GameObject bombGo, float visualYOffsetTiles, bool refund = true)
    {
        if (bombGo == null)
            return;

        if (_removedBombs.Contains(bombGo))
            return;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 snappedCheck = SnapToTileCenter(snapTm, holeCheckWorldPos, out _, out _);

        if (!HasHoleAt(snappedCheck))
        {
            TryHandleGroundBombAt(snappedCheck, bombGo);
            if (TryDestroyBombIfOnWater(bombGo, snappedCheck, refund))
                return;

            TryDestroyBombIfOnHole(bombGo, snappedCheck, refund);
            return;
        }

        ResolveTilemaps();
        if (holeTiles == null)
            return;

        Vector3Int holeCell = holeTiles.WorldToCell(snappedCheck);
        TileBase holeTile = holeTiles.GetTile(holeCell);
        if (holeTile == null)
            return;

        _removedBombs.Add(bombGo);

        Vector3 hc = holeTiles.GetCellCenterWorld(holeCell);
        float tile = 1f;

        Vector2 sinkPos = new(
            Mathf.Round(hc.x),
            Mathf.Round(hc.y) + (Mathf.RoundToInt(visualYOffsetTiles) * tile)
        );

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
        {
            bomb.StopAllCoroutines();
            bomb.LockWorldPosition(sinkPos);
            bomb.ForceStopExternalMovementAndSnap(sinkPos);
        }

        PlayHoleDestroySfx();
        UnregisterBomb(bombGo);

        if (refund)
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);

        StartSafeCoroutine(HoleSinkAndDestroyRoutine(bombGo, sinkPos));
    }

    private bool IsPowerBombEnabled()
    {
        var abilitySystem = GetAbilitySystem();
        return abilitySystem != null && abilitySystem.IsEnabled(PowerBombAbility.AbilityId);
    }

    private bool HasActivePowerBombAlive()
    {
        if (activePowerBomb == null)
            return false;

        if (!activePowerBomb.TryGetComponent<Bomb>(out var b) || b == null)
        {
            activePowerBomb = null;
            return false;
        }

        if (b.HasExploded)
        {
            activePowerBomb = null;
            return false;
        }

        return true;
    }

    private void TrackNewActivePowerBomb(GameObject bombGo)
    {
        activePowerBomb = bombGo;
    }

    private void ClearActivePowerBombIfMatches(GameObject bombGo)
    {
        if (bombGo == null)
            return;

        if (bombGo == activePowerBomb)
        {
            activePowerBomb = null;
        }
    }

    private bool HasAnyAliveBombOwnedByMe()
    {
        foreach (var b in Bomb.ActiveBombs)
        {
            if (b == null) continue;
            if (b.HasExploded) continue;
            if (b.Owner != this) continue;

            return true;
        }

        return false;
    }

    private bool CanUsePowerBombNow()
    {
        if (!IsPowerBombEnabled())
            return false;

        if (HasAnyAliveBombOwnedByMe())
            return false;

        if (HasActivePowerBombAlive())
            return false;

        return true;
    }

    public void DestroyBombExternally(GameObject bombGo, bool refund = true)
    {
        if (bombGo == null)
            return;

        if (_removedBombs.Contains(bombGo))
            return;

        _removedBombs.Add(bombGo);

        UnregisterBomb(bombGo);

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
        {
            if (bomb.IsPowerBomb)
                ClearActivePowerBombIfMatches(bombGo);

            if (!bomb.HasExploded)
                bomb.MarkAsExploded();

            bomb.ForceStopExternalMovementAndSnap(bomb.GetLogicalPosition());
        }

        if (refund)
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);

        Destroy(bombGo);
    }

    public bool TryExplodeAllControlledBombs()
    {
        CleanupNullBombs();

        bool any = false;

        for (int i = plantedBombs.Count - 1; i >= 0; i--)
        {
            var b = plantedBombs[i];
            if (b == null)
            {
                plantedBombs.RemoveAt(i);
                continue;
            }

            if (!b.TryGetComponent<Bomb>(out var bombComp)
                || bombComp == null
                || !bombComp.IsControlBomb)
            {
                plantedBombs.RemoveAt(i);
                continue;
            }

            if (bombComp.IsBeingPunched)
                continue;

            plantedBombs.RemoveAt(i);
            ExplodeBomb(b);
            any = true;
        }

        return any;
    }

    public void NotifyBombDestroyedExternally(GameObject bomb)
    {
        if (bomb == null)
            return;

        if (_removedBombs.Contains(bomb))
            return;

        _removedBombs.Add(bomb);

        UnregisterBomb(bomb);

        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    private int GetSpotlightSubId(int baseId, int offset)
    {
        return (baseId * 10) + offset;
    }

    private int AllocateSpotlightBaseId()
    {
        if (_nextSpotlightBaseId > int.MaxValue / 10)
            _nextSpotlightBaseId = 1;

        return _nextSpotlightBaseId++;
    }

    private void RegisterBlackoutSpotlightsForExplosion(
        int baseSpotlightId,
        Vector2 center,
        int upReach,
        int downReach,
        int leftReach,
        int rightReach)
    {
        if (StageBlackout.Instance == null)
            return;

        int centerId = GetSpotlightSubId(baseSpotlightId, 0);
        StageBlackout.Instance.RegisterExplosionSpotlight(centerId, center, new Vector2(0.5f, 0.5f));
        StageBlackout.Instance.UpdateExplosionSpotlight(centerId, 0f);
        _activeSpotlightIds.Add(centerId);

        if (upReach > 0)
        {
            int id = GetSpotlightSubId(baseSpotlightId, 1);
            Vector2 boxCenter = center + (Vector2.up * (upReach * 0.5f));
            Vector2 halfSize = new Vector2(0.5f, upReach * 0.5f);
            StageBlackout.Instance.RegisterExplosionSpotlight(id, boxCenter, halfSize);
            StageBlackout.Instance.UpdateExplosionSpotlight(id, 0f);
            _activeSpotlightIds.Add(id);
        }

        if (downReach > 0)
        {
            int id = GetSpotlightSubId(baseSpotlightId, 2);
            Vector2 boxCenter = center + (Vector2.down * (downReach * 0.5f));
            Vector2 halfSize = new Vector2(0.5f, downReach * 0.5f);
            StageBlackout.Instance.RegisterExplosionSpotlight(id, boxCenter, halfSize);
            StageBlackout.Instance.UpdateExplosionSpotlight(id, 0f);
            _activeSpotlightIds.Add(id);
        }

        if (leftReach > 0)
        {
            int id = GetSpotlightSubId(baseSpotlightId, 3);
            Vector2 boxCenter = center + (Vector2.left * (leftReach * 0.5f));
            Vector2 halfSize = new Vector2(leftReach * 0.5f, 0.5f);
            StageBlackout.Instance.RegisterExplosionSpotlight(id, boxCenter, halfSize);
            StageBlackout.Instance.UpdateExplosionSpotlight(id, 0f);
            _activeSpotlightIds.Add(id);
        }

        if (rightReach > 0)
        {
            int id = GetSpotlightSubId(baseSpotlightId, 4);
            Vector2 boxCenter = center + (Vector2.right * (rightReach * 0.5f));
            Vector2 halfSize = new Vector2(rightReach * 0.5f, 0.5f);
            StageBlackout.Instance.RegisterExplosionSpotlight(id, boxCenter, halfSize);
            StageBlackout.Instance.UpdateExplosionSpotlight(id, 0f);
            _activeSpotlightIds.Add(id);
        }
    }

    private IEnumerator AnimateBlackoutSpotlights(int baseSpotlightId, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        int[] ids =
        {
        GetSpotlightSubId(baseSpotlightId, 0),
        GetSpotlightSubId(baseSpotlightId, 1),
        GetSpotlightSubId(baseSpotlightId, 2),
        GetSpotlightSubId(baseSpotlightId, 3),
        GetSpotlightSubId(baseSpotlightId, 4)
    };

        while (elapsed < duration)
        {
            if (StageBlackout.Instance == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float intensity;
            if (t <= 0.5f)
            {
                float riseT = t / 0.5f;
                intensity = Mathf.SmoothStep(0f, 1f, riseT);
            }
            else
            {
                float fallT = (t - 0.5f) / 0.5f;
                intensity = Mathf.SmoothStep(1f, 0f, fallT);
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (_activeSpotlightIds.Contains(ids[i]))
                    StageBlackout.Instance.UpdateExplosionSpotlight(ids[i], intensity);
            }

            yield return null;
        }

        if (StageBlackout.Instance != null)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (_activeSpotlightIds.Remove(ids[i]))
                {
                    StageBlackout.Instance.UpdateExplosionSpotlight(ids[i], 0f);
                    StageBlackout.Instance.UnregisterExplosionSpotlight(ids[i]);
                }
            }
        }
    }

    private void OnDisable()
    {
        ClearAllBlackoutSpotlights();
    }

    private void OnDestroy()
    {
        ClearAllBlackoutSpotlights();
    }

    private void ClearAllBlackoutSpotlights()
    {
        if (StageBlackout.Instance != null)
        {
            foreach (int spotlightId in _activeSpotlightIds)
                StageBlackout.Instance.UnregisterExplosionSpotlight(spotlightId);
        }

        _activeSpotlightIds.Clear();
    }

    private bool HasStartExplosionAt(Vector2 worldPos)
    {
        EnsureQueryCaches();

        if (explosionMask == 0)
            return false;

        int hitCount = Physics2D.OverlapBox(
            worldPos,
            Vector2.one * 0.6f,
            0f,
            explosionContactFilter,
            _explosionOverlapBuffer);

        if (hitCount <= 0)
            return false;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _explosionOverlapBuffer[i];
            _explosionOverlapBuffer[i] = null;
            if (hit == null)
                continue;

            BombExplosion explosion =
                hit.GetComponent<BombExplosion>() ??
                hit.GetComponentInParent<BombExplosion>() ??
                hit.GetComponentInChildren<BombExplosion>();

            if (explosion != null && explosion.CurrentPart == BombExplosion.ExplosionPart.Start)
                return true;
        }

        return false;
    }

    private Coroutine StartSafeCoroutine(IEnumerator routine)
    {
        if (routine == null)
            return null;

        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            return StartCoroutine(routine);

        return BombCoroutineRunner.Run(routine);
    }
}
