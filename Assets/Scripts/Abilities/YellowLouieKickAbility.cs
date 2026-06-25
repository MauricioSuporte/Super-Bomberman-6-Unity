using Assets.Scripts.Interface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class YellowLouieKickAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "YellowLouieDestructibleKick";
    const string BattleMode6SceneName = "BattleMode_6";

    [SerializeField] private bool enabledAbility = true;

    [Header("Move")]
    public float cellsPerSecond = 10f;

    [Header("Kick Timing")]
    public float kickCooldownSeconds = 0.2f;

    [Header("Chain")]
    public int maxChainTransfers = 32;

    [Header("Stop Shake (visual feedback)")]
    public float stopShakeAmplitude = 0.05f;
    public float stopShakeFrequency = 22f;

    [Header("SFX")]
    public AudioClip kickSfx;
    [Range(0f, 1f)] public float kickSfxVolume = 1f;

    [Header("Debug")]
    private bool debugKickTrace = false;

    private readonly Dictionary<Bomb, Vector2> _bombPlantDirection = new();
    private readonly HashSet<Bomb> _bombEarlyKickUnlocked = new();
    private readonly HashSet<Bomb> yellowLouieMovingBombs = new();
    private Vector2 _lastOwnerDirection = Vector2.zero;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;
    BattleMode6RedirectionController stage6RedirectionController;

    Coroutine routine;
    Coroutine kickVisualRoutine;
    bool kickActive;
    float nextAllowedKickTime;

    IYellowLouieDestructibleKickExternalAnimator externalAnimator;

    static readonly HashSet<Vector3Int> _reservedCells = new();

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    enum ChainMoverType
    {
        None = 0,
        Bomb = 1,
        Tile = 2
    }

    sealed class BombQueueTransfer
    {
        public bool hitDestructible;
        public Vector3Int destructibleCell;
        public TileBase destructibleTile;
        public readonly List<ItemPickup> pushedSkulls = new();

        public void Reset()
        {
            hitDestructible = false;
            destructibleCell = default;
            destructibleTile = null;
            pushedSkulls.Clear();
        }
    }

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;
        audioSource = GetComponent<AudioSource>();
    }

    void OnDisable() => CancelKick();
    void OnDestroy() => CancelKick();

    public void SetExternalAnimator(IYellowLouieDestructibleKickExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    public void SetKickSfx(AudioClip clip, float volume)
    {
        kickSfx = clip;
        kickSfxVolume = Mathf.Clamp01(volume);
    }

    void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (movement == null || movement.isDead)
            return;

        PruneEarlyKickState();

        Vector2 currentDir = movement.Direction != Vector2.zero
            ? movement.Direction
            : movement.FacingDirection;

        if (currentDir != Vector2.zero)
            NotifyOwnerDirectionChanged(currentDir);

        if (Time.time < nextAllowedKickTime)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        var input = PlayerInputManager.Instance;
        int pid = movement.PlayerId;
        if (input == null || !input.GetDown(pid, PlayerAction.ActionC))
            return;

        nextAllowedKickTime = Time.time + kickCooldownSeconds;

        if (routine != null)
            return;

        routine = StartCoroutine(KickRoutine());
    }

    IEnumerator KickRoutine()
    {
        if (movement == null || rb == null)
        {
            routine = null;
            yield break;
        }

        Vector2 dir = movement.Direction != Vector2.zero ? movement.Direction : movement.FacingDirection;
        if (dir == Vector2.zero)
            dir = Vector2.down;

        Vector3Int step = new Vector3Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y), 0);
        if (step == Vector3Int.zero)
        {
            routine = null;
            yield break;
        }

        StartKickVisuals(dir);

        float animEndTime = Time.time + kickCooldownSeconds;
        bool inputUnlockedAfterAnim = false;

        movement.SetInputLocked(true, false);

        var gm = FindAnyObjectByType<GameManager>();
        var destructibleTilemap = gm != null ? gm.destructibleTilemap : null;

        Vector3Int frontCell = destructibleTilemap != null
            ? destructibleTilemap.WorldToCell(rb.position) + step
            : new Vector3Int(
                Mathf.RoundToInt((rb.position.x + dir.normalized.x * movement.tileSize) / movement.tileSize),
                Mathf.RoundToInt((rb.position.y + dir.normalized.y * movement.tileSize) / movement.tileSize),
                0);

        bool hasBombInFront = TryGetBombAtCell(SnapToGrid(rb.position, movement.tileSize) + dir.normalized * movement.tileSize, out Bomb firstBomb);
        bool hasTileInFront = TryGetDestructibleAtCell(destructibleTilemap, frontCell, out TileBase firstTile);

        LogKickTrace(
            $"kick-start owner:{GetOwnerLabel()} ownerTile:{FormatVec(SnapToGrid(rb.position, movement.tileSize))} " +
            $"dir:{FormatVec(dir.normalized)} frontCell:{frontCell} firstBomb:{FormatBomb(firstBomb)} " +
            $"firstTile:{(firstTile != null ? firstTile.name : "none")}");

        if (!hasBombInFront && !hasTileInFront)
        {
            LogKickTrace("kick-cancel no-bomb-or-tile-in-front");

            yield return WaitSecondsAndReleaseInput(kickCooldownSeconds, animEndTime, () =>
            {
                if (!inputUnlockedAfterAnim)
                {
                    inputUnlockedAfterAnim = true;
                    if (movement != null)
                        movement.SetInputLocked(false);
                }
            });

            if (movement != null)
                movement.SetInputLocked(false);

            routine = null;
            yield break;
        }

        if (firstBomb != null && firstBomb.IsBeingKicked)
        {
            bool redirected = firstBomb.TryRedirectKick(dir);
            LogKickTrace(
                $"kick-redirect-moving-bomb bomb:{FormatBomb(firstBomb)} " +
                $"dir:{FormatVec(dir.normalized)} redirected:{redirected}");

            if (redirected && audioSource != null && kickSfx != null)
                GameAudioSettings.PlaySfx(audioSource, kickSfx, kickSfxVolume);

            yield return WaitSecondsAndReleaseInput(kickCooldownSeconds, animEndTime, () =>
            {
                if (!inputUnlockedAfterAnim)
                {
                    inputUnlockedAfterAnim = true;
                    if (movement != null)
                        movement.SetInputLocked(false);
                }
            });

            if (movement != null)
                movement.SetInputLocked(false);

            routine = null;
            yield break;
        }

        if (audioSource != null && kickSfx != null)
            GameAudioSettings.PlaySfx(audioSource, kickSfx, kickSfxVolume);

        yield return MixedChainRoutine(
            dir,
            destructibleTilemap,
            frontCell,
            firstBomb,
            firstTile,
            animEndTime,
            () =>
            {
                if (!inputUnlockedAfterAnim)
                {
                    inputUnlockedAfterAnim = true;
                    if (movement != null)
                        movement.SetInputLocked(false);
                }
            });

        if (movement != null)
            movement.SetInputLocked(false);

        routine = null;
    }

    IEnumerator MixedChainRoutine(
        Vector2 dir,
        Tilemap destructibleTilemap,
        Vector3Int firstCell,
        Bomb firstBomb,
        TileBase firstTile,
        float animEndTime,
        System.Action releaseInputIfNeeded)
    {
        Vector2 kickDir = dir.normalized;
        float tileSize = movement != null ? movement.tileSize : 1f;
        float stopShakeDuration = GetChainTransferDuration();
        int transfers = 0;

        ChainMoverType moverType = ChainMoverType.None;
        Bomb currentBomb = null;
        TileBase currentTile = null;
        Vector3Int currentTileCell = firstCell;
        GameObject ghost = null;
        bool tileRemovedFromMap = false;

        GameObject currentCellBlocker = null;
        GameObject nextCellBlocker = null;

        var reservedLocal = new HashSet<Vector3Int>();

        System.Action<Vector3Int> reserve = c =>
        {
            _reservedCells.Add(c);
            reservedLocal.Add(c);
        };

        System.Action<Vector3Int> release = c =>
        {
            _reservedCells.Remove(c);
            reservedLocal.Remove(c);
        };

        void ReleaseInputIfNeeded()
        {
            if (Time.time >= animEndTime)
                releaseInputIfNeeded?.Invoke();
        }

        void RefreshCurrentCellBlockerPosition(Vector3Int cell)
        {
            if (currentCellBlocker != null && destructibleTilemap != null)
                currentCellBlocker.transform.position = destructibleTilemap.GetCellCenterWorld(cell);
        }

        void DestroyNextCellBlocker()
        {
            if (nextCellBlocker != null)
            {
                Destroy(nextCellBlocker);
                nextCellBlocker = null;
            }
        }

        void DestroyCurrentCellBlocker()
        {
            if (currentCellBlocker != null)
            {
                Destroy(currentCellBlocker);
                currentCellBlocker = null;
            }
        }

        void BeginTileMover(Vector3Int cell, TileBase tile)
        {
            currentTileCell = cell;
            currentTile = tile;

            destructibleTilemap.SetTile(cell, null);
            destructibleTilemap.RefreshTile(cell);
            tileRemovedFromMap = true;

            if (ghost != null)
                Destroy(ghost);

            ghost = CreateGhost(destructibleTilemap, cell, tile);

            reserve(cell);
            ApplyShadowForCell(cell);

            DestroyCurrentCellBlocker();
            currentCellBlocker = CreateCellBlocker(destructibleTilemap.GetCellCenterWorld(cell), "YellowKickBlock_CurrentCell");

            moverType = ChainMoverType.Tile;
        }

        void SettleCurrentTileAtCurrentCell()
        {
            if (destructibleTilemap == null || !tileRemovedFromMap || currentTile == null)
                return;

            destructibleTilemap.SetTile(currentTileCell, currentTile);
            destructibleTilemap.RefreshTile(currentTileCell);
            tileRemovedFromMap = false;

            release(currentTileCell);
            ApplyShadowForCell(currentTileCell);

            DestroyNextCellBlocker();
            DestroyCurrentCellBlocker();

            if (ghost != null)
            {
                Destroy(ghost);
                ghost = null;
            }
        }

        try
        {
            if (firstBomb != null)
            {
                moverType = ChainMoverType.Bomb;
                currentBomb = firstBomb;
            }
            else if (firstTile != null && destructibleTilemap != null)
            {
                BeginTileMover(firstCell, firstTile);
            }
            else
            {
                yield return WaitSecondsAndReleaseInput(kickCooldownSeconds, animEndTime, releaseInputIfNeeded);
                yield break;
            }

            while (enabledAbility && movement != null && !movement.isDead && moverType != ChainMoverType.None)
            {
                ReleaseInputIfNeeded();

                transfers++;
                if (transfers > maxChainTransfers)
                    break;

                if (moverType == ChainMoverType.Tile)
                {
                    Vector3Int nextCell = currentTileCell + new Vector3Int(Mathf.RoundToInt(kickDir.x), Mathf.RoundToInt(kickDir.y), 0);

                    bool hasBombAhead = TryGetBombAtCell(destructibleTilemap.GetCellCenterWorld(nextCell), out Bomb nextBomb);
                    bool hasTileAhead = TryGetDestructibleAtCell(destructibleTilemap, nextCell, out TileBase nextTile);

                    if (hasBombAhead && nextBomb != null)
                    {
                        SettleCurrentTileAtCurrentCell();
                        StartCoroutine(ShakeSettledTileVisual(
                            destructibleTilemap,
                            currentTileCell,
                            currentTile,
                            stopShakeDuration,
                            stopShakeAmplitude,
                            stopShakeFrequency));

                        currentBomb = nextBomb;
                        moverType = ChainMoverType.Bomb;
                        continue;
                    }

                    if (hasTileAhead && nextTile != null)
                    {
                        SettleCurrentTileAtCurrentCell();
                        StartCoroutine(ShakeSettledTileVisual(
                            destructibleTilemap,
                            currentTileCell,
                            currentTile,
                            stopShakeDuration,
                            stopShakeAmplitude,
                            stopShakeFrequency));

                        BeginTileMover(nextCell, nextTile);
                        continue;
                    }

                    if (HasEnemyAt(destructibleTilemap.GetCellCenterWorld(nextCell)))
                    {
                        SettleCurrentTileAtCurrentCell();
                        yield return ShakeSettledTileVisual(
                            destructibleTilemap,
                            currentTileCell,
                            currentTile,
                            stopShakeDuration,
                            stopShakeAmplitude,
                            stopShakeFrequency);

                        break;
                    }

                    if (IsMixedChainSolidAt(destructibleTilemap.GetCellCenterWorld(nextCell), kickDir, currentBomb))
                    {
                        SettleCurrentTileAtCurrentCell();
                        yield return ShakeSettledTileVisual(
                            destructibleTilemap,
                            currentTileCell,
                            currentTile,
                            stopShakeDuration,
                            stopShakeAmplitude,
                            stopShakeFrequency);

                        break;
                    }

                    reserve(nextCell);
                    ApplyShadowForCell(nextCell);

                    DestroyNextCellBlocker();
                    nextCellBlocker = CreateCellBlocker(destructibleTilemap.GetCellCenterWorld(nextCell), "YellowKickBlock_NextCell");

                    Vector3 from = destructibleTilemap.GetCellCenterWorld(currentTileCell);
                    Vector3 to = destructibleTilemap.GetCellCenterWorld(nextCell);

                    float stepSeconds = cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);
                    float tMove = 0f;
                    bool characterEnteredDestinationDuringMove = false;

                    while (tMove < 1f)
                    {
                        if (!enabledAbility || movement == null || movement.isDead)
                            break;

                        ReleaseInputIfNeeded();

                        if (HasPlayerAt(to) || HasEnemyAt(to))
                        {
                            characterEnteredDestinationDuringMove = true;
                            break;
                        }

                        tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);

                        if (ghost != null)
                            ghost.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(tMove));

                        yield return null;
                    }

                    if (characterEnteredDestinationDuringMove)
                    {
                        release(nextCell);
                        ApplyShadowForCell(nextCell);
                        DestroyNextCellBlocker();

                        if (ghost != null)
                            ghost.transform.position = from;

                        RefreshCurrentCellBlockerPosition(currentTileCell);
                        SettleCurrentTileAtCurrentCell();

                        yield return ShakeSettledTileVisual(
                            destructibleTilemap,
                            currentTileCell,
                            currentTile,
                            stopShakeDuration,
                            stopShakeAmplitude,
                            stopShakeFrequency);

                        break;
                    }

                    release(currentTileCell);
                    ApplyShadowForCell(currentTileCell);

                    currentTileCell = nextCell;

                    if (ghost != null)
                        ghost.transform.position = destructibleTilemap.GetCellCenterWorld(currentTileCell);

                    DestroyCurrentCellBlocker();
                    currentCellBlocker = nextCellBlocker;
                    nextCellBlocker = null;
                    RefreshCurrentCellBlockerPosition(currentTileCell);

                    continue;
                }

                if (moverType == ChainMoverType.Bomb)
                {
                    if (currentBomb == null)
                        break;

                    var transfer = new BombQueueTransfer();
                    yield return BombQueueRoutine(currentBomb, kickDir, destructibleTilemap, animEndTime, ReleaseInputIfNeeded, transfer);

                    if (transfer.hitDestructible && transfer.destructibleTile != null)
                    {
                        BeginTileMover(transfer.destructibleCell, transfer.destructibleTile);
                        currentBomb = null;
                        moverType = ChainMoverType.Tile;
                        continue;
                    }

                    break;
                }
            }

            float finalWait = Mathf.Max(0f, animEndTime - Time.time);
            if (finalWait > 0f)
            {
                yield return WaitSecondsAndReleaseInput(finalWait, animEndTime, releaseInputIfNeeded);
            }
            else
            {
                releaseInputIfNeeded?.Invoke();
            }
        }
        finally
        {
            if (ghost != null)
                Destroy(ghost);

            DestroyNextCellBlocker();
            DestroyCurrentCellBlocker();

            foreach (var c in reservedLocal)
                _reservedCells.Remove(c);

            if (destructibleTilemap != null && tileRemovedFromMap && currentTile != null)
            {
                destructibleTilemap.SetTile(currentTileCell, currentTile);
                destructibleTilemap.RefreshTile(currentTileCell);
            }

            if (destructibleTilemap != null)
                ApplyShadowForCell(currentTileCell);
        }
    }

    bool TryGetBombAtCell(Vector2 worldCellCenter, out Bomb bomb)
    {
        bomb = null;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer < 0)
            return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            worldCellCenter,
            Vector2.one * (movement.tileSize * 0.6f),
            0f,
            1 << bombLayer);

        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            var foundBomb = hit.GetComponent<Bomb>();
            if (foundBomb == null)
                continue;

            bomb = foundBomb;
            return true;
        }

        return false;
    }

    bool TryGetDestructibleAtCell(Tilemap destructibleTilemap, Vector3Int cell, out TileBase tile)
    {
        tile = null;

        if (destructibleTilemap == null)
            return false;

        tile = destructibleTilemap.GetTile(cell);
        if (tile == null)
            return false;

        return true;
    }

    IEnumerator BombQueueRoutine(
        Bomb firstBomb,
        Vector2 kickDir,
        Tilemap destructibleTilemap,
        float animEndTime,
        System.Action releaseInputIfNeeded,
        BombQueueTransfer transfer)
    {
        if (firstBomb == null || movement == null)
            yield break;

        kickDir = kickDir.normalized;
        float tileSize = movement.tileSize;
        float stepSeconds = cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);
        var queue = new List<Bomb>(8) { firstBomb };
        var queueDirections = new List<Vector2>(8) { kickDir };
        int segment = 0;
        SetYellowLouieBombMoving(firstBomb);

        LogKickTrace(
            $"bomb-queue-start dir:{FormatVec(kickDir)} first:{FormatBomb(firstBomb)} " +
            $"remainingFuse:{FormatFuse(firstBomb)}");

        while (enabledAbility && movement != null && !movement.isDead)
        {
            releaseInputIfNeeded?.Invoke();

            ExtendBombQueue(queue, queueDirections, tileSize);
            for (int i = 0; i < queue.Count; i++)
                SetYellowLouieBombMoving(queue[i]);

            if (queue.Count == 0 || queueDirections.Count != queue.Count)
            {
                LogKickTrace("bomb-queue-stop empty-queue");
                ClearYellowLouieBombMovement();
                yield break;
            }

            Vector2 frontDirection = queueDirections[queueDirections.Count - 1];
            if (!CanMoveBombQueue(queue, queueDirections, tileSize, destructibleTilemap, transfer))
            {
                Bomb front = queue[queue.Count - 1];
                Vector2 frontCell = front != null ? SnapToGrid(front.transform.position, tileSize) : Vector2.zero;
                Vector2 nextCell = frontCell + frontDirection * tileSize;

                if (front != null &&
                    front.IsRubberBomb &&
                    (transfer == null || !transfer.hitDestructible) &&
                    TryReverseRubberBombQueue(queue, queueDirections, tileSize, destructibleTilemap, transfer))
                {
                    LogKickTrace(
                        $"bomb-queue-rubber-bounce segment:{segment} queue:{FormatBombQueue(queue)} " +
                        $"newDir:{FormatVec(queueDirections[queueDirections.Count - 1])} blocked:{FormatVec(nextCell)}");
                    front.PlayKickBounceSfx();
                    continue;
                }

                LogKickTrace(
                    $"bomb-queue-stop blocked segment:{segment} queue:{FormatBombQueue(queue)} " +
                    $"front:{FormatBomb(front)} next:{FormatVec(nextCell)} " +
                    $"hitDestructible:{(transfer != null && transfer.hitDestructible)} " +
                    $"destructibleCell:{(transfer != null ? transfer.destructibleCell.ToString() : "none")}");
                ClearYellowLouieBombMovement();
                yield break;
            }

            var starts = new Vector2[queue.Count];
            var ends = new Vector2[queue.Count];
            var bodies = new Rigidbody2D[queue.Count];
            var frontBomb = queue[queue.Count - 1];
            var frontStart = SnapToGrid(frontBomb.transform.position, tileSize);
            frontDirection = queueDirections[queueDirections.Count - 1];
            var frontCollider = frontBomb.GetComponent<Collider2D>();

            for (int i = 0; i < queue.Count; i++)
            {
                Bomb bomb = queue[i];
                if (bomb == null || bomb.HasExploded)
                {
                    ClearYellowLouieBombMovement();
                    yield break;
                }

                bomb.MarkMovedByKickOrPunch();
                starts[i] = SnapToGrid(bomb.transform.position, tileSize);
                ends[i] = starts[i] + queueDirections[i] * tileSize;
                bodies[i] = bomb.GetComponent<Rigidbody2D>();
            }

            StartBombQueuePushedSkulls(transfer, frontDirection, tileSize, frontCollider, frontStart);

            LogKickTrace(
                $"bomb-queue-segment segment:{segment} queue:{FormatBombQueue(queue)} " +
                $"from:{FormatVec(starts[0])} to:{FormatVec(ends[0])} " +
                $"frontTo:{FormatVec(ends[ends.Length - 1])} firstFuse:{FormatFuse(queue[0])}");

            float tMove = 0f;
            bool hitMovingBomb = false;
            bool hitPlayer = false;
            while (tMove < 1f)
            {
                if (!enabledAbility || movement == null || movement.isDead)
                {
                    ClearYellowLouieBombMovement();
                    yield break;
                }

                if (Time.time >= animEndTime)
                    releaseInputIfNeeded?.Invoke();

                if (HasMovingBombOutsideQueueAtCell(ends[ends.Length - 1], queue))
                {
                    hitMovingBomb = true;
                    break;
                }

                if (HasPlayerAt(ends[ends.Length - 1]))
                {
                    hitPlayer = true;
                    break;
                }

                tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);
                float t = Mathf.Clamp01(tMove);

                for (int i = 0; i < queue.Count; i++)
                {
                    Bomb bomb = queue[i];
                    if (bomb == null || bomb.HasExploded)
                        continue;

                    Vector2 pos = Vector2.Lerp(starts[i], ends[i], t);
                    if (bodies[i] != null)
                        bodies[i].MovePosition(pos);

                    bomb.transform.position = pos;
                }

                Vector2 frontPosition = Vector2.Lerp(frontStart, ends[ends.Length - 1], t);
                DestroyNonSkullItemsAtWorld(frontPosition);
                UpdateBombQueuePushedSkulls(transfer, frontDirection, tileSize, frontCollider, frontStart, t);

                yield return null;
            }

            if (hitMovingBomb || hitPlayer)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    Bomb bomb = queue[i];
                    if (bomb != null && !bomb.HasExploded)
                        bomb.ForceSetLogicalPosition(starts[i]);
                }

                FinishBombQueuePushedSkulls(transfer, frontDirection, tileSize, frontCollider, frontStart);

                if (frontBomb != null &&
                    frontBomb.IsRubberBomb &&
                    TryReverseRubberBombQueue(queue, queueDirections, tileSize, destructibleTilemap, transfer))
                {
                    LogKickTrace(
                        $"bomb-queue-rubber-moving-bomb-bounce segment:{segment} " +
                        $"queue:{FormatBombQueue(queue)} newDir:{FormatVec(queueDirections[queueDirections.Count - 1])}");
                    frontBomb.PlayKickBounceSfx();
                    continue;
                }

                LogKickTrace(
                    $"bomb-queue-stop dynamic-collision segment:{segment} " +
                    $"reason:{(hitPlayer ? "player" : "moving-bomb")} " +
                    $"queue:{FormatBombQueue(queue)} target:{FormatVec(ends[ends.Length - 1])}");
                ClearYellowLouieBombMovement();
                yield break;
            }

            for (int i = 0; i < queue.Count; i++)
            {
                Bomb bomb = queue[i];
                if (bomb == null || bomb.HasExploded)
                    continue;

                bomb.ForceSetLogicalPosition(ends[i]);
                _bombPlantDirection.Remove(bomb);
                _bombEarlyKickUnlocked.Remove(bomb);
            }

            FinishBombQueuePushedSkulls(transfer, frontDirection, tileSize, frontCollider, ends[ends.Length - 1]);
            DestroyNonSkullItemsAtWorld(ends[ends.Length - 1]);

            LogKickTrace(
                $"bomb-queue-segment-done segment:{segment} queue:{FormatBombQueue(queue)} " +
                $"logicalFirst:{FormatVec(queue[0] != null ? queue[0].GetLogicalPosition() : Vector2.zero)} " +
                $"firstFuse:{FormatFuse(queue[0])}");

            for (int i = 0; i < queue.Count; i++)
            {
                Vector2 nextDirection = queueDirections[i];
                TryApplyStage6Redirection(queue[i], ref nextDirection, tileSize);
                queueDirections[i] = nextDirection;
            }

            segment++;
        }

        ClearYellowLouieBombMovement();
    }

    void TryApplyStage6Redirection(Bomb frontBomb, ref Vector2 kickDir, float tileSize)
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, BattleMode6SceneName, System.StringComparison.Ordinal))
            return;

        if (frontBomb == null || frontBomb.HasExploded)
            return;

        if (stage6RedirectionController == null || !stage6RedirectionController.isActiveAndEnabled)
            stage6RedirectionController = FindAnyObjectByType<BattleMode6RedirectionController>();

        if (stage6RedirectionController == null || !stage6RedirectionController.isActiveAndEnabled)
            return;

        Vector2 frontCell = SnapToGrid(frontBomb.transform.position, tileSize);
        var tile = new Vector2Int(
            Mathf.RoundToInt(frontCell.x / Mathf.Max(0.0001f, tileSize)),
            Mathf.RoundToInt(frontCell.y / Mathf.Max(0.0001f, tileSize)));

        if (!stage6RedirectionController.TryGetRedirection(tile, out Vector2Int redirectedDirection) ||
            redirectedDirection == Vector2Int.zero)
        {
            return;
        }

        Vector2 nextKickDir = new(redirectedDirection.x, redirectedDirection.y);
        if (nextKickDir == kickDir)
            return;

        LogKickTrace(
            $"bomb-queue-stage6-redirect bomb:{FormatBomb(frontBomb)} " +
            $"tile:{tile} from:{FormatVec(kickDir)} to:{FormatVec(nextKickDir)}");

        kickDir = nextKickDir;
    }

    void ExtendBombQueue(List<Bomb> queue, List<Vector2> queueDirections, float tileSize)
    {
        if (queue == null || queueDirections == null || queue.Count == 0 || queueDirections.Count != queue.Count)
            return;

        var seen = new HashSet<Bomb>(queue);

        while (queue.Count < Mathf.Max(1, maxChainTransfers))
        {
            Bomb front = queue[queue.Count - 1];
            if (front == null)
                return;

            Vector2 kickDir = queueDirections[queueDirections.Count - 1];
            Vector2 frontCell = SnapToGrid(front.transform.position, tileSize);
            Vector2 nextCell = frontCell + kickDir * tileSize;

            if (!TryGetBombAtCell(nextCell, out Bomb nextBomb))
                return;

            if (nextBomb == null || seen.Contains(nextBomb))
                return;

            if (!CanStartYellowBombKick(nextBomb, kickDir, unlockEarlyKick: false))
                return;

            queue.Add(nextBomb);
            queueDirections.Add(kickDir);
            seen.Add(nextBomb);
        }
    }

    bool CanMoveBombQueue(
        List<Bomb> queue,
        List<Vector2> queueDirections,
        float tileSize,
        Tilemap destructibleTilemap,
        BombQueueTransfer transfer,
        bool validateKickEligibility = true)
    {
        if (transfer != null)
            transfer.Reset();

        if (queue == null || queueDirections == null || queue.Count == 0 || queueDirections.Count != queue.Count)
            return false;

        for (int i = 0; validateKickEligibility && i < queue.Count; i++)
        {
            Bomb bomb = queue[i];
            Vector2 kickDir = queueDirections[i];
            if (bomb == null ||
                (bomb.IsBeingKicked &&
                 !bomb.IsBeingMovedByYellowLouie) ||
                (!bomb.IsBeingMovedByYellowLouie &&
                 !CanStartYellowBombKick(bomb, kickDir, unlockEarlyKick: true)))
            {
                return false;
            }
        }

        Bomb front = queue[queue.Count - 1];
        if (front == null)
            return false;

        Vector2 frontDirection = queueDirections[queueDirections.Count - 1];
        Vector2 frontCell = SnapToGrid(front.transform.position, tileSize);
        Vector2 nextCellWorld = frontCell + frontDirection * tileSize;
        Vector3Int nextTileCell = new Vector3Int(
            Mathf.RoundToInt(nextCellWorld.x / tileSize),
            Mathf.RoundToInt(nextCellWorld.y / tileSize),
            0);

        if (TryGetBombAtCell(nextCellWorld, out Bomb blockingBomb) &&
            blockingBomb != null &&
            !queue.Contains(blockingBomb))
            return false;

        if (TryGetDestructibleAtCell(destructibleTilemap, nextTileCell, out TileBase nextTile))
        {
            if (transfer != null)
            {
                transfer.hitDestructible = true;
                transfer.destructibleCell = nextTileCell;
                transfer.destructibleTile = nextTile;
            }

            return false;
        }

        if (HasEnemyAt(nextCellWorld))
            return false;

        CollectSkullsAt(nextCellWorld, out List<ItemPickup> pushedSkulls);

        if (transfer != null)
        {
            transfer.pushedSkulls.Clear();
            for (int i = 0; i < pushedSkulls.Count; i++)
            {
                if (pushedSkulls[i] != null && !transfer.pushedSkulls.Contains(pushedSkulls[i]))
                    transfer.pushedSkulls.Add(pushedSkulls[i]);
            }
        }

        if (IsMixedChainSolidAt(nextCellWorld, frontDirection, front))
            return false;

        return true;
    }

    bool CanStartYellowBombKick(Bomb bomb, Vector2 dir, bool unlockEarlyKick)
    {
        if (bomb == null)
            return false;

        if (bomb.IsBeingKicked)
            return false;

        if (!bomb.CanBeKicked && !bomb.CanBeKickedEarly)
            return false;

        dir = dir.normalized;

        if (!bomb.IsSolid && _bombPlantDirection.TryGetValue(bomb, out var plantDir))
        {
            plantDir = plantDir.normalized;

            if (!_bombEarlyKickUnlocked.Contains(bomb))
            {
                if (Vector2.Dot(plantDir, dir) < -0.9f)
                {
                    if (unlockEarlyKick)
                        _bombEarlyKickUnlocked.Add(bomb);
                }
                else
                {
                    return false;
                }
            }
        }

        return true;
    }

    bool IsMixedChainSolidAt(Vector2 nextCell, Vector2 dir, Bomb currentBomb)
    {
        if (movement == null)
            return true;

        if (currentBomb == null && HasBlockingNonSkullItemAt(nextCell, out _))
            return true;

        if (HasPlayerAt(nextCell))
            return true;

        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(movement.tileSize * 0.6f, movement.tileSize * 0.2f)
            : new Vector2(movement.tileSize * 0.2f, movement.tileSize * 0.6f);

        int mask = movement.obstacleMask.value;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int bombLayer = LayerMask.NameToLayer("Bomb");

        if (enemyLayer >= 0)
            mask |= 1 << enemyLayer;

        Collider2D[] hits = Physics2D.OverlapBoxAll(nextCell, size, 0f, mask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (currentBomb != null && hit.gameObject == currentBomb.gameObject)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (bombLayer >= 0 && hit.gameObject.layer == bombLayer)
                continue;

            if (hit.gameObject.name == "KickBombOriginBlocker" || hit.gameObject.name == "MagnetBombOriginBlocker")
                continue;

            if (hit.gameObject.name == "YellowKickBlock_CurrentCell" || hit.gameObject.name == "YellowKickBlock_NextCell")
                continue;

            ItemPickup pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (pickup != null &&
                (currentBomb != null || pickup.type == ItemType.Skull))
            {
                continue;
            }

            if (hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    bool TryReverseRubberBombQueue(
        List<Bomb> queue,
        List<Vector2> queueDirections,
        float tileSize,
        Tilemap destructibleTilemap,
        BombQueueTransfer transfer)
    {
        if (queue == null || queueDirections == null || queue.Count == 0 || queueDirections.Count != queue.Count)
            return false;

        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i] == null ||
                queue[i].HasExploded ||
                (queue[i].IsBeingKicked &&
                 !queue[i].IsBeingMovedByYellowLouie))
            {
                return false;
            }
        }

        var originalDirections = new Vector2[queueDirections.Count];
        for (int i = 0; i < queueDirections.Count; i++)
            originalDirections[i] = queueDirections[i];

        queue.Reverse();
        queueDirections.Reverse();
        for (int i = 0; i < queueDirections.Count; i++)
            queueDirections[i] = -queueDirections[i];

        if (!CanMoveBombQueue(
                queue,
                queueDirections,
                tileSize,
                destructibleTilemap,
                transfer,
                validateKickEligibility: false))
        {
            queue.Reverse();
            queueDirections.Clear();
            for (int i = 0; i < originalDirections.Length; i++)
                queueDirections.Add(originalDirections[i]);
            return false;
        }

        for (int i = 0; i < queue.Count; i++)
        {
            Bomb bomb = queue[i];
            bomb.MarkMovedByKickOrPunch();
            _bombPlantDirection.Remove(bomb);
            _bombEarlyKickUnlocked.Remove(bomb);
        }

        return true;
    }

    void StartBombQueuePushedSkulls(
        BombQueueTransfer transfer,
        Vector2 kickDir,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 frontSegmentStart)
    {
        if (transfer == null || transfer.pushedSkulls.Count == 0)
            return;

        for (int i = transfer.pushedSkulls.Count - 1; i >= 0; i--)
        {
            ItemPickup skull = transfer.pushedSkulls[i];
            if (skull == null ||
                !skull.StartKickedBombPushSegment(kickDir, tileSize, ignoredCollider, frontSegmentStart, i + 1))
            {
                transfer.pushedSkulls.RemoveAt(i);
            }
        }
    }

    void UpdateBombQueuePushedSkulls(
        BombQueueTransfer transfer,
        Vector2 kickDir,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 frontSegmentStart,
        float progress)
    {
        if (transfer == null || transfer.pushedSkulls.Count == 0)
            return;

        for (int i = transfer.pushedSkulls.Count - 1; i >= 0; i--)
        {
            ItemPickup skull = transfer.pushedSkulls[i];
            if (skull == null ||
                !skull.UpdateKickedBombPushSegment(kickDir, tileSize, ignoredCollider, frontSegmentStart, progress, i + 1))
            {
                transfer.pushedSkulls.RemoveAt(i);
            }
        }
    }

    void FinishBombQueuePushedSkulls(
        BombQueueTransfer transfer,
        Vector2 kickDir,
        float tileSize,
        Collider2D ignoredCollider,
        Vector2 frontBombWorldCenter)
    {
        if (transfer == null || transfer.pushedSkulls.Count == 0)
            return;

        for (int i = transfer.pushedSkulls.Count - 1; i >= 0; i--)
        {
            ItemPickup skull = transfer.pushedSkulls[i];
            if (skull == null)
                continue;

            skull.TryMoveSkullInFrontOfKickedBomb(
                kickDir,
                tileSize,
                ignoredCollider,
                frontBombWorldCenter,
                i + 1,
                finishPush: true);
        }

        transfer.pushedSkulls.Clear();
    }

    float GetChainTransferDuration()
    {
        return cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);
    }

    Vector2 SnapToGrid(Vector2 worldPos, float tileSize)
    {
        tileSize = Mathf.Max(0.0001f, tileSize);
        worldPos.x = Mathf.Round(worldPos.x / tileSize) * tileSize;
        worldPos.y = Mathf.Round(worldPos.y / tileSize) * tileSize;
        return worldPos;
    }

    void StartKickVisuals(Vector2 dir)
    {
        kickActive = true;

        if (kickVisualRoutine != null)
            StopCoroutine(kickVisualRoutine);

        externalAnimator?.Play(dir);

        kickVisualRoutine = StartCoroutine(StopKickVisualsAfter(kickCooldownSeconds));
    }

    IEnumerator StopKickVisualsAfter(float seconds)
    {
        float end = Time.time + Mathf.Max(0.01f, seconds);

        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead)
                break;

            yield return null;
        }

        StopKickVisuals();
    }

    void StopKickVisuals()
    {
        if (!kickActive)
            return;

        kickActive = false;

        externalAnimator?.Stop();

        if (kickVisualRoutine != null)
        {
            StopCoroutine(kickVisualRoutine);
            kickVisualRoutine = null;
        }
    }

    IEnumerator WaitSecondsAndReleaseInput(float seconds, float animEndTime, System.Action releaseInputIfNeeded)
    {
        float end = Time.time + Mathf.Max(0f, seconds);
        while (Time.time < end)
        {
            if (Time.time >= animEndTime)
                releaseInputIfNeeded?.Invoke();

            yield return null;
        }

        if (Time.time >= animEndTime)
            releaseInputIfNeeded?.Invoke();
    }

    IEnumerator ShakeGhost(GameObject ghost, Vector3 basePos, float duration, float amplitude, float frequencyHz)
    {
        float dur = Mathf.Max(0.01f, duration);
        float amp = Mathf.Max(0f, amplitude);
        float hz = Mathf.Max(1f, frequencyHz);

        float end = Time.time + dur;
        float seed = Random.value * 1000f;

        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead || ghost == null)
                yield break;

            float t = Time.time - (end - dur);
            float phase = (t * hz) * (Mathf.PI * 2f);

            float x = Mathf.Sin(phase + seed) * amp;
            float y = Mathf.Cos(phase * 1.23f + seed) * amp;

            ghost.transform.position = basePos + new Vector3(x, y, 0f);
            yield return null;
        }

        if (ghost != null)
            ghost.transform.position = basePos;
    }

    GameObject CreateGhost(Tilemap tilemap, Vector3Int cell, TileBase tile)
    {
        GameObject ghost = new("YellowKickBlock_Ghost");
        ghost.transform.position = tilemap.GetCellCenterWorld(cell);

        int stageLayer = LayerMask.NameToLayer("Stage");
        if (stageLayer >= 0)
            ghost.layer = stageLayer;

        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        sr.sprite = GetPreviewSprite(tile);

        var col = ghost.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        float ts = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        col.size = new Vector2(ts * 0.90f, ts * 0.90f);
        col.offset = Vector2.zero;

        return ghost;
    }

    GameObject CreateCellBlocker(Vector3 worldCenter, string objectName)
    {
        GameObject blocker = new(objectName);
        blocker.transform.position = worldCenter;

        int stageLayer = LayerMask.NameToLayer("Stage");
        if (stageLayer >= 0)
            blocker.layer = stageLayer;

        var col = blocker.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        float ts = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        col.size = new Vector2(ts * 0.90f, ts * 0.90f);
        col.offset = Vector2.zero;

        return blocker;
    }

    Sprite GetPreviewSprite(TileBase tile)
    {
        if (tile == null)
            return null;

        if (tile is Tile t && t.sprite != null)
            return t.sprite;

        if (tile is AnimatedTile at && at.m_AnimatedSprites != null && at.m_AnimatedSprites.Length > 0)
            return at.m_AnimatedSprites[0];

        return null;
    }

    void ApplyShadowForCell(Vector3Int cell)
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm == null || gm.groundTilemap == null || gm.groundTile == null || gm.groundShadowTile == null)
            return;

        if (gm.destructibleTilemap == null)
            return;

        var below = new Vector3Int(cell.x, cell.y - 1, cell.z);
        var currentGround = gm.groundTilemap.GetTile(below);

        bool hasBlock = gm.destructibleTilemap.GetTile(cell) != null || _reservedCells.Contains(cell);

        if (hasBlock)
        {
            if (currentGround == gm.groundTile)
                gm.groundTilemap.SetTile(below, gm.groundShadowTile);
        }
        else
        {
            if (currentGround == gm.groundShadowTile)
                gm.groundTilemap.SetTile(below, gm.groundTile);
        }
    }

    bool HasItemAt(Vector3 center)
    {
        return HasBlockingNonSkullItemAt(center, out _);
    }

    void CollectSkullsAt(Vector3 center, out List<ItemPickup> skulls)
    {
        skulls = new List<ItemPickup>();

        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer < 0)
            return;

        float tileSize = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        float size = tileSize * 0.55f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            center,
            new Vector2(size, size),
            0f,
            1 << itemLayer);

        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            ItemPickup pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (pickup != null &&
                pickup.type == ItemType.Skull &&
                !skulls.Contains(pickup))
            {
                skulls.Add(pickup);
            }
        }
    }

    void DestroyNonSkullItemsAtWorld(Vector2 worldCenter)
    {
        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer < 0)
            return;

        float tileSize = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        float size = tileSize * 0.45f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            worldCenter,
            new Vector2(size, size),
            0f,
            1 << itemLayer);

        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            ItemPickup pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (pickup == null || pickup.type == ItemType.Skull)
                continue;

            pickup.DestroyFromMovingBombImpact();
        }
    }

    bool HasBlockingNonSkullItemAt(Vector3 center, out List<ItemPickup> skulls)
    {
        skulls = new List<ItemPickup>();

        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer < 0)
            return false;

        float ts = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        float s = ts * 0.55f;

        var hits = Physics2D.OverlapBoxAll(center, new Vector2(s, s), 0f, 1 << itemLayer);
        if (hits == null || hits.Length == 0)
            return false;

        bool foundAnyItem = false;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            ItemPickup pickup = hit.GetComponent<ItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<ItemPickup>();

            if (pickup == null)
                continue;

            foundAnyItem = true;
            if (pickup.type != ItemType.Skull)
                return true;

            if (!skulls.Contains(pickup))
                skulls.Add(pickup);
        }

        return foundAnyItem && skulls.Count == 0;
    }

    bool HasPlayerAt(Vector3 center)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer < 0)
            return false;

        return HasAnyColliderAt(center, 1 << playerLayer);
    }

    bool HasEnemyAt(Vector3 center)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
            return false;

        return HasAnyColliderAt(center, 1 << enemyLayer);
    }

    bool HasAnyColliderAt(Vector3 center, int mask)
    {
        if (mask == 0)
            return false;

        float ts = movement != null ? Mathf.Max(0.1f, movement.tileSize) : 1f;
        float s = ts * 0.55f;

        var hits = Physics2D.OverlapBoxAll(center, new Vector2(s, s), 0f, mask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            return true;
        }

        return false;
    }

    bool HasMovingBombOutsideQueueAtCell(Vector2 worldCellCenter, List<Bomb> queue)
    {
        if (movement == null)
            return false;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer < 0)
            return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            worldCellCenter,
            Vector2.one * (movement.tileSize * 0.6f),
            0f,
            1 << bombLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Bomb bomb = hit.GetComponent<Bomb>() ?? hit.GetComponentInParent<Bomb>();
            if (bomb != null &&
                bomb.IsBeingKicked &&
                (queue == null || !queue.Contains(bomb)))
            {
                return true;
            }
        }

        return false;
    }

    void SetYellowLouieBombMoving(Bomb bomb)
    {
        if (bomb == null || bomb.HasExploded || yellowLouieMovingBombs.Contains(bomb))
            return;

        yellowLouieMovingBombs.Add(bomb);
        bomb.SetYellowLouieKickMovement(true);
    }

    void ClearYellowLouieBombMovement()
    {
        foreach (Bomb bomb in yellowLouieMovingBombs)
        {
            if (bomb != null)
                bomb.SetYellowLouieKickMovement(false);
        }

        yellowLouieMovingBombs.Clear();
    }

    void CancelKick()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopKickVisuals();
        ClearYellowLouieBombMovement();

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;
        CancelKick();

        _bombPlantDirection.Clear();
        _bombEarlyKickUnlocked.Clear();
        _lastOwnerDirection = Vector2.zero;
    }

    public void CancelKickForDeath()
    {
        enabledAbility = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ClearYellowLouieBombMovement();

        if (kickVisualRoutine != null)
        {
            StopCoroutine(kickVisualRoutine);
            kickVisualRoutine = null;
        }

        kickActive = false;
        externalAnimator?.Stop();

        if (movement != null)
            movement.SetInputLocked(false);

        externalAnimator = null;
    }

    GameObject CreateVisualGhost(Tilemap tilemap, Vector3Int cell, TileBase tile, int extraSortingOrder = 1)
    {
        if (tilemap == null || tile == null)
            return null;

        GameObject ghost = new("YellowKickBlock_StopShakeFx");
        ghost.transform.position = tilemap.GetCellCenterWorld(cell);

        int stageLayer = LayerMask.NameToLayer("Stage");
        if (stageLayer >= 0)
            ghost.layer = stageLayer;

        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = GetPreviewSprite(tile);

        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
        {
            sr.sortingLayerID = tilemapRenderer.sortingLayerID;
            sr.sortingOrder = tilemapRenderer.sortingOrder + extraSortingOrder;
        }
        else
        {
            sr.sortingOrder = 11;
        }

        return ghost;
    }

    IEnumerator ShakeSettledTileVisual(Tilemap tilemap, Vector3Int cell, TileBase tile, float duration, float amplitude, float frequencyHz)
    {
        if (tilemap == null || tile == null)
            yield break;

        GameObject visual = CreateVisualGhost(tilemap, cell, tile, 1);
        if (visual == null)
            yield break;

        Vector3 basePos = tilemap.GetCellCenterWorld(cell);

        float dur = Mathf.Max(0.01f, duration);
        float amp = Mathf.Max(0f, amplitude);
        float hz = Mathf.Max(1f, frequencyHz);

        float end = Time.time + dur;
        float seed = Random.value * 1000f;

        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead || visual == null)
            {
                if (visual != null)
                    Destroy(visual);
                yield break;
            }

            float t = Time.time - (end - dur);
            float phase = (t * hz) * (Mathf.PI * 2f);

            float x = Mathf.Sin(phase + seed) * amp;
            float y = Mathf.Cos(phase * 1.23f + seed) * amp;

            visual.transform.position = basePos + new Vector3(x, y, 0f);
            yield return null;
        }

        if (visual != null)
        {
            visual.transform.position = basePos;
            Destroy(visual);
        }
    }

    public void NotifyBombPlanted(Bomb bomb, Vector2 movementDirectionAtPlant)
    {
        if (bomb == null)
            return;

        if (movementDirectionAtPlant == Vector2.zero)
        {
            if (movement != null && movement.FacingDirection != Vector2.zero)
                movementDirectionAtPlant = movement.FacingDirection.normalized;
            else
                movementDirectionAtPlant = Vector2.down;
        }
        else
        {
            movementDirectionAtPlant = movementDirectionAtPlant.normalized;
        }

        _bombPlantDirection[bomb] = movementDirectionAtPlant;
        _bombEarlyKickUnlocked.Remove(bomb);
        _lastOwnerDirection = movementDirectionAtPlant;
    }

    public void NotifyOwnerDirectionChanged(Vector2 newDirection)
    {
        if (newDirection == Vector2.zero)
            return;

        newDirection = newDirection.normalized;

        if (_lastOwnerDirection == newDirection)
            return;

        _lastOwnerDirection = newDirection;

        if (_bombPlantDirection.Count == 0)
            return;

        var bombsToUnlock = new List<Bomb>(4);

        foreach (var kv in _bombPlantDirection)
        {
            var bomb = kv.Key;
            var plantDir = kv.Value.normalized;

            if (bomb == null || bomb.HasExploded || bomb.IsSolid || bomb.IsBeingKicked)
                continue;

            if (Vector2.Dot(plantDir, newDirection) < -0.9f)
                bombsToUnlock.Add(bomb);
        }

        for (int i = 0; i < bombsToUnlock.Count; i++)
            _bombEarlyKickUnlocked.Add(bombsToUnlock[i]);
    }

    void PruneEarlyKickState()
    {
        if (_bombPlantDirection.Count == 0)
            return;

        var bombsToRemove = new List<Bomb>(4);

        foreach (var kv in _bombPlantDirection)
        {
            var bomb = kv.Key;

            if (bomb == null || bomb.HasExploded || bomb.IsSolid || bomb.IsBeingKicked)
                bombsToRemove.Add(bomb);
        }

        for (int i = 0; i < bombsToRemove.Count; i++)
        {
            _bombPlantDirection.Remove(bombsToRemove[i]);
            _bombEarlyKickUnlocked.Remove(bombsToRemove[i]);
        }
    }

    void LogKickTrace(string message)
    {
        if (!debugKickTrace)
            return;

        Debug.Log($"[YellowLouieKickTrace][{name}] t:{Time.time:F3} {message}", this);
    }

    string GetOwnerLabel()
    {
        if (movement == null)
            return name;

        return $"P{movement.PlayerId}:{name}";
    }

    static string FormatVec(Vector2 value)
    {
        return $"({value.x:F2},{value.y:F2})";
    }

    static string FormatBomb(Bomb bomb)
    {
        if (bomb == null)
            return "none";

        return $"{bomb.name}@{FormatVec(bomb.GetLogicalPosition())}";
    }

    static string FormatFuse(Bomb bomb)
    {
        if (bomb == null)
            return "n/a";

        return bomb.RemainingFuseSeconds.ToString("F2");
    }

    static string FormatBombQueue(List<Bomb> queue)
    {
        if (queue == null || queue.Count == 0)
            return "empty";

        System.Text.StringBuilder sb = new();
        for (int i = 0; i < queue.Count; i++)
        {
            if (i > 0)
                sb.Append(" -> ");

            sb.Append(FormatBomb(queue[i]));
        }

        return sb.ToString();
    }
}
