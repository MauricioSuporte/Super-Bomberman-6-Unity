using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public class MoleMountDrillAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "MoleMountDrill";

    [SerializeField] private bool enabledAbility = true;

    [Header("Teleport Search")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask blockingMask;
    [SerializeField, Min(1)] private int searchRadiusTiles = 10;
    [SerializeField] private bool preferFartherTiles = true;

    [Header("Enemy Avoidance")]
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField, Min(0)] private int enemyAvoidanceRadiusTiles = 5;

    [Header("Enemy Avoidance - Behavior")]
    [SerializeField] private bool enemyAvoidanceIgnoreTriggers = false;

    [Header("Enemy Avoidance - Extra Safety")]
    [SerializeField] private bool rejectTilesWithEnemyOverlap = true;

    [Header("Avoid Destructibles")]
    [SerializeField] private string destructiblesTag = "Destructibles";
    [SerializeField] private LayerMask destructiblesLayerMask;

    [Header("Input Lock")]
    [SerializeField] private bool lockInputWhileDrilling = true;

    [Header("Burrowed (after Phase 3, before teleport)")]
    [SerializeField, Min(0f)] private float burrowedSeconds = 0.5f;

    [Header("Phase 1 HeadOnlyDown Offset Delta")]
    private static readonly Vector2 Phase1HeadOnlyDownDelta = new(0f, -0.3f);

    MovementController movement;
    Rigidbody2D rb;
    Coroutine routine;
    Vector2 lastFacingDir = Vector2.down;

    IMoleMountDrillExternalAnimator externalAnimator;

    AudioClip drillSfx;
    float drillVolume = 1f;
    AudioSource audioSource;

    bool running;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    MountEggQueue eggQueue;
    bool eggQueuePrevVisible;
    bool eggQueueCached;
    bool eggQueueHiddenByThisAbility;

    MountVisualController cachedMountVisual;

    bool cachedMountVisualPrevEnabled;
    bool cachedMountVisualDisabledByThisAbility;

    struct CachedAsrState
    {
        public AnimatedSpriteRenderer asr;
        public bool enabled;
        public bool idle;
        public bool loop;
        public bool pingPong;
        public int frame;
    }

    readonly List<CachedAsrState> _globalCachedAsr = new(64);
    bool _globalSuppressionActive;

    void Awake()
    {
        eggQueue = GetComponentInChildren<MountEggQueue>(true);
        eggQueueCached = true;

        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;

        audioSource = GetComponentInParent<AudioSource>();

        if (enemyLayerMask.value == 0)
            enemyLayerMask = LayerMask.GetMask("Enemy");

        if (destructiblesLayerMask.value == 0)
            destructiblesLayerMask = LayerMask.GetMask("Stage");
    }

    void OnDisable() => Cancel();
    void OnDestroy() => Cancel();

    void LateUpdate()
    {
        if (!running || !_globalSuppressionActive)
            return;

        EnforceGlobalSuppression();
    }

    public void SetDrillSfx(AudioClip clip, float volume)
    {
        drillSfx = clip;
        drillVolume = Mathf.Clamp01(volume);
    }

    public void SetExternalAnimator(IMoleMountDrillExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    public void SetGroundTilemap(Tilemap t)
    {
        groundTilemap = t;
    }

    void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (movement == null || movement.isDead || movement.InputLocked)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        if (movement.Direction != Vector2.zero)
            lastFacingDir = movement.Direction;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        int pid = movement.PlayerId;
        if (!input.GetDown(pid, PlayerAction.ActionC))
            return;

        if (!IsMountedOnMole())
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(DrillRoutine());
    }

    IEnumerator DrillRoutine()
    {
        if (!eggQueueCached)
        {
            eggQueue = GetComponentInChildren<MountEggQueue>(true);
            eggQueueCached = true;
        }

        if (eggQueue != null)
        {
            eggQueuePrevVisible = !eggQueue.IsForcedHidden;
            eggQueueHiddenByThisAbility = true;
            eggQueue.ForceVisible(false);
        }
        else
        {
            eggQueueHiddenByThisAbility = false;
        }

        if (drillSfx != null && audioSource != null)
            audioSource.PlayOneShot(drillSfx, drillVolume);

        if (movement == null || rb == null)
        {
            routine = null;
            RestoreEggQueueIfNeeded();
            yield break;
        }

        if (running)
        {
            routine = null;
            RestoreEggQueueIfNeeded();
            yield break;
        }

        running = true;

        Vector2 dir = lastFacingDir == Vector2.zero ? Vector2.down : lastFacingDir;

        if (lockInputWhileDrilling)
            movement.SetInputLocked(true, false);

        try
        {
            float p1 = 1f;
            float p2 = 0.5f;
            float p3 = 0.5f;
            float p2rev = 0.5f;

            if (externalAnimator is MoleMountDrillAnimator anim)
            {
                p1 = anim.Phase1Duration;
                p2 = anim.Phase2Duration;
                p3 = anim.Phase3Duration;
                p2rev = anim.Phase2ReverseDuration;
            }

            ApplyPhase1HeadOnlyDownDelta(true);

            movement.SetExternalVisualSuppressed(false);
            movement.SetInactivityMountedDownOverride(true);

            BeginGlobalSuppression();

            externalAnimator?.PlayPhase(1, dir);
            yield return new WaitForSeconds(p1);

            ApplyPhase1HeadOnlyDownDelta(false);

            movement.SetInactivityMountedDownOverride(false);
            movement.SetExternalVisualSuppressed(true);

            externalAnimator?.PlayPhase(2, dir);
            yield return new WaitForSeconds(p2);

            externalAnimator?.PlayPhase(3, dir);
            yield return new WaitForSeconds(p3);

            yield return new WaitForSeconds(burrowedSeconds);

            TryTeleportToOtherGroundTile();

            externalAnimator?.PlayPhase(4, dir);
            yield return new WaitForSeconds(p2rev);

            movement.ForceFacingDirection(Vector2.down);
            movement.SetInactivityMountedDownOverride(true);
            movement.SetExternalVisualSuppressed(false);

            yield return null;
        }
        finally
        {
            ApplyPhase1HeadOnlyDownDelta(false);

            externalAnimator?.Stop();

            EndGlobalSuppression();

            if (movement != null)
            {
                movement.SetInactivityMountedDownOverride(false);
                movement.SetExternalVisualSuppressed(false);

                if (lockInputWhileDrilling)
                    movement.SetInputLocked(false);
            }

            RestoreEggQueueIfNeeded();

            running = false;
            routine = null;
        }
    }

    void BeginGlobalSuppression()
    {
        _globalCachedAsr.Clear();

        var root = GetSuppressionRoot();
        if (root == null)
        {
            _globalSuppressionActive = false;
            return;
        }

        _globalSuppressionActive = true;

        if (cachedMountVisual == null)
            cachedMountVisual = GetComponentInChildren<MountVisualController>(true);

        if (cachedMountVisual != null && !cachedMountVisualDisabledByThisAbility)
        {
            cachedMountVisualPrevEnabled = cachedMountVisual.enabled;
            if (cachedMountVisualPrevEnabled)
            {
                cachedMountVisual.enabled = false;
                cachedMountVisualDisabledByThisAbility = true;
            }
        }

        var drillAnim = externalAnimator as MoleMountDrillAnimator;

        var all = root.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var a = all[i];
            if (a == null)
                continue;

            if (drillAnim != null && drillAnim.IsAbilitySpritePublic(a))
                continue;

            _globalCachedAsr.Add(new CachedAsrState
            {
                asr = a,
                enabled = a.enabled,
                idle = a.idle,
                loop = a.loop,
                pingPong = a.pingPong,
                frame = a.CurrentFrame
            });

            a.enabled = false;
        }
    }

    void EnforceGlobalSuppression()
    {
        for (int i = 0; i < _globalCachedAsr.Count; i++)
        {
            var a = _globalCachedAsr[i].asr;
            if (a == null)
                continue;

            if (a.enabled)
                a.enabled = false;
        }
    }

    void EndGlobalSuppression()
    {
        if (!_globalSuppressionActive)
            return;

        for (int i = 0; i < _globalCachedAsr.Count; i++)
        {
            var st = _globalCachedAsr[i];
            if (st.asr == null)
                continue;

            st.asr.idle = st.idle;
            st.asr.loop = st.loop;
            st.asr.pingPong = st.pingPong;
            st.asr.CurrentFrame = st.frame;
            st.asr.enabled = st.enabled;

            if (st.asr.enabled)
                st.asr.RefreshFrame();
        }

        _globalCachedAsr.Clear();
        _globalSuppressionActive = false;

        if (cachedMountVisualDisabledByThisAbility && cachedMountVisual != null)
        {
            cachedMountVisual.enabled = cachedMountVisualPrevEnabled;
            cachedMountVisualDisabledByThisAbility = false;
        }
    }

    void RestoreEggQueueIfNeeded()
    {
        if (!eggQueueHiddenByThisAbility)
            return;

        if (!eggQueueCached)
        {
            eggQueue = GetComponentInChildren<MountEggQueue>(true);
            eggQueueCached = true;
        }

        if (eggQueue != null)
        {
            eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
            eggQueue.ForceVisible(eggQueuePrevVisible);
        }

        eggQueueHiddenByThisAbility = false;
    }

    void ApplyPhase1HeadOnlyDownDelta(bool on)
    {
        if (Phase1HeadOnlyDownDelta == Vector2.zero)
            return;

        if (movement == null)
            return;

        if (cachedMountVisual == null)
            cachedMountVisual = GetComponentInChildren<MountVisualController>(true);

        if (cachedMountVisual == null)
            return;

        cachedMountVisual.SetTemporaryHeadOnlyDownDelta(Phase1HeadOnlyDownDelta, on);
    }

    bool IsMountedOnMole()
    {
        if (!TryGetComponent<PlayerMountCompanion>(out var mount) || mount == null)
            return false;

        return mount.GetMountedLouieType() == MountedType.Mole;
    }

    void TryTeleportToOtherGroundTile()
    {
        if (groundTilemap == null)
            return;

        if (movement == null || rb == null)
            return;

        Vector3 world = rb.position;
        Vector3Int originCell = groundTilemap.WorldToCell(world);

        if (!TryFindTargetCell(originCell, out var targetCell))
            return;

        Vector3 targetWorld = groundTilemap.GetCellCenterWorld(targetCell);

        rb.position = targetWorld;
        transform.position = targetWorld;
    }

    bool TryFindTargetCell(Vector3Int origin, out Vector3Int target)
    {
        target = origin;

        var safe = new List<Vector3Int>(256);
        var fallback = new List<Vector3Int>(256);

        int r = Mathf.Max(1, searchRadiusTiles);

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var c = new Vector3Int(origin.x + dx, origin.y + dy, origin.z);

                if (!groundTilemap.HasTile(c))
                    continue;

                Vector3 w = groundTilemap.GetCellCenterWorld(c);

                if (IsBlockedAtWorld(w))
                    continue;

                if (IsDestructibleAtWorld(w))
                    continue;

                if (rejectTilesWithEnemyOverlap && IsEnemyOverlappingExactSpot(w))
                    continue;

                fallback.Add(c);

                if (enemyAvoidanceRadiusTiles > 0)
                {
                    if (IsEnemyNearWorld(w))
                        continue;
                }

                safe.Add(c);
            }
        }

        var pool = safe.Count > 0 ? safe : fallback;
        if (pool.Count == 0)
            return false;

        if (!preferFartherTiles)
        {
            target = pool[Random.Range(0, pool.Count)];
            return true;
        }

        pool.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.x - origin.x) + Mathf.Abs(a.y - origin.y);
            int db = Mathf.Abs(b.x - origin.x) + Mathf.Abs(b.y - origin.y);
            return db.CompareTo(da);
        });

        int pickPool = Mathf.Min(12, pool.Count);
        int pickIndex = Random.Range(0, pickPool);
        target = pool[pickIndex];

        return true;
    }

    bool IsDestructibleAtWorld(Vector3 worldPos)
    {
        if (destructiblesLayerMask.value == 0 || string.IsNullOrEmpty(destructiblesTag))
            return false;

        float tileSize = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        Vector2 size = new Vector2(tileSize * 0.6f, tileSize * 0.6f);

        var hits = Physics2D.OverlapBoxAll(worldPos, size, 0f, destructiblesLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (h.gameObject == gameObject)
                continue;

            if (h.isTrigger)
                continue;

            if (h.CompareTag(destructiblesTag))
                return true;
        }

        return false;
    }

    bool IsEnemyNearWorld(Vector3 worldPos)
    {
        if (enemyLayerMask.value == 0 || enemyAvoidanceRadiusTiles <= 0)
            return false;

        float tile = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        float radius = enemyAvoidanceRadiusTiles * tile;

        var hits = Physics2D.OverlapCircleAll(worldPos, radius, enemyLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (h.gameObject == gameObject)
                continue;

            if (enemyAvoidanceIgnoreTriggers && h.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    bool IsEnemyOverlappingExactSpot(Vector3 worldPos)
    {
        if (enemyLayerMask.value == 0)
            return false;

        float tileSize = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        Vector2 size = new Vector2(tileSize * 0.7f, tileSize * 0.7f);

        var hits = Physics2D.OverlapBoxAll(worldPos, size, 0f, enemyLayerMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (h.gameObject == gameObject)
                continue;

            if (enemyAvoidanceIgnoreTriggers && h.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    bool IsBlockedAtWorld(Vector3 worldPos)
    {
        float tileSize = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        Vector2 size = new Vector2(tileSize * 0.6f, tileSize * 0.6f);

        var hits = Physics2D.OverlapBoxAll(worldPos, size, 0f, blockingMask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null)
                continue;

            if (h.gameObject == gameObject)
                continue;

            if (h.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ApplyPhase1HeadOnlyDownDelta(false);

        externalAnimator?.Stop();

        EndGlobalSuppression();

        if (movement != null)
        {
            movement.SetInactivityMountedDownOverride(false);
            movement.SetExternalVisualSuppressed(false);

            if (lockInputWhileDrilling)
                movement.SetInputLocked(false);
        }

        RestoreEggQueueIfNeeded();

        running = false;
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        Cancel();
    }

    Component GetSuppressionRoot()
    {
        if (externalAnimator is Component c && c != null)
            return c;

        if (cachedMountVisual != null)
            return cachedMountVisual;

        return null;
    }
}
