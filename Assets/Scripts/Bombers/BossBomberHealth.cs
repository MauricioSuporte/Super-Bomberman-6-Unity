using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public class BossBomberHealth : MonoBehaviour
{
    public int life = 3;
    public float hitInvulnerableDuration = 3f;
    public float hitBlinkInterval = 0.1f;

    bool isInvulnerable;
    bool isDead;

    SpriteRenderer[] spriteRenderers;
    Color[] originalColors;
    Coroutine hitRoutine;
    MovementController movement;

    void Awake()
    {
        movement = GetComponent<MovementController>();

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

        if (life <= 0)
        {
            life = 0;
            Die();
            return;
        }

        if (hitInvulnerableDuration > 0f)
            StartHitInvulnerability();
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

        if (movement != null)
            movement.Kill();
        else
            Destroy(gameObject);
    }
}
