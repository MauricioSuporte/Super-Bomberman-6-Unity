using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(ClownMaskMovement))]
public class ClownMaskBoss : MonoBehaviour, IKillable
{
    [Header("References")]
    public CharacterHealth characterHealth;
    public ClownMaskMovement clownMovement;
    public BossEndStageSequence bossEndSequence;

    [Header("Renderers")]
    public AnimatedSpriteRenderer introRenderer;
    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer specialRenderer;
    public AnimatedSpriteRenderer hurtRenderer;
    public AnimatedSpriteRenderer attackRenderer;

    [Header("Intro")]
    public float introDuration = 2f;

    [Header("Special Animation")]
    public float minSpecialInterval = 5f;
    public float maxSpecialInterval = 12f;

    [Header("After Hit")]
    public float extraHitStopDuringAttack = 0.3f;
    public float delayBeforeAttack = 0.1f;

    [Header("Hurt State")]
    public float hurtAnimationDuration = 0.5f;
    public float hurtWalkDuration = 3f;

    [Header("Attack")]
    public ClownStarProjectile clownStarProjectile;
    public float starSpeed = 6f;
    public float starLifeTime = 3f;
    public float starSpawnRadius = 0.5f;

    bool isDead;
    bool introFinished;
    bool inDamageSequence;
    bool isInHurtWalk;

    Coroutine specialRoutine;
    Coroutine damageSequenceRoutine;

    void Awake()
    {
        if (!characterHealth)
            characterHealth = GetComponent<CharacterHealth>();

        if (!clownMovement)
            clownMovement = GetComponent<ClownMaskMovement>();

        if (!bossEndSequence)
            bossEndSequence = FindFirstObjectByType<BossEndStageSequence>();

        if (characterHealth != null)
        {
            characterHealth.Damaged += OnHealthDamaged;
            characterHealth.Died += OnHealthDied;
        }

        EnableOnly(introRenderer);
    }

    void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Damaged -= OnHealthDamaged;
            characterHealth.Died -= OnHealthDied;
        }
    }

    void OnEnable()
    {
        StartCoroutine(IntroSequence());
    }

    IEnumerator IntroSequence()
    {
        if (clownMovement != null)
            clownMovement.enabled = false;

        if (StageIntroTransition.Instance != null)
        {
            while (StageIntroTransition.Instance.IntroRunning)
                yield return null;
        }

        EnableOnly(introRenderer);

        float duration = introDuration;

        if (introRenderer != null)
        {
            if (duration <= 0f)
            {
                if (introRenderer.useSequenceDuration && introRenderer.sequenceDuration > 0f)
                    duration = introRenderer.sequenceDuration;
                else if (introRenderer.animationSprite != null && introRenderer.animationSprite.Length > 0)
                    duration = introRenderer.animationTime * introRenderer.animationSprite.Length;
                else
                    duration = 2f;
            }
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        EnableOnly(idleRenderer);

        if (clownMovement != null)
            clownMovement.enabled = true;

        introFinished = true;
        specialRoutine = StartCoroutine(SpecialLoop());
    }

    IEnumerator SpecialLoop()
    {
        while (!isDead)
        {
            float wait = Random.Range(minSpecialInterval, maxSpecialInterval);
            yield return new WaitForSeconds(wait);

            if (isDead || !introFinished || inDamageSequence)
                continue;

            yield return PlaySpecialOnce();
        }
    }

    IEnumerator PlaySpecialOnce()
    {
        if (specialRenderer == null)
            yield break;

        AnimatedSpriteRenderer previous = GetCurrentRenderer();

        EnableOnly(specialRenderer);

        float duration;

        if (specialRenderer.useSequenceDuration && specialRenderer.sequenceDuration > 0f)
            duration = specialRenderer.sequenceDuration;
        else if (specialRenderer.animationSprite != null && specialRenderer.animationSprite.Length > 0)
            duration = specialRenderer.animationTime * specialRenderer.animationSprite.Length;
        else
            duration = 1f;

        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            if (previous != null && previous != specialRenderer)
                EnableOnly(previous);
            else
                EnableOnly(idleRenderer);
        }
    }

    void OnHealthDamaged(int amount)
    {
        if (isDead)
            return;

        bool wasInHurtWalk = isInHurtWalk;
        bool wasInHurtAnim = hurtRenderer != null && hurtRenderer.enabled && !hurtRenderer.idle;
        bool useHurtIdleDuringInvuln = wasInHurtWalk || wasInHurtAnim;

        if (damageSequenceRoutine != null)
            StopCoroutine(damageSequenceRoutine);

        damageSequenceRoutine = StartCoroutine(DamageSequenceRoutine(useHurtIdleDuringInvuln));
    }

    IEnumerator DamageSequenceRoutine(bool useHurtIdleDuringInvuln)
    {
        inDamageSequence = true;
        isInHurtWalk = false;

        if (clownMovement != null)
            clownMovement.OnHit();

        SpawnStarBurst();

        float invulDuration = characterHealth != null ? characterHealth.hitInvulnerableDuration : 0f;

        if (useHurtIdleDuringInvuln)
        {
            if (hurtRenderer != null)
            {
                EnableOnly(hurtRenderer);
                if (hurtRenderer.animationSprite != null && hurtRenderer.animationSprite.Length > 0)
                {
                    int first = 0;
                    hurtRenderer.CurrentFrame = first;
                    hurtRenderer.idleSprite = hurtRenderer.animationSprite[first];
                }
                hurtRenderer.idle = true;
                hurtRenderer.loop = false;
            }
        }
        else
        {
            if (idleRenderer != null)
            {
                EnableOnly(idleRenderer);
                idleRenderer.idle = true;
                idleRenderer.loop = true;
            }
        }

        if (invulDuration > 0f)
            yield return new WaitForSeconds(invulDuration);

        if (isDead)
        {
            inDamageSequence = false;
            isInHurtWalk = false;
            damageSequenceRoutine = null;
            yield break;
        }

        if (hurtRenderer != null && hurtRenderer.animationSprite != null && hurtRenderer.animationSprite.Length > 0)
        {
            EnableOnly(hurtRenderer);
            hurtRenderer.loop = false;
            hurtRenderer.idle = false;

            int frames = hurtRenderer.animationSprite.Length;
            if (hurtAnimationDuration > 0f && frames > 0)
                hurtRenderer.animationTime = hurtAnimationDuration / frames;

            float hurtDuration = hurtAnimationDuration > 0f ? hurtAnimationDuration : 0.5f;
            yield return new WaitForSeconds(hurtDuration);

            if (isDead)
            {
                inDamageSequence = false;
                isInHurtWalk = false;
                damageSequenceRoutine = null;
                yield break;
            }

            int last = hurtRenderer.animationSprite.Length - 1;
            hurtRenderer.CurrentFrame = last;
            hurtRenderer.idleSprite = hurtRenderer.animationSprite[last];
            hurtRenderer.idle = true;
            hurtRenderer.loop = false;

            isInHurtWalk = true;

            float walkDuration = hurtWalkDuration > 0f ? hurtWalkDuration : 3f;
            float elapsed = 0f;

            while (elapsed < walkDuration && !isDead)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        isInHurtWalk = false;
        inDamageSequence = false;
        damageSequenceRoutine = null;

        if (!isDead && idleRenderer != null)
            EnableOnly(idleRenderer);
    }

    void SpawnStarBurst()
    {
        if (clownStarProjectile == null)
            return;

        Vector2 origin = transform.position;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right,
            new Vector2(1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 dir = dirs[i];
            Vector2 spawnPos = origin + dir * starSpawnRadius;

            ClownStarProjectile proj = Instantiate(clownStarProjectile, spawnPos, Quaternion.identity);
            if (proj != null)
                proj.Initialize(dir, starSpeed, starLifeTime);
        }
    }

    void OnHealthDied()
    {
        Kill();
    }

    public void Kill()
    {
        if (isDead)
            return;

        isDead = true;

        if (specialRoutine != null)
        {
            StopCoroutine(specialRoutine);
            specialRoutine = null;
        }

        if (damageSequenceRoutine != null)
        {
            StopCoroutine(damageSequenceRoutine);
            damageSequenceRoutine = null;
        }

        if (clownMovement != null)
            clownMovement.enabled = false;

        if (bossEndSequence != null)
            bossEndSequence.StartBossDefeatedSequence();

        Destroy(gameObject, 0.5f);
    }

    void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (introRenderer != null)
            introRenderer.enabled = (target == introRenderer);

        if (idleRenderer != null)
            idleRenderer.enabled = (target == idleRenderer);

        if (specialRenderer != null)
            specialRenderer.enabled = (target == specialRenderer);

        if (hurtRenderer != null)
            hurtRenderer.enabled = (target == hurtRenderer);

        if (attackRenderer != null)
            attackRenderer.enabled = (target == attackRenderer);

        if (target != null)
            target.RefreshFrame();
    }

    AnimatedSpriteRenderer GetCurrentRenderer()
    {
        if (introRenderer != null && introRenderer.enabled)
            return introRenderer;

        if (idleRenderer != null && idleRenderer.enabled)
            return idleRenderer;

        if (specialRenderer != null && specialRenderer.enabled)
            return specialRenderer;

        if (hurtRenderer != null && hurtRenderer.enabled)
            return hurtRenderer;

        if (attackRenderer != null && attackRenderer.enabled)
            return attackRenderer;

        return null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead)
            return;

        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Explosion"))
        {
            if (characterHealth != null)
                characterHealth.TakeDamage(1);
            return;
        }

        if (layer == LayerMask.NameToLayer("Player") || other.CompareTag("Player"))
        {
            if (other.TryGetComponent<MovementController>(out var movement))
            {
                movement.Kill();
                return;
            }

            if (other.TryGetComponent<CharacterHealth>(out var health))
                health.TakeDamage(health.life);
        }
    }
}
