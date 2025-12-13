using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(ClownMaskMovement))]
[RequireComponent(typeof(AudioSource))]
public class ClownMaskBoss : MonoBehaviour, IKillable
{
    [Header("References")]
    public CharacterHealth characterHealth;
    public ClownMaskMovement clownMovement;
    public BossEndStageSequence bossEndSequence;

    [Header("Renderers")]
    SpriteRenderer[] bossSpriteRenderers;
    public AnimatedSpriteRenderer introRenderer;
    public AnimatedSpriteRenderer idleRenderer;
    public AnimatedSpriteRenderer specialRenderer;
    public AnimatedSpriteRenderer hurtRenderer;
    public AnimatedSpriteRenderer deathRenderer;

    [Header("Intro")]
    public float introDuration = 2f;

    [Header("Boss Intro Setup")]
    public Vector2 bossIntroPosition = new Vector2(-1f, 2f);
    public float delayAfterStageIntroToShowPlayer = 1f;
    public Vector2 playerIntroPosition = new Vector2(-3f, -6f);
    public float introWaitAfterPlayerShown = 1f;

    [Header("Spotlight")]
    public float introDarknessAlpha = 0.9f;
    public float introWaitBeforeSpotlight = 1f;
    public float introSpotlightRadiusTiles = 4f;
    public float introSpotlightSoftnessTiles = 1f;

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

    [Header("Death")]
    public float deathDuration = 5f;

    [Header("Death Explosions")]
    public ClownExplosionVfx explosionPrefab;
    public float explosionSpawnInterval = 0.05f;
    public float explosionSpawnRadius = 1.2f;
    public float explosionMinScale = 0.7f;
    public float explosionMaxScale = 1.2f;

    [Header("Death Explosion SFX")]
    public AudioClip deathExplosionSfx;
    public float deathExplosionSfxInterval = 0.12f;
    [Range(0f, 1f)] public float deathExplosionSfxVolume = 1f;
    public Vector2 deathExplosionPitchRange = new Vector2(0.95f, 1.05f);
    public bool deathSfxUseTempAudioObject = true;
    public float deathSfxSpatialBlend = 0f;

    bool isDead;
    bool introFinished;
    bool inDamageSequence;
    bool isInHurtWalk;
    bool bossSpawned;

    Coroutine introRoutine;
    Coroutine specialRoutine;
    Coroutine damageSequenceRoutine;
    Coroutine deathRoutine;

    AudioSource audioSource;
    float nextDeathSfxTime;

    MovementController player;
    BombController playerBomb;

    Collider2D bossCollider;
    Rigidbody2D bossRb;

    void Awake()
    {
        if (!characterHealth)
            characterHealth = GetComponent<CharacterHealth>();

        if (!clownMovement)
            clownMovement = GetComponent<ClownMaskMovement>();

        if (!bossEndSequence)
            bossEndSequence = FindFirstObjectByType<BossEndStageSequence>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.playOnAwake = false;

        bossCollider = GetComponent<Collider2D>();
        bossRb = GetComponent<Rigidbody2D>();

        bossSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            player = playerGo.GetComponent<MovementController>();
            playerBomb = playerGo.GetComponent<BombController>();
        }

        if (characterHealth != null)
        {
            characterHealth.Damaged += OnHealthDamaged;
            characterHealth.Died += OnHealthDied;
        }

        ResetBossState();
        MoveBossToIntroPosition();
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
        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(IntroSequence());
    }

    void OnDisable()
    {
        ResetBossState();
    }

    void ResetBossState()
    {
        if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
        if (specialRoutine != null) { StopCoroutine(specialRoutine); specialRoutine = null; }
        if (damageSequenceRoutine != null) { StopCoroutine(damageSequenceRoutine); damageSequenceRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }

        isDead = false;
        introFinished = false;
        inDamageSequence = false;
        isInHurtWalk = false;
        bossSpawned = false;

        if (clownMovement != null)
            clownMovement.enabled = false;

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (bossRb != null)
        {
            bossRb.simulated = false;
            bossRb.linearVelocity = Vector2.zero;
        }

        SetAllRenderers(false);
        SetBossSpriteRenderersVisible(false);
    }

    IEnumerator IntroSequence()
    {
        ResetBossState();
        MoveBossToIntroPosition();

        LockPlayer(true);
        SetPlayerHidden(true);

        while (StageIntroTransition.Instance != null &&
               StageIntroTransition.Instance.IntroRunning)
            yield return null;

        float waitAfterStageIntro = Mathf.Max(0f, delayAfterStageIntroToShowPlayer);
        if (waitAfterStageIntro > 0f)
            yield return new WaitForSeconds(waitAfterStageIntro);

        SpawnPlayerForBossIntro();
        ShowPlayerIdleUpOnly();

        float waitAfterShowPlayer = Mathf.Max(0f, introWaitAfterPlayerShown);
        if (waitAfterShowPlayer > 0f)
            yield return new WaitForSeconds(waitAfterShowPlayer);

        if (StageIntroTransition.Instance != null)
            yield return StageIntroTransition.Instance.FadeToFullDarknessAndWait(
                introDarknessAlpha,
                StageIntroTransition.Instance.spotlightFadeInDuration
            );

        yield return new WaitForSeconds(1f);

        SpawnBossForIntroOnly();

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.SetSpotlightWorld(
                new Vector3(bossIntroPosition.x, bossIntroPosition.y, 0f),
                introSpotlightRadiusTiles,
                introDarknessAlpha,
                introSpotlightSoftnessTiles
            );

        EnableOnly(introRenderer);

        float duration = introDuration;
        if (introRenderer != null && duration <= 0f)
        {
            if (introRenderer.useSequenceDuration && introRenderer.sequenceDuration > 0f)
                duration = introRenderer.sequenceDuration;
            else if (introRenderer.animationSprite != null && introRenderer.animationSprite.Length > 0)
                duration = introRenderer.animationTime * introRenderer.animationSprite.Length;
            else
                duration = 2f;
        }

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (StageIntroTransition.Instance != null)
            yield return StageIntroTransition.Instance.FadeSpotlightAlphaAndWait(0f, 0.5f);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.DisableSpotlight();

        EnableOnly(idleRenderer);

        EnableBossCombat();

        LockPlayer(false);
        SetPlayerHidden(false);
        if (player != null)
            player.ForceIdleUp();

        introFinished = true;
        specialRoutine = StartCoroutine(SpecialLoop());
    }

    void SpawnBossForIntroOnly()
    {
        bossSpawned = true;

        MoveBossToIntroPosition();

        if (bossRb != null)
        {
            bossRb.simulated = false;
            bossRb.linearVelocity = Vector2.zero;
        }

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (clownMovement != null)
            clownMovement.enabled = false;
    }

    void EnableBossCombat()
    {
        if (isDead)
            return;

        if (bossRb != null)
        {
            bossRb.simulated = true;
            bossRb.linearVelocity = Vector2.zero;
        }

        if (bossCollider != null)
            bossCollider.enabled = true;

        if (characterHealth != null)
            characterHealth.enabled = true;

        if (clownMovement != null)
            clownMovement.enabled = true;

        bossSpawned = true;
    }

    void MoveBossToIntroPosition()
    {
        if (bossRb != null)
        {
            bossRb.position = bossIntroPosition;
            bossRb.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = new Vector3(bossIntroPosition.x, bossIntroPosition.y, transform.position.z);
        }
    }

    void SpawnPlayerForBossIntro()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.GetComponent<MovementController>();
                playerBomb = go.GetComponent<BombController>();
            }
        }

        if (player == null)
            return;

        if (player.Rigidbody != null)
        {
            player.Rigidbody.simulated = true;
            player.Rigidbody.position = playerIntroPosition;
            player.Rigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = new Vector3(playerIntroPosition.x, playerIntroPosition.y, player.transform.position.z);
        }

        player.ForceIdleUp();
    }

    void SetPlayerHidden(bool hidden)
    {
        if (player == null) return;

        var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null)
                srs[i].enabled = !hidden;

        var anims = player.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
            if (anims[i] != null)
                anims[i].enabled = !hidden;
    }

    void ShowPlayerIdleUpOnly()
    {
        if (player == null)
            return;

        player.ForceIdleUp();

        var anims = player.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
            if (anims[i] != null)
                anims[i].enabled = false;

        var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null)
                srs[i].enabled = false;

        if (player.spriteRendererUp != null)
        {
            player.spriteRendererUp.enabled = true;
            player.spriteRendererUp.idle = true;
            player.spriteRendererUp.loop = true;
            player.spriteRendererUp.RefreshFrame();

            var upSr = player.spriteRendererUp.GetComponent<SpriteRenderer>();
            if (upSr != null)
                upSr.enabled = true;
        }
    }

    void LockPlayer(bool locked)
    {
        if (player != null)
        {
            player.SetInputLocked(locked);
            player.SetExplosionInvulnerable(locked);
        }

        if (playerBomb != null)
            playerBomb.enabled = !locked;
    }

    IEnumerator SpecialLoop()
    {
        while (!isDead)
        {
            float wait = Random.Range(minSpecialInterval, maxSpecialInterval);
            yield return new WaitForSeconds(wait);

            if (isDead || !introFinished || inDamageSequence || !bossSpawned)
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
        if (isDead || !bossSpawned || !introFinished)
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

        if (characterHealth != null)
            characterHealth.enabled = false;

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        nextDeathSfxTime = 0f;
        deathRoutine = StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        AnimatedSpriteRenderer target = deathRenderer != null ? deathRenderer : idleRenderer;
        EnableOnly(target);

        SpriteRenderer sr = null;
        if (target != null)
            sr = target.GetComponent<SpriteRenderer>();

        float blinkInterval = characterHealth != null && characterHealth.hitBlinkInterval > 0f
            ? characterHealth.hitBlinkInterval
            : 0.1f;

        Coroutine explosions = null;
        if (explosionPrefab != null)
            explosions = StartCoroutine(SpawnDeathExplosions());

        float elapsed = 0f;
        bool visible = true;

        while (elapsed < deathDuration)
        {
            visible = !visible;
            if (sr != null)
                sr.enabled = visible;

            TryPlayDeathExplosionSfx();

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (explosions != null)
            StopCoroutine(explosions);

        if (bossEndSequence != null)
            bossEndSequence.StartBossDefeatedSequence();

        Destroy(gameObject);
    }

    IEnumerator SpawnDeathExplosions()
    {
        float elapsed = 0f;

        while (elapsed < deathDuration)
        {
            SpawnOneExplosion();
            yield return new WaitForSeconds(explosionSpawnInterval);
            elapsed += explosionSpawnInterval;
        }
    }

    void SpawnOneExplosion()
    {
        if (explosionPrefab == null)
            return;

        Vector2 origin = transform.position;
        Vector2 offset = Random.insideUnitCircle * explosionSpawnRadius;
        Vector3 pos = new(origin.x + offset.x, origin.y + offset.y, transform.position.z);

        ClownExplosionVfx fx = Instantiate(explosionPrefab, pos, Quaternion.identity);

        float scale = Random.Range(explosionMinScale, explosionMaxScale);
        fx.transform.localScale = new Vector3(scale, scale, 1f);

        TryPlayDeathExplosionSfx();
    }

    void TryPlayDeathExplosionSfx()
    {
        if (deathExplosionSfx == null)
            return;

        float interval = deathExplosionSfxInterval <= 0f ? 0.01f : deathExplosionSfxInterval;
        if (Time.time < nextDeathSfxTime)
            return;

        nextDeathSfxTime = Time.time + interval;

        float pitch = Random.Range(deathExplosionPitchRange.x, deathExplosionPitchRange.y);
        Vector3 pos = transform.position;

        if (deathSfxUseTempAudioObject)
        {
            GameObject go = new GameObject("ClownDeathSfx");
            go.transform.position = pos;

            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = deathSfxSpatialBlend;
            s.volume = deathExplosionSfxVolume;
            s.pitch = pitch;

            s.PlayOneShot(deathExplosionSfx, 1f);

            float clipDuration = deathExplosionSfx.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            Destroy(go, clipDuration + 0.1f);
            return;
        }

        if (audioSource == null)
            return;

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(deathExplosionSfx, deathExplosionSfxVolume);
        audioSource.pitch = 1f;
    }

    void SetAllRenderers(bool value)
    {
        if (introRenderer) introRenderer.enabled = value;
        if (idleRenderer) idleRenderer.enabled = value;
        if (specialRenderer) specialRenderer.enabled = value;
        if (hurtRenderer) hurtRenderer.enabled = value;
        if (deathRenderer) deathRenderer.enabled = value;
    }

    void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (introRenderer != null) introRenderer.enabled = (target == introRenderer);
        if (idleRenderer != null) idleRenderer.enabled = (target == idleRenderer);
        if (specialRenderer != null) specialRenderer.enabled = (target == specialRenderer);
        if (hurtRenderer != null) hurtRenderer.enabled = (target == hurtRenderer);
        if (deathRenderer != null) deathRenderer.enabled = (target == deathRenderer);

        SetBossSpriteRenderersVisible(false);

        if (target != null)
        {
            target.RefreshFrame();

            var sr = target.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.enabled = true;
        }
    }

    AnimatedSpriteRenderer GetCurrentRenderer()
    {
        if (introRenderer != null && introRenderer.enabled) return introRenderer;
        if (idleRenderer != null && idleRenderer.enabled) return idleRenderer;
        if (specialRenderer != null && specialRenderer.enabled) return specialRenderer;
        if (hurtRenderer != null && hurtRenderer.enabled) return hurtRenderer;
        if (deathRenderer != null && deathRenderer.enabled) return deathRenderer;
        return null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || !bossSpawned || !introFinished)
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

    void SetBossSpriteRenderersVisible(bool visible)
    {
        if (bossSpriteRenderers == null)
            bossSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < bossSpriteRenderers.Length; i++)
        {
            var sr = bossSpriteRenderers[i];
            if (sr != null)
                sr.enabled = visible;
        }
    }
}
