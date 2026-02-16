using UnityEngine;

public class MountMovementController : MovementController
{
    [Header("Louie Follow")]
    [SerializeField] private MovementController owner;
    [SerializeField] private Vector2 followOffset = new(-0.35f, -0.15f);
    [SerializeField] private float followLerp = 25f;

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

        if (owner == null || owner.isDead)
        {
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
            activeSpriteRenderer.idle = (dir == Vector2.zero);
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
        if (TryGetComponent<MountVisualController>(out var visual))
        {
            visual.SetInactivityEmote(false);
            visual.enabled = false;
        }

        base.DeathSequence();
    }
}
