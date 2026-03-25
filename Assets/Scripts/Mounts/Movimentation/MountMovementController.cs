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
        RedLouiePunchStunAbility ownerPunch = null;
        GreenLouieDashAbility ownerDash = null;
        BombPunchAbility ownerBluePunch = null;
        PinkLouieJumpAbility ownerPinkJump = null;
        PurpleLouieBombLineAbility ownerPurpleLine = null;
        YellowLouieKickAbility ownerYellowKick = null;
        BlackLouieDashPushAbility ownerBlackDash = null;
        TankMountShootAbility ownerTankShoot = null;
        MoleMountDrillAbility ownerMoleDrill = null;

        bool ownerHadRedPunch = false;
        bool ownerHadGreenDash = false;
        bool ownerHadBluePunch = false;
        bool ownerHadPinkJump = false;
        bool ownerHadPurpleLine = false;
        bool ownerHadYellowKick = false;
        bool ownerHadBlackDash = false;
        bool ownerHadTankShoot = false;
        bool ownerHadMoleDrill = false;

        if (owner != null)
        {
            ownerHadRedPunch = owner.TryGetComponent(out ownerPunch) && ownerPunch != null;
            ownerHadGreenDash = owner.TryGetComponent(out ownerDash) && ownerDash != null;
            ownerHadBluePunch = owner.TryGetComponent(out ownerBluePunch) && ownerBluePunch != null;
            ownerHadPinkJump = owner.TryGetComponent(out ownerPinkJump) && ownerPinkJump != null;
            ownerHadPurpleLine = owner.TryGetComponent(out ownerPurpleLine) && ownerPurpleLine != null;
            ownerHadYellowKick = owner.TryGetComponent(out ownerYellowKick) && ownerYellowKick != null;
            ownerHadBlackDash = owner.TryGetComponent(out ownerBlackDash) && ownerBlackDash != null;
            ownerHadTankShoot = owner.TryGetComponent(out ownerTankShoot) && ownerTankShoot != null;
            ownerHadMoleDrill = owner.TryGetComponent(out ownerMoleDrill) && ownerMoleDrill != null;
        }

        if (ownerHadRedPunch)
            ownerPunch.Disable();

        if (ownerHadGreenDash)
            ownerDash.Disable();

        // IMPORTANTE: NÃO desabilitar BombPunchAbility do owner
        // if (ownerHadBluePunch)
        //     ownerBluePunch.Disable();

        if (ownerHadPinkJump)
            ownerPinkJump.Disable();

        if (ownerHadPurpleLine)
            ownerPurpleLine.Disable();

        if (ownerHadYellowKick)
            ownerYellowKick.Disable();

        if (ownerHadBlackDash)
            ownerBlackDash.Disable();

        if (ownerHadTankShoot)
            ownerTankShoot.Disable();

        if (ownerHadMoleDrill)
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

        if (TryGetComponent<YellowLouieKickAbility>(out var yellowKick) && yellowKick != null)
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

        AnimatedSpriteRenderer rendererToUse =
            deathRequestedByExplosion && spriteRendererDeathByExplosion != null
                ? spriteRendererDeathByExplosion
                : spriteRendererDeath;

        if (spriteRendererDeath != null && spriteRendererDeath != rendererToUse)
        {
            spriteRendererDeath.enabled = false;
            if (spriteRendererDeath.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = false;
        }

        if (spriteRendererDeathByExplosion != null && spriteRendererDeathByExplosion != rendererToUse)
        {
            spriteRendererDeathByExplosion.enabled = false;
            if (spriteRendererDeathByExplosion.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = false;
        }

        if (rendererToUse != null)
        {
            rendererToUse.enabled = true;

            if (rendererToUse.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = true;

            rendererToUse.idle = false;
            rendererToUse.loop = false;
            rendererToUse.pingPong = false;
            rendererToUse.CurrentFrame = 0;
            rendererToUse.RefreshFrame();

            activeSpriteRenderer = rendererToUse;
        }
    }
}
