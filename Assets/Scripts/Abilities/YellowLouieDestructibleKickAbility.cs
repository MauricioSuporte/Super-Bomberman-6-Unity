using Assets.Scripts.Interface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class YellowLouieDestructibleKickAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "YellowLouieDestructibleKick";

    [SerializeField] private bool enabledAbility = true;

    [Header("Move")]
    public float cellsPerSecond = 10f;

    [Header("Kick Timing")]
    public float kickCooldownSeconds = 0.25f;

    [Header("Chain")]
    public float chainTransferDelaySeconds = 0.2f;
    public int maxChainTransfers = 32;

    [Header("Stop Shake (visual feedback)")]
    public float stopShakeAmplitude = 0.03f;
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

    enum BlockType
    {
        None = 0,
        Solid = 1,
        Destructible = 2
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

        if (TryKickBombInFront(dir, destructibleTilemap))
        {
            if (audioSource != null && kickSfx != null)
                audioSource.PlayOneShot(kickSfx, kickSfxVolume);

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

        if (destructibleTilemap == null)
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

        Vector3Int playerCell = destructibleTilemap.WorldToCell(rb.position);
        Vector3Int hitCell = playerCell + step;

        TileBase movingTile = destructibleTilemap.GetTile(hitCell);
        if (movingTile == null)
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

        GameObject ghost = null;
        bool tileRemovedFromMap = false;
        Vector3Int currentCell = hitCell;

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

        try
        {
            destructibleTilemap.SetTile(hitCell, null);
            destructibleTilemap.RefreshTile(hitCell);
            tileRemovedFromMap = true;

            ghost = CreateGhost(destructibleTilemap, hitCell, movingTile);
            reserve(currentCell);

            ApplyShadowForCell(currentCell);

            float stepSeconds = cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);
            int transfers = 0;

            bool endedBySolid = false;

            while (enabledAbility && movement != null && !movement.isDead)
            {
                if (!inputUnlockedAfterAnim && Time.time >= animEndTime)
                {
                    inputUnlockedAfterAnim = true;
                    movement.SetInputLocked(false);
                }

                Vector3Int nextCell = currentCell + step;

                TileBase blockingTile;
                var blockType = GetBlockType(destructibleTilemap, nextCell, dir, out blockingTile);

                if (blockType == BlockType.Solid)
                {
                    endedBySolid = true;
                    break;
                }

                if (blockType == BlockType.Destructible)
                {
                    transfers++;
                    if (transfers > maxChainTransfers)
                        break;

                    Vector3 basePos = destructibleTilemap.GetCellCenterWorld(currentCell);

                    if (ghost != null)
                    {
                        yield return ShakeGhost(ghost, basePos, chainTransferDelaySeconds, stopShakeAmplitude, stopShakeFrequency);
                        if (ghost != null)
                            ghost.transform.position = basePos;
                    }
                    else
                    {
                        yield return WaitSecondsAndReleaseInput(chainTransferDelaySeconds, animEndTime, () =>
                        {
                            if (!inputUnlockedAfterAnim)
                            {
                                inputUnlockedAfterAnim = true;
                                if (movement != null)
                                    movement.SetInputLocked(false);
                            }
                        });
                    }

                    destructibleTilemap.SetTile(currentCell, movingTile);
                    destructibleTilemap.RefreshTile(currentCell);
                    tileRemovedFromMap = false;

                    if (ghost != null)
                        Destroy(ghost);
                    ghost = null;

                    release(currentCell);
                    ApplyShadowForCell(currentCell);

                    movingTile = blockingTile;
                    currentCell = nextCell;

                    destructibleTilemap.SetTile(currentCell, null);
                    destructibleTilemap.RefreshTile(currentCell);
                    tileRemovedFromMap = true;

                    ghost = CreateGhost(destructibleTilemap, currentCell, movingTile);
                    reserve(currentCell);

                    ApplyShadowForCell(currentCell);

                    continue;
                }

                reserve(nextCell);
                ApplyShadowForCell(nextCell);

                Vector3 from = destructibleTilemap.GetCellCenterWorld(currentCell);
                Vector3 to = destructibleTilemap.GetCellCenterWorld(nextCell);

                float tMove = 0f;
                while (tMove < 1f)
                {
                    if (!enabledAbility || movement == null || movement.isDead)
                        break;

                    if (!inputUnlockedAfterAnim && Time.time >= animEndTime)
                    {
                        inputUnlockedAfterAnim = true;
                        movement.SetInputLocked(false);
                    }

                    tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);

                    if (ghost != null)
                        ghost.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(tMove));

                    yield return null;
                }

                release(currentCell);
                ApplyShadowForCell(currentCell);

                currentCell = nextCell;
            }

            float finalWait = Mathf.Max(0f, animEndTime - Time.time);
            if (finalWait > 0f)
            {
                yield return WaitSecondsAndReleaseInput(finalWait, animEndTime, () =>
                {
                    if (!inputUnlockedAfterAnim)
                    {
                        inputUnlockedAfterAnim = true;
                        if (movement != null)
                            movement.SetInputLocked(false);
                    }
                });
            }
            else
            {
                if (!inputUnlockedAfterAnim && movement != null)
                {
                    inputUnlockedAfterAnim = true;
                    movement.SetInputLocked(false);
                }
            }

            if (endedBySolid && ghost != null && enabledAbility && movement != null && !movement.isDead)
            {
                Vector3 basePos = destructibleTilemap.GetCellCenterWorld(currentCell);
                yield return ShakeGhost(ghost, basePos, chainTransferDelaySeconds, stopShakeAmplitude, stopShakeFrequency);
                if (ghost != null)
                    ghost.transform.position = basePos;
            }
        }
        finally
        {
            if (ghost != null)
                Destroy(ghost);

            foreach (var c in reservedLocal)
                _reservedCells.Remove(c);

            if (destructibleTilemap != null && tileRemovedFromMap)
            {
                destructibleTilemap.SetTile(currentCell, movingTile);
                destructibleTilemap.RefreshTile(currentCell);
            }

            ApplyShadowForCell(currentCell);

            if (!deathCancelInProgress)
            {
                if (movement != null)
                    movement.SetInputLocked(false);
            }

            routine = null;
            deathCancelInProgress = false;
        }
    }

    bool TryKickBombInFront(Vector2 dir, Tilemap destructibleTilemap)
    {
        if (movement == null || rb == null)
            return false;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        if (bombLayer < 0)
            return false;

        Vector2 origin = SnapToGrid(rb.position, movement.tileSize);
        Vector2 target = origin + dir.normalized * movement.tileSize;

        Collider2D hit = Physics2D.OverlapBox(
            target,
            Vector2.one * (movement.tileSize * 0.6f),
            0f,
            1 << bombLayer);

        if (hit == null)
            return false;

        Bomb bomb = hit.GetComponent<Bomb>();
        if (bomb == null || bomb.IsBeingKicked || !bomb.CanBeKicked)
            return false;

        LayerMask bombObstacles = movement.obstacleMask.value | LayerMask.GetMask("Enemy");

        bool kicked = bomb.StartKick(
            dir.normalized,
            movement.tileSize,
            bombObstacles,
            destructibleTilemap,
            LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie"),
            bombKickOverlapSize,
            bombKickOriginBlockerSize,
            bombKickOriginBlockerUseTrigger
        );

        return kicked;
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

            float t = (Time.time - (end - dur));
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

    BlockType GetBlockType(Tilemap destructibleTilemap, Vector3Int cell, Vector2 dir, out TileBase blockingTile)
    {
        blockingTile = destructibleTilemap.GetTile(cell);
        if (blockingTile != null)
            return BlockType.Destructible;

        if (_reservedCells.Contains(cell))
            return BlockType.Solid;

        Vector3 center = destructibleTilemap.GetCellCenterWorld(cell);

        if (HasItemAt(center) || HasPlayerAt(center))
            return BlockType.Solid;

        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(movement.tileSize * 0.6f, movement.tileSize * 0.2f)
            : new Vector2(movement.tileSize * 0.2f, movement.tileSize * 0.6f);

        int mask = movement.obstacleMask.value;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            mask |= 1 << enemyLayer;

        var hits = Physics2D.OverlapBoxAll(center, size, 0f, mask);
        if (hits == null || hits.Length == 0)
            return BlockType.None;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            return BlockType.Solid;
        }

        return BlockType.None;
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

    public void Enable() => enabledAbility = true;

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
}