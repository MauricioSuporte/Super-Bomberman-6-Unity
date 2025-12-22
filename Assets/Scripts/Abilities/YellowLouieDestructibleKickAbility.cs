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
    [Tooltip("Células por segundo.")]
    public float cellsPerSecond = 10f;

    [Header("Collision")]
    public string destructiblesTag = "Destructibles";

    [Header("SFX")]
    public AudioClip kickSfx;
    [Range(0f, 1f)] public float kickSfxVolume = 1f;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    Coroutine routine;
    bool kickActive;

    IYellowLouieDestructibleKickExternalAnimator externalAnimator;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

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

        if (movement == null || movement.isDead || movement.InputLocked)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        if (!Input.GetKeyDown(triggerKey))
            return;

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

        var gm = FindFirstObjectByType<GameManager>();
        var tilemap = gm != null ? gm.destructibleTilemap : null;

        if (tilemap == null)
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

        Vector3Int playerCell = tilemap.WorldToCell(rb.position);
        Vector3Int hitCell = playerCell + step;

        var hitTile = tilemap.GetTile(hitCell);
        if (hitTile == null)
        {
            routine = null;
            yield break;
        }

        // remove tile do mapa para liberar collider durante o movimento
        tilemap.SetTile(hitCell, null);
        tilemap.RefreshTile(hitCell);

        kickActive = true;

        if (audioSource != null && kickSfx != null)
            audioSource.PlayOneShot(kickSfx, kickSfxVolume);

        movement.SetInputLocked(true, false);

        GameObject ghost = new GameObject("YellowKickBlock_Ghost");
        ghost.transform.position = tilemap.GetCellCenterWorld(hitCell);

        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10; // ajuste se precisar

        // tenta extrair sprite do tile (Tile)
        if (hitTile is Tile t && t.sprite != null)
            sr.sprite = t.sprite;

        externalAnimator?.Play(dir);

        Vector3Int currentCell = hitCell;

        try
        {
            float stepSeconds = cellsPerSecond <= 0.01f ? 0.05f : (1f / cellsPerSecond);

            while (enabledAbility && movement != null && !movement.isDead)
            {
                Vector3Int nextCell = currentCell + step;

                if (IsCellBlocked(tilemap, nextCell, dir))
                    break;

                Vector3 from = tilemap.GetCellCenterWorld(currentCell);
                Vector3 to = tilemap.GetCellCenterWorld(nextCell);

                float tMove = 0f;
                while (tMove < 1f)
                {
                    if (!enabledAbility || movement == null || movement.isDead)
                        yield break;

                    tMove += Time.deltaTime / Mathf.Max(0.0001f, stepSeconds);
                    ghost.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(tMove));
                    yield return null;
                }

                currentCell = nextCell;
            }
        }
        finally
        {
            kickActive = false;

            externalAnimator?.Stop();

            if (ghost != null)
                Destroy(ghost);

            // coloca o tile na última célula livre
            if (tilemap != null)
            {
                tilemap.SetTile(currentCell, hitTile);
                tilemap.RefreshTile(currentCell);
            }

            if (movement != null)
                movement.SetInputLocked(false);

            routine = null;
        }
    }

    bool IsCellBlocked(Tilemap destructibleTilemap, Vector3Int cell, Vector2 dir)
    {
        // já tem outro destrutível ali
        if (destructibleTilemap.GetTile(cell) != null)
            return true;

        Vector3 center = destructibleTilemap.GetCellCenterWorld(cell);

        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(movement.tileSize * 0.6f, movement.tileSize * 0.2f)
            : new Vector2(movement.tileSize * 0.2f, movement.tileSize * 0.6f);

        // usa obstacleMask do movement (Stage/Bomb) e adiciona Enemy
        int mask = movement.obstacleMask.value;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            mask |= 1 << enemyLayer;

        var hits = Physics2D.OverlapBoxAll(center, size, 0f, mask);
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            // NÃO atravessa destrutíveis (a habilidade é “chutar”, não “passar”)
            // então qualquer collider sólido encontrado bloqueia.
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

        if (kickActive)
        {
            kickActive = false;
            externalAnimator?.Stop();

            if (movement != null)
                movement.SetInputLocked(false);
        }
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        CancelKick();
    }
}
