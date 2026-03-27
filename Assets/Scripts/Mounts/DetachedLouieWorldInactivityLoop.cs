using UnityEngine;

[DisallowMultipleComponent]
public sealed class DetachedLouieWorldInactivityLoop : MonoBehaviour
{
    [SerializeField, Min(1f)] private float switchInterval = 15f;
    [SerializeField, Range(0f, 1f)] private float chanceAltAnimation = 0.3f;
    [SerializeField] private bool refreshFrameOnEnter = true;

    private MountVisualController visual;
    private MovementController louieMovement;

    private bool usingAlt;
    private float nextSwitchTime;
    private bool started;

    public void Init(
        MountVisualController targetVisual,
        MovementController targetMovement,
        float chanceAlt,
        bool refreshFrame,
        float intervalSeconds = 15f)
    {
        visual = targetVisual;
        louieMovement = targetMovement;
        chanceAltAnimation = Mathf.Clamp01(chanceAlt);
        refreshFrameOnEnter = refreshFrame;
        switchInterval = Mathf.Max(1f, intervalSeconds);

        StartLoop();
    }

    private void OnEnable()
    {
        if (started)
            nextSwitchTime = Time.time + switchInterval;
    }

    private void Update()
    {
        if (!started)
            return;

        if (visual == null || louieMovement == null)
        {
            Destroy(this);
            return;
        }

        if (louieMovement.isDead)
        {
            StopLoopForDeath();
            return;
        }

        if (Time.time >= nextSwitchTime)
        {
            SwitchRenderer();
            nextSwitchTime = Time.time + switchInterval;
        }
    }

    public void StopLoop()
    {
        if (visual != null)
            visual.SetInactivityEmote(false);

        started = false;
        Destroy(this);
    }

    private void StopLoopForDeath()
    {
        started = false;

        if (visual != null)
        {
            DisableRendererOnly(visual.LouieInactivityEmoteLoop);
            DisableRendererOnly(visual.LouieInactivityEmoteLoopAlt);

            visual.enabled = false;
        }

        Destroy(this);
    }

    private static void DisableRendererOnly(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = false;

        if (renderer.TryGetComponent(out SpriteRenderer sr) && sr != null)
            sr.enabled = false;
    }

    private void StartLoop()
    {
        if (visual == null || louieMovement == null)
            return;

        usingAlt = visual.LouieInactivityEmoteLoopAlt != null &&
                   Random.value <= chanceAltAnimation;

        ApplyCurrentRenderer();

        nextSwitchTime = Time.time + switchInterval;
        started = true;
    }

    private void SwitchRenderer()
    {
        if (visual == null)
            return;

        bool hasPrimary = visual.LouieInactivityEmoteLoop != null;
        bool hasAlt = visual.LouieInactivityEmoteLoopAlt != null;

        if (!hasPrimary && !hasAlt)
            return;

        if (hasPrimary && hasAlt)
            usingAlt = !usingAlt;
        else
            usingAlt = hasAlt;

        ApplyCurrentRenderer();
    }

    private void ApplyCurrentRenderer()
    {
        if (visual == null)
            return;

        var chosen = usingAlt
            ? visual.LouieInactivityEmoteLoopAlt
            : visual.LouieInactivityEmoteLoop;

        if (chosen == null)
            chosen = visual.LouieInactivityEmoteLoop != null
                ? visual.LouieInactivityEmoteLoop
                : visual.LouieInactivityEmoteLoopAlt;

        visual.SetInactivityEmote(chosen, refreshFrameOnEnter);
    }
}