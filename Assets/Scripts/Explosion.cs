using UnityEngine;

public class Explosion : MonoBehaviour
{
    public AnimatedSpriteRenderer start;
    public AnimatedSpriteRenderer middle;
    public AnimatedSpriteRenderer end;

    public enum ExplosionPart
    {
        Start,
        Middle,
        End
    }

    public ExplosionPart CurrentPart { get; private set; }


    public void SetStart()
    {
        SetRenderer(start, ExplosionPart.Start);
    }

    public void SetMiddle()
    {
        SetRenderer(middle, ExplosionPart.Middle);
    }

    public void SetEnd()
    {
        SetRenderer(end, ExplosionPart.End);
    }

    public void UpgradeToMiddleIfNeeded()
    {
        if (CurrentPart == ExplosionPart.End)
        {
            SetMiddle();
        }
    }

    private void SetRenderer(AnimatedSpriteRenderer renderer, ExplosionPart part)
    {
        start.enabled = (renderer == start);
        middle.enabled = (renderer == middle);
        end.enabled = (renderer == end);

        CurrentPart = part;
    }

    public void SetDirection(Vector2 diretion)
    {
        float angle = Mathf.Atan2(diretion.y, diretion.x);
        transform.rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward);
    }

    public void DestroyAfter(float seconds)
    {
        Destroy(gameObject, seconds);
    }
}
