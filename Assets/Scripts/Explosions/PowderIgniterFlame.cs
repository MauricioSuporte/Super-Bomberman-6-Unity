using System.Collections;
using UnityEngine;

public sealed class PowderIgniterFlame : MonoBehaviour
{
    public enum FlameKind
    {
        Horizontal,
        Vertical,
        CurveDownLeft,
        CurveDownRight,
        CurveUpLeft,
        CurveUpRight
    }

    public AnimatedSpriteRenderer horizontal;
    public AnimatedSpriteRenderer vertical;
    public AnimatedSpriteRenderer curveDownLeft;
    public AnimatedSpriteRenderer curveDownRight;
    public AnimatedSpriteRenderer curveUpLeft;
    public AnimatedSpriteRenderer curveUpRight;

    [SerializeField, Min(0.01f)] private float durationSeconds = 0.5f;

    public void Play(FlameKind kind, float durationOverride = -1f)
    {
        if (durationOverride > 0f)
            durationSeconds = durationOverride;

        DisableAll();

        var r = ResolveRenderer(kind);
        if (r != null)
        {
            r.enabled = true;
            r.RefreshFrame();
        }

        StopAllCoroutines();
        StartCoroutine(LifeRoutine());
    }

    private IEnumerator LifeRoutine()
    {
        float d = Mathf.Max(0.01f, durationSeconds);
        yield return new WaitForSeconds(d);
        Destroy(gameObject);
    }

    private AnimatedSpriteRenderer ResolveRenderer(FlameKind kind)
    {
        return kind switch
        {
            FlameKind.Horizontal => horizontal,
            FlameKind.Vertical => vertical,
            FlameKind.CurveDownLeft => curveDownLeft,
            FlameKind.CurveDownRight => curveDownRight,
            FlameKind.CurveUpLeft => curveUpLeft,
            FlameKind.CurveUpRight => curveUpRight,
            _ => null,
        };
    }

    private void DisableAll()
    {
        if (horizontal != null) horizontal.enabled = false;
        if (vertical != null) vertical.enabled = false;
        if (curveDownLeft != null) curveDownLeft.enabled = false;
        if (curveDownRight != null) curveDownRight.enabled = false;
        if (curveUpLeft != null) curveUpLeft.enabled = false;
        if (curveUpRight != null) curveUpRight.enabled = false;
    }
}
