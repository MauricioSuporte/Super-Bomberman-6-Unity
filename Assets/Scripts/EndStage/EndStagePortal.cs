using UnityEngine;

[RequireComponent(typeof(AnimatedSpriteRenderer))]
public sealed class EndStagePortal : EndStage
{
    [Header("Visual")]
    public AnimatedSpriteRenderer idleRenderer;

    void Awake()
    {
        if (idleRenderer == null)
            idleRenderer = GetComponent<AnimatedSpriteRenderer>();
    }

    protected override void OnStartSetup()
    {
        if (idleRenderer != null)
        {
            idleRenderer.idle = true;
            idleRenderer.loop = false;
        }
    }

    protected override void OnUnlocked()
    {
        if (idleRenderer != null)
        {
            idleRenderer.idle = false;
            idleRenderer.loop = true;
        }
    }
}
