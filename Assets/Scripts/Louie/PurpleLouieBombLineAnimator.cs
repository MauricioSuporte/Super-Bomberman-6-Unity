using System.Collections;
using UnityEngine;

public class PurpleLouieBombLineAnimator : MonoBehaviour, IPurpleLouieBombLineExternalAnimator
{
    [Header("Magic Sprites (PURPLE LOUIE)")]
    public AnimatedSpriteRenderer magicUp;
    public AnimatedSpriteRenderer magicDown;
    public AnimatedSpriteRenderer magicLeft;
    public AnimatedSpriteRenderer magicRight;

    AnimatedSpriteRenderer active;
    AnimatedSpriteRenderer cachedMovement;

    public IEnumerator Play(Vector2 dir, float lockSeconds)
    {
        CacheAndDisableMovementSprite();

        active = GetMagic(dir);
        DisableAllMagic();

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            active.enabled = true;
            active.idle = false;
            active.loop = false;
            active.CurrentFrame = 0;
            active.RefreshFrame();
        }

        yield return new WaitForSeconds(lockSeconds);

        ForceStop();
    }

    public void ForceStop()
    {
        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            active.enabled = false;
        }

        active = null;
        DisableAllMagic();
        RestoreMovementSprite();
    }

    AnimatedSpriteRenderer GetMagic(Vector2 dir)
    {
        if (dir == Vector2.up) return magicUp;
        if (dir == Vector2.down) return magicDown;
        if (dir == Vector2.left) return magicLeft;
        if (dir == Vector2.right) return magicRight;
        return magicDown;
    }

    void DisableAllMagic()
    {
        if (magicUp != null) magicUp.enabled = false;
        if (magicDown != null) magicDown.enabled = false;
        if (magicLeft != null) magicLeft.enabled = false;
        if (magicRight != null) magicRight.enabled = false;
    }

    void CacheAndDisableMovementSprite()
    {
        cachedMovement = null;

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == magicUp || sprites[i] == magicDown || sprites[i] == magicLeft || sprites[i] == magicRight)
                continue;

            if (sprites[i].enabled)
            {
                cachedMovement = sprites[i];
                cachedMovement.enabled = false;
                break;
            }
        }
    }

    void RestoreMovementSprite()
    {
        if (cachedMovement != null)
        {
            cachedMovement.enabled = true;
            cachedMovement.idle = true;
            cachedMovement.RefreshFrame();
            cachedMovement = null;
        }
    }
}
