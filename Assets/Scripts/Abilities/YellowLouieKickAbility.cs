using Assets.Scripts.Interface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class YellowLouieKickAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "YellowLouieDestructibleKick";

    [SerializeField] private bool enabledAbility = true;

    [Header("Move")]
    public float cellsPerSecond = 10f;

    [Header("Kick Timing")]
    public float kickCooldownSeconds = 0.25f;

    [Header("Chain")]
    public int maxChainTransfers = 32;

    [Header("Stop Shake (visual feedback)")]
    public float stopShakeAmplitude = 0.05f;
    public float stopShakeFrequency = 22f;

    [Header("SFX")]
    public AudioClip kickSfx;
    [Range(0f, 1f)] public float kickSfxVolume = 1f;

    [Header("Bomb Kick")]
    [SerializeField] private float bombKickOverlapSize = 0.60f;
    [SerializeField] private float bombKickOriginBlockerSize = 0.90f;
    [SerializeField] private bool bombKickOriginBlockerUseTrigger = false;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    Coroutine routine;
    Coroutine kickVisualRoutine;
    bool kickActive;
    float nextAllowedKickTime;

    IYellowLouieDestructibleKickExternalAnimator externalAnimator;

    static readonly HashSet<Vector3Int> _reservedCells = new();

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    bool deathCancelInProgress;

    enum ChainMoverType
    {
        None = 0,
        Bomb = 1,
        Tile = 2
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

        var gm = FindFirstObjectByType<GameManager>();
        var destructibleTilemap = gm != null ? gm.destructibleTilemap : null;

        Vector3Int frontCell = destructibleTilemap != null
            ? destructibleTilemap.WorldToCell(rb.position) + step
            : new Vector3Int(
                Mathf.RoundToInt((rb.position.x + dir.normalized.x * movement.tileSize) / movement.tileSize),
                Mathf.RoundToInt((rb.position.y + dir.normalized.y * movement.tileSize) / movement.tileSize),
                0);

        bool hasBombInFront = TryGetBombAtCell(SnapToGrid(rb.position, movement.tileSize) + dir.normalized * movement.tileSize, out Bomb firstBomb);
        bool hasTileInFront = TryGetDestructibleAtCell(destructibleTilemap, frontCell, out TileBase firstTile);

        if (!hasBombInFront && !hasTileInFront)
        {
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
            audioSource.PlayOneShot(kickSfx, kickSfxVolume);

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
                    bool enemyEnteredDestinationDuringMove = false;

                    while (tMove < 1f)
                    {
                        if (!enabledAbility || movement == null || movement.isDead)
                            break;

                        ReleaseInputIfNeeded();

                        if (HasEnemyAt(to))
                        {
                            enemyEnteredDestinationDuringMove = true;
                            break;
                        }

                        tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);

                        if (ghost != null)
                            ghost.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(tMove));

                        yield return null;
                    }

                    if (enemyEnteredDestinationDuringMove)
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

                    Vector2 currentBombCell = SnapToGrid(currentBomb.transform.position, tileSize);
                    Vector3Int nextCell = new Vector3Int(
                        Mathf.RoundToInt((currentBombCell.x + kickDir.x * tileSize) / tileSize),
                        Mathf.RoundToInt((currentBombCell.y + kickDir.y * tileSize) / tileSize),
                        0);

                    bool hasAdjacentBomb = TryGetBombAtCell(SnapToGrid(currentBomb.transform.position, tileSize) + kickDir * tileSize, out Bomb adjacentBomb)
                                           && adjacentBomb != null
                                           && adjacentBomb != currentBomb
                                           && !adjacentBomb.IsBeingKicked;

                    bool hasAdjacentTile = TryGetDestructibleAtCell(destructibleTilemap, nextCell, out TileBase adjacentTile);

                    if (hasAdjacentBomb)
                    {
                        currentBomb = adjacentBomb;
                        moverType = ChainMoverType.Bomb;
                        continue;
                    }

                    if (hasAdjacentTile)
                    {
                        BeginTileMover(nextCell, adjacentTile);
                        continue;
                    }

                    bool started = StartBombKick(currentBomb, kickDir, destructibleTilemap);
                    if (!started)
                        break;

                    Vector2 currentCell = SnapToGrid(currentBomb.transform.position, tileSize);
                    Bomb nextBomb = null;
                    TileBase nextTile = null;
                    Vector3Int nextTileCell = default;
                    bool endedBySolid = false;

                    while (currentBomb != null && currentBomb.IsBeingKicked && enabledAbility && movement != null && !movement.isDead)
                    {
                        ReleaseInputIfNeeded();

                        Vector2 snapped = SnapToGrid(currentBomb.transform.position, tileSize);
                        if (snapped != currentCell)
                            currentCell = snapped;

                        Vector2 nextBombCellWorld = currentCell + kickDir * tileSize;
                        nextTileCell = new Vector3Int(
                            Mathf.RoundToInt(nextBombCellWorld.x / tileSize),
                            Mathf.RoundToInt(nextBombCellWorld.y / tileSize),
                            0);

                        if (TryGetBombAtCell(nextBombCellWorld, out Bomb foundBomb) &&
                            foundBomb != null &&
                            foundBomb != currentBomb &&
                            !foundBomb.IsBeingKicked)
                        {
                            nextBomb = foundBomb;
                            break;
                        }

                        if (TryGetDestructibleAtCell(destructibleTilemap, nextTileCell, out TileBase foundTile))
                        {
                            nextTile = foundTile;
                            break;
                        }

                        if (IsMixedChainSolidAt(nextBombCellWorld, kickDir, currentBomb))
                        {
                            endedBySolid = true;
                            break;
                        }

                        yield return null;
                    }

                    if (currentBomb != null && currentBomb.IsBeingKicked)
                        currentBomb.StopKickAndSnapToGrid(tileSize);

                    if (nextBomb != null)
                    {
                        currentBomb = nextBomb;
                        moverType = ChainMoverType.Bomb;
                        continue;
                    }

                    if (nextTile != null)
                    {
                        BeginTileMover(nextTileCell, nextTile);
                        continue;
                    }

                    if (endedBySolid && currentBomb != null)
                    {
                        Vector3 basePos = SnapToGrid(currentBomb.transform.position, tileSize);
                        yield return ShakeBombVisual(currentBomb, basePos, stopShakeDuration, stopShakeAmplitude, stopShakeFrequency);
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

    bool TryGetBombInFront(Vector2 dir, out Bomb bomb)
    {
        bomb = null;

        if (movement == null || rb == null)
            return false;

        Vector2 origin = SnapToGrid(rb.position, movement.tileSize);
        Vector2 target = origin + dir.normalized * movement.tileSize;

        bool found = TryGetBombAtCell(target, out bomb);
        return found;
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

    bool StartBombKick(Bomb bomb, Vector2 dir, Tilemap destructibleTilemap)
    {
        if (bomb == null)
            return false;

        if (bomb.IsBeingKicked)
            return false;

        if (!bomb.CanBeKicked)
            return false;

        LayerMask bombObstacles = movement.obstacleMask.value | LayerMask.GetMask("Enemy");

        bool result = bomb.StartKick(
            dir.normalized,
            movement.tileSize,
            bombObstacles,
            destructibleTilemap,
            LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie"),
            bombKickOverlapSize,
            bombKickOriginBlockerSize,
            bombKickOriginBlockerUseTrigger
        );

        return result;
    }

    bool IsMixedChainSolidAt(Vector2 nextCell, Vector2 dir, Bomb currentBomb)
    {
        if (movement == null)
            return true;

        if (HasItemAt(nextCell))
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

            if (hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    IEnumerator ShakeBombVisual(Bomb bomb, Vector3 basePos, float duration, float amplitude, float frequencyHz)
    {
        if (bomb == null)
            yield break;

        float dur = Mathf.Max(0.01f, duration);
        float amp = Mathf.Max(0f, amplitude);
        float hz = Mathf.Max(1f, frequencyHz);

        float end = Time.time + dur;
        float seed = Random.value * 1000f;

        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead || bomb == null)
                yield break;

            float t = Time.time - (end - dur);
            float phase = (t * hz) * (Mathf.PI * 2f);

            float x = Mathf.Sin(phase + seed) * amp;
            float y = Mathf.Cos(phase * 1.23f + seed) * amp;

            bomb.transform.position = basePos + new Vector3(x, y, 0f);
            yield return null;
        }

        if (bomb != null)
            bomb.transform.position = basePos;
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
        var gm = FindFirstObjectByType<GameManager>();
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
        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer < 0)
            return false;

        return HasAnyColliderAt(center, 1 << itemLayer);
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

    void CancelKick()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopKickVisuals();

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
    }

    public void CancelKickForDeath()
    {
        deathCancelInProgress = true;

        enabledAbility = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

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
}