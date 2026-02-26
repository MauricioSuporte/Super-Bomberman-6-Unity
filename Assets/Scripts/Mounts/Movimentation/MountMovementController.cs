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

        if (TryGetComponent<GreenLouieDashAbility>(out var dash))
            dash.CancelDashForDeath();

        if (owner != null && owner.TryGetComponent<BlackLouieDashPushAbility>(out var ownerBlackDash) && ownerBlackDash != null)
            ownerBlackDash.Disable();

        var dashAnim = GetComponentInChildren<GreenLouieDashAnimator>(true);
        if (dashAnim != null)
            dashAnim.CancelForDeath();

        if (TryGetComponent<RedLouiePunchStunAbility>(out var punch))
            punch.CancelPunchForDeath();

        var punchAnim = GetComponentInChildren<RedLouiePunchAnimator>(true);
        if (punchAnim != null)
            punchAnim.CancelForDeath();

        var blueAnim = GetComponentInChildren<BlueLouiePunchAnimator>(true);
        if (blueAnim != null)
            blueAnim.CancelForDeath();

        if (TryGetComponent<PinkLouieJumpAbility>(out var pinkJump))
            pinkJump.CancelJumpForDeath();

        var pinkAnim = GetComponentInChildren<PinkLouieJumpAnimator>(true);
        if (pinkAnim != null)
            pinkAnim.CancelForDeath();

        if (TryGetComponent<PurpleLouieBombLineAbility>(out var purpleLine) && purpleLine != null)
            purpleLine.CancelCastForDeath();

        var purpleAnim = GetComponentInChildren<PurpleLouieBombLineAnimator>(true);
        if (purpleAnim != null)
            purpleAnim.CancelForDeath();

        if (TryGetComponent<BlackLouieDashPushAbility>(out var blackDash) && blackDash != null)
            blackDash.CancelDashForDeath();

        var blackAnim = GetComponentInChildren<BlackLouieDashAnimator>(true);
        if (blackAnim != null)
            blackAnim.CancelForDeath();

        if (TryGetComponent<MountVisualController>(out var visual))
        {
            visual.SetInactivityEmote(false);
            visual.enabled = false;
        }

        base.DeathSequence();
    }
}
