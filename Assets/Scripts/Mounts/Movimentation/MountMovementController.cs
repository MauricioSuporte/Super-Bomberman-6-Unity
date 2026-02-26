using UnityEngine;

public class MountMovementController : MovementController
{
    [Header("Louie Follow")]
    [SerializeField] private MovementController owner;
    [SerializeField] private Vector2 followOffset = new(-0.35f, -0.15f);
    [SerializeField] private float followLerp = 25f;

    [Header("Visual")]
    [SerializeField] private bool keepMoveAnimationWhenIdle = false;

    private Vector2 lastNonZeroDir = Vector2.down;

    public void BindOwner(MovementController ownerMovement, Vector2 offset)
    {
        owner = ownerMovement;
        followOffset = offset;

        if (owner != null)
        {
            Vector2 startFacing = owner.FacingDirection;
            if (startFacing != Vector2.zero)
                lastNonZeroDir = startFacing;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (bombController != null)
            bombController.enabled = false;

        obstacleMask = 0;

        SetExplosionInvulnerable(true);
    }

    protected override void Update()
    {
        if (GamePauseController.IsPaused)
            return;

        if (isDead)
            return;

        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (owner.isDead)
        {
            if (owner.IsHoleDeathInProgress)
                return;

            Destroy(gameObject);
            return;
        }

        if (BoatRideZone.IsRidingBoat(owner))
            return;

        Vector2 dir = owner.Direction;

        if (dir != Vector2.zero)
            lastNonZeroDir = dir;
        else if (owner.FacingDirection != Vector2.zero)
            lastNonZeroDir = owner.FacingDirection;

        ApplyDirectionFromVector(lastNonZeroDir);

        if (activeSpriteRenderer != null)
        {
            if (keepMoveAnimationWhenIdle)
            {
                activeSpriteRenderer.idle = false;
                activeSpriteRenderer.loop = true;
            }
            else
            {
                activeSpriteRenderer.idle = (dir == Vector2.zero);
                activeSpriteRenderer.loop = (dir != Vector2.zero);
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (GamePauseController.IsPaused)
            return;

        if (isDead)
            return;

        if (owner == null)
            return;

        Vector2 desired = owner.Rigidbody != null
            ? owner.Rigidbody.position + followOffset
            : (Vector2)owner.transform.position + followOffset;

        Vector2 current = Rigidbody.position;
        float t = 1f - Mathf.Exp(-followLerp * Time.fixedDeltaTime);
        Vector2 next = Vector2.Lerp(current, desired, t);

        Rigidbody.MovePosition(next);
    }

    protected override void OnTriggerEnter2D(Collider2D other) { }

    protected override void DeathSequence()
    {
        if (owner != null && owner.TryGetComponent<RedLouiePunchStunAbility>(out var ownerPunch) && ownerPunch != null)
            ownerPunch.Disable();

        if (owner != null && owner.TryGetComponent<GreenLouieDashAbility>(out var ownerDash) && ownerDash != null)
            ownerDash.Disable();

        if (owner != null && owner.TryGetComponent<BombPunchAbility>(out var ownerBluePunch) && ownerBluePunch != null)
            ownerBluePunch.Disable();

        if (owner != null && owner.TryGetComponent<PinkLouieJumpAbility>(out var ownerPinkJump) && ownerPinkJump != null)
            ownerPinkJump.Disable();

        if (owner != null && owner.TryGetComponent<PurpleLouieBombLineAbility>(out var ownerPurpleLine) && ownerPurpleLine != null)
            ownerPurpleLine.Disable();

        if (owner != null && owner.TryGetComponent<YellowLouieDestructibleKickAbility>(out var ownerYellowKick) && ownerYellowKick != null)
            ownerYellowKick.Disable();

        if (owner != null && owner.TryGetComponent<BlackLouieDashPushAbility>(out var ownerBlackDash) && ownerBlackDash != null)
            ownerBlackDash.Disable();

        if (owner != null && owner.TryGetComponent<TankMountShootAbility>(out var ownerTankShoot) && ownerTankShoot != null)
            ownerTankShoot.Disable();

        if (owner != null && owner.TryGetComponent<MoleMountDrillAbility>(out var ownerMoleDrill) && ownerMoleDrill != null)
            ownerMoleDrill.Disable();

        if (TryGetComponent<GreenLouieDashAbility>(out var dash) && dash != null)
            dash.CancelDashForDeath();

        if (TryGetComponent<RedLouiePunchStunAbility>(out var punch) && punch != null)
            punch.CancelPunchForDeath();

        if (TryGetComponent<PinkLouieJumpAbility>(out var pinkJump) && pinkJump != null)
            pinkJump.CancelJumpForDeath();

        if (TryGetComponent<PurpleLouieBombLineAbility>(out var purpleLine) && purpleLine != null)
            purpleLine.CancelCastForDeath();

        if (TryGetComponent<BlackLouieDashPushAbility>(out var blackDash) && blackDash != null)
            blackDash.CancelDashForDeath();

        if (TryGetComponent<YellowLouieDestructibleKickAbility>(out var yellowKick) && yellowKick != null)
            yellowKick.CancelKickForDeath();

        if (TryGetComponent<TankMountShootAbility>(out var tankShoot) && tankShoot != null)
            tankShoot.CancelShootForDeath();

        if (TryGetComponent<MoleMountDrillAbility>(out var moleDrill) && moleDrill != null)
            moleDrill.CancelDrillForDeath();

        var dashAnim = GetComponentInChildren<GreenLouieDashAnimator>(true);
        if (dashAnim != null) dashAnim.CancelForDeath();

        var punchAnim = GetComponentInChildren<RedLouiePunchAnimator>(true);
        if (punchAnim != null) punchAnim.CancelForDeath();

        var blueAnim = GetComponentInChildren<BlueLouiePunchAnimator>(true);
        if (blueAnim != null) blueAnim.CancelForDeath();

        var pinkAnim = GetComponentInChildren<PinkLouieJumpAnimator>(true);
        if (pinkAnim != null) pinkAnim.CancelForDeath();

        var purpleAnim = GetComponentInChildren<PurpleLouieBombLineAnimator>(true);
        if (purpleAnim != null) purpleAnim.CancelForDeath();

        var blackAnim = GetComponentInChildren<BlackLouieDashAnimator>(true);
        if (blackAnim != null) blackAnim.CancelForDeath();

        var yellowAnim = GetComponentInChildren<YellowLouieKickAnimator>(true);
        if (yellowAnim != null) yellowAnim.CancelForDeath();

        var tankAnim = GetComponentInChildren<TankMountShootAnimator>(true);
        if (tankAnim != null) tankAnim.CancelForDeath();

        var moleAnim = GetComponentInChildren<MoleMountDrillAnimator>(true);
        if (moleAnim != null) moleAnim.CancelForDeath();

        if (TryGetComponent<MountVisualController>(out var visual))
        {
            visual.SetInactivityEmote(false);
            visual.enabled = false;
        }

        base.DeathSequence();
    }
}
