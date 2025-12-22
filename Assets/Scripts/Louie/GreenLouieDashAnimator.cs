using UnityEngine;

public class GreenLouieDashAnimator : MonoBehaviour, IGreenLouieDashExternalAnimator
{
    public AnimatedSpriteRenderer rollUp;
    public AnimatedSpriteRenderer rollDown;
    public AnimatedSpriteRenderer rollLeft;
    public AnimatedSpriteRenderer rollRight;

    AnimatedSpriteRenderer active;
    AnimatedSpriteRenderer cachedMovement;

    public void Play(Vector2 dir)
    {
        CacheAndDisableMovementSprite();

        active = GetRoll(dir);
        DisableAll();

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            active.enabled = true;
            active.idle = false;
            active.loop = true;
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

    AnimatedSpriteRenderer GetRoll(Vector2 dir)
    {
        if (dir == Vector2.up) return rollUp;
        if (dir == Vector2.down) return rollDown;
        if (dir == Vector2.left) return rollLeft;
        if (dir == Vector2.right) return rollRight;
        return rollDown;
    }

    void DisableAll()
    {
        if (rollUp) rollUp.enabled = false;
        if (rollDown) rollDown.enabled = false;
        if (rollLeft) rollLeft.enabled = false;
        if (rollRight) rollRight.enabled = false;
    }

    void CacheAndDisableMovementSprite()
    {
        cachedMovement = null;

        var sprites = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        foreach (var s in sprites)
        {
            if (s == rollUp || s == rollDown || s == rollLeft || s == rollRight)
                continue;

            if (s.enabled)
            {
                cachedMovement = s;
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
