using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class YellowLouieDestructibleKickAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "YellowLouieDestructibleKick";

    [SerializeField] private bool enabledAbility = true;

    [Header("Input")]
    public KeyCode triggerKey = KeyCode.B;

    [Header("Move")]
    public float cellsPerSecond = 10f;

    [Header("Kick Timing")]
    public float kickCooldownSeconds = 0.25f;

    [Header("Chain")]
    public int maxChainTransfers = 32;

    [Header("SFX")]
    public AudioClip kickSfx;
    [Range(0f, 1f)] public float kickSfxVolume = 1f;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    Coroutine routine;
    bool kickActive;
    float nextAllowedKickTime;

    IYellowLouieDestructibleKickExternalAnimator externalAnimator;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

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

        if (!Input.GetKeyDown(triggerKey))
            return;

        nextAllowedKickTime = Time.time + kickCooldownSeconds;

        if (routine != null)
            StopCoroutine(routine);

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

        kickActive = true;
        externalAnimator?.Play(dir);

        float animEndTime = Time.time + kickCooldownSeconds;

        var gm = FindFirstObjectByType<GameManager>();
        var tilemap = gm != null ? gm.destructibleTilemap : null;

        if (tilemap == null)
        {
            float w = animEndTime - Time.time;
            if (w > 0f) yield return new WaitForSeconds(w);

            StopKickVisuals();
            routine = null;
            yield break;
        }

        Vector3Int playerCell = tilemap.WorldToCell(rb.position);
        Vector3Int hitCell = playerCell + step;

        TileBase movingTile = tilemap.GetTile(hitCell);

        if (movingTile == null)
        {
            float w = animEndTime - Time.time;
            if (w > 0f) yield return new WaitForSeconds(w);

            StopKickVisuals();
            routine = null;
            yield break;
        }

        if (audioSource != null && kickSfx != null)
            audioSource.PlayOneShot(kickSfx, kickSfxVolume);

        tilemap.SetTile(hitCell, null);
        tilemap.RefreshTile(hitCell);

        movement.SetInputLocked(true, false);

        GameObject ghost = CreateGhost(tilemap, hitCell, movingTile);

        Vector3Int currentCell = hitCell;

        float stepSeconds = cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);
        int transfers = 0;

        while (enabledAbility && movement != null && !movement.isDead)
        {
            Vector3Int nextCell = currentCell + step;

            TileBase blockingTile;
            var blockType = GetBlockType(tilemap, nextCell, dir, out blockingTile);

            if (blockType == BlockType.Solid)
                break;

            if (blockType == BlockType.Destructible)
            {
                transfers++;
                if (transfers > Mathf.Max(0, maxChainTransfers))
                    break;

                tilemap.SetTile(currentCell, movingTile);
                tilemap.RefreshTile(currentCell);

                if (ghost != null)
                    Destroy(ghost);

                movingTile = blockingTile;
                currentCell = nextCell;

                tilemap.SetTile(currentCell, null);
                tilemap.RefreshTile(currentCell);

                ghost = CreateGhost(tilemap, currentCell, movingTile);

                continue;
            }

            Vector3 from = tilemap.GetCellCenterWorld(currentCell);
            Vector3 to = tilemap.GetCellCenterWorld(nextCell);

            float tMove = 0f;
            while (tMove < 1f)
            {
                if (!enabledAbility || movement == null || movement.isDead)
                    break;

                tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);

                if (ghost != null)
                    ghost.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(tMove));

                yield return null;
            }

            currentCell = nextCell;
        }

        float finalWait = animEndTime - Time.time;
        if (finalWait > 0f)
            yield return new WaitForSeconds(finalWait);

        if (ghost != null)
            Destroy(ghost);

        if (tilemap != null)
        {
            tilemap.SetTile(currentCell, movingTile);
            tilemap.RefreshTile(currentCell);
        }

        if (movement != null)
            movement.SetInputLocked(false);

        StopKickVisuals();
        routine = null;
    }

    GameObject CreateGhost(Tilemap tilemap, Vector3Int cell, TileBase tile)
    {
        GameObject ghost = new GameObject("YellowKickBlock_Ghost");
        ghost.transform.position = tilemap.GetCellCenterWorld(cell);

        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        if (tile is Tile t && t.sprite != null)
            sr.sprite = t.sprite;

        return ghost;
    }

    BlockType GetBlockType(Tilemap destructibleTilemap, Vector3Int cell, Vector2 dir, out TileBase blockingTile)
    {
        blockingTile = null;

        blockingTile = destructibleTilemap.GetTile(cell);
        if (blockingTile != null)
            return BlockType.Destructible;

        Vector3 center = destructibleTilemap.GetCellCenterWorld(cell);

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
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            if (enemyLayer >= 0 && hit.gameObject.layer == enemyLayer)
                return BlockType.Solid;

            return BlockType.Solid;
        }

        return BlockType.None;
    }

    void StopKickVisuals()
    {
        kickActive = false;
        externalAnimator?.Stop();
    }

    void CancelKick()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        kickActive = false;
        externalAnimator?.Stop();

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        CancelKick();
    }
}
