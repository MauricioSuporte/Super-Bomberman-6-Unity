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

    void Awake()
    {
        TryAutoWire();

        if (animator != null)
        {
            animator.SetJumping(false);
            animator.SetMoving(false);
        }
    }

    void LateUpdate()
    {
        TryAutoWire();

        if (followTarget == null)
            return;

        Vector2 basePos = (jumping && hasJumpGroundPos) ? jumpGroundPos : (Vector2)followTarget.position;

        transform.position = new Vector3(basePos.x, basePos.y + localY, worldZ);

        if (animator != null)
        {
            if (jumping)
                animator.SetMoving(false);
            else
                animator.SetMoving(IsOwnerMoving());
        }
    }

    void TryAutoWire()
    {
        if (animator == null)
            animator = GetComponentInChildren<PinkLouieShadowAnimator>(true);

        if (followTarget == null)
        {
            var louieVisual = GetComponentInParent<LouieVisualController>();
            followTarget = louieVisual != null ? louieVisual.transform : transform.parent;
        }

        if (cachedOwner == null)
        {
            var louieVisual = GetComponentInParent<LouieVisualController>();
            if (louieVisual != null && louieVisual.owner != null)
                cachedOwner = louieVisual.owner;
            else
                cachedOwner = GetComponentInParent<MovementController>();
        }
    }

    bool IsOwnerMoving()
    {
        if (cachedOwner == null)
        {
            var louieVisual = GetComponentInParent<LouieVisualController>();
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

        if (animator != null && !jumping)
        {
            animator.SetJumping(false);
            animator.SetMoving(IsOwnerMoving());
        }
    }

    public void BeginJump(Vector2 groundPos)
    {
        jumping = true;
        jumpGroundPos = groundPos;
        hasJumpGroundPos = true;

        if (animator != null)
        {
            animator.SetJumping(true);
            animator.SetMoving(false);
        }
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

        if (animator != null)
        {
            animator.SetJumping(false);
            animator.SetMoving(IsOwnerMoving());
        }
    }
}
