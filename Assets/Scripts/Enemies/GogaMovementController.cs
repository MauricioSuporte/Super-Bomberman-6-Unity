using System.Collections;
using UnityEngine;

public sealed class GogaMovementController : JunctionTurningEnemyMovementController
{
    private const float SubmergedDuration = 8f, FrameDuration = .12f;
    [Header("Submerge Ability")]
    [SerializeField, Min(0.1f)] private float bombVisionDistance = 10f;
    [SerializeField, Min(0f)] private float reemergeAbilityCooldown = 10f;

    private enum State { Surface, Entering, Submerged, Exiting }
    private AnimatedSpriteRenderer downVisual, upVisual, leftVisual, submergeVisual, underwaterVisual, deathVisual;
    private CharacterHealth gogaHealth;
    private Sprite[] downFrames, upFrames, leftFrames, submergeFrames, underwaterFrames;
    private State state;
    private float stateTimer;
    private float abilityCooldownTimer;

    protected override void Awake()
    {
        downVisual = transform.Find("Down")?.GetComponent<AnimatedSpriteRenderer>();
        upVisual = transform.Find("Up")?.GetComponent<AnimatedSpriteRenderer>();
        leftVisual = transform.Find("Left")?.GetComponent<AnimatedSpriteRenderer>();
        submergeVisual = transform.Find("Submerge")?.GetComponent<AnimatedSpriteRenderer>();
        underwaterVisual = transform.Find("Underwater")?.GetComponent<AnimatedSpriteRenderer>();
        deathVisual = transform.Find("Death")?.GetComponent<AnimatedSpriteRenderer>();
        ApplyDownMaterialToVisualChildren();
        LoadConfiguredFrames();
        spriteDown = downVisual;
        spriteUp = upVisual;
        spriteLeft = leftVisual;
        spriteRight = leftVisual;
        spriteDeath = deathVisual;
        gogaHealth = GetComponent<CharacterHealth>();
        base.Awake();
    }

    protected override void Start() { base.Start(); state = State.Surface; UpdateSpriteDirection(direction); }
    protected override void FixedUpdate()
    {
        if (isDead) return;

        if (state == State.Surface)
        {
            abilityCooldownTimer = Mathf.Max(0f, abilityCooldownTimer - Time.fixedDeltaTime);
            if (abilityCooldownTimer <= 0f && TrySeeBomb())
            {
                StartCoroutine(EnterSubmersion());
                return;
            }

            base.FixedUpdate();
        }
        else if (state == State.Submerged)
        {
            base.FixedUpdate();
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f) StartCoroutine(ExitSubmersion());
        }
        else if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (state == State.Entering || state == State.Exiting) return;
        AnimatedSpriteRenderer visual = state == State.Submerged ? underwaterVisual : dir == Vector2.up ? upVisual : dir == Vector2.down ? downVisual : leftVisual;
        SetFrames(visual, state == State.Submerged ? underwaterFrames : dir == Vector2.up ? upFrames : dir == Vector2.down ? downFrames : leftFrames, true);
        if (visual != null && visual.TryGetComponent(out SpriteRenderer renderer)) renderer.flipX = dir == Vector2.right;
    }
    protected override void Die() { if (gogaHealth != null) gogaHealth.SetExternalInvulnerability(false); base.Die(); }
    private IEnumerator EnterSubmersion()
    {
        state = State.Entering;
        SnapToGrid();
        yield return PlayOnce(true);

        // Only the underwater phase is invulnerable.
        if (gogaHealth != null) gogaHealth.SetExternalInvulnerability(true);
        state = State.Submerged;
        stateTimer = SubmergedDuration;
        SetFrames(underwaterVisual, underwaterFrames, true);
    }

    private IEnumerator ExitSubmersion()
    {
        state = State.Exiting;
        // The same six frames play from last to first before Goga can be hit again.
        yield return PlayOnce(false);

        if (gogaHealth != null) gogaHealth.SetExternalInvulnerability(false);
        state = State.Surface;
        abilityCooldownTimer = reemergeAbilityCooldown;
        UpdateSpriteDirection(direction);
    }

    private bool TrySeeBomb()
    {
        if (rb == null || bombLayerMask.value == 0)
            return false;

        int collisionMask = obstacleMask | bombLayerMask;
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 scanDirection = directions[i];
            Vector2 origin = rb.position + scanDirection * (tileSize * 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(origin, scanDirection, bombVisionDistance, collisionMask);

            if (hit.collider != null && ((1 << hit.collider.gameObject.layer) & bombLayerMask) != 0)
                return true;
        }

        return false;
    }
    private IEnumerator PlayOnce(bool forward)
    {
        if (submergeVisual == null || submergeFrames == null || submergeFrames.Length == 0) yield break;
        ShowOnly(submergeVisual); submergeVisual.animationSprite = submergeFrames; submergeVisual.SetManualAnimationUpdate(true); submergeVisual.idle = false; submergeVisual.loop = false;
        int last = submergeFrames.Length - 1;
        for (int i = forward ? 0 : last; forward ? i <= last : i >= 0; i += forward ? 1 : -1) { submergeVisual.CurrentFrame = i; submergeVisual.RefreshFrame(); yield return new WaitForSeconds(FrameDuration); }
        submergeVisual.SetManualAnimationUpdate(false);
    }
    private void SetFrames(AnimatedSpriteRenderer visual, Sprite[] frames, bool loop)
    {
        if (visual == null || frames == null || frames.Length == 0) return;
        ShowOnly(visual); visual.idleSprite = frames[0]; visual.animationSprite = frames; visual.animationTime = FrameDuration; visual.loop = loop; visual.idle = false; visual.enabled = true; activeSprite = visual;
    }
    private void LoadConfiguredFrames()
    {
        downFrames = downVisual?.animationSprite;
        upFrames = upVisual?.animationSprite;
        leftFrames = leftVisual?.animationSprite;
        submergeFrames = submergeVisual?.animationSprite;
        underwaterFrames = underwaterVisual?.animationSprite;

    }

    private void ApplyDownMaterialToVisualChildren()
    {
        SpriteRenderer downRenderer = downVisual != null ? downVisual.GetComponent<SpriteRenderer>() : null;
        if (downRenderer == null || downRenderer.sharedMaterial == null)
            return;

        foreach (AnimatedSpriteRenderer visual in new[] { upVisual, leftVisual, submergeVisual, underwaterVisual, deathVisual })
        {
            if (visual == null || !visual.TryGetComponent(out SpriteRenderer renderer))
                continue;

            if (renderer.sharedMaterial == null)
                renderer.sharedMaterial = downRenderer.sharedMaterial;
        }
    }
    private void ShowOnly(AnimatedSpriteRenderer selected)
    {
        foreach (AnimatedSpriteRenderer visual in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (visual == null || visual == deathVisual)
                continue;

            bool isSelected = visual == selected;
            visual.enabled = isSelected;
            if (visual.TryGetComponent(out SpriteRenderer renderer))
                renderer.enabled = isSelected;
        }
    }
}
