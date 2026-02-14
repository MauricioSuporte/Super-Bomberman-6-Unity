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
    private readonly HashSet<int> scheduledChainBombs = new();

    [Header("Chain Explosion")]
    [SerializeField] private float chainBombDelaySeconds = 0.1f;

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

    [Header("Water Sink Animation")]
    [SerializeField, Min(0f)] private float waterSinkSeconds = 0.1f;

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

    [Header("Debug - Surgical")]
    [SerializeField] private bool debugWaterBomb = true;
    [SerializeField] private bool debugExplodeTeleport = true;
    [SerializeField, Range(0f, 2f)] private float debugCooldown = 0.15f;

    private float _dbgNextAt;

    private void DLog(string msg, Object ctx = null)
    {
        if (!debugWaterBomb && !debugExplodeTeleport) return;
        if (Time.time < _dbgNextAt) return;
        _dbgNextAt = Time.time + Mathf.Max(0f, debugCooldown);
        Debug.Log($"[BombDbg][Controller:{name}] t={Time.time:0.000} f={Time.frameCount} {msg}", ctx != null ? ctx : this);
    }

    private static string BombTag(GameObject bombGo)
    {
        if (bombGo == null) return "bomb=NULL";
        int id = bombGo.GetInstanceID();
        var t = bombGo.transform;
        Vector3 p = t != null ? t.position : Vector3.zero;
        return $"bomb='{bombGo.name}' id={id} pos=({p.x:0.###},{p.y:0.###})";
    }

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

        // worldPos já chega "snapped" no chão pelo seu fluxo (NotifyBombAt / PlaceBomb)
        // Checa água DIRETO na célula do waterTiles (sem SnapToTileCenter no waterTiles).
        Vector3Int waterCell = waterTiles.WorldToCell(worldPos);
        TileBase waterTile = waterTiles.GetTile(waterCell);
        bool hasWater = waterTile != null;

        if (!hasWater)
            return false;

        _removedBombIds.Add(id);

        Vector3 wc = waterTiles.GetCellCenterWorld(waterCell);
        Vector2 sinkPos = new Vector2(Mathf.Round(wc.x), Mathf.Round(wc.y));

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

    private void DisableBombDrivers(GameObject bombGo)
    {
        if (bombGo == null)
            return;

        if (bombGo.TryGetComponent<BombAtGroundTileNotifier>(out var notifier) && notifier != null)
            notifier.enabled = false;

        if (bombGo.TryGetComponent<AnimatedSpriteRenderer>(out var anim) && anim != null)
            anim.enabled = false;
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

        Vector3 startScale = t != null ? t.localScale : Vector3.one;

        float stepSeconds = 0.1f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(waterSinkSeconds / stepSeconds));

        for (int i = 1; i <= steps; i++)
        {
            if (bombGo == null)
                yield break;

            float a = i / (float)steps;

            if (t != null)
                t.localScale = Vector3.Lerp(startScale, Vector3.zero, a);

            yield return new WaitForSeconds(stepSeconds);
        }

        if (t != null)
            t.localScale = Vector3.zero;

        DisableBombDrivers(bombGo);

        if (bombGo.TryGetComponent<Bomb>(out var bomb) && bomb != null)
            bomb.enabled = false;

        if (bombGo != null)
            Destroy(bombGo);
    }

    public void ExplodeBomb(GameObject bomb)
    {
        if (bomb == null)
            return;

        int id = bomb.GetInstanceID();

        if (debugExplodeTeleport)
        {
            bomb.TryGetComponent<Rigidbody2D>(out var rb0);
            bomb.TryGetComponent<Bomb>(out var bombComp0);
            Vector2 logical0 = bombComp0 != null ? bombComp0.GetLogicalPosition() : (Vector2)bomb.transform.position;
            Vector2 rbPos0 = rb0 != null ? rb0.position : Vector2.negativeInfinity;

            DLog(
                $"ExplodeBomb ENTER: {BombTag(bomb)} removedHas={_removedBombIds.Contains(id)} " +
                $"HasExploded={(bombComp0 != null && bombComp0.HasExploded)} logical=({logical0.x:0.###},{logical0.y:0.###}) rb=({rbPos0.x:0.###},{rbPos0.y:0.###})",
                bomb
            );
        }

        if (_removedBombIds.Contains(id))
        {
            if (debugExplodeTeleport) DLog($"ExplodeBomb ABORT (already removed): {BombTag(bomb)}", bomb);
            return;
        }

        bomb.TryGetComponent<Bomb>(out var bombComp);

        BombController realOwner = bombComp != null ? bombComp.Owner : null;
        if (realOwner != null && realOwner != this)
        {
            if (debugExplodeTeleport) DLog($"ExplodeBomb FORWARD to realOwner='{realOwner.name}' -> {BombTag(bomb)}", bomb);
            realOwner.ExplodeBomb(bomb);
            return;
        }

        if (bombComp != null && bombComp.IsControlBomb && bombComp.IsBeingPunched)
        {
            if (debugExplodeTeleport) DLog($"ExplodeBomb ABORT (control+punch): {BombTag(bomb)}", bomb);
            return;
        }

        UnregisterBomb(bomb);

        Vector2 logicalPos = bombComp != null
            ? bombComp.GetLogicalPosition()
            : (Vector2)bomb.transform.position;

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 snappedLogical = SnapToTileCenter(snapTm, logicalPos, out var snapCell, out var snapCenter);

        if (debugExplodeTeleport)
        {
            bomb.TryGetComponent<Rigidbody2D>(out var rb1);
            Vector2 rbPos1 = rb1 != null ? rb1.position : Vector2.negativeInfinity;

            DLog(
                $"ExplodeBomb SNAP: {BombTag(bomb)} logical=({logicalPos.x:0.###},{logicalPos.y:0.###}) " +
                $"snapCell=({snapCell.x},{snapCell.y}) snapped=({snappedLogical.x:0.###},{snappedLogical.y:0.###}) rbBefore=({rbPos1.x:0.###},{rbPos1.y:0.###})",
                bomb
            );
        }

        bomb.transform.position = snappedLogical;

        if (bomb.TryGetComponent<Rigidbody2D>(out var rb))
            rb.position = snappedLogical;

        if (bombComp != null)
        {
            if (bombComp.HasExploded)
            {
                if (debugExplodeTeleport) DLog($"ExplodeBomb ABORT (bombComp already HasExploded after snap): {BombTag(bomb)}", bomb);
                return;
            }

            bombComp.MarkAsExploded();
            bombComp.ForceSetLogicalPosition(snappedLogical);
        }

        int effectiveRadius = IsFullFireEnabled()
            ? PlayerPersistentStats.MaxExplosionRadius
            : explosionRadius;

        Vector2 position = snappedLogical;
        bool pierce = bombComp != null && bombComp.IsPierceBomb;

        TryApplyGroundExplosionModifiers(position, ref effectiveRadius, ref pierce);

        HideBombVisuals(bomb);

        if (bomb.TryGetComponent<AudioSource>(out var explosionAudio))
        {
            if (currentExplosionAudio != null && currentExplosionAudio.isPlaying)
                currentExplosionAudio.Stop();

            currentExplosionAudio = explosionAudio;
            PlayExplosionSfx(currentExplosionAudio, effectiveRadius);
        }

        if (explosionPrefab == null)
        {
            Debug.LogError($"[BombController] explosionPrefab NULL on '{name}' (scene='{gameObject.scene.name}')");
            return;
        }

        BombExplosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        centerExplosion.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, position);

        Explode(position, Vector2.up, effectiveRadius, pierce);
        Explode(position, Vector2.down, effectiveRadius, pierce);
        Explode(position, Vector2.left, effectiveRadius, pierce);
        Explode(position, Vector2.right, effectiveRadius, pierce);

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

        if (debugExplodeTeleport) DLog($"ExplodeBomb EXIT: scheduled Destroy in {destroyDelay:0.###}s -> {BombTag(bomb)} bombsRemaining={bombsRemaining}", bomb);
    }

    public void ExplodeBombChained(GameObject bomb)
    {
        if (bomb == null)
            return;

        int id = bomb.GetInstanceID();
        if (_removedBombIds.Contains(id))
        {
            if (debugExplodeTeleport) DLog($"ExplodeBombChained ABORT (removed): {BombTag(bomb)}", bomb);
            return;
        }

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            return;

        if (!scheduledChainBombs.Add(id))
            return;

        float delayFromController = chainBombDelaySeconds;
        float delayFromBomb = bombComp != null ? Mathf.Max(0f, bombComp.chainStepDelay) : -1f;
        float delay = delayFromBomb >= 0f ? delayFromBomb : delayFromController;

        if (debugExplodeTeleport) DLog($"ExplodeBombChained SCHEDULE: delay={delay:0.###} -> {BombTag(bomb)}", bomb);

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
        {
            if (debugExplodeTeleport) DLog($"ExplodeBombAfterDelay ABORT (removed): {BombTag(bomb)}", bomb);
            yield break;
        }

        if (bomb.TryGetComponent<Bomb>(out var bombComp) && bombComp.HasExploded)
            yield break;

        if (debugExplodeTeleport) DLog($"ExplodeBombAfterDelay FIRE -> {BombTag(bomb)}", bomb);
        ExplodeBomb(bomb);
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

    private void PlaceBomb()
    {
        var movement = GetComponent<MovementController>();
        if (movement != null && (movement.InputLocked || movement.isDead || movement.IsEndingStage))
            return;

        if (TryGetComponent<GreenLouieDashAbility>(out var dashAbility) && dashAbility != null && dashAbility.DashActive)
            return;

        Tilemap snapTm = GetSnapTilemapForGround();

        Vector2 rawPos = transform.position;
        Vector2 position = SnapToTileCenter(snapTm, rawPos, out _, out _);

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

        if (debugWaterBomb) DLog($"PlaceBomb: spawned -> {BombTag(bomb)} snappedPos=({position.x:0.###},{position.y:0.###}) bombsRemaining={bombsRemaining}", bomb);

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
        {
            if (debugWaterBomb) DLog($"PlaceBomb: destroyed by water immediately -> {BombTag(bomb)}", bomb);
            return;
        }

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            if (debugExplodeTeleport) DLog($"PlaceBomb: explosionAlreadyHere -> ExplodeBomb now -> {BombTag(bomb)}", bomb);
            ExplodeBomb(bomb);
            return;
        }

        if (!controlEnabled)
            bombComponent.BeginFuse();
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

            bool hitDestroyingDestructible = HasDestroyingDestructibleAt(position);
            if (hitDestroyingDestructible)
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

    private bool IsControlEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(ControlBombAbility.AbilityId);

        return false;
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

    private bool IsFullFireEnabled()
    {
        if (TryGetComponent<AbilitySystem>(out var abilitySystem))
            return abilitySystem.IsEnabled(FullFireAbility.AbilityId);

        return false;
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
        {
            if (debugWaterBomb) DLog($"NotifyBombAt SKIP (removed): {BombTag(bombGo)} worldPos=({worldPos.x:0.###},{worldPos.y:0.###})", bombGo);
            return;
        }

        Tilemap snapTm = GetSnapTilemapForGround();
        Vector2 snapped = SnapToTileCenter(snapTm, worldPos, out _, out _);

        if (debugWaterBomb)
        {
            bombGo.TryGetComponent<Rigidbody2D>(out var rb);
            bombGo.TryGetComponent<Bomb>(out var bombComp);
            Vector2 logical = bombComp != null ? bombComp.GetLogicalPosition() : (Vector2)bombGo.transform.position;
            Vector2 rbPos = rb != null ? rb.position : Vector2.negativeInfinity;

            DLog(
                $"NotifyBombAt: {BombTag(bombGo)} worldPos=({worldPos.x:0.###},{worldPos.y:0.###}) snapped=({snapped.x:0.###},{snapped.y:0.###}) " +
                $"logical=({logical.x:0.###},{logical.y:0.###}) rb=({rbPos.x:0.###},{rbPos.y:0.###})",
                bombGo
            );
        }

        TryHandleGroundBombAt(snapped, bombGo);

        TryDestroyBombIfOnWater(bombGo, snapped, refund: true);
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

    public bool CanSpawnTileEffectAt(Vector2 worldPos)
    {
        if (!HasGroundAt(worldPos))
            return false;

        if (HasIndestructibleAt(worldPos))
            return false;

        return true;
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

        if (explosionPrefab == null)
        {
            Debug.LogError($"[BombController] explosionPrefab NULL on '{name}' (scene='{gameObject.scene.name}')");
            return;
        }

        BombExplosion centerExp = Instantiate(explosionPrefab, p, Quaternion.identity);
        centerExp.Play(BombExplosion.ExplosionPart.Start, Vector2.zero, 0f, explosionDuration, p);

        Explode(p, Vector2.up, effectiveRadius, effectivePierce);
        Explode(p, Vector2.down, effectiveRadius, effectivePierce);
        Explode(p, Vector2.left, effectiveRadius, effectivePierce);
        Explode(p, Vector2.right, effectiveRadius, effectivePierce);
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

    private void PlayWaterDestroySfx()
    {
        if (waterDestroySfx == null)
            return;

        AudioSource src = playerAudioSource != null ? playerAudioSource : _localAudio;
        if (src == null)
            return;

        src.PlayOneShot(waterDestroySfx, waterDestroyVolume);
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

        if (debugWaterBomb)
            DLog($"TryPlaceBombAtIgnoringInputLock: spawned -> {BombTag(bomb)} snappedPos=({position.x:0.###},{position.y:0.###}) consume={consumeBomb} bombsRemaining={bombsRemaining}", bomb);

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
            if (debugWaterBomb)
                DLog($"TryPlaceBombAtIgnoringInputLock: destroyed by water immediately -> {BombTag(bomb)}", bomb);

            placedBomb = null;
            return false;
        }

        if (bomb.TryGetComponent<Collider2D>(out var bombCollider))
            bombCollider.isTrigger = true;

        if (controlEnabled)
            RegisterBomb(bomb);

        if (explosionAlreadyHere)
        {
            if (debugExplodeTeleport)
                DLog($"TryPlaceBombAtIgnoringInputLock: explosionAlreadyHere -> ExplodeBomb now -> {BombTag(bomb)}", bomb);

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
