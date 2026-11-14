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
            Assets.Scripts.Netcode.BombermanNetworkManager.CoopTrackRenderer(r.GetComponent<SpriteRenderer>());
        }

        StopAllCoroutines();
        StartCoroutine(LifeRoutine());
    }

    // A Chunk tilemap has one sorting anchor for the whole stage. Sharing its
    // order can hide flames in the far end even though their renderers are enabled.
    public void SetGroundSorting(Renderer ground)
    {
        if (ground == null) return;
        int order = Mathf.Min(short.MaxValue, ground.sortingOrder + 1);
        foreach (var animation in new[] { horizontal, vertical, curveDownLeft, curveDownRight, curveUpLeft, curveUpRight })
            if (animation != null && animation.TryGetComponent<SpriteRenderer>(out var sprite))
            { sprite.sortingLayerID = ground.sortingLayerID; sprite.sortingOrder = order; }
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
