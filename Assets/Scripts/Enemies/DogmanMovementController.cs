using System.Collections;
using UnityEngine;

/// <summary>
/// Dogman follows normal junction-turning navigation, but can briefly stop at
/// a tile center to play its defensive ability before continuing its route.
/// </summary>
public sealed class DogmanMovementController : JunctionTurningEnemyMovementController
{
    [Header("Defensive Ability")]
    [SerializeField] private AnimatedSpriteRenderer abilitySprite;
    [SerializeField, Range(0f, 1f)] private float chanceToUseAbilityAtJunction = 0.2f;
    [SerializeField, Min(0f)] private float abilityCooldownSeconds = 3f;

    private CharacterHealth dogmanHealth;
    private Coroutine abilityRoutine;
    private float abilityCooldownRemaining;

    protected override void Awake()
    {
        base.Awake();

        dogmanHealth = GetComponent<CharacterHealth>();
        SetAbilityVisualEnabled(false);
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (abilityCooldownRemaining > 0f)
            abilityCooldownRemaining = Mathf.Max(0f, abilityCooldownRemaining - Time.fixedDeltaTime);

        if (abilityRoutine != null)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    protected override void DecideNextTile()
    {
        if (abilityRoutine != null || isDead)
        {
            if (rb != null)
                targetTile = rb.position;

            return;
        }

        if (abilityCooldownRemaining <= 0f &&
            chanceToUseAbilityAtJunction > 0f &&
            IsAtJunction() &&
            Random.value <= chanceToUseAbilityAtJunction)
        {
            abilityRoutine = StartCoroutine(UseAbilityRoutine());
            return;
        }

        base.DecideNextTile();
    }

    protected override void Die()
    {
        StopAbility();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopAbility();
        base.OnDestroy();
    }

    private IEnumerator UseAbilityRoutine()
    {
        if (rb != null)
        {
            SnapToGrid();
            rb.linearVelocity = Vector2.zero;
            targetTile = rb.position;
        }

        if (dogmanHealth != null)
            dogmanHealth.SetExternalInvulnerability(true);

        SetAbilityVisualEnabled(true);

        if (abilitySprite != null)
            yield return abilitySprite.PlayCycles(1);

        abilityRoutine = null;

        if (dogmanHealth != null)
            dogmanHealth.SetExternalInvulnerability(false);

        if (isDead)
            yield break;

        SetAbilityVisualEnabled(false);
        UpdateSpriteDirection(direction);

        if (abilityCooldownSeconds > 0f)
            abilityCooldownRemaining = abilityCooldownSeconds;

        DecideNextTile();
    }

    private bool IsAtJunction()
    {
        if (rb == null)
            return false;

        int availablePaths = 0;
        for (int i = 0; i < Dirs.Length; i++)
        {
            if (!IsTileBlocked(rb.position + Dirs[i] * tileSize))
                availablePaths++;
        }

        return availablePaths >= minAvailablePathsToTurn;
    }

    private void StopAbility()
    {
        if (abilityRoutine != null)
        {
            StopCoroutine(abilityRoutine);
            abilityRoutine = null;
        }

        if (dogmanHealth != null)
            dogmanHealth.SetExternalInvulnerability(false);

        SetAbilityVisualEnabled(false);
    }

    private void SetAbilityVisualEnabled(bool enabled)
    {
        if (abilitySprite == null)
            return;

        if (enabled)
        {
            SetVisualEnabled(spriteUp, false);
            SetVisualEnabled(spriteDown, false);
            SetVisualEnabled(spriteLeft, false);
            SetVisualEnabled(spriteRight, false);
        }

        abilitySprite.enabled = enabled;

        if (abilitySprite.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer visual, bool enabled)
    {
        if (visual == null)
            return;

        visual.enabled = enabled;

        if (visual.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }
}
