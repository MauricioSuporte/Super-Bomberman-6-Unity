using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BlackLouieDashPushAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "BlackLouieDashPush";

    [SerializeField] private bool enabledAbility = true;

    [Header("Dash")]
    public int dashTiles = 3;
    public float dashMoveSeconds = 0.5f;
    public float dashCooldownSeconds = 0.6f;

    [Header("Impact Hold (when push succeeds)")]
    [SerializeField, Min(0f)] private float impactHoldSeconds = 0.25f;

    [Header("Enemy Push")]
    public int enemyPushTiles = 2;
    public float enemyPushTilesPerSecond = 14f;
    public float enemyStunSeconds = 0.5f;

    [Header("Extra Invulnerability After Push")]
    public float extraInvulAfterPushSeconds = 0.5f;

    [Header("Destructible Pass (Dash)")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("SFX")]
    public AudioClip dashSfx;
    [Range(0f, 1f)] public float dashSfxVolume = 1f;

    MovementController movement;
    Rigidbody2D rb;
    Collider2D myCollider;
    AudioSource audioSource;
    BombController bombController;
    PlayerMountCompanion louieCompanion;
    AbilitySystem abilitySystem;

    float nextAllowedTime;
    Coroutine routine;

    IBlackLouieDashExternalAnimator externalAnimator;

    int enemyLayer;
    int bombLayer;
    int playerLayer;
    int playerLayerMask;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;
    public bool DashActive => routine != null;
    bool deathCancelInProgress;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        louieCompanion = GetComponent<PlayerMountCompanion>();
        abilitySystem = GetComponent<AbilitySystem>();

        enemyLayer = LayerMask.NameToLayer("Enemy");
        bombLayer = LayerMask.NameToLayer("Bomb");
        playerLayer = LayerMask.NameToLayer("Player");
        playerLayerMask = LayerMask.GetMask("Player");
    }

    void OnDisable() => Cancel();
    void OnDestroy() => Cancel();

    public void SetExternalAnimator(IBlackLouieDashExternalAnimator animator) => externalAnimator = animator;

    public void SetDashSfx(AudioClip clip, float volume)
    {
        dashSfx = clip;
        dashSfxVolume = Mathf.Clamp01(volume);
    }

    bool IsTriggerPressed()
    {
        var input = PlayerInputManager.Instance;
        if (input == null || movement == null)
            return false;

        return input.GetDown(movement.PlayerId, PlayerAction.ActionC);
    }

    void Update()
    {
        if (!enabledAbility) return;
        if (!CompareTag("Player")) return;
        if (movement == null || movement.isDead) return;
        if (Time.time < nextAllowedTime) return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        if (!IsTriggerPressed())
            return;

        if (routine != null)
            return;

        routine = StartCoroutine(DashRoutine());
    }

    bool CanPassDestructiblesNow()
    {
        if (!CompareTag("Player"))
            return false;

        if (PlayerPersistentStats.Get(GetPlayerId()).CanPassDestructibles)
            return true;

        return abilitySystem != null &&
               abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
    }

    IEnumerator DashRoutine()
    {
        if (movement == null || rb == null)
        {
            routine = null;
            yield break;
        }

        Vector2 dir = movement.Direction != Vector2.zero
            ? movement.Direction
            : movement.FacingDirection;

        if (dir == Vector2.zero)
            dir = Vector2.down;

        dir = NormalizeCardinal(dir);

        nextAllowedTime = Time.time + Mathf.Max(0.01f, dashCooldownSeconds);

        externalAnimator?.Play(dir);
        movement.SetInputLocked(true, false);

        if (audioSource != null && dashSfx != null)
            audioSource.PlayOneShot(dashSfx, dashSfxVolume);

        float duration = Mathf.Max(0.01f, dashMoveSeconds);

        if (louieCompanion != null)
            louieCompanion.StartOrExtendDashInvulnerability(duration);

        float tile = Mathf.Max(0.01f, movement.tileSize);
        int tiles = Mathf.Max(1, dashTiles);

        Vector2 startPos = rb.position;
        Vector2 endPos = startPos + dir * (tiles * tile);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!enabledAbility || movement == null || movement.isDead || rb == null)
                break;

            float t = elapsed / duration;
            Vector2 desired = Vector2.Lerp(startPos, endPos, t);

            Vector2 current = rb.position;
            Vector2 delta = desired - current;
            float dist = delta.magnitude;

            if (dist > 0.0001f)
            {
                Vector2 castDir = delta / dist;

                if (TryGetFirstBlockerAlongPath(current, castDir, dist, out var hit))
                {
                    if (TryKickBomb(hit, dir))
                    {
                        EndDash();
                        yield break;
                    }

                    if (TryPushTarget(hit, dir, tile, out var pushedEnemy))
                    {
                        if (pushedEnemy)
                        {
                            ExtendInvulnerabilityAfterPush();
                            yield return HoldImpactThenEnd();
                            yield break;
                        }

                        EndDash();
                        yield break;
                    }

                    EndDash();
                    yield break;
                }
            }

            rb.MovePosition(desired);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (enabledAbility && movement != null && !movement.isDead && rb != null)
        {
            Vector2 current = rb.position;
            Vector2 delta = endPos - current;
            float dist = delta.magnitude;

            if (dist > 0.0001f)
            {
                Vector2 castDir = delta / dist;

                if (!TryGetFirstBlockerAlongPath(current, castDir, dist, out var hitFinal))
                {
                    rb.MovePosition(endPos);
                }
                else
                {
                    TryKickBomb(hitFinal, dir);

                    if (TryPushTarget(hitFinal, dir, tile, out var pushedEnemy) && pushedEnemy)
                    {
                        ExtendInvulnerabilityAfterPush();
                        yield return HoldImpactThenEnd();
                        yield break;
                    }
                }
            }
        }

        EndDash();
    }

    IEnumerator HoldImpactThenEnd()
    {
        float hold = Mathf.Max(0f, impactHoldSeconds);

        // Mantém o último frame visível no local do impacto.
        if (hold > 0f)
        {
            if (externalAnimator is BlackLouieDashAnimator anim)
                anim.HoldImpact(hold);
        }

        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        EndDash();
    }

    void ExtendInvulnerabilityAfterPush()
    {
        if (louieCompanion == null)
            return;

        float extra = Mathf.Max(0f, extraInvulAfterPushSeconds);
        if (extra <= 0f)
            return;

        louieCompanion.StartOrExtendDashInvulnerability(extra);
    }

    void EndDash()
    {
        if (!deathCancelInProgress)
        {
            externalAnimator?.Stop();

            if (movement != null)
                movement.SetInputLocked(false);
        }

        routine = null;
        deathCancelInProgress = false;
    }

    bool TryKickBomb(Collider2D hit, Vector2 dir)
    {
        if (hit == null || hit.gameObject.layer != bombLayer)
            return false;

        var bomb = hit.GetComponent<Bomb>();
        if (bomb == null || bomb.IsBeingKicked || !bomb.CanBeKicked)
            return true;

        LayerMask bombObstacles = movement.obstacleMask | LayerMask.GetMask("Enemy");

        bomb.StartKick(
            dir,
            movement.tileSize,
            bombObstacles,
            bombController != null ? bombController.destructibleTiles : null
        );

        return true;
    }

    bool TryPushTarget(Collider2D hit, Vector2 dir, float tileSize, out bool pushedTarget)
    {
        pushedTarget = false;

        if (hit == null)
            return false;

        bool isEnemyTarget = hit.gameObject.layer == enemyLayer;
        bool isBattlePlayerTarget = IsBattleModeScene() && IsOtherPlayerTarget(hit);

        if (!isEnemyTarget && !isBattlePlayerTarget)
            return false;

        var targetRb = hit.attachedRigidbody;
        if (targetRb == null)
        {
            pushedTarget = true;
            return true;
        }

        var receiver = hit.GetComponentInParent<StunReceiver>() ?? hit.GetComponent<StunReceiver>();
        receiver?.Stun(enemyStunSeconds);

        StartCoroutine(PushTargetRoutine(targetRb, dir, tileSize));
        pushedTarget = true;
        return true;
    }

    IEnumerator PushTargetRoutine(Rigidbody2D enemyRb, Vector2 pushDir, float tileSize)
    {
        if (enemyRb == null)
            yield break;

        int tiles = Mathf.Max(1, enemyPushTiles);

        enemyRb.position = SnapToGrid(enemyRb.position, tileSize);

        for (int i = 0; i < tiles; i++)
        {
            if (enemyRb == null)
                yield break;

            enemyRb.linearVelocity = Vector2.zero;

            Vector2 from = enemyRb.position;
            Vector2 to = from + pushDir * tileSize;

            if (IsBlockedForTarget(enemyRb, to, pushDir, tileSize))
                yield break;

            yield return MoveTargetOverTime(enemyRb, from, to, enemyPushTilesPerSecond, tileSize);
            enemyRb.position = SnapToGrid(enemyRb.position, tileSize);
        }
    }

    IEnumerator MoveTargetOverTime(Rigidbody2D enemyRb, Vector2 from, Vector2 to, float tilesPerSecond, float tileSize)
    {
        float speed = tilesPerSecond * tileSize;
        float dist = Vector2.Distance(from, to);
        float dur = dist / Mathf.Max(0.01f, speed);

        float end = Time.time + dur;

        while (Time.time < end)
        {
            if (enemyRb == null)
                yield break;

            float t = 1f - ((end - Time.time) / dur);
            enemyRb.MovePosition(Vector2.Lerp(from, to, Mathf.Clamp01(t)));
            yield return null;
        }

        enemyRb.MovePosition(to);
    }

    bool TryGetFirstBlockerAlongPath(Vector2 origin, Vector2 dir, float distance, out Collider2D blocker)
    {
        blocker = null;

        float tile = movement != null ? Mathf.Max(0.01f, movement.tileSize) : 1f;

        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(tile * 0.6f, tile * 0.2f)
            : new Vector2(tile * 0.2f, tile * 0.6f);

        int mask = (movement != null ? movement.obstacleMask.value : 0) | LayerMask.GetMask("Enemy");
        if (IsBattleModeScene())
            mask |= playerLayerMask;

        var hits = Physics2D.BoxCastAll(origin, size, 0f, dir, distance, mask);
        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles = CanPassDestructiblesNow();

        float bestDist = float.MaxValue;
        Collider2D best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            var col = h.collider;

            if (col == null)
                continue;

            if (col.gameObject == gameObject)
                continue;

            if (IsOwnPlayerCollider(col))
                continue;

            if (col.isTrigger && col.gameObject.layer != enemyLayer)
                continue;

            if (canPassDestructibles && col.CompareTag(destructiblesTag))
                continue;

            float d = h.distance;
            if (d < bestDist)
            {
                bestDist = d;
                best = col;
            }
        }

        if (best == null)
            return false;

        blocker = best;
        return true;
    }

    bool IsBlockedForTarget(Rigidbody2D enemyRb, Vector2 targetPos, Vector2 dir, float tileSize)
    {
        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(tileSize * 0.6f, tileSize * 0.2f)
            : new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        int mask = movement.obstacleMask.value | LayerMask.GetMask("Enemy") | playerLayerMask;

        var hits = Physics2D.OverlapBoxAll(targetPos, size, 0f, mask);
        if (hits == null)
            return false;

        foreach (var h in hits)
        {
            if (h == null)
                continue;

            if (h.isTrigger && h.gameObject.layer == enemyLayer)
                continue;

            if (h.isTrigger)
                continue;

            if (enemyRb != null && h.attachedRigidbody == enemyRb)
                continue;

            return true;
        }

        return false;
    }

    bool IsOtherPlayerTarget(Collider2D hit)
    {
        if (hit == null || hit.gameObject.layer != playerLayer)
            return false;

        if (IsOwnPlayerCollider(hit))
            return false;

        var targetMovement = hit.GetComponentInParent<MovementController>();
        return targetMovement != null && targetMovement.CompareTag("Player") && !targetMovement.isDead;
    }

    bool IsOwnPlayerCollider(Collider2D hit)
    {
        if (hit == null || movement == null)
            return false;

        var targetMovement = hit.GetComponentInParent<MovementController>();
        return targetMovement == movement;
    }

    static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", System.StringComparison.OrdinalIgnoreCase);
    }

    Vector2 SnapToGrid(Vector2 pos, float tileSize)
    {
        pos.x = Mathf.Round(pos.x / tileSize) * tileSize;
        pos.y = Mathf.Round(pos.y / tileSize) * tileSize;
        return pos;
    }

    Vector2 NormalizeCardinal(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x >= 0 ? Vector2.right : Vector2.left;

        return dir.y >= 0 ? Vector2.up : Vector2.down;
    }

    void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        externalAnimator?.Stop();

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void CancelDashForExternalInterruption()
    {
        if (!DashActive)
            return;

        Cancel();
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        Cancel();
    }

    int GetPlayerId()
    {
        if (TryGetComponent<PlayerIdentity>(out var id) && id != null)
            return Mathf.Clamp(id.playerId, 1, 6);

        var parentId = GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null)
            return Mathf.Clamp(parentId.playerId, 1, 6);

        return 1;
    }

    public void CancelDashForDeath()
    {
        deathCancelInProgress = true;

        enabledAbility = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        externalAnimator?.Stop();

        if (movement != null)
            movement.SetInputLocked(false);

        externalAnimator = null;
    }
}
