using System.Collections;
using UnityEngine;

public sealed class CoreMechanismsDestructible : Destructible
{
    [SerializeField] private AnimatedSpriteRenderer animationRenderer;
    [SerializeField] private AnimatedSpriteRenderer deathRenderer;
    [SerializeField, Min(0.01f)] private float deathDurationSeconds = 0.5f;
    [SerializeField] private bool playDeathOnStart;

    private bool dying;

    void Awake()
    {
        ResolveRenderers();
        ShowAlive();
    }

    private void Start()
    {
        if (playDeathOnStart)
            PlayDeath();
    }

    public void PlayDeath()
    {
        if (dying)
            return;

        dying = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SetColliders(false);
        SetRenderer(animationRenderer, false);

        if (deathRenderer != null)
        {
            deathRenderer.gameObject.SetActive(true);
            deathRenderer.enabled = true;
            deathRenderer.idle = false;
            deathRenderer.loop = false;
            deathRenderer.useSequenceDuration = true;
            deathRenderer.sequenceDuration = Mathf.Max(0.01f, deathDurationSeconds);
            deathRenderer.CurrentFrame = 0;
            deathRenderer.RestartAnimation();
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, deathDurationSeconds));
        Destroy(gameObject);
    }

    private void ShowAlive()
    {
        SetColliders(true);

        if (animationRenderer != null)
        {
            animationRenderer.gameObject.SetActive(true);
            animationRenderer.enabled = true;
            animationRenderer.idle = false;
            animationRenderer.loop = true;
        }

        SetRenderer(deathRenderer, false);
    }

    private void ResolveRenderers()
    {
        if (animationRenderer == null)
            animationRenderer = FindRenderer("Animation");

        if (deathRenderer == null)
            deathRenderer = FindRenderer("Death");
    }

    private AnimatedSpriteRenderer FindRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null && child.TryGetComponent(out AnimatedSpriteRenderer renderer))
            return renderer;

        return null;
    }

    private static void SetRenderer(AnimatedSpriteRenderer renderer, bool enabled)
    {
        if (renderer == null)
            return;

        renderer.enabled = enabled;

        if (renderer.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

    private void SetColliders(bool enabled)
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;
    }
}
