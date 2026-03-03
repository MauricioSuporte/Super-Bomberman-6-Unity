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
    [Min(0f)] public float deathHoldDuration = 5f;

    [Header("Death Eyes Flow")]
    [Min(0f)] public float deathEyesDamagedDuration = 1f;
    [Min(0f)] public float deathEyesFrontDuration = 0.75f; // mantido por compatibilidade (não usado agora)

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

    private bool isDead;
    private Coroutine hurtRoutine;
    private Coroutine deathRoutine;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private float nextTouchDamageTime;

    private int bombLayer;

    private Vector2 cachedMoveDirection;
    private bool hasCachedMoveDirection;

    private SunMaskEyesController eyes;

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

        hasCachedMoveDirection = false;
        cachedMoveDirection = default;

        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }

        if (movement != null)
            movement.enabled = true;

        EnableOnly(walkRenderer);
        SetRendererAsLooping(walkRenderer, looping: true);
    }

    void OnDisable()
    {
        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
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

        MovementController player;
        if (!other.TryGetComponent<MovementController>(out player) || player == null)
            return;

        if (player.isDead || player.IsEndingStage)
            return;

        CharacterHealth playerHealth = null;
        CharacterHealth ph;
        if (player.TryGetComponent<CharacterHealth>(out ph) && ph != null)
            playerHealth = ph;

        if (playerHealth != null && playerHealth.IsInvulnerable)
            return;

        int dmg = Mathf.Max(1, touchDamage);

        if (player.IsMountedOnLouie)
        {
            PlayerMountCompanion companion;
            if (player.TryGetComponent<PlayerMountCompanion>(out companion) && companion != null)
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

        EnableOnly(hurtRenderer);
        SetRendererAsLooping(hurtRenderer, looping: false);

        if (eyes != null)
            eyes.BeginDeathEyesFlowDamagedThenSul(deathEyesDamagedDuration);

        float t = Mathf.Max(0f, deathHoldDuration);
        if (t > 0f)
            yield return new WaitForSeconds(t);

        Destroy(gameObject);
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

            SpriteRenderer sr;
            if (target.TryGetComponent<SpriteRenderer>(out sr))
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
}