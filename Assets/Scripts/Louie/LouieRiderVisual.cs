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
        {
            if (active != null)
                active.enabled = false;

            target.enabled = true;
            active = target;
        }

        active.idle = isIdle;

        if (active.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (faceDir == Vector2.right) sr.flipX = true;
            else if (faceDir == Vector2.left) sr.flipX = false;
        }
    }

    public bool TryPlayEndStage(float totalTime, int frameCount)
    {
        if (louieEndStage == null)
            return false;

        playingEndStage = true;

        if (louieUp != null) louieUp.enabled = false;
        if (louieDown != null) louieDown.enabled = false;
        if (louieLeft != null) louieLeft.enabled = false;

        louieEndStage.enabled = true;
        louieEndStage.idle = false;
        louieEndStage.loop = true;
        louieEndStage.CurrentFrame = 0;
        louieEndStage.RefreshFrame();

        if (frameCount > 0)
            louieEndStage.animationTime = totalTime / frameCount;

        active = louieEndStage;

        return true;
    }
}
