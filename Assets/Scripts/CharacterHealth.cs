using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(IKillable))]
public class CharacterHealth : MonoBehaviour
{
    [Header("Health")]
    public int life = 1;

    [Header("Hit / Invulnerability")]
    public float hitInvulnerableDuration = 3f;
    public float hitBlinkInterval = 0.1f;

    [Header("Temporary Invulnerability Visual")]
    [Range(0f, 1f)]
    public float tempSlowdownStartNormalized = 0.7f;
    public float tempEndBlinkMultiplier = 4f;

    public event Action<int> Damaged;
    public event Action Died;

    bool isInvulnerable;
    bool isDead;

    SpriteRenderer[] spriteRenderers;
    Color[] originalColors;
    Coroutine hitRoutine;

    IKillable killable;

    public bool IsInvulnerable => isInvulnerable;

    void Awake()
    {
        killable = GetComponent<IKillable>();

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            originalColors[i] = spriteRenderers[i].color;
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        if (isInvulnerable)
            return;

        life -= amount;

        Damaged?.Invoke(amount);

        if (life <= 0)
        {
            life = 0;
            Die();
            return;
        }

        if (hitInvulnerableDuration > 0f)
            StartHitInvulnerability();
    }

    public void AddLife(int amount)
    {
        life += amount;
        if (life < 0)
            life = 0;
    }

    public void StartTemporaryInvulnerability(float seconds)
        => StartTemporaryInvulnerability(seconds, withBlink: true);

    public void StartTemporaryInvulnerability(float seconds, bool withBlink)
    {
        if (seconds <= 0f)
            return;

        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        if (withBlink)
            hitRoutine = StartCoroutine(TemporaryInvulnerabilityRoutine(seconds));
        else
            hitRoutine = StartCoroutine(TemporaryInvulnerabilityNoBlinkRoutine(seconds));
    }

    public void StopInvulnerability()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        isInvulnerable = false;

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
    }

    IEnumerator TemporaryInvulnerabilityNoBlinkRoutine(float seconds)
    {
        isInvulnerable = true;

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];

        yield return new WaitForSeconds(seconds);

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];

        isInvulnerable = false;
        hitRoutine = null;
    }

    IEnumerator TemporaryInvulnerabilityRoutine(float seconds)
    {
        isInvulnerable = true;
        float elapsed = 0f;
        bool faded = false;

        float baseInterval = hitBlinkInterval > 0f ? hitBlinkInterval : 0.05f;
        float endInterval = baseInterval * Mathf.Max(1f, tempEndBlinkMultiplier);

        while (elapsed < seconds)
        {
            faded = !faded;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null)
                    continue;

                Color baseColor = originalColors[i];

                if (faded)
                    spriteRenderers[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
                else
                    spriteRenderers[i].color = baseColor;
            }

            float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
            float slowT = Mathf.InverseLerp(tempSlowdownStartNormalized, 1f, t);
            float wait = Mathf.Lerp(baseInterval, endInterval, slowT);

            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];

        isInvulnerable = false;
        hitRoutine = null;
    }

    void StartHitInvulnerability()
    {
        if (hitRoutine != null)
            StopCoroutine(hitRoutine);

        hitRoutine = StartCoroutine(HitInvulnerabilityRoutine());
    }

    IEnumerator HitInvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float elapsed = 0f;
        bool faded = false;

        while (elapsed < hitInvulnerableDuration)
        {
            faded = !faded;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null)
                    continue;

                Color baseColor = originalColors[i];

                if (faded)
                    spriteRenderers[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
                else
                    spriteRenderers[i].color = baseColor;
            }

            float wait = hitBlinkInterval > 0f ? hitBlinkInterval : 0.05f;
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];

        isInvulnerable = false;
        hitRoutine = null;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        isInvulnerable = false;

        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];

        Died?.Invoke();

        if (killable != null)
            killable.Kill();
        else
            Destroy(gameObject);
    }
}
