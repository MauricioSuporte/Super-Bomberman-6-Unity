using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(MovementController))]
public class BombPunchAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "BombPunch";

    private const string PunchClipResourcesPath = "Sounds/PunchBomb";
    private static AudioClip cachedPunchClip;

    [SerializeField] private bool enabledAbility;

    [Header("Punch Settings")]
    public int punchDistanceTiles = 3;
    public float punchLockTime = 0.1f;

    [Header("Auto Punch While Holding ActionC")]
    [SerializeField] private bool enableAutoPunchWhileHolding = true;

    [Header("Early Punch")]
    [SerializeField, Range(0.5f, 2.0f)]
    private float earlyPunchSearchSize = 1.4f;

    [SerializeField, Range(0f, 1.0f)]
    private float earlyPunchMinExitFraction = 0.3f;

    [Header("Punch Sprites (PLAYER)")]
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    private AudioSource audioSource;
    private BombController bombController;
    private MovementController movement;
    private BombKickAbility kickAbility;

    private AnimatedSpriteRenderer prevMoveSprite;
    private AnimatedSpriteRenderer activePunchSprite;

    private Vector2 lastFacingDir = Vector2.down;
    private Coroutine punchLockRoutine;

    private IBombPunchExternalAnimator externalAnimator;

    private bool lockedByLouie;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        bombController = GetComponent<BombController>();
        movement = GetComponent<MovementController>();
        kickAbility = GetComponent<BombKickAbility>();

        if (cachedPunchClip == null)
            cachedPunchClip = Resources.Load<AudioClip>(PunchClipResourcesPath);
    }

    public void SetExternalAnimator(IBombPunchExternalAnimator animator)
    {
        if (externalAnimator != null && externalAnimator != animator)
            externalAnimator.ForceStop();

        externalAnimator = animator;
    }

    public void SetLockedByLouie(bool locked, bool resetVisuals)
    {
        if (lockedByLouie == locked)
            return;

        lockedByLouie = locked;

        if (lockedByLouie)
        {
            externalAnimator?.ForceStop();

            if (resetVisuals)
                ForceResetPunchSprites();
        }
    }

    public void SetLockedByLouie(bool locked)
    {
        SetLockedByLouie(locked, resetVisuals: true);
    }

    private bool IsHoldingBomb()
    {
        if (!TryGetComponent<AbilitySystem>(out var ab) || ab == null)
            return false;

        ab.RebuildCache();

        var glove = ab.Get<PowerGloveAbility>(PowerGloveAbility.AbilityId);
        if (glove != null && glove.IsEnabled && glove.IsHoldingBomb)
            return true;

        return false;
    }

    private void Update()
    {
        using var performanceSample = BattleModePerformanceMarkers.AbilityUpdate.Auto();

        if (!enabledAbility)
            return;

        if (lockedByLouie)
            return;

        if (!CompareTag("Player"))
            return;

        if (GamePauseController.IsPaused)
            return;

        if (ClownMaskBoss.BossIntroRunning)
            return;

        if (movement == null || movement.isDead)
            return;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
            return;

        if (movement.InputLocked)
            return;

        if (IsHoldingBomb())
            return;

        Vector2 moveDir = movement.Direction;
        if (moveDir != Vector2.zero)
            lastFacingDir = moveDir;
        else if (movement.FacingDirection != Vector2.zero)
            lastFacingDir = movement.FacingDirection;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        int pid = movement.PlayerId;

        bool wantsPunch =
            enableAutoPunchWhileHolding
                ? input.Get(pid, PlayerAction.ActionC)
                : input.GetDown(pid, PlayerAction.ActionC);

        if (!wantsPunch)
            return;

        TryPunchAhead();
    }

    private void TryPunchAhead()
    {
        if (movement == null)
            return;

        if (punchLockRoutine != null)
            return;

        Vector2 dir = lastFacingDir;
        if (dir == Vector2.zero)
            dir = movement.FacingDirection != Vector2.zero ? movement.FacingDirection : Vector2.down;

        Vector2 playerPos = movement.Rigidbody != null
            ? movement.Rigidbody.position
            : (Vector2)transform.position;

        playerPos.x = Mathf.Round(playerPos.x / movement.tileSize) * movement.tileSize;
        playerPos.y = Mathf.Round(playerPos.y / movement.tileSize) * movement.tileSize;

        Bomb bomb = FindPunchableBombAhead(playerPos, dir);

        if (bomb == null)
            return;

        movement.CancelBombReentryCentering();

        if (bomb.IsBeingKicked)
        {
            if (kickAbility == null || !kickAbility.TryConvertKickToPunch(bomb))
                return;
        }

        LayerMask obstacles = movement.obstacleMask | LayerMask.GetMask("Enemy", "Bomb", "Player");
        Vector2 bombLogicalPos = bomb.GetLogicalPosition();

        bool punched = bomb.StartPunch(
            dir,
            movement.tileSize,
            punchDistanceTiles,
            obstacles,
            bombController != null ? bombController.destructibleTiles : null,
            visualStartYOffset: 0f,
            logicalOriginOverride: bombLogicalPos
        );

        if (!punched)
            return;

        if (audioSource != null && cachedPunchClip != null)
            audioSource.PlayOneShot(cachedPunchClip);

        if (punchLockRoutine != null)
            StopCoroutine(punchLockRoutine);

        punchLockRoutine = StartCoroutine(PunchAnimLock(dir));
    }

    private Bomb FindPunchableBombAhead(Vector2 playerSnappedPos, Vector2 dir)
    {
        float tileSize = movement.tileSize;

        Vector2 frontTileCenter = playerSnappedPos + dir * tileSize;

        int bombLayer = LayerMask.NameToLayer("Bomb");
        int bombMask = 1 << bombLayer;

        float searchBoxSize = tileSize * 0.55f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(frontTileCenter, Vector2.one * searchBoxSize, 0f, bombMask);

        if (hits == null || hits.Length == 0)
            return null;

        Bomb bestBomb = null;
        float bestScore = float.MaxValue;

        float maxDistanceFromFrontTile = tileSize * 0.35f;

        float maxPerpendicularOffset = tileSize * 0.15f;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            var candidate = hit.GetComponent<Bomb>();
            if (candidate == null)
                continue;

            Vector2 bombPos = candidate.GetLogicalPosition();
            Vector2 offsetFromPlayer = bombPos - playerSnappedPos;

            float forwardDistance = Vector2.Dot(offsetFromPlayer, dir);
            if (forwardDistance <= 0f)
                continue;

            Vector2 perpendicular = new Vector2(-dir.y, dir.x);
            float perpendicularOffset = Mathf.Abs(Vector2.Dot(offsetFromPlayer, perpendicular));
            if (perpendicularOffset > maxPerpendicularOffset)
                continue;

            float distanceFromFrontTile = Vector2.Distance(bombPos, frontTileCenter);
            if (distanceFromFrontTile > maxDistanceFromFrontTile)
                continue;

            bool acceptable = candidate.CanBePunched || CanEarlyPunch(candidate, playerSnappedPos, bombPos);
            if (!acceptable)
                continue;

            if (distanceFromFrontTile < bestScore)
            {
                bestScore = distanceFromFrontTile;
                bestBomb = candidate;
            }
        }

        if (bestBomb != null)
            return bestBomb;

        return FindKickConvertibleBombAhead(playerSnappedPos, dir);
    }

    private bool CanEarlyPunch(Bomb bomb, Vector2 playerPos, Vector2 bombPos)
    {
        if (bomb == null)
            return false;

        if (bomb.HasExploded || bomb.IsBeingKicked || bomb.IsBeingPunched)
            return false;

        float dist = Vector2.Distance(playerPos, bombPos);
        float minDist = movement.tileSize * earlyPunchMinExitFraction;

        bool exitedEnough = dist >= minDist;

        return exitedEnough;
    }

    private Bomb FindKickConvertibleBombAhead(Vector2 playerSnappedPos, Vector2 dir)
    {
        if (movement == null || kickAbility == null || !kickAbility.IsEnabled)
            return null;

        Vector2 perpendicular = new Vector2(-dir.y, dir.x);
        Vector2 preferredPunchOrigin = playerSnappedPos + dir * movement.tileSize;

        float maxForwardDistance = movement.tileSize * Mathf.Max(1f, earlyPunchSearchSize);
        float maxPerpendicularOffset = movement.tileSize * 0.35f;

        Bomb bestBomb = null;
        float bestScore = float.MaxValue;

        foreach (var candidate in Bomb.ActiveBombs)
        {
            if (candidate == null || candidate.HasExploded || candidate.IsBeingPunched)
                continue;

            if (!kickAbility.CanConvertKickToPunch(candidate))
                continue;

            Vector2 bombPos = candidate.GetLogicalPosition();
            Vector2 offsetFromPlayer = bombPos - playerSnappedPos;

            float forwardDistance = Vector2.Dot(offsetFromPlayer, dir);
            if (forwardDistance <= 0f || forwardDistance > maxForwardDistance)
                continue;

            float perpendicularOffset = Mathf.Abs(Vector2.Dot(offsetFromPlayer, perpendicular));
            if (perpendicularOffset > maxPerpendicularOffset)
                continue;

            float score = Vector2.Distance(bombPos, preferredPunchOrigin);
            if (score < bestScore)
            {
                bestScore = score;
                bestBomb = candidate;
            }
        }

        return bestBomb;
    }

    private IEnumerator PunchAnimLock(Vector2 dir)
    {
        bool wasLocked = movement.InputLocked;

        movement.SetInputLocked(true, false);

        if (externalAnimator != null)
        {
            yield return externalAnimator.Play(dir, punchLockTime);

            bool globalLock =
                GamePauseController.IsPaused ||
                MechaBossSequence.MechaIntroRunning ||
                ClownMaskBoss.BossIntroRunning ||
                (StageIntroTransition.Instance != null &&
                 (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

            movement.SetInputLocked(globalLock || wasLocked, false);
            punchLockRoutine = null;
            yield break;
        }

        prevMoveSprite = GetMoveSprite(dir);
        activePunchSprite = GetPunchSprite(dir);

        SetMoveSprites(false);
        SetPunchSprites(false);

        if (activePunchSprite != null)
        {
            activePunchSprite.enabled = true;
            activePunchSprite.idle = false;
            activePunchSprite.loop = false;
            activePunchSprite.CurrentFrame = 0;
            activePunchSprite.RefreshFrame();
        }

        yield return new WaitForSeconds(punchLockTime);

        if (activePunchSprite != null)
            activePunchSprite.enabled = false;

        if (prevMoveSprite != null)
        {
            prevMoveSprite.enabled = true;
            prevMoveSprite.idle = true;
        }

        bool globalLock2 =
            GamePauseController.IsPaused ||
            MechaBossSequence.MechaIntroRunning ||
            ClownMaskBoss.BossIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));

        movement.SetInputLocked(globalLock2 || wasLocked, false);
        punchLockRoutine = null;
    }

    private AnimatedSpriteRenderer GetMoveSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return movement.spriteRendererUp;
        if (dir == Vector2.down) return movement.spriteRendererDown;
        if (dir == Vector2.left) return movement.spriteRendererLeft;
        if (dir == Vector2.right) return movement.spriteRendererRight;

        if (movement.spriteRendererDown != null) return movement.spriteRendererDown;
        return null;
    }

    private AnimatedSpriteRenderer GetPunchSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return punchUp;
        if (dir == Vector2.down) return punchDown;
        if (dir == Vector2.left) return punchLeft;
        if (dir == Vector2.right) return punchRight;

        return punchDown;
    }

    private void SetMoveSprites(bool enabled)
    {
        if (movement.spriteRendererUp != null) movement.spriteRendererUp.enabled = enabled;
        if (movement.spriteRendererDown != null) movement.spriteRendererDown.enabled = enabled;
        if (movement.spriteRendererLeft != null) movement.spriteRendererLeft.enabled = enabled;
        if (movement.spriteRendererRight != null) movement.spriteRendererRight.enabled = enabled;
    }

    private void SetPunchSprites(bool enabled)
    {
        if (punchUp != null) punchUp.enabled = enabled;
        if (punchDown != null) punchDown.enabled = enabled;
        if (punchLeft != null) punchLeft.enabled = enabled;
        if (punchRight != null) punchRight.enabled = enabled;
    }

    public void Enable()
    {
        enabledAbility = true;
    }

    public void Disable()
    {
        enabledAbility = false;

        SetPunchSprites(false);
        externalAnimator?.ForceStop();

        if (punchLockRoutine != null)
        {
            StopCoroutine(punchLockRoutine);
            punchLockRoutine = null;
        }
    }

    public void ForceResetPunchSprites()
    {
        if (punchLockRoutine != null)
        {
            StopCoroutine(punchLockRoutine);
            punchLockRoutine = null;
        }

        externalAnimator?.ForceStop();

        SetPunchSprites(false);

        if (movement == null)
            return;

        SetMoveSprites(false);

        var sprite = GetMoveSprite(lastFacingDir);
        if (sprite == null) sprite = movement.spriteRendererDown;

        if (sprite != null)
        {
            sprite.enabled = true;
            sprite.idle = true;
            sprite.loop = true;
            sprite.RefreshFrame();
        }
    }
}
