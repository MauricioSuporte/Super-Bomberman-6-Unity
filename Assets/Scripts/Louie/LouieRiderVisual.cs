using UnityEngine;

public class LouieRiderVisual : MonoBehaviour
{
    [Header("Owner")]
    public MovementController owner;

    [Header("Visual Offset (local)")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Sprites (Louie)")]
    public AnimatedSpriteRenderer louieUp;
    public AnimatedSpriteRenderer louieDown;
    public AnimatedSpriteRenderer louieLeft;

    [Header("End Stage (Louie)")]
    public AnimatedSpriteRenderer louieEndStage;

    private AnimatedSpriteRenderer active;
    private bool playingEndStage;

    public void Bind(MovementController movement)
    {
        owner = movement;
        playingEndStage = false;

        SetExclusive(louieDown != null ? louieDown : louieUp);
        ApplyDirection(Vector2.down, true);
    }

    private void LateUpdate()
    {
        if (owner == null || owner.isDead)
        {
            Destroy(gameObject);
            return;
        }

        transform.localPosition = localOffset;

        if (playingEndStage)
            return;

        bool isIdle = owner.Direction == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

        ApplyDirection(faceDir, isIdle);
    }

    private void ApplyDirection(Vector2 faceDir, bool isIdle)
    {
        AnimatedSpriteRenderer target;

        if (faceDir == Vector2.up)
            target = louieUp;
        else if (faceDir == Vector2.down)
            target = louieDown;
        else
            target = louieLeft;

        if (target == null)
            return;

        if (active != target)
            SetExclusive(target);

        active.idle = isIdle;

        if (active.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (faceDir == Vector2.right) sr.flipX = true;
            else if (faceDir == Vector2.left) sr.flipX = false;
        }

        active.RefreshFrame();
    }

    private void SetExclusive(AnimatedSpriteRenderer keep)
    {
        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            bool on = (a == keep);
            a.enabled = on;

            if (a.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = on;
        }

        if (louieUp != null && louieUp != keep) louieUp.enabled = false;
        if (louieDown != null && louieDown != keep) louieDown.enabled = false;
        if (louieLeft != null && louieLeft != keep) louieLeft.enabled = false;
        if (louieEndStage != null && louieEndStage != keep) louieEndStage.enabled = false;

        keep.enabled = true;
        if (keep.TryGetComponent<SpriteRenderer>(out var keepSr))
            keepSr.enabled = true;

        active = keep;
    }

    public bool TryPlayEndStage(float totalTime, int frameCount)
    {
        if (louieEndStage == null)
            return false;

        playingEndStage = true;

        SetExclusive(louieEndStage);

        louieEndStage.idle = false;
        louieEndStage.loop = true;
        louieEndStage.CurrentFrame = 0;
        louieEndStage.RefreshFrame();

        if (frameCount > 0)
            louieEndStage.animationTime = totalTime / frameCount;

        return true;
    }

    public void ForceIdleUp()
    {
        playingEndStage = false;

        if (louieUp == null)
            return;

        SetExclusive(louieUp);

        louieUp.idle = true;
        louieUp.loop = false;
        louieUp.RefreshFrame();
    }

    public void ForceOnlyUpEnabled()
    {
        ForceIdleUp();
    }
}
