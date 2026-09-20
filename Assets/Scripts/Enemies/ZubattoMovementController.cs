using UnityEngine;

/// <summary>
/// Penguin movement and explosion evasion with Zubatto's shared-direction jump
/// animation: ascend, a flip at the apex, descend, then a landing stand.
/// </summary>
public sealed class ZubattoMovementController : PenguinMovementController
{
    [Header("Zubatto Jump Visuals")]
    [SerializeField] private AnimatedSpriteRenderer ascend;
    [SerializeField] private AnimatedSpriteRenderer flip;
    [SerializeField] private AnimatedSpriteRenderer descend;
    [SerializeField] private AnimatedSpriteRenderer stand;

    [Header("Zubatto Jump Phases")]
    [SerializeField, Min(0f)] private float flipSeconds = 0.1f;

    protected override void Awake()
    {
        ResolveVisuals();
        base.Awake();
    }

    protected override void ShowJumpVisual(Vector2 jumpDirection)
    {
        SetZubattoVisual(ascend);
    }

    protected override void Die()
    {
        SetZubattoVisual(null);
        base.Die();
    }

    protected override void ApplyJumpArc(float progress)
    {
        float elapsed = Mathf.Clamp01(progress) * jumpDurationSeconds;
        float flipDuration = Mathf.Min(flipSeconds, jumpDurationSeconds);
        float ascendDuration = Mathf.Max(0.01f, (jumpDurationSeconds - flipDuration) * 0.5f);
        float flipStart = ascendDuration;
        float flipEnd = flipStart + flipDuration;

        AnimatedSpriteRenderer visual;
        if (elapsed < flipStart)
        {
            visual = ascend;
        }
        else if (elapsed < flipEnd)
        {
            visual = flip;
        }
        else
        {
            visual = descend;
        }

        SetZubattoVisual(visual);
        float heightProgress = elapsed < flipStart
            ? elapsed / ascendDuration
            : elapsed < flipEnd
                ? 1f
                : 1f - Mathf.Clamp01((elapsed - flipEnd) / ascendDuration);
        float height = Mathf.Round(heightProgress * jumpHeightTiles * tileSize * Mathf.Max(1, pixelsPerUnit)) / Mathf.Max(1, pixelsPerUnit);
        if (visual != null)
            visual.SetExternalBaseOffsetFromInitial(Vector3.up * height);
    }

    protected override void BeginLandingPause()
    {
        base.BeginLandingPause();
        SetZubattoVisual(stand);
    }

    protected override void ResumeWalking()
    {
        SetZubattoVisual(null);
        base.ResumeWalking();
        // When the next tile is directly ahead, DecideNextTile preserves the
        // current direction without calling UpdateSpriteDirection. Stand hid
        // that renderer, so explicitly restore it before the first movement.
        UpdateSpriteDirection(direction);
    }

    /// <summary>Lets a Rocket Penguin impact trigger the same evasion as an explosion.</summary>
    public bool TryEvadeRocketImpact(Vector2 impactPosition)
    {
        return TryEvadeExplosion();
    }

    protected override void SetJumpVisualsEnabled(bool enabled)
    {
        if (!enabled)
            SetZubattoVisual(null);
    }

    protected override void ClearJumpArc()
    {
        ClearOffset(ascend);
        ClearOffset(flip);
        ClearOffset(descend);
        ClearOffset(stand);
    }

    private void ResolveVisuals()
    {
        ascend ??= FindVisual("Ascend");
        flip ??= FindVisual("Flip");
        descend ??= FindVisual("Descend");
        stand ??= FindVisual("Stand");
    }

    private AnimatedSpriteRenderer FindVisual(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private void SetZubattoVisual(AnimatedSpriteRenderer selected)
    {
        if (selected != null)
            SetWalkingVisualsEnabled(false);

        SetVisible(ascend, selected == ascend);
        SetVisible(flip, selected == flip);
        SetVisible(descend, selected == descend);
        SetVisible(stand, selected == stand);

    }

    private void SetWalkingVisualsEnabled(bool enabled)
    {
        if (spriteUp != null) spriteUp.enabled = enabled;
        if (spriteDown != null) spriteDown.enabled = enabled;
        if (spriteLeft != null) spriteLeft.enabled = enabled;
        if (spriteRight != null) spriteRight.enabled = enabled;
    }

    private void SetVisible(AnimatedSpriteRenderer visual, bool visible)
    {
        if (visual == null)
            return;

        if (visible)
        {
            if (visual == flip)
            {
                visual.loop = false;
                visual.useSequenceDuration = true;
                visual.sequenceDuration = Mathf.Max(0.0001f, flipSeconds);
            }

            visual.enabled = true;
            visual.idle = false;
            return;
        }

        visual.enabled = false;
    }

    private static void ClearOffset(AnimatedSpriteRenderer visual)
    {
        if (visual != null)
            visual.ClearExternalBase();
    }
}
