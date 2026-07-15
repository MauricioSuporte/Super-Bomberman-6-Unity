using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AnimatedSpriteRenderer))]
public sealed class EndStagePortal : EndStage
{
    [Header("Visual")]
    [SerializeField] private AnimatedSpriteRenderer portalRenderer;

    [Header("Unlock Visual Mode")]
    [SerializeField] private bool enableLoopWhenUnlocked = true;

    private Collider2D col;
    private bool forceHiddenVisuals;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (portalRenderer == null)
            portalRenderer = GetComponent<AnimatedSpriteRenderer>();
    }

    protected override void OnStartSetup()
    {
        if (portalRenderer != null)
        {
            if (forceHiddenVisuals)
            {
                HideVisuals();
                return;
            }

            portalRenderer.enabled = true;

            portalRenderer.idle = true;
            portalRenderer.loop = false;
            portalRenderer.SetFrozen(false);

            portalRenderer.CurrentFrame = 0;
            portalRenderer.RefreshFrame();
        }
    }

    protected override void OnUnlocked()
    {
        if (portalRenderer != null)
        {
            if (forceHiddenVisuals)
            {
                HideVisuals();
                return;
            }

            portalRenderer.enabled = true;

            portalRenderer.idle = false;
            portalRenderer.loop = enableLoopWhenUnlocked;
            portalRenderer.SetFrozen(false);

            portalRenderer.CurrentFrame = 0;
            portalRenderer.RefreshFrame();
        }
    }

    public void SetForceHiddenVisuals(bool value)
    {
        forceHiddenVisuals = value;
        if (forceHiddenVisuals)
            HideVisuals();
    }

    private void HideVisuals()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].enabled = false;
        }

        AnimatedSpriteRenderer[] animatedRenderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < animatedRenderers.Length; i++)
        {
            if (animatedRenderers[i] != null)
                animatedRenderers[i].enabled = false;
        }
    }
}
