using System.Collections;
using UnityEngine;

public class BombExplosion : MonoBehaviour
{
    public AnimatedSpriteRenderer start;
    public AnimatedSpriteRenderer middle;
    public AnimatedSpriteRenderer end;

    public enum ExplosionPart { Start, Middle, End }

    public ExplosionPart CurrentPart { get; private set; }
    public Vector2 Origin { get; private set; }

    public void SetOrigin(Vector2 origin) => Origin = origin;

    public void SetStart() => SetRenderer(start, ExplosionPart.Start);
    public void SetMiddle() => SetRenderer(middle, ExplosionPart.Middle);
    public void SetEnd() => SetRenderer(end, ExplosionPart.End);

    public void UpgradeToMiddleIfNeeded()
    {
        if (CurrentPart == ExplosionPart.End) SetMiddle();
    }

    void SetRenderer(AnimatedSpriteRenderer renderer, ExplosionPart part)
    {
        if (start != null) start.enabled = renderer == start;
        if (middle != null) middle.enabled = renderer == middle;
        if (end != null) end.enabled = renderer == end;
        CurrentPart = part;
    }

    public void SetDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x);
        transform.rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward);
    }

    public void DestroyAfter(float seconds) => Destroy(gameObject, seconds);

    public void Play(ExplosionPart part, Vector2 direction, float delay, float duration, Vector2 origin)
    {
        SetOrigin(origin);
        SetDirection(direction);
        StartCoroutine(PlayRoutine(part, delay, duration));
    }

    IEnumerator PlayRoutine(ExplosionPart part, float delay, float duration)
    {
        if (start != null) start.enabled = false;
        if (middle != null) middle.enabled = false;
        if (end != null) end.enabled = false;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        switch (part)
        {
            case ExplosionPart.Start: SetStart(); break;
            case ExplosionPart.Middle: SetMiddle(); break;
            case ExplosionPart.End: SetEnd(); break;
        }

        if (duration <= 0f) { DestroyAfter(0f); yield break; }

        yield return new WaitForSeconds(duration);

        DestroyAfter(0f);
    }
}