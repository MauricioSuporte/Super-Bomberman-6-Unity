using Assets.Scripts.Interface;
using UnityEngine;

public class PinkLouieJumpAnimator : MonoBehaviour, IPinkLouieJumpExternalAnimator
{
    public AnimatedSpriteRenderer jumpUp;
    public AnimatedSpriteRenderer jumpDown;
    public AnimatedSpriteRenderer jumpLeft;
    public AnimatedSpriteRenderer jumpRight;

    [Header("Fix Right Local X")]
    public bool fixRightLocalX = true;
    public float rightLocalX = -0.3f;

    AnimatedSpriteRenderer active;
    LouieRiderVisual riderVisual;

    bool playing;

    void Awake()
    {
        riderVisual = GetComponent<LouieRiderVisual>();
        ForceOffJumpSprites();
    }

    void OnDisable() => Stop();
    void OnDestroy() => Stop();

    public void Play(Vector2 dir)
    {
        if (playing)
            Stop();

        if (dir == Vector2.zero)
            dir = Vector2.down;

        ForceDisableRiderVisualRenderers();

        if (riderVisual != null)
            riderVisual.enabled = false;

        active = GetSprite(dir);

        SetRendererEnabled(jumpUp, active == jumpUp);
        SetRendererEnabled(jumpDown, active == jumpDown);
        SetRendererEnabled(jumpLeft, active == jumpLeft);
        SetRendererEnabled(jumpRight, active == jumpRight);

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            if (fixRightLocalX)
            {
                if (dir == Vector2.right)
                    active.SetRuntimeBaseLocalX(rightLocalX);
                else
                    active.ClearRuntimeBaseLocalX();
            }
            else
            {
                active.ClearRuntimeBaseLocalX();
            }

            active.enabled = true;
            active.idle = false;
            active.loop = true;
            active.CurrentFrame = 0;
            active.RefreshFrame();

            if (active.TryGetComponent<SpriteRenderer>(out var asr))
                asr.enabled = true;
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing)
            return;

        if (active != null)
        {
            if (active.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = false;

            if (fixRightLocalX)
                active.ClearRuntimeBaseLocalX();

            SetRendererEnabled(active, false);
        }

        active = null;

        ForceOffJumpSprites();

        if (riderVisual != null)
        {
            riderVisual.enabled = true;
            ForceDisableRiderVisualRenderers();
        }

        playing = false;
    }

    AnimatedSpriteRenderer GetSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return jumpUp;
        if (dir == Vector2.down) return jumpDown;
        if (dir == Vector2.left) return jumpLeft;
        if (dir == Vector2.right) return jumpRight;
        return jumpDown;
    }

    void ForceOffJumpSprites()
    {
        SetRendererEnabled(jumpUp, false);
        SetRendererEnabled(jumpDown, false);
        SetRendererEnabled(jumpLeft, false);
        SetRendererEnabled(jumpRight, false);
    }

    void SetRendererEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }

    void ForceDisableRiderVisualRenderers()
    {
        if (riderVisual == null)
            return;

        SetRendererEnabled(riderVisual.louieUp, false);
        SetRendererEnabled(riderVisual.louieDown, false);
        SetRendererEnabled(riderVisual.louieLeft, false);
        SetRendererEnabled(riderVisual.louieRight, false);
        SetRendererEnabled(riderVisual.louieEndStage, false);
    }
}
