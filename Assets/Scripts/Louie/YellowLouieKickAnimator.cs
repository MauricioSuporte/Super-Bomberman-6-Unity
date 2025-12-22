using Assets.Scripts.Interface;
using UnityEngine;

public class YellowLouieKickAnimator : MonoBehaviour, IYellowLouieDestructibleKickExternalAnimator
{
    public AnimatedSpriteRenderer kickUp;
    public AnimatedSpriteRenderer kickDown;
    public AnimatedSpriteRenderer kickLeft;
    public AnimatedSpriteRenderer kickRight;

    AnimatedSpriteRenderer active;
    AnimatedSpriteRenderer cachedMovementSprite;

    public void Play(Vector2 dir)
    {
        CacheAndDisableMovementSprite();

        active = GetKickSprite(dir);
        DisableAll();

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
    }

    public void Stop()
    {
        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            active.enabled = false;
        }

        active = null;
        DisableAll();
        RestoreMovementSprite();
    }

    AnimatedSpriteRenderer GetKickSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return kickUp;
        if (dir == Vector2.down) return kickDown;
        if (dir == Vector2.left) return kickLeft;
        if (dir == Vector2.right) return kickRight;
        return kickDown;
    }

    void DisableAll()
    {
        if (kickUp != null) kickUp.enabled = false;
        if (kickDown != null) kickDown.enabled = false;
        if (kickLeft != null) kickLeft.enabled = false;
        if (kickRight != null) kickRight.enabled = false;
    }

    void CacheAndDisableMovementSprite()
    {
        cachedMovementSprite = null;

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        foreach (var s in sprites)
        {
            if (s == kickUp || s == kickDown || s == kickLeft || s == kickRight)
                continue;

            if (s.enabled)
            {
                cachedMovementSprite = s;
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
}
