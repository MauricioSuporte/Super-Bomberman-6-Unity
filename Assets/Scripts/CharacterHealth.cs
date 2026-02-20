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

    [Header("Damaged Animation Instead Of Blink")]
    public bool playDamagedLoopInsteadOfBlink;

    public event Action<int> Damaged;
    public event Action Died;

    public event Action<float> HitInvulnerabilityStarted;
    public event Action HitInvulnerabilityEnded;

    bool isInvulnerable;
    bool isDead;

    SpriteRenderer[] spriteRenderers;
    Color[] originalColors;
    Coroutine hitRoutine;

    IKillable killable;

    public bool IsInvulnerable => isInvulnerable;
    private bool externalInvulnerability;

    // --- Spawn blink override control ---
    private int _blinkOverrideToken;
    private Coroutine _restoreBlinkIntervalRoutine;

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
        if (isDead || isInvulnerable || externalInvulnerability)
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

    // NOVO: invencibilidade “real” de spawn + blink com intervalo customizado (restaura depois)
    public void StartSpawnInvulnerability(float seconds, float blinkInterval)
    {
        if (seconds <= 0f)
            return;

        float previousInterval = hitBlinkInterval;

        _blinkOverrideToken++;
        int token = _blinkOverrideToken;

        if (blinkInterval > 0f)
            hitBlinkInterval = blinkInterval;

        StartTemporaryInvulnerability(seconds, withBlink: true);

        if (_restoreBlinkIntervalRoutine != null)
            StopCoroutine(_restoreBlinkIntervalRoutine);

        _restoreBlinkIntervalRoutine = StartCoroutine(RestoreBlinkIntervalAfter(seconds, token, previousInterval));
    }

    IEnumerator RestoreBlinkIntervalAfter(float seconds, int token, float previousInterval)
    {
        yield return new WaitForSeconds(seconds);

        if (token != _blinkOverrideToken)
            yield break;

        hitBlinkInterval = previousInterval;
        _restoreBlinkIntervalRoutine = null;
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

        HitInvulnerabilityStarted?.Invoke(hitInvulnerableDuration);
        hitRoutine = StartCoroutine(HitInvulnerabilityRoutine());
    }

    IEnumerator HitInvulnerabilityRoutine()
    {
        isInvulnerable = true;

        if (!playDamagedLoopInsteadOfBlink)
        {
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
        }
        else
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = originalColors[i];

            yield return new WaitForSeconds(hitInvulnerableDuration);
        }

        isInvulnerable = false;
        hitRoutine = null;
        HitInvulnerabilityEnded?.Invoke();
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

    public void SetExternalInvulnerability(bool value)
    {
        externalInvulnerability = value;

        if (externalInvulnerability)
        {
            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                hitRoutine = null;
            }

            isInvulnerable = true;

            for (int i = 0; i < spriteRenderers.Length; i++)
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = originalColors[i];
        }
        else
        {
            isInvulnerable = false;

            for (int i = 0; i < spriteRenderers.Length; i++)
                if (spriteRenderers[i] != null)
                    spriteRenderers[i].color = originalColors[i];
        }
    }
}