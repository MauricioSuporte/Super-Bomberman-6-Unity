using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterHealth))]
public partial class BombController : MonoBehaviour
{
    [Header("Player Id (only used if tagged Player)")]
    [SerializeField, Range(1, 4)] private int playerId = 1;
    public int PlayerId => playerId;

    [Header("Input")]
    public bool useAIInput = false;
    private bool bombRequested;

    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public GameObject pierceBombPrefab;
    public GameObject controlBombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmout = 1;

    [Header("Chain Explosion")]
    [SerializeField] private float chainBombDelaySeconds = 0.1f;
    private readonly HashSet<int> scheduledChainBombs = new();

    private int bombsRemaining = 0;
    public int BombsRemaining => bombsRemaining;

    [Header("Control Bomb")]
    private readonly List<GameObject> plantedBombs = new();

    [Header("Explosion Settings")]
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

    private static AudioSource currentExplosionAudio;

    private GameManager _gm;
    private AudioSource _localAudio;

    private static readonly Collider2D[] _bombOverlapBuffer = new Collider2D[16];

    private GameObject lastPlacedBomb;
    public GameObject GetLastPlacedBomb() => lastPlacedBomb;

    [Header("Ground Tile Effects")]
    [SerializeField] private GroundTileResolver groundTileResolver;

    [Header("Indestructible Tile Effects")]
    [SerializeField] private IndestructibleTileResolver indestructibleTileResolver;

    private readonly HashSet<int> _removedBombIds = new(128);

    public void SetPlayerId(int id)
    {
        playerId = Mathf.Clamp(id, 1, 4);
    }

    private void Awake()
    {
        _localAudio = GetComponent<AudioSource>();
        if (playerAudioSource == null)
            playerAudioSource = _localAudio;

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

    private void OnEnable()
    {
        bombAmout = Mathf.Min(bombAmout, PlayerPersistentStats.MaxBombAmount);
        bombsRemaining = bombAmout;
        lastPlacedBomb = null;
        _removedBombIds.Clear();
        scheduledChainBombs.Clear();
        CleanupNullBombs();
    }

    private void Update()
    {
        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
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

    public bool IsControlAbilityActive() => IsControlEnabled();

    private void ResolveTilemaps()
    {
        if (_gm == null)
            _gm = FindFirstObjectByType<GameManager>();

        if (_gm != null)
        {
            if (groundTiles == null) groundTiles = _gm.groundTilemap;
            if (destructibleTiles == null) destructibleTiles = _gm.destructibleTilemap;

            if (stageBoundsTiles == null)
                stageBoundsTiles = _gm.indestructibleTilemap != null ? _gm.indestructibleTilemap : _gm.groundTilemap;

            destructiblePrefab = _gm.destructiblePrefab;
        }

        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

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

        destructibleTileResolver = FindFirstObjectByType<DestructibleTileResolver>();
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

        groundTileResolver = FindFirstObjectByType<GroundTileResolver>();
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

        indestructibleTileResolver = FindFirstObjectByType<IndestructibleTileResolver>();
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

    public void AddBomb()
    {
        if (bombAmout >= PlayerPersistentStats.MaxBombAmount)
            return;

        bombAmout++;
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    private bool IsPierceEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(PierceBombAbility.AbilityId);

        return false;
    }

    private bool IsControlEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(ControlBombAbility.AbilityId);

        return false;
    }

    private bool IsFullFireEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(FullFireAbility.AbilityId);

        return false;
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
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        var filter = new ContactFilter2D { useLayerMask = true };
        filter.SetLayerMask(bombMask);
        filter.useTriggers = true;

        int count = Physics2D.OverlapBox(position, Vector2.one * size, 0f, filter, _bombOverlapBuffer);

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
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer < 0)
            return false;

        int mask = 1 << explosionLayer;

        var hit = Physics2D.OverlapBox(
            position,
            Vector2.one * 0.6f,
            0f,
            mask
        );

        return hit != null;
    }

    private void ExplodeAnyBombAt(Vector2 position)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        var filter = new ContactFilter2D { useLayerMask = true };
        filter.SetLayerMask(bombMask);
        filter.useTriggers = true;

        int count = Physics2D.OverlapBox(position, Vector2.one * 0.6f, 0f, filter, _bombOverlapBuffer);
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

    private void PlaceBomb()
    {
        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
            return;

        if (GamePauseController.IsPaused)
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

        bool controlEnabled = IsControlEnabled();
        bool pierceEnabled = !controlEnabled && IsPierceEnabled();

        GameObject prefabToUse =
            controlEnabled && controlBombPrefab != null ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return;

        PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        lastPlacedBomb = bomb;
        bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsControlBomb = controlEnabled;

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.SetFuseSeconds(bombFuseTime);
        bombComponent.Initialize(this);

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
    }

    private bool TryDestroyBombIfOnWater(GameObject bombGo, Vector2 worldPos, bool refund)
    {
        if (bombGo == null)
            return false;

        int id = bombGo.GetInstanceID();
        if (_removedBombIds.Contains(id))
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

        _removedBombIds.Add(id);

        Vector3 wc = waterTiles.GetCellCenterWorld(waterCell);
        Vector2 sinkPos = new(Mathf.Round(wc.x), Mathf.Round(wc.y));

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
        {
            bomb.LockWorldPosition(sinkPos);
            bomb.ForceStopExternalMovementAndSnap(sinkPos);
            bomb.MarkAsExploded();
        }

        PlayWaterDestroySfx();
        UnregisterBomb(bombGo);

        if (refund)
            bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);

        StartCoroutine(WaterSinkAndDestroyRoutine(bombGo, sinkPos));
        return true;
    }

    private bool TryDestroyBombIfOnHole(GameObject bombGo, Vector2 worldPos, bool refund)
    {
        if (bombGo == null)
            return false;

        int id = bombGo.GetInstanceID();
        if (_removedBombIds.Contains(id))
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

        _removedBombIds.Add(id);

        Vector3 hc = holeTiles.GetCellCenterWorld(holeCell);
        Vector2 sinkPos = new(Mathf.Round(hc.x), Mathf.Round(hc.y));

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

        StartCoroutine(HoleSinkAndDestroyRoutine(bombGo, sinkPos));
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

        int id = bomb.GetInstanceID();
        if (_removedBombIds.Contains(id))
            return;

        bomb.TryGetComponent<Bomb>(out var bombComp);

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

        int effectiveRadius = IsFullFireEnabled()
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
            PlayExplosionSfx(currentExplosionAudio, effectiveRadius);
        }

        TryHandleGroundExplosionHit(snapped);

        BombExplosion centerExplosion = Instantiate(explosionPrefab, snapped, Quaternion.identity);
        centerExplosion.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, snapped);

        Explode(snapped, Vector2.up, effectiveRadius, pierce);
        Explode(snapped, Vector2.down, effectiveRadius, pierce);
        Explode(snapped, Vector2.left, effectiveRadius, pierce);
        Explode(snapped, Vector2.right, effectiveRadius, pierce);

        float destroyDelay = 0.1f;

        if (explosionSfxByRadius != null && explosionSfxByRadius.Length > 0)
        {
            int sfxIndex = Mathf.Clamp(effectiveRadius - 1, 0, explosionSfxByRadius.Length - 1);
            AudioClip sfx = explosionSfxByRadius[sfxIndex];

            if (sfx != null)
                destroyDelay = sfx.length;
            else if (explosionAudio != null && explosionAudio.clip != null)
                destroyDelay = explosionAudio.clip.length;
        }
        else if (explosionAudio != null && explosionAudio.clip != null)
        {
            destroyDelay = explosionAudio.clip.length;
        }

        Destroy(bomb, destroyDelay);
        bombsRemaining = Mathf.Min(bombsRemaining + 1, bombAmout);
    }

    public void ExplodeBombChained(GameObject bomb)
    {
        if (bomb == null)
            return;

        int id = bomb.GetInstanceID();
        if (_removedBombIds.Contains(id))
            return;

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            return;

        if (!scheduledChainBombs.Add(id))
            return;

        float delay = chainBombDelaySeconds;
        if (bombComp != null)
            delay = Mathf.Max(0f, bombComp.chainStepDelay);

        StartCoroutine(ExplodeBombAfterDelay(bomb, id, delay));
    }

    private IEnumerator ExplodeBombAfterDelay(GameObject bomb, int id, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        scheduledChainBombs.Remove(id);

        if (bomb == null)
            yield break;

        if (_removedBombIds.Contains(id))
            yield break;

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            yield break;

        ExplodeBomb(bomb);
    }

    private void Explode(Vector2 origin, Vector2 direction, int length, bool pierce)
    {
        if (length <= 0)
            return;

        List<Vector2> positionsToSpawn = new(length);
        Vector2 position = origin;

        Tilemap snapTm = GetSnapTilemapForGround();

        for (int i = 0; i < length; i++)
        {
            position += direction;
            position = SnapToTileCenter(snapTm, position);

            if (HasWaterAt(position))
                break;

            if (HasIndestructibleAt(position))
            {
                TryHandleIndestructibleTileHit(position);
                break;
            }

            var itemHit = Physics2D.OverlapBox(position, Vector2.one * 0.5f, 0f, itemLayerMask);
            if (itemHit != null)
            {
                if (itemHit.TryGetComponent<ItemPickup>(out var item))
                    item.DestroyWithAnimation();

                if (pierce)
                {
                    positionsToSpawn.Add(position);
                    continue;
                }

                break;
            }

            if (TryGetDestructibleTileAt(position, out var cell, out var tile))
            {
                if (!TryHandleDestructibleTileEffect(position, cell, tile))
                    ClearDestructibleForEffect(position);

                positionsToSpawn.Add(position);

                if (!pierce)
                    break;

                continue;
            }

            if (HasDestroyingDestructibleAt(position))
            {
                positionsToSpawn.Add(position);

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
                    positionsToSpawn.Add(position);

                    int oid = otherBombGo.GetInstanceID();
                    if (otherBombGo.TryGetComponent<Bomb>(out var otherBomb) && otherBomb != null && otherBomb.Owner != null)
                        otherBomb.Owner.ExplodeBombChained(otherBombGo);
                    else
                        ExplodeBombChained(otherBombGo);

                    _removedBombIds.Remove(oid);
                }

                break;
            }

            positionsToSpawn.Add(position);
        }

        bool reachedMaxRange = positionsToSpawn.Count == length;

        for (int i = 0; i < positionsToSpawn.Count; i++)
        {
            Vector2 p = positionsToSpawn[i];
            bool isLastSpawned = i == positionsToSpawn.Count - 1;

            BombExplosion.ExplosionPart part =
                (isLastSpawned && reachedMaxRange)
                    ? BombExplosion.ExplosionPart.End
                    : BombExplosion.ExplosionPart.Middle;

            TryHandleGroundExplosionHit(p);

            BombExplosion explosion = Instantiate(explosionPrefab, p, Quaternion.identity);
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
            _gm = FindFirstObjectByType<GameManager>();

        if (_gm != null)
            _gm.OnDestructibleDestroyed(cell);

        Transform parent = destructibleTiles != null ? destructibleTiles.transform : null;

        if (spawnDestructiblePrefab && destructiblePrefab != null)
        {
            if (parent != null)
                Instantiate(destructiblePrefab, position, Quaternion.identity, parent);
            else
                Instantiate(destructiblePrefab, position, Quaternion.identity);
        }

        if (spawnHiddenObject && _gm != null)
        {
            GameObject spawnPrefab = _gm.GetSpawnForDestroyedBlock();
            if (spawnPrefab != null)
            {
                float delay = GetDestructibleDestroyTime();
                StartCoroutine(SpawnHiddenObjectAfterDelay(spawnPrefab, position, parent, delay));
            }
        }

        destructibleTiles.SetTile(cell, null);
    }

    private IEnumerator SpawnHiddenObjectAfterDelay(GameObject prefab, Vector2 position, Transform parent, float delay)
    {
        if (prefab == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (parent != null)
            Instantiate(prefab, position, Quaternion.identity, parent);
        else
            Instantiate(prefab, position, Quaternion.identity);
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

    private void PlayExplosionSfx(AudioSource source, int radius)
    {
        if (source == null || explosionSfxByRadius == null || explosionSfxByRadius.Length == 0)
            return;

        int index = Mathf.Clamp(radius - 1, 0, explosionSfxByRadius.Length - 1);
        AudioClip clip = explosionSfxByRadius[index];

        if (clip != null)
            source.PlayOneShot(clip, explosionSfxVolume);
    }

    public void PlayExplosionSfxExclusive(AudioSource source, int radius)
    {
        if (source == null || explosionSfxByRadius == null || explosionSfxByRadius.Length == 0)
            return;

        int index = Mathf.Clamp(radius - 1, 0, explosionSfxByRadius.Length - 1);
        AudioClip clip = explosionSfxByRadius[index];
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

    private void TryHandleIndestructibleTileHit(Vector2 worldPos)
    {
        ResolveIndestructibleTileResolver();

        if (indestructibleTileResolver == null)
            return;

        if (!TryGetIndestructibleTileAt(worldPos, out var cell, out var tile))
            return;

        if (!indestructibleTileResolver.TryGetHandler(tile, out var handler))
            return;

        handler.HandleExplosionHit(this, worldPos, cell, tile);
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

    private void UnregisterBomb(GameObject bomb)
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
        var bombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        if (bombs == null || bombs.Length == 0)
            return;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
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
    }

    public void NotifyBombAt(Vector2 worldPos, GameObject bombGo)
    {
        if (bombGo == null)
            return;

        int id = bombGo.GetInstanceID();
        if (_removedBombIds.Contains(id))
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
        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 p = SnapToTileCenter(snapTm, origin, out _, out _);

        BombExplosion centerExp = Instantiate(explosionPrefab, p, Quaternion.identity);
        centerExp.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, radius, pierce);
        Explode(p, Vector2.down, radius, pierce);
        Explode(p, Vector2.left, radius, pierce);
        Explode(p, Vector2.right, radius, pierce);
    }

    public void SpawnExplosionCrossForEffectWithTileEffects(
        Vector2 origin,
        int radius,
        bool pierce,
        AudioSource sfxSource = null)
    {
        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 p = SnapToTileCenter(snapTm, origin, out _, out _);

        int effectiveRadius = Mathf.Max(0, radius);
        bool effectivePierce = pierce;

        TryApplyGroundExplosionModifiers(p, ref effectiveRadius, ref effectivePierce);

        if (sfxSource != null)
            PlayExplosionSfxExclusive(sfxSource, effectiveRadius);

        BombExplosion centerExp = Instantiate(explosionPrefab, p, Quaternion.identity);
        centerExp.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, effectiveRadius, effectivePierce);
        Explode(p, Vector2.down, effectiveRadius, effectivePierce);
        Explode(p, Vector2.left, effectiveRadius, effectivePierce);
        Explode(p, Vector2.right, effectiveRadius, effectivePierce);
    }

    public bool TryPlaceBombAtIgnoringInputLock(Vector2 worldPos, out GameObject placedBomb, bool consumeBomb = true)
    {
        placedBomb = null;

        if (ClownMaskBoss.BossIntroRunning)
            return false;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return false;

        if (GamePauseController.IsPaused)
            return false;

        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.isDead || movement.IsEndingStage))
            return false;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
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

        bool controlEnabled = IsControlEnabled();
        bool pierceEnabled = !controlEnabled && IsPierceEnabled();

        GameObject prefabToUse =
            controlEnabled && controlBombPrefab != null ? controlBombPrefab :
            (pierceEnabled && pierceBombPrefab != null) ? pierceBombPrefab :
            bombPrefab;

        if (prefabToUse == null)
            return false;

        PlayPlaceBombSfx();

        GameObject bomb = Instantiate(prefabToUse, position, Quaternion.identity);
        placedBomb = bomb;
        lastPlacedBomb = bomb;

        if (consumeBomb)
            bombsRemaining--;

        if (!bomb.TryGetComponent<Bomb>(out var bombComponent))
            bombComponent = bomb.AddComponent<Bomb>();

        bombComponent.IsPierceBomb = pierceEnabled;
        bombComponent.IsControlBomb = controlEnabled;

        bombComponent.SetStageBoundsTilemap(stageBoundsTiles);
        bombComponent.SetFuseSeconds(bombFuseTime);
        bombComponent.Initialize(this);

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

        return true;
    }

    public bool TryPlaceBombAtIgnoringInputLock(Vector2 worldPos)
    {
        return TryPlaceBombAtIgnoringInputLock(worldPos, out _, consumeBomb: true);
    }
}