using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class BattleMode7PortalController : MonoBehaviour
{
    const string BattleMode7SceneName = "BattleMode_7";
    const string DefaultEnterSfxResourcesPath = "Sounds/start";
    const int BombPortalPunchDistanceTiles = 3;

    [Header("Portal Cells")]
    [SerializeField]
    private Vector2Int[] portalCells =
    {
        new(-5, 2),
        new(3, 2),
        new(3, -4),
        new(-5, -4),
    };

    [Header("Teleport")]
    [SerializeField, Min(0.01f)] private float teleportSeconds = 0.5f;
    [SerializeField, Min(0f)] private float retriggerGraceSeconds = 0.05f;
    [SerializeField] private bool snapToDestinationCenter = true;

    [Header("Teleport Stars")]
    [SerializeField] private bool spawnTeleportStars = true;
    [SerializeField] private Sprite[] teleportStarSprites;
    [SerializeField, Min(0)] private int teleportStarCount = 32;
    [SerializeField, Min(0.01f)] private float teleportStarLifetime = 0.32f;
    [SerializeField] private Vector2 teleportStarScaleRange = new(0.18f, 0.32f);
    [SerializeField] private Vector2 teleportStarDriftRange = new(0.16f, 0.42f);
    [SerializeField] private Vector2 teleportStarSpinRange = new(-220f, 220f);
    [SerializeField, Range(0f, 1f)] private float teleportStarPathJitter = 0.22f;
    [SerializeField, Min(0.01f)] private float teleportStarAnimationFrameTime = 0.1f;
    [SerializeField] private int teleportStarSortingOrder = 90;

    [Header("SFX")]
    [SerializeField] private AudioClip enterSfx;
    [SerializeField, Range(0f, 1f)] private float enterSfxVolume = 1f;

    Tilemap groundTilemap;
    AudioSource audioSource;

    readonly HashSet<MovementController> activeTeleporters = new();
    readonly HashSet<Bomb> activeBombTeleporters = new();
    readonly Dictionary<MovementController, Vector3Int> waitingForPortalExit = new();
    readonly Dictionary<MovementController, TeleportState> activeStates = new();
    readonly Dictionary<Bomb, Vector3Int> bombsWaitingForPortalExit = new();

    sealed class TeleportState
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
        public MountEggQueue eggQueue;
        public Bomb heldBomb;
        public PowerGloveAbility powerGlove;
        public SpriteRenderer[] heldBombRenderers;
        public bool[] heldBombRendererStates;
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
        if (!string.Equals(activeScene.name, BattleMode7SceneName, System.StringComparison.Ordinal))
            return;

        if (FindAnyObjectByType<BattleMode7PortalController>() != null)
            return;

        var host = new GameObject(nameof(BattleMode7PortalController));
        host.AddComponent<BattleMode7PortalController>();
    }

    void Awake()
    {
        if (!IsBattleMode7Active())
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        EnsureAudioSource();
        LoadDefaultSfxIfNeeded();
    }

    void OnDisable()
    {
        foreach (var pair in activeStates)
            RestoreTeleportState(pair.Key, pair.Value);

        activeStates.Clear();
        activeTeleporters.Clear();
        activeBombTeleporters.Clear();
        waitingForPortalExit.Clear();
        bombsWaitingForPortalExit.Clear();
    }

    void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.ArenaUpdate.Auto();

        if (!IsBattleMode7Active())
            return;

        ResolveReferences();

        if (portalCells == null || portalCells.Length < 2)
            return;

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
            TryHandlePlayer(players[i]);

        TryHandleBombs();
    }

    void TryHandleBombs()
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove ||
                bomb.IsBeingPunched ||
                activeBombTeleporters.Contains(bomb))
                continue;

            Vector3Int currentCell = WorldToCell(bomb.GetLogicalPosition());

            if (bombsWaitingForPortalExit.TryGetValue(bomb, out Vector3Int blockedCell))
            {
                if (currentCell == blockedCell)
                    continue;

                bombsWaitingForPortalExit.Remove(bomb);
            }

            int sourceIndex = GetPortalIndex(currentCell);
            if (sourceIndex < 0)
                continue;

            StartCoroutine(TeleportAndPunchBombRoutine(bomb, sourceIndex));
        }
    }

    IEnumerator TeleportAndPunchBombRoutine(Bomb bomb, int sourceIndex)
    {
        if (bomb == null || portalCells == null || portalCells.Length < 2)
            yield break;

        activeBombTeleporters.Add(bomb);

        int destinationIndex = GetClockwiseDestinationIndex(sourceIndex);
        int nextPortalIndex = GetClockwiseDestinationIndex(destinationIndex);

        Vector3Int sourceCell = ToCell(portalCells[sourceIndex]);
        Vector3Int destinationCell = ToCell(portalCells[destinationIndex]);
        Vector2 source = GetCellCenter(sourceCell);
        Vector2 destination = GetCellCenter(destinationCell);
        Vector2 nextPortal = GetCellCenter(ToCell(portalCells[nextPortalIndex]));
        Vector2 launchDirection = GetCardinalDirection(nextPortal - destination);

        if (launchDirection == Vector2.zero)
        {
            activeBombTeleporters.Remove(bomb);
            yield break;
        }

        bomb.ForceStopExternalMovementAndSnap(source);

        SpriteRenderer[] renderers = bomb.GetComponentsInChildren<SpriteRenderer>(true);
        bool[] rendererStates = CaptureRendererStates(renderers);
        Collider2D bombCollider = bomb.GetComponent<Collider2D>();
        bool colliderWasEnabled = bombCollider != null && bombCollider.enabled;
        BombAtGroundTileNotifier notifier = bomb.GetComponent<BombAtGroundTileNotifier>();
        bool notifierWasEnabled = notifier != null && notifier.enabled;

        SetRenderersEnabled(renderers, false);
        if (bombCollider != null)
            bombCollider.enabled = false;
        if (notifier != null)
            notifier.enabled = false;

        PlayEnterSfx();
        int spawnedStars = 0;
        if (spawnTeleportStars)
        {
            SpawnTeleportStar(source);
            spawnedStars = 1;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, teleportSeconds);
        while (elapsed < duration)
        {
            if (bomb == null || bomb.HasExploded)
            {
                activeBombTeleporters.Remove(bomb);
                yield break;
            }

            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            spawnedStars = SpawnTeleportStarsAlongPath(source, destination, t, spawnedStars);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (bomb == null || bomb.HasExploded)
        {
            activeBombTeleporters.Remove(bomb);
            yield break;
        }

        bomb.ForceStopExternalMovementAndSnap(destination);
        RestoreRendererStates(renderers, rendererStates);
        if (bombCollider != null)
            bombCollider.enabled = colliderWasEnabled;
        if (notifier != null)
            notifier.enabled = notifierWasEnabled;

        SpawnTeleportStarsAlongPath(source, destination, 1f, spawnedStars);

        BombController owner = bomb.Owner;
        MovementController ownerMovement = owner != null
            ? owner.GetComponent<MovementController>()
            : null;
        LayerMask obstacles = ownerMovement != null
            ? ownerMovement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player")
            : LayerMask.GetMask("Stage", "Enemy", "Bomb", "Player");
        Tilemap destructibleTilemap = owner != null ? owner.destructibleTiles : null;
        float tileSize = ownerMovement != null ? Mathf.Max(0.0001f, ownerMovement.tileSize) : 1f;

        bool launched = bomb.StartPunch(
            launchDirection,
            tileSize,
            BombPortalPunchDistanceTiles,
            obstacles,
            destructibleTilemap,
            logicalOriginOverride: destination);

        if (!launched)
        {
            activeBombTeleporters.Remove(bomb);
            yield break;
        }

        bombsWaitingForPortalExit[bomb] = destinationCell;
        activeBombTeleporters.Remove(bomb);
    }

    static Vector2 GetCardinalDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2.zero;

        return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
    }

    public float TeleportDurationSeconds
        => Mathf.Max(0.01f, teleportSeconds);

    public bool TryGetBombPortalTrajectory(
        Vector2Int sourcePortal,
        out Vector2Int destinationPortal,
        out Vector2Int launchDirection,
        out Vector2Int punchLandingTile)
    {
        destinationPortal = sourcePortal;
        launchDirection = Vector2Int.zero;
        punchLandingTile = sourcePortal;

        int sourceIndex = GetPortalIndex(ToCell(sourcePortal));
        if (sourceIndex < 0 || portalCells == null || portalCells.Length < 2)
            return false;

        int destinationIndex = GetClockwiseDestinationIndex(sourceIndex);
        int nextPortalIndex = GetClockwiseDestinationIndex(destinationIndex);
        destinationPortal = portalCells[destinationIndex];
        Vector2Int nextPortal = portalCells[nextPortalIndex];
        Vector2 direction = GetCardinalDirection(nextPortal - destinationPortal);
        launchDirection = new Vector2Int(
            Mathf.RoundToInt(direction.x),
            Mathf.RoundToInt(direction.y));
        if (launchDirection == Vector2Int.zero)
            return false;

        punchLandingTile =
            destinationPortal + launchDirection * BombPortalPunchDistanceTiles;
        return true;
    }

    public void CopyPortalCells(List<Vector2Int> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (portalCells == null)
            return;

        for (int i = 0; i < portalCells.Length; i++)
            destination.Add(portalCells[i]);
    }

    public bool IsPortalCell(Vector2Int cell)
        => GetPortalIndex(ToCell(cell)) >= 0;

    public bool IsMovementAtPortal(
        MovementController mover,
        Vector2Int portalCell)
    {
        return mover != null &&
               mover.Rigidbody != null &&
               WorldToCell(mover.Rigidbody.position) ==
               ToCell(portalCell);
    }

    public bool TryGetPortalWorldCenter(
        Vector2Int portalCell,
        out Vector2 worldCenter)
    {
        if (!IsPortalCell(portalCell))
        {
            worldCenter = Vector2.zero;
            return false;
        }

        ResolveReferences();
        worldCenter = GetCellCenter(ToCell(portalCell));
        return true;
    }

    public bool TryGetClockwiseDestination(
        Vector2Int source,
        out Vector2Int destination)
    {
        int sourceIndex = GetPortalIndex(ToCell(source));
        if (sourceIndex < 0 || portalCells == null || portalCells.Length < 2)
        {
            destination = source;
            return false;
        }

        destination =
            portalCells[GetClockwiseDestinationIndex(sourceIndex)];
        return true;
    }

    void TryHandlePlayer(MovementController mover)
    {
        if (mover == null || mover.Rigidbody == null || !mover.CompareTag("Player"))
            return;

        if (mover.isDead || mover.IsEndingStage || !mover.gameObject.activeInHierarchy)
            return;

        if (IsPinkLouieJumping(mover))
            return;

        Vector3Int currentCell = WorldToCell(mover.Rigidbody.position);

        if (waitingForPortalExit.TryGetValue(mover, out Vector3Int blockedCell))
        {
            if (currentCell == blockedCell)
                return;

            waitingForPortalExit.Remove(mover);
        }

        if (activeTeleporters.Contains(mover))
            return;

        int portalIndex = GetPortalIndex(currentCell);
        if (portalIndex < 0)
            return;

        int destinationIndex = GetClockwiseDestinationIndex(portalIndex);
        StartCoroutine(TeleportRoutine(mover, portalIndex, destinationIndex));
    }

    static bool IsPinkLouieJumping(MovementController mover)
    {
        return mover != null &&
               mover.TryGetComponent(out PinkLouieJumpAbility pinkJump) &&
               pinkJump != null &&
               pinkJump.JumpActive;
    }

    IEnumerator TeleportRoutine(MovementController mover, int sourceIndex, int destinationIndex)
    {
        if (mover == null || mover.Rigidbody == null)
            yield break;

        activeTeleporters.Add(mover);

        Vector3Int sourceCell = ToCell(portalCells[sourceIndex]);
        Vector3Int destinationCell = ToCell(portalCells[destinationIndex]);
        Vector2 source = GetCellCenter(sourceCell);
        Vector2 destination = GetCellCenter(destinationCell);

        if (mover.TryGetComponent(
                out BattleModeComStage7PortalEscapeAbility comPortalAbility))
        {
            comPortalAbility.LogTeleportStarted(
                portalCells[sourceIndex],
                portalCells[destinationIndex]);
        }

        CancelActiveMountMovementAbilities(mover);

        TeleportState state = CaptureAndApplyTeleportState(mover);
        activeStates[mover] = state;

        PlayEnterSfx();

        try
        {
            float duration = Mathf.Max(0.01f, teleportSeconds);
            float elapsed = 0f;
            int spawnedStars = 0;
            if (spawnTeleportStars && teleportStarCount > 0)
            {
                SpawnTeleportStar(source);
                spawnedStars = 1;
            }

            while (elapsed < duration)
            {
                if (mover == null || mover.Rigidbody == null)
                    yield break;

                if (mover.IsEndingStage)
                    yield break;

                if (GamePauseController.IsPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 position = Vector2.Lerp(source, destination, SmoothTeleportT(t));

                mover.Rigidbody.position = position;
                mover.Rigidbody.linearVelocity = Vector2.zero;

                spawnedStars = SpawnTeleportStarsAlongPath(source, destination, t, spawnedStars);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (mover != null && !mover.IsEndingStage)
            {
                Vector2 finalPosition = snapToDestinationCenter ? destination : mover.Rigidbody.position;
                mover.SnapToWorldPoint(finalPosition, roundToGrid: false);

                if (state.eggQueue != null)
                    state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
            }

            SpawnTeleportStarsAlongPath(source, destination, 1f, spawnedStars);
        }
        finally
        {
            RestoreTeleportState(mover, state);
            activeStates.Remove(mover);
            if (comPortalAbility != null)
            {
                comPortalAbility.LogTeleportCompleted(
                    portalCells[sourceIndex],
                    portalCells[destinationIndex]);
            }

            StartCoroutine(ReleaseTeleporterAfterGrace(mover, destinationCell));
        }
    }

    TeleportState CaptureAndApplyTeleportState(MovementController mover)
    {
        var state = new TeleportState
        {
            prevInputLocked = mover.InputLocked,
            prevPlayerExplosionInvulnerable = mover.explosionInvulnerable,
            playerCollider = mover.GetComponent<Collider2D>(),
            mountMovement = mover.GetComponentInChildren<MountMovementController>(true),
            healths = mover.GetComponentsInChildren<CharacterHealth>(true),
            mountCompanion = mover.GetComponent<PlayerMountCompanion>(),
            eggQueue = mover.GetComponentInChildren<MountEggQueue>(true),
        };

        state.powerGlove = mover.GetComponent<PowerGloveAbility>();
        state.heldBomb = state.powerGlove != null
            ? state.powerGlove.HeldBombForExternalTransition
            : null;
        if (state.heldBomb != null)
        {
            state.heldBombRenderers = state.heldBomb.GetComponentsInChildren<SpriteRenderer>(true);
            state.heldBombRendererStates = CaptureRendererStates(state.heldBombRenderers);
        }

        state.prevColliderEnabled = state.playerCollider != null && state.playerCollider.enabled;
        state.prevMountExplosionInvulnerable = state.mountMovement != null && state.mountMovement.explosionInvulnerable;
        state.hadBombController = mover.TryGetComponent(out state.bombController) && state.bombController != null;
        state.prevBombEnabled = state.hadBombController && state.bombController.enabled;

        mover.SetInputLocked(true, forceIdle: false);
        mover.SetExternalMovementOverride(true);
        mover.SetVisualOverrideActive(true);
        mover.SetAllSpritesVisible(false);
        state.powerGlove?.SetTeleportVisualSuppressed(true);
        SetRenderersEnabled(state.heldBombRenderers, false);
        mover.SetExplosionInvulnerable(true);

        if (state.bombController != null)
            state.bombController.enabled = false;

        if (state.playerCollider != null)
            state.playerCollider.enabled = false;

        if (state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(true);

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(false);

        if (state.eggQueue != null)
            state.eggQueue.ForceVisible(false);

        SetHealthInvulnerability(state.healths, true);

        return state;
    }

    void CancelActiveMountMovementAbilities(MovementController mover)
    {
        if (mover == null)
            return;

        var greenDash = mover.GetComponent<GreenLouieDashAbility>();
        if (greenDash != null && greenDash.DashActive)
            greenDash.CancelDashForExternalInterruption();

        var pinkJump = mover.GetComponent<PinkLouieJumpAbility>();
        if (pinkJump != null && pinkJump.JumpActive)
            pinkJump.CancelJumpForExternalInterruption();

        var blackDash = mover.GetComponent<BlackLouieDashPushAbility>();
        if (blackDash != null && blackDash.DashActive)
            blackDash.CancelDashForExternalInterruption();
    }

    void RestoreTeleportState(MovementController mover, TeleportState state)
    {
        if (state == null)
            return;

        if (mover != null)
        {
            mover.SetExternalMovementOverride(false);
            mover.SetVisualOverrideActive(false);

            if (mover.Rigidbody != null)
                mover.Rigidbody.linearVelocity = Vector2.zero;

            if (!mover.IsEndingStage)
            {
                mover.SetInputLocked(state.prevInputLocked, forceIdle: false);
                mover.EnableExclusiveFromState();
                mover.SetExplosionInvulnerable(state.prevPlayerExplosionInvulnerable);
                RestoreRendererStates(state.heldBombRenderers, state.heldBombRendererStates);
                state.powerGlove?.SetTeleportVisualSuppressed(false);
            }
        }

        bool restoreGameplayState = mover == null || !mover.IsEndingStage;

        if (restoreGameplayState && state.mountMovement != null)
            state.mountMovement.SetExplosionInvulnerable(state.prevMountExplosionInvulnerable);

        if (restoreGameplayState && state.bombController != null)
            state.bombController.enabled = state.prevBombEnabled;

        if (restoreGameplayState && state.playerCollider != null)
            state.playerCollider.enabled = state.prevColliderEnabled;

        if (state.mountCompanion != null)
            state.mountCompanion.SetMountedLouieVisible(true);

        if (state.eggQueue != null)
        {
            state.eggQueue.ForceVisible(true);
            state.eggQueue.SnapQueueToOwnerNow(resetHistoryToOwnerNow: true);
        }

        if (restoreGameplayState)
            SetHealthInvulnerability(state.healths, false);
    }

    IEnumerator ReleaseTeleporterAfterGrace(MovementController mover, Vector3Int destinationCell)
    {
        if (retriggerGraceSeconds > 0f)
            yield return new WaitForSeconds(retriggerGraceSeconds);

        activeTeleporters.Remove(mover);

        if (mover != null)
            waitingForPortalExit[mover] = destinationCell;
    }

    void ResolveReferences()
    {
        if (groundTilemap == null)
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
                groundTilemap = gm.groundTilemap;
        }

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("ground");

        if (groundTilemap == null)
            groundTilemap = FindTilemapByName("Ground");
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
    }

    void PlayEnterSfx()
    {
        EnsureAudioSource();
        LoadDefaultSfxIfNeeded();

        if (enterSfx != null && audioSource != null)
            GameAudioSettings.PlaySfx(audioSource, enterSfx, enterSfxVolume);
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

        var star = new GameObject("BattleMode7TeleportStar");
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

    int GetPortalIndex(Vector3Int cell)
    {
        if (portalCells == null)
            return -1;

        for (int i = 0; i < portalCells.Length; i++)
        {
            if (ToCell(portalCells[i]) == cell)
                return i;
        }

        return -1;
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

    static bool[] CaptureRendererStates(SpriteRenderer[] renderers)
    {
        if (renderers == null)
            return null;

        bool[] states = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            states[i] = renderers[i] != null && renderers[i].enabled;

        return states;
    }

    static void SetRenderersEnabled(SpriteRenderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    static void RestoreRendererStates(SpriteRenderer[] renderers, bool[] states)
    {
        if (renderers == null || states == null)
            return;

        int count = Mathf.Min(renderers.Length, states.Length);
        for (int i = 0; i < count; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = states[i];
        }
    }

    static Vector3Int ToCell(Vector2Int cell)
        => new(cell.x, cell.y, 0);

    int GetClockwiseDestinationIndex(int portalIndex)
    {
        return (portalIndex + 1) % portalCells.Length;
    }

    static float SmoothTeleportT(float t)
        => t * t * (3f - 2f * t);

    static bool IsBattleMode7Active()
        => string.Equals(SceneManager.GetActiveScene().name, BattleMode7SceneName, System.StringComparison.Ordinal);

    static Tilemap FindTilemapByName(string tilemapName)
    {
        var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null && string.Equals(tilemaps[i].name, tilemapName, System.StringComparison.OrdinalIgnoreCase))
                return tilemaps[i];
        }

        return null;
    }
}
