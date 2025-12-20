using System.Collections;
using UnityEngine;

public class BlueLouiePunchAnimator : MonoBehaviour, IBombPunchExternalAnimator
{
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    AnimatedSpriteRenderer activePunch;
    AnimatedSpriteRenderer cachedMovementSprite;

    public IEnumerator Play(Vector2 dir, float punchLockTime)
    {
        CacheAndDisableMovementSprite();

        var target = GetPunchSprite(dir);
        if (target != null)
        {
            DisableAllPunch();
            activePunch = target;

            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            activePunch.enabled = true;
            activePunch.idle = false;
            activePunch.loop = false;
            activePunch.CurrentFrame = 0;
            activePunch.RefreshFrame();
        }

        yield return new WaitForSeconds(punchLockTime);

        ForceStop();
    }

    public void ForceStop()
    {
        if (activePunch != null)
        {
            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            activePunch.enabled = false;
        }

        activePunch = null;
        DisableAllPunch();
        RestoreMovementSprite();
    }

    void CacheAndDisableMovementSprite()
    {
        cachedMovementSprite = null;

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == punchUp ||
                sprites[i] == punchDown ||
                sprites[i] == punchLeft ||
                sprites[i] == punchRight)
                continue;

            if (sprites[i].enabled)
            {
                cachedMovementSprite = sprites[i];
                cachedMovementSprite.enabled = false;
                break;
            }
        }
    }

    void RestoreMovementSprite()
    {
        if (cachedMovementSprite != null)
        {
            cachedMovementSprite.enabled = true;
            cachedMovementSprite.idle = true;
            cachedMovementSprite.RefreshFrame();
            cachedMovementSprite = null;
        }
    }

    AnimatedSpriteRenderer GetPunchSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return punchUp;
        if (dir == Vector2.down) return punchDown;
        if (dir == Vector2.left) return punchLeft;
        if (dir == Vector2.right) return punchRight;
        return punchDown;
    }

    void DisableAllPunch()
    {
        if (punchUp != null) punchUp.enabled = false;
        if (punchDown != null) punchDown.enabled = false;
        if (punchLeft != null) punchLeft.enabled = false;
        if (punchRight != null) punchRight.enabled = false;
    }
}
