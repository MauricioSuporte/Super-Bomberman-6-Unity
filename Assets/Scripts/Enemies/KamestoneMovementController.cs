using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public sealed class KamestoneMovementController : JunctionTurningEnemyMovementController
{
    [Header("Invulnerability Ability")]
    [SerializeField, Min(0.01f)] private float minCooldownSeconds = 5f;
    [SerializeField, Min(0.01f)] private float maxCooldownSeconds = 10f;
    [SerializeField, Min(0.01f)] private float invulnerabilitySeconds = 5f;
    [SerializeField, Range(0f, 1f)] private float invulnerableAlpha = 0.5f;

    private CharacterHealth kamestoneHealth;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Coroutine abilityRoutine;
    private bool isInvulnerableAbilityActive;

    protected override void Awake()
    {
        base.Awake();

        kamestoneHealth = GetComponent<CharacterHealth>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
    }

    private void OnEnable()
    {
        if (abilityRoutine == null)
            abilityRoutine = StartCoroutine(AbilityLoop());
    }

    private void OnDisable()
    {
        if (abilityRoutine != null)
        {
            StopCoroutine(abilityRoutine);
            abilityRoutine = null;
        }

        EndInvulnerabilityAbility();
    }

    protected override void OnDestroy()
    {
        EndInvulnerabilityAbility();
        base.OnDestroy();
    }

    protected override void Die()
    {
        EndInvulnerabilityAbility();
        base.Die();
    }

    private IEnumerator AbilityLoop()
    {
        while (!isDead && isActiveAndEnabled)
        {
            float min = Mathf.Max(0.01f, minCooldownSeconds);
            float max = Mathf.Max(min, maxCooldownSeconds);
            yield return new WaitForSeconds(Random.Range(min, max));

            if (isDead || !isActiveAndEnabled)
                yield break;

            BeginInvulnerabilityAbility();
            yield return new WaitForSeconds(Mathf.Max(0.01f, invulnerabilitySeconds));
            EndInvulnerabilityAbility();
        }

        abilityRoutine = null;
    }

    private void BeginInvulnerabilityAbility()
    {
        if (isInvulnerableAbilityActive)
            return;

        isInvulnerableAbilityActive = true;

        if (kamestoneHealth != null)
            kamestoneHealth.SetExternalInvulnerability(true);

        ApplyAlpha(Mathf.Clamp01(invulnerableAlpha));
    }

    private void EndInvulnerabilityAbility()
    {
        if (!isInvulnerableAbilityActive)
            return;

        isInvulnerableAbilityActive = false;

        if (kamestoneHealth != null)
            kamestoneHealth.SetExternalInvulnerability(false);

        RestoreColors();
    }

    private void ApplyAlpha(float alpha)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            Color color = originalColors[i];
            color.a *= alpha;
            spriteRenderers[i].color = color;
        }
    }

    private void RestoreColors()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
    }
}
