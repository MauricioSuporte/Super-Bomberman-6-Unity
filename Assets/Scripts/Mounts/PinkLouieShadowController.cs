using UnityEngine;

public class PinkLouieShadowController : MonoBehaviour
{
    public Transform followTarget;

    [Header("Follow")]
    public float localY = -0.2f;
    public float worldZ = 0f;

    [Header("Animator")]
    public PinkLouieShadowAnimator animator;

    bool jumping;
    Vector2 jumpGroundPos;
    bool hasJumpGroundPos;

    MovementController cachedOwner;
    bool initialSyncDone;

    void Awake()
    {
        TryAutoWire();
        SyncAnimatorState();
    }

    void Start()
    {
        TryAutoWire();
        SyncAnimatorState();
    }

    void OnEnable()
    {
        TryAutoWire();
        SyncAnimatorState();
    }

    void LateUpdate()
    {
        TryAutoWire();

        if (followTarget != null)
        {
            Vector2 basePos = (jumping && hasJumpGroundPos)
                ? jumpGroundPos
                : (Vector2)followTarget.position;

            transform.position = new Vector3(basePos.x, basePos.y + localY, worldZ);
        }

        SyncAnimatorState();
        initialSyncDone = true;
    }

    void TryAutoWire()
    {
        if (animator == null)
            animator = GetComponentInChildren<PinkLouieShadowAnimator>(true);

        var louieVisual = GetComponentInParent<MountVisualController>();

        if (followTarget == null)
            followTarget = louieVisual != null ? louieVisual.transform : transform.parent;

        if (cachedOwner == null)
        {
            if (louieVisual != null && louieVisual.owner != null)
                cachedOwner = louieVisual.owner;
            else
                cachedOwner = GetComponentInParent<MovementController>();
        }
    }

    void SyncAnimatorState()
    {
        if (animator == null)
            return;

        if (jumping)
        {
            animator.SetJumping(true);
            animator.SetMoving(false);
            return;
        }

        animator.SetJumping(false);
        animator.SetMoving(IsOwnerMoving());
    }

    bool IsOwnerMoving()
    {
        if (cachedOwner == null)
        {
            var louieVisual = GetComponentInParent<MountVisualController>();
            cachedOwner = (louieVisual != null && louieVisual.owner != null)
                ? louieVisual.owner
                : GetComponentInParent<MovementController>();
        }

        if (cachedOwner == null)
            return false;

        if (cachedOwner.isDead)
            return false;

        if (cachedOwner.InputLocked)
            return false;

        return cachedOwner.Direction != Vector2.zero;
    }

    public void BindToPinkLouieRoot(Transform pinkLouieRoot)
    {
        followTarget = pinkLouieRoot;
        cachedOwner = null;

        TryAutoWire();

        if (followTarget != null)
            transform.position = new Vector3(
                followTarget.position.x,
                followTarget.position.y + localY,
                worldZ);

        SyncAnimatorState();
    }

    public void BeginJump(Vector2 groundPos)
    {
        jumping = true;
        jumpGroundPos = groundPos;
        hasJumpGroundPos = true;

        SyncAnimatorState();
    }

    public void SetJumpGroundPosition(Vector2 groundPos)
    {
        if (!jumping)
            return;

        jumpGroundPos = groundPos;
        hasJumpGroundPos = true;
    }

    public void EndJump()
    {
        jumping = false;
        hasJumpGroundPos = false;

        SyncAnimatorState();
    }
}