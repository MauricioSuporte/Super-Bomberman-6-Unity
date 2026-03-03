using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(SunMaskMovement))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class SunMaskBoss : MonoBehaviour, IKillable
{
    [Header("References")]
    public CharacterHealth characterHealth;
    public SunMaskMovement movement;

    [Header("Renderers")]
    public AnimatedSpriteRenderer walkRenderer;
    public AnimatedSpriteRenderer hurtRenderer;
    public AnimatedSpriteRenderer deathRenderer;

    [Header("Timings")]
    [Min(0f)] public float hurtStopDuration = 0.5f;

    [Tooltip("Duração das explosões (NÃO inclui o 1s inicial de Damaged dos olhos).")]
    [Min(0f)] public float deathHoldDuration = 5f;

    [Header("Death Eyes Flow")]
    [Min(0f)] public float deathEyesDamagedDuration = 1f;
    [Min(0f)] public float deathEyesFrontDuration = 0.75f; // mantido por compatibilidade (não usado agora)

    [Header("Death Final Blink (Body + Eyes via CharacterHealth SpriteRenderers)")]
    [SerializeField] private bool enableDeathFinalBlink = true;
    [SerializeField, Min(0f)] private float deathFinalBlinkDuration = 1.0f;
    [SerializeField, Min(0.001f)] private float deathFinalBlinkInterval = 0.06f;

    [Header("After Death Spawn")]
    [Tooltip("Prefab do MagnetBomber (ou root) para spawnar quando o SunMask for destruído.")]
    [SerializeField] private GameObject magnetBomberPrefab;

    [Tooltip("Se true, ao spawnar o MagnetBomber, força tocar a animação 'Death' (AnimatedSpriteRenderer) e depois destrói o objeto.")]
    [SerializeField] private bool playMagnetBomberDeathOnSpawn = true;

    [Tooltip("Fallback caso não dê para calcular a duração pela animação.")]
    [SerializeField, Min(0.05f)] private float magnetBomberDeathFallbackDuration = 2.0f;

    [Header("Damage")]
    public int explosionDamage = 1;

    [Header("Damage SFX")]
    public AudioClip damagedSfx;
    [Range(0f, 1f)] public float damagedSfxVolume = 0.9f;

    [Header("Touch Damage")]
    public int touchDamage = 1;
    [Min(0f)] public float touchDamageCooldown = 0.35f;

    [Header("Bomb Destroy")]
    [Tooltip("Se true, destrói bombas ao encostar.")]
    public bool destroyBombsOnTouch = true;

    [Header("Death Explosions")]
    public GameObject explosionPrefab;
    [Min(0.001f)] public float explosionSpawnInterval = 0.05f;
    [Min(0f)] public float explosionSpawnRadius = 1.2f;
    [Min(0.01f)] public float explosionMinScale = 0.7f;
    [Min(0.01f)] public float explosionMaxScale = 1.2f;

    [Header("Death Explosion SFX")]
    public AudioClip deathExplosionSfx;
    [Min(0.001f)] public float deathExplosionSfxInterval = 0.12f;
    [Range(0f, 1f)] public float deathExplosionSfxVolume = 1f;
    public Vector2 deathExplosionPitchRange = new(0.95f, 1.05f);
    public bool deathSfxUseTempAudioObject = true;
    public float deathSfxSpatialBlend = 0f;

    private bool isDead;
    private Coroutine hurtRoutine;
    private Coroutine deathRoutine;
    private Coroutine deathExplosionsRoutine;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private float nextTouchDamageTime;

    private int bombLayer;

    private Vector2 cachedMoveDirection;
    private bool hasCachedMoveDirection;

    private SunMaskEyesController eyes;

    private float nextDeathSfxTime;

    // Ensures only ONE death explosion SFX plays at a time (replaces/stops previous).
    private static readonly object deathSfxGate = new();
    private static AudioSource deathSfxOwner;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        if (!characterHealth)
            characterHealth = GetComponent<CharacterHealth>();

        if (!movement)
            movement = GetComponent<SunMaskMovement>();

        eyes = GetComponentInChildren<SunMaskEyesController>(true);

        if (characterHealth != null)
        {
            characterHealth.Damaged += OnDamaged;
            characterHealth.Died += OnDied;
        }

        bombLayer = LayerMask.NameToLayer("Bomb");

        EnableOnly(walkRenderer);
        SetRendererAsLooping(walkRenderer, looping: true);
    }

    void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Damaged -= OnDamaged;
            characterHealth.Died -= OnDied;
        }
    }

    void OnEnable()
    {
        isDead = false;
        nextTouchDamageTime = 0f;
        nextDeathSfxTime = 0f;

        hasCachedMoveDirection = false;
        cachedMoveDirection = default;

        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
        if (deathExplosionsRoutine != null) { StopCoroutine(deathExplosionsRoutine); deathExplosionsRoutine = null; }

        if (movement != null)
            movement.enabled = true;

        EnableOnly(walkRenderer);
        SetRendererAsLooping(walkRenderer, looping: true);
    }

    void OnDisable()
    {
        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
        if (deathExplosionsRoutine != null) { StopCoroutine(deathExplosionsRoutine); deathExplosionsRoutine = null; }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDestroyBombOnTouch(other);
        TryApplyExplosionDamage(other);
        TryApplyTouchDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDestroyBombOnTouch(other);
        TryApplyTouchDamage(other);
    }

    void TryDestroyBombOnTouch(Collider2D other)
    {
        if (!destroyBombsOnTouch)
            return;

        if (isDead)
            return;

        if (bombLayer < 0)
            return;

        if (other == null || other.gameObject == null)
            return;

        if (other.gameObject.layer != bombLayer)
            return;

        Bomb bomb = other.GetComponentInParent<Bomb>();
        if (bomb == null)
            bomb = other.GetComponent<Bomb>();

        if (bomb == null)
            return;

        if (bomb.HasExploded)
            return;

        BombController owner = bomb.Owner;
        if (owner != null)
        {
            owner.DestroyBombExternally(bomb.gameObject, refund: true);
            return;
        }

        bomb.MarkAsExploded();
        Destroy(bomb.gameObject);
    }

    void TryApplyExplosionDamage(Collider2D other)
    {
        if (isDead)
            return;

        if (characterHealth == null || !characterHealth.enabled)
            return;

        if (characterHealth.IsInvulnerable)
            return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer < 0)
            return;

        if (other.gameObject.layer != explosionLayer)
            return;

        int dmg = Mathf.Max(1, explosionDamage);
        characterHealth.TakeDamage(dmg);
    }

    void TryApplyTouchDamage(Collider2D other)
    {
        if (isDead)
            return;

        if (Time.time < nextTouchDamageTime)
            return;

        if (!other.CompareTag("Player") && other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        if (!other.TryGetComponent<MovementController>(out var player) || player == null)
            return;

        if (player.isDead || player.IsEndingStage)
            return;

        CharacterHealth playerHealth = null;
        if (player.TryGetComponent<CharacterHealth>(out var ph) && ph != null)
            playerHealth = ph;

        if (playerHealth != null && playerHealth.IsInvulnerable)
            return;

        int dmg = Mathf.Max(1, touchDamage);

        if (player.IsMountedOnLouie)
        {
            if (player.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
            {
                companion.OnMountedLouieHit(dmg, false);
                nextTouchDamageTime = Time.time + touchDamageCooldown;
                return;
            }
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(dmg);
            nextTouchDamageTime = Time.time + touchDamageCooldown;
            return;
        }

        player.Kill();
        nextTouchDamageTime = Time.time + touchDamageCooldown;
    }

    void OnDamaged(int amount)
    {
        if (isDead)
            return;

        PlayDamagedSfx();

        CacheDirectionBeforeHurt();

        if (hurtRoutine != null)
            StopCoroutine(hurtRoutine);

        hurtRoutine = StartCoroutine(HurtStopRoutine());
    }

    void PlayDamagedSfx()
    {
        if (audioSource == null)
            return;

        if (damagedSfx == null)
            return;

        audioSource.PlayOneShot(damagedSfx, Mathf.Clamp01(damagedSfxVolume));
    }

    void CacheDirectionBeforeHurt()
    {
        if (movement == null)
            return;

        Vector2 dir = movement.GetCurrentDirection();
        if (dir.sqrMagnitude > 0.0001f)
        {
            cachedMoveDirection = dir;
            hasCachedMoveDirection = true;
        }
    }

    IEnumerator HurtStopRoutine()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (movement != null)
        {
            movement.OnHit(hurtStopDuration);

            if (hasCachedMoveDirection)
                movement.SetCurrentDirection(cachedMoveDirection);
        }

        EnableOnly(hurtRenderer);
        SetRendererAsLooping(hurtRenderer, looping: false);

        float t = Mathf.Max(0f, hurtStopDuration);
        if (t > 0f)
            yield return new WaitForSeconds(t);

        if (!isDead)
        {
            if (movement != null && hasCachedMoveDirection)
                movement.SetCurrentDirection(cachedMoveDirection);

            EnableOnly(walkRenderer);
            SetRendererAsLooping(walkRenderer, looping: true);
        }

        hurtRoutine = null;
    }

    void OnDied()
    {
        Kill();
    }

    public void Kill()
    {
        if (isDead)
            return;

        isDead = true;

        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
            hurtRoutine = null;
        }

        if (deathRoutine != null)
            StopCoroutine(deathRoutine);

        deathRoutine = StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        StopMovement_DisableComponent();

        AnimatedSpriteRenderer target = deathRenderer != null ? deathRenderer : hurtRenderer;
        EnableOnly(target);
        SetRendererAsLooping(target, looping: false);

        float eyesDelay = Mathf.Max(0f, deathEyesDamagedDuration);
        float explosionDuration = Mathf.Max(0f, deathHoldDuration);

        // 1) olhos Damaged no começo (1s)
        if (eyes != null)
            eyes.BeginDeathEyesFlowDamagedThenSul(deathEyesDamagedDuration);

        if (eyesDelay > 0f)
            yield return new WaitForSeconds(eyesDelay);

        // 2) inicia explosões por 5s
        if (explosionPrefab != null && explosionDuration > 0f)
        {
            nextDeathSfxTime = 0f;
            deathExplosionsRoutine = StartCoroutine(SpawnDeathExplosions(explosionDuration));
        }

        // 3) blink só no final da janela de explosões
        if (explosionDuration > 0f)
        {
            float blinkDur = (enableDeathFinalBlink && deathFinalBlinkDuration > 0f) ? deathFinalBlinkDuration : 0f;
            blinkDur = Mathf.Min(blinkDur, explosionDuration);

            float waitBeforeBlink = explosionDuration - blinkDur;

            if (waitBeforeBlink > 0f)
                yield return new WaitForSeconds(waitBeforeBlink);

            if (blinkDur > 0f && characterHealth != null && characterHealth.enabled)
            {
                float interval = Mathf.Max(0.001f, deathFinalBlinkInterval);
                characterHealth.StartSpawnInvulnerability(blinkDur, interval);
                yield return new WaitForSeconds(blinkDur);
            }
            else if (blinkDur > 0f)
            {
                yield return new WaitForSeconds(blinkDur);
            }
        }

        if (deathExplosionsRoutine != null)
        {
            StopCoroutine(deathExplosionsRoutine);
            deathExplosionsRoutine = null;
        }

        SpawnMagnetBomberDeath();

        Destroy(gameObject);
    }

    IEnumerator SpawnDeathExplosions(float duration)
    {
        float elapsed = 0f;
        float interval = Mathf.Max(0.001f, explosionSpawnInterval);

        while (elapsed < duration)
        {
            SpawnOneExplosion();
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    void SpawnOneExplosion()
    {
        if (explosionPrefab == null)
            return;

        Vector2 origin = transform.position;
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, explosionSpawnRadius);
        Vector3 pos = new(origin.x + offset.x, origin.y + offset.y, transform.position.z);

        GameObject fxGo = Instantiate(explosionPrefab, pos, Quaternion.identity);

        float minS = Mathf.Max(0.01f, explosionMinScale);
        float maxS = Mathf.Max(minS, explosionMaxScale);
        float scale = Random.Range(minS, maxS);

        if (fxGo != null)
            fxGo.transform.localScale = new Vector3(scale, scale, 1f);

        if (fxGo != null)
        {
            float life = 0.5f;

            var anim = fxGo.GetComponentInChildren<AnimatedSpriteRenderer>(true);
            if (anim != null)
            {
                if (anim.useSequenceDuration && anim.sequenceDuration > 0f) life = anim.sequenceDuration;
                else if (anim.animationSprite != null && anim.animationSprite.Length > 0) life = anim.animationTime * anim.animationSprite.Length;
            }

            Destroy(fxGo, Mathf.Max(0.05f, life) + 0.05f);
        }

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
            lock (deathSfxGate)
            {
                if (deathSfxOwner != null)
                {
                    var oldGo = deathSfxOwner.gameObject;
                    deathSfxOwner.Stop();
                    deathSfxOwner = null;
                    if (oldGo != null) Destroy(oldGo);
                }

                GameObject go = new("SunMaskDeathSfx");
                go.transform.position = pos;

                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = deathSfxSpatialBlend;
                s.volume = deathExplosionSfxVolume;
                s.pitch = pitch;

                var tracker = go.AddComponent<TempDeathSfxOwnerTracker>();
                tracker.source = s;

                deathSfxOwner = s;

                s.PlayOneShot(deathExplosionSfx, 1f);

                float clipDuration = deathExplosionSfx.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
                Destroy(go, clipDuration + 0.1f);
            }

            return;
        }

        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(deathExplosionSfx, deathExplosionSfxVolume);
        audioSource.pitch = 1f;
    }

    private sealed class TempDeathSfxOwnerTracker : MonoBehaviour
    {
        public AudioSource source;

        void OnDestroy()
        {
            lock (deathSfxGate)
            {
                if (deathSfxOwner != null && deathSfxOwner == source)
                    deathSfxOwner = null;
            }
        }
    }

    void StopMovement_DisableComponent()
    {
        if (movement != null)
            movement.enabled = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (walkRenderer != null) walkRenderer.enabled = (target == walkRenderer);
        if (hurtRenderer != null) hurtRenderer.enabled = (target == hurtRenderer);
        if (deathRenderer != null) deathRenderer.enabled = (target == deathRenderer);

        if (target != null)
        {
            target.RefreshFrame();

            if (target.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = true;
        }
    }

    void SetRendererAsLooping(AnimatedSpriteRenderer r, bool looping)
    {
        if (r == null)
            return;

        r.idle = false;
        r.loop = looping;
        r.CurrentFrame = 0;
        r.RefreshFrame();
    }

    private void SpawnMagnetBomberDeath()
    {
        if (magnetBomberPrefab == null)
            return;

        Vector3 pos = transform.position;
        GameObject go = Instantiate(magnetBomberPrefab, pos, Quaternion.identity);
        if (go == null)
            return;

        if (!playMagnetBomberDeathOnSpawn)
            return;

        var ai = go.GetComponentInChildren<MovementControllerAI>(true);
        if (ai != null)
        {
            ai.SetIntroIdle(false);
            ai.ForceDisableOptionalVisualsNow();
        }

        var mc = go.GetComponentInChildren<MovementController>(true);

        if (mc != null)
            mc.SetExternalMovementOverride(true);

        if (mc != null)
        {
            mc.Kill();

            float life = Mathf.Max(0.05f, mc.deathDisableSeconds);
            Destroy(go, life + 0.2f);
            return;
        }

        var ch = go.GetComponentInChildren<CharacterHealth>(true);
        if (ch != null)
        {
            ch.TakeDamage(9999);
            Destroy(go, Mathf.Max(0.05f, magnetBomberDeathFallbackDuration) + 0.2f);
            return;
        }

        Destroy(go, Mathf.Max(0.05f, magnetBomberDeathFallbackDuration) + 0.2f);
    }
}