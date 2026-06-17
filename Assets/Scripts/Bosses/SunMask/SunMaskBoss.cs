using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(SunMaskMovement))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(CircleCollider2D))]
public class SunMaskBoss : MonoBehaviour, IKillable
{
    const int HardExtraLife = 2;
    const float HardSpeedMultiplier = 1.5f;

    [Header("References")]
    public CharacterHealth characterHealth;
    public SunMaskMovement movement;

    [Header("Renderers")]
    public AnimatedSpriteRenderer walkRenderer;
    public AnimatedSpriteRenderer closedRenderer;
    public AnimatedSpriteRenderer hurtRenderer;
    public AnimatedSpriteRenderer deathRenderer;

    [Header("Low Health Tint")]
    [SerializeField] bool tintWhenBelowHalfLife = true;
    Color lowHealthTintColor = new(1.35f, 0.45f, 0.45f, 1f);
    [SerializeField, Range(0f, 1f)] float lowHealthTintStrength = 0.4f;

    [Header("Angry")]
    [SerializeField] private AnimatedSpriteRenderer angryRenderer;
    [SerializeField, Min(0f)] private float angryFreezeSeconds = 1f;
    [SerializeField, Min(0f)] private float angryChaseSeconds = 5f;
    [SerializeField, Min(0f)] private float angryAfterChaseStopSeconds = 1f;
    [SerializeField] private float angryEyesYOffset = 0.3f;

    [Header("Angry - Arc (pre-chase)")]
#pragma warning disable CS0414
    [SerializeField, Min(0f)] private float angryArcSeconds = 1f;
#pragma warning restore CS0414
    [SerializeField, Min(0f)] private float angryArcRadius = 1.25f;
#pragma warning disable CS0414
    [SerializeField] private bool angryArcClockwise = true;
#pragma warning restore CS0414

    [Header("Angry - Chase Steering")]
    [SerializeField, Range(0.1f, 1f)] private float angryChaseSpeedMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float angryChaseCloseSlowDistance = 7f;
    [SerializeField, Range(0.1f, 1f)] private float angryChaseCloseSpeedMultiplier = 0.55f;
    [SerializeField, Min(0f)] private float angryChaseMinimumSpeed = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float angryChaseCloseTurnSpeedMultiplier = 0.35f;
    [SerializeField, Min(0.01f)] private float angryRetargetInterval = 0.08f;
    [SerializeField, Min(1f)] private float angryTurnSpeedDegrees = 220f;

    public bool IsAngryRendererActive => angryRenderer != null && angryRenderer.enabled;
    public float AngryEyesYOffset => angryEyesYOffset;

    [Header("Wink / Kiss Attack")]
    [SerializeField] private bool enableWinkAttack = true;
#pragma warning disable CS0414
    [SerializeField, Min(0f)] private float winkMinInterval = 3f;
    [SerializeField, Min(0f)] private float winkMaxInterval = 5f;
#pragma warning restore CS0414
    [SerializeField, Min(0f)] private float winkHoldDuration = 1f;
    private bool nextWinkOrKissAttackShouldBeWink = true;

    [SerializeField] private AnimatedSpriteRenderer winkRenderer;
    [SerializeField] private AnimatedSpriteRenderer kissRenderer;

    [Header("Wink (50%) - Star Burst")]
    [SerializeField] private StarProjectile starProjectilePrefab;
    [SerializeField, Min(0f)] private float starSpeed = 6f;
    [SerializeField, Min(0f)] private float starLifeTime = 3f;
    [SerializeField, Min(0f)] private float starSpawnRadius = 0.5f;
    [SerializeField] private int starDamage = 1;
    [SerializeField] private LayerMask starObstacleMask;

    [Header("Kiss (50%) - Heart")]
    [SerializeField] private SunMaskHeartProjectile heartPrefab;

    [SerializeField, Min(0f)] private float heartSpeed = 2.25f;
    [SerializeField, Min(0f)] private float heartLifeTime = 6f;
    [SerializeField, Min(0f)] private float heartSpawnRadius = 0.35f;
    [SerializeField, Min(0f)] private float heartFloatAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float heartFloatFrequency = 3.5f;

    [Header("Hard/Hardcore Low Health Special Attack")]
    [SerializeField] private bool enableHardLowHealthSpecialAttack = true;
    [SerializeField] private Vector2 hardSpecialCenter = new(-3f, -2f);
    [SerializeField, Min(0.01f)] private float hardSpecialMoveDuration = 2f;
    [SerializeField, Min(0f)] private float hardSpecialClosedHoldDuration = 1f;
    [SerializeField, Min(0f)] private float hardSpecialDamagedHoldDuration = 1f;
    [SerializeField, Min(1)] private int hardSpecialWinkBurstCount = 5;
    [SerializeField, Min(0f)] private float hardSpecialWinkBurstInterval = 1f;
    [SerializeField, Min(0.01f)] private float hardSpecialStarSpeed = 4f;
    [FormerlySerializedAs("hardSpecialStarTurnSpeedDegrees")]
    [SerializeField] private float hardSpecialStarCurveDegrees = 12f;
    [SerializeField, Range(0.01f, 1f)] private float hardSpecialSecondHeartSpeedMultiplier = 0.5f;

    [Header("Timings")]
    [Min(0f)] public float hurtStopDuration = 0.5f;
    [Min(0f)] public float deathHoldDuration = 5f;

    [Header("Death Eyes Flow")]
    [Min(0f)] public float deathEyesDamagedDuration = 1f;
    [Min(0f)] public float deathEyesFrontDuration = 0.75f;

    [Header("Death Final Blink (Body + Eyes via CharacterHealth SpriteRenderers)")]
    [SerializeField] private bool enableDeathFinalBlink = true;
    [SerializeField, Min(0f)] private float deathFinalBlinkDuration = 1.0f;
    [SerializeField, Min(0.001f)] private float deathFinalBlinkInterval = 0.06f;

    [Header("After Death Spawn")]
    [SerializeField] private GameObject magnetBomberPrefab;
    [SerializeField] private bool playMagnetBomberDeathOnSpawn = true;
    [SerializeField, Min(0.05f)] private float magnetBomberDeathFallbackDuration = 2.0f;

    [Header("End Stage (after MagnetBomber dies)")]
    [SerializeField] private BossEndStageSequence bossEndSequence;
    [SerializeField, Min(0f)] private float endStageDelayAfterMagnetDeath = 1f;

    [Header("Damage")]
    public int explosionDamage = 1;

    [Header("Damage SFX")]
    public AudioClip damagedSfx;
    [Range(0f, 1f)] public float damagedSfxVolume = 0.9f;

    [Header("Touch Damage")]
    public int touchDamage = 1;
    [Min(0f)] public float touchDamageCooldown = 0.35f;

    [Header("Bomb Destroy")]
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

    [Header("Pixel Perfect")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;

    private static readonly List<Bomb> s_bombSnapshot = new(256);
    private static readonly List<Bomb> s_bombsToDestroy = new(32);

    [SerializeField, Min(0f)] private float extraBombDestroyRadius = 0.10f;
    [SerializeField, Min(0.01f)] private float bombScanInterval = 0.05f;

    private CircleCollider2D bossCircle;
    private float nextBombScanTime;

    private bool isDead;
    private bool inWinkAttack;
    private bool inAngry;
    private bool hardLowHealthSpecialPerformed;
    private bool hardLowHealthSpecialCompleted;
    private bool inHardLowHealthSpecial;
    private bool hardSpecialCanEnterAngry;

    private int initialFightLife;
    private bool fightLifeInitialized;

    private Coroutine hurtRoutine;
    private Coroutine deathRoutine;
    private Coroutine deathExplosionsRoutine;
    private Coroutine winkRoutine;
    private Coroutine angryRoutine;
    private Coroutine hardLowHealthSpecialRoutine;

    private Rigidbody2D rb;
    private AudioSource audioSource;
    private float nextTouchDamageTime;

    private int bombLayer;

    private Vector2 cachedMoveDirection;
    private bool hasCachedMoveDirection;

    private SunMaskEyesController eyes;

    private float nextDeathSfxTime;

    private static readonly object deathSfxGate = new();
    private static AudioSource deathSfxOwner;

    private static readonly Vector2[] StarDirs =
    {
        new Vector2(1f, 0f).normalized,
        new Vector2(0.8660254f, 0.5f).normalized,
        new Vector2(0.5f, 0.8660254f).normalized,
        new Vector2(0f, 1f).normalized,
        new Vector2(-0.5f, 0.8660254f).normalized,
        new Vector2(-0.8660254f, 0.5f).normalized,
        new Vector2(-1f, 0f).normalized,
        new Vector2(-0.8660254f, -0.5f).normalized,
        new Vector2(-0.5f, -0.8660254f).normalized,
        new Vector2(0f, -1f).normalized,
        new Vector2(0.5f, -0.8660254f).normalized,
        new Vector2(0.8660254f, -0.5f).normalized
    };

    void FixedUpdate()
    {
        ScanAndDestroyPunchedBombsTouchingBoss();
    }

    void ScanAndDestroyPunchedBombsTouchingBoss()
    {
        if (!destroyBombsOnTouch) return;
        if (isDead) return;

        float now = Time.time;
        if (now < nextBombScanTime) return;
        nextBombScanTime = now + Mathf.Max(0.01f, bombScanInterval);

        Vector2 bossPos = rb != null ? rb.position : (Vector2)transform.position;

        float bossRadius;
        if (bossCircle != null)
        {
            float sx = Mathf.Abs(transform.lossyScale.x);
            float sy = Mathf.Abs(transform.lossyScale.y);
            bossRadius = Mathf.Max(0.01f, bossCircle.radius * Mathf.Max(sx, sy));
        }
        else
        {
            var col = GetComponent<Collider2D>();
            var ext = col != null ? col.bounds.extents : (Vector3.one * 0.5f);
            bossRadius = Mathf.Max(0.01f, Mathf.Max(ext.x, ext.y));
        }

        float extra = Mathf.Max(0f, extraBombDestroyRadius);

        s_bombSnapshot.Clear();
        s_bombsToDestroy.Clear();

        foreach (var b in Bomb.ActiveBombs)
            s_bombSnapshot.Add(b);

        for (int i = 0; i < s_bombSnapshot.Count; i++)
        {
            var bomb = s_bombSnapshot[i];
            if (bomb == null) continue;
            if (bomb.HasExploded) continue;

            if (!bomb.IsBeingPunched && !bomb.IsBeingKicked && !bomb.IsBeingMagnetPulled)
                continue;

            Vector2 bombPos = (Vector2)bomb.transform.position;

            float r = bossRadius + Mathf.Max(0.01f, bomb.ApproxRadius) + extra;
            if ((bombPos - bossPos).sqrMagnitude > r * r)
                continue;

            s_bombsToDestroy.Add(bomb);
        }

        for (int i = 0; i < s_bombsToDestroy.Count; i++)
            DestroyBombLikeTouch(s_bombsToDestroy[i]);
    }

    void DestroyBombLikeTouch(Bomb bomb)
    {
        if (bomb == null) return;
        if (bomb.HasExploded) return;

        BombController owner = bomb.Owner;
        if (owner != null)
        {
            owner.DestroyBombExternally(bomb.gameObject, refund: true);
            return;
        }

        bomb.MarkAsExploded();
        Destroy(bomb.gameObject);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        if (!characterHealth)
            characterHealth = GetComponent<CharacterHealth>();

        if (!movement)
            movement = GetComponent<SunMaskMovement>();

        ApplyCampaignDifficultyModifiers();

        eyes = GetComponentInChildren<SunMaskEyesController>(true);

        if (characterHealth != null)
        {
            characterHealth.Damaged += OnDamaged;
            characterHealth.Died += OnDied;
        }

        bombLayer = LayerMask.NameToLayer("Bomb");

        EnableOnly(walkRenderer);
        SetRendererAsLooping(walkRenderer, looping: true);
        bossCircle = GetComponent<CircleCollider2D>();

        if (rb != null)
        {
            rb.position = SnapToPixel(rb.position);
            rb.MovePosition(rb.position);
        }
        else
        {
            transform.position = SnapToPixel(transform.position);
        }
    }

    void ApplyCampaignDifficultyModifiers()
    {
        if (!UsesHardCampaignModifiers())
            return;

        if (characterHealth != null)
            characterHealth.AddLife(HardExtraLife);

        if (movement != null)
            movement.ApplySpeedMultiplier(HardSpeedMultiplier);
    }

    static bool UsesHardCampaignModifiers()
    {
        if (BossRushSession.IsActive ||
            !string.Equals(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                "Stage_2-7",
                System.StringComparison.Ordinal))
        {
            return false;
        }

        Assets.Scripts.SaveSystem.StageSlot slot = SaveSystem.ActiveSlot;
        if (slot == null || !slot.started)
            return false;

        Assets.Scripts.SaveSystem.NormalGameDifficulty difficulty =
            System.Enum.IsDefined(typeof(Assets.Scripts.SaveSystem.NormalGameDifficulty), slot.difficulty)
                ? (Assets.Scripts.SaveSystem.NormalGameDifficulty)slot.difficulty
                : Assets.Scripts.SaveSystem.NormalGameDifficulty.Normal;

        return difficulty == Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard ||
               difficulty == Assets.Scripts.SaveSystem.NormalGameDifficulty.Hardcore;
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
        inWinkAttack = false;
        inAngry = false;
        hardLowHealthSpecialPerformed = false;
        hardLowHealthSpecialCompleted = false;
        inHardLowHealthSpecial = false;
        hardSpecialCanEnterAngry = false;

        nextWinkOrKissAttackShouldBeWink = true;

        nextTouchDamageTime = 0f;
        nextDeathSfxTime = 0f;

        hasCachedMoveDirection = false;
        cachedMoveDirection = default;

        fightLifeInitialized = false;
        initialFightLife = 0;

        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
        if (deathExplosionsRoutine != null) { StopCoroutine(deathExplosionsRoutine); deathExplosionsRoutine = null; }
        if (winkRoutine != null) { StopCoroutine(winkRoutine); winkRoutine = null; }
        if (angryRoutine != null) { StopCoroutine(angryRoutine); angryRoutine = null; }
        if (hardLowHealthSpecialRoutine != null) { StopCoroutine(hardLowHealthSpecialRoutine); hardLowHealthSpecialRoutine = null; }

        if (movement != null)
            movement.enabled = true;

        EnableOnly(walkRenderer);
        SetRendererAsLooping(walkRenderer, looping: true);

        if (rb != null)
        {
            rb.position = SnapToPixel(rb.position);
            rb.MovePosition(rb.position);
        }
        else
        {
            transform.position = SnapToPixel(transform.position);
        }

        if (characterHealth != null)
        {
            initialFightLife = Mathf.Max(1, characterHealth.life);
            fightLifeInitialized = true;
            RefreshLowHealthTint();
        }

        if (enableWinkAttack)
            winkRoutine = StartCoroutine(WinkAttackLoop());
    }

    void OnDisable()
    {
        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (deathRoutine != null) { StopCoroutine(deathRoutine); deathRoutine = null; }
        if (deathExplosionsRoutine != null) { StopCoroutine(deathExplosionsRoutine); deathExplosionsRoutine = null; }
        if (winkRoutine != null) { StopCoroutine(winkRoutine); winkRoutine = null; }
        if (angryRoutine != null) { StopCoroutine(angryRoutine); angryRoutine = null; }
        if (hardLowHealthSpecialRoutine != null) { StopCoroutine(hardLowHealthSpecialRoutine); hardLowHealthSpecialRoutine = null; }

        inWinkAttack = false;
        inAngry = false;
        inHardLowHealthSpecial = false;
        hardSpecialCanEnterAngry = false;

        if (eyes != null)
            eyes.ClearHardAttackBlackDamagedEyes();

        ClearLowHealthTint();
    }

    void RefreshLowHealthTint()
    {
        if (characterHealth == null)
            return;

        if (!tintWhenBelowHalfLife || !fightLifeInitialized || isDead)
        {
            ClearLowHealthTint();
            return;
        }

        if (characterHealth.life < (initialFightLife * 0.5f))
            characterHealth.SetPersistentTint(lowHealthTintColor, lowHealthTintStrength);
        else
            characterHealth.ClearPersistentTint();
    }

    void ClearLowHealthTint()
    {
        if (characterHealth != null)
            characterHealth.ClearPersistentTint();
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
        TryApplyExplosionDamage(other);
        TryApplyTouchDamage(other);
    }

    void TryDestroyBombOnTouch(Collider2D other)
    {
        if (!destroyBombsOnTouch) return;
        if (isDead) return;
        if (bombLayer < 0) return;
        if (other == null || other.gameObject == null) return;
        if (other.gameObject.layer != bombLayer) return;

        Bomb bomb = other.GetComponentInParent<Bomb>();
        if (bomb == null) bomb = other.GetComponent<Bomb>();
        if (bomb == null) return;
        if (bomb.HasExploded) return;

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
        if (isDead) return;
        if (characterHealth == null || !characterHealth.enabled) return;
        if (characterHealth.IsInvulnerable) return;

        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer < 0) return;
        if (other.gameObject.layer != explosionLayer) return;

        int dmg = Mathf.Max(1, explosionDamage);
        characterHealth.TakeDamage(dmg);
    }

    void TryApplyTouchDamage(Collider2D other)
    {
        if (isDead) return;
        if (Time.time < nextTouchDamageTime) return;

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

        if (player.IsMounted)
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

        RefreshLowHealthTint();
        PlayDamagedSfx();

        bool startHardLowHealthSpecial = ShouldStartHardLowHealthSpecialAttack();
        if (startHardLowHealthSpecial)
        {
            hardLowHealthSpecialPerformed = true;

            if (angryRoutine != null)
            {
                StopCoroutine(angryRoutine);
                angryRoutine = null;
            }

            inAngry = false;
            hardLowHealthSpecialRoutine = StartCoroutine(HardLowHealthSpecialAttackRoutine());
        }
        else if (hardLowHealthSpecialCompleted)
        {
            SpawnCurvedStarBurst(-Mathf.Abs(hardSpecialStarCurveDegrees), hardSpecialStarSpeed);
        }

        if (inAngry)
            return;

        CacheDirectionBeforeHurt();

        if (hurtRoutine != null)
            StopCoroutine(hurtRoutine);

        hurtRoutine = StartCoroutine(HurtStopRoutine());

    }

    void PlayDamagedSfx()
    {
        if (audioSource == null) return;
        if (damagedSfx == null) return;

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

        if (!isDead && !inWinkAttack && !inAngry)
        {
            if (movement != null && hasCachedMoveDirection)
                movement.SetCurrentDirection(cachedMoveDirection);

            EnableOnly(walkRenderer);
            SetRendererAsLooping(walkRenderer, looping: true);
        }

        hurtRoutine = null;
    }

    bool ShouldStartHardLowHealthSpecialAttack()
    {
        return enableHardLowHealthSpecialAttack &&
               !hardLowHealthSpecialPerformed &&
               !inHardLowHealthSpecial &&
               characterHealth != null &&
               fightLifeInitialized &&
               characterHealth.life < (initialFightLife * 0.5f) &&
               UsesHardCampaignModifiers();
    }

    IEnumerator HardLowHealthSpecialAttackRoutine()
    {
        yield return null;
        while (!isDead && characterHealth != null && characterHealth.IsInvulnerable)
            yield return null;

        if (isDead)
        {
            hardLowHealthSpecialRoutine = null;
            yield break;
        }

        inHardLowHealthSpecial = true;
        inWinkAttack = true;
        hardSpecialCanEnterAngry = false;

        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (winkRoutine != null) { StopCoroutine(winkRoutine); winkRoutine = null; }

        StopMovement_DisableComponent();

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        EnableOnly(closedRenderer);
        SetRendererStaticFirstFrame(closedRenderer);

        float moveDuration = Mathf.Max(0.01f, hardSpecialMoveDuration);
        float closedHoldDuration = Mathf.Max(0f, hardSpecialClosedHoldDuration);
        float damagedHoldDuration = Mathf.Max(0f, hardSpecialDamagedHoldDuration);
        int burstCount = Mathf.Max(1, hardSpecialWinkBurstCount);
        float interval = Mathf.Max(0f, hardSpecialWinkBurstInterval);
        float attackInvulnerability =
            moveDuration +
            closedHoldDuration +
            damagedHoldDuration +
            interval * Mathf.Max(0, burstCount - 1);
        characterHealth.StartTemporaryInvulnerability(attackInvulnerability, withBlink: false);

        yield return MoveToHardSpecialCenter(moveDuration);
        if (closedHoldDuration > 0f && !isDead)
            yield return new WaitForSeconds(closedHoldDuration);

        if (isDead)
        {
            hardLowHealthSpecialRoutine = null;
            yield break;
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.PlayDefaultMusic(restart: true);

        EnableOnly(hurtRenderer);
        SetRendererAsLooping(hurtRenderer, looping: true);
        if (eyes != null)
            eyes.BeginHardAttackBlackDamagedEyes();

        if (damagedHoldDuration > 0f)
            yield return new WaitForSeconds(damagedHoldDuration);

        for (int i = 0; i < burstCount && !isDead; i++)
        {
            SpawnCurvedStarBurst(
                i % 2 == 0 ? -hardSpecialStarCurveDegrees : hardSpecialStarCurveDegrees,
                hardSpecialStarSpeed);

            if (i < burstCount - 1)
            {
                if (interval > 0f)
                    yield return new WaitForSeconds(interval);
            }
        }

        if (!isDead)
        {
            hardSpecialCanEnterAngry = true;
            yield return HardSpecialKissFinishRoutine();
        }

        if (eyes != null)
            eyes.ClearHardAttackBlackDamagedEyes();

        inHardLowHealthSpecial = false;
        hardSpecialCanEnterAngry = false;
        inWinkAttack = false;
        hardLowHealthSpecialCompleted = true;
        hardLowHealthSpecialRoutine = null;

        if (!isDead && !inAngry)
        {
            if (movement != null)
                movement.enabled = true;

            EnableOnly(walkRenderer);
            SetRendererAsLooping(walkRenderer, looping: true);
        }

        if (!isDead && enableWinkAttack && winkRoutine == null)
            winkRoutine = StartCoroutine(WinkAttackLoop());
    }

    IEnumerator MoveToHardSpecialCenter(float duration)
    {
        Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
        float elapsed = 0f;

        while (elapsed < duration && !isDead)
        {
            elapsed += Time.deltaTime;
            Vector2 next = Vector2.Lerp(start, hardSpecialCenter, Mathf.Clamp01(elapsed / duration));
            if (movement != null)
                next = movement.SnapToPixelPerfect(next);
            else
                next = SnapToPixel(next);

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.MovePosition(next);
            }
            else
            {
                transform.position = next;
            }

            yield return null;
        }

        if (!isDead)
        {
            Vector2 end = movement != null ? movement.SnapToPixelPerfect(hardSpecialCenter) : SnapToPixel(hardSpecialCenter);
            if (rb != null)
            {
                rb.position = end;
                rb.MovePosition(end);
            }
            else
            {
                transform.position = end;
            }
        }
    }

    IEnumerator HardSpecialKissFinishRoutine()
    {
        if (eyes != null)
            eyes.ClearHardAttackBlackDamagedEyes();

        EnableOnly(kissRenderer);
        SetRendererStaticFirstFrame(kissRenderer);

        SpawnHeart(1f);
        SpawnHeart(hardSpecialSecondHeartSpeedMultiplier);

        float hold = Mathf.Max(0f, winkHoldDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);
    }

    void OnDied() => Kill();

    public void Kill()
    {
        if (isDead)
            return;

        DisableDamageCollisionsForDeath();

        isDead = true;
        inWinkAttack = false;
        inAngry = false;
        inHardLowHealthSpecial = false;
        hardSpecialCanEnterAngry = false;

        ClearLowHealthTint();

        if (winkRoutine != null) { StopCoroutine(winkRoutine); winkRoutine = null; }
        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        if (angryRoutine != null) { StopCoroutine(angryRoutine); angryRoutine = null; }
        if (hardLowHealthSpecialRoutine != null) { StopCoroutine(hardLowHealthSpecialRoutine); hardLowHealthSpecialRoutine = null; }

        if (eyes != null)
            eyes.ClearHardAttackBlackDamagedEyes();

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

        if (eyes != null)
            eyes.BeginDeathEyesFlowDamagedThenSul(deathEyesDamagedDuration);

        if (eyesDelay > 0f)
            yield return new WaitForSeconds(eyesDelay);

        if (explosionPrefab != null && explosionDuration > 0f)
        {
            nextDeathSfxTime = 0f;
            deathExplosionsRoutine = StartCoroutine(SpawnDeathExplosions(explosionDuration));
        }

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

        CreateEndStageRunnerAndSpawnMagnet();

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

    IEnumerator WinkAttackLoop()
    {
        const float fixedAttackInterval = 5f;

        while (!isDead)
        {
            yield return new WaitForSeconds(fixedAttackInterval);

            if (isDead) continue;
            if (!enableWinkAttack) continue;
            if (inWinkAttack) continue;
            if (inAngry) continue;
            if (inHardLowHealthSpecial) continue;
            if (hurtRoutine != null) continue;
            if (deathRoutine != null) continue;
            if (movement != null && !movement.enabled) continue;

            bool canWink = winkRenderer != null && starProjectilePrefab != null;
            bool canKiss = kissRenderer != null && heartPrefab != null;

            if (!canWink && !canKiss)
                continue;

            yield return WinkOrKissOnce(canWink, canKiss);
        }
    }

    IEnumerator WinkOrKissOnce(bool canWink, bool canKiss)
    {
        inWinkAttack = true;

        CacheDirectionBeforeHurt();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (movement != null)
        {
            movement.OnHit(Mathf.Max(0f, winkHoldDuration));
            if (hasCachedMoveDirection)
                movement.SetCurrentDirection(cachedMoveDirection);
        }

        AnimatedSpriteRenderer previous = GetCurrentRenderer();

        bool doWink;

        if (canWink && canKiss)
        {
            doWink = nextWinkOrKissAttackShouldBeWink;
            nextWinkOrKissAttackShouldBeWink = !nextWinkOrKissAttackShouldBeWink;
        }
        else
        {
            doWink = canWink;
        }

        if (doWink)
        {
            EnableOnly(winkRenderer);
            SetRendererStaticFirstFrame(winkRenderer);

            if (!isDead)
            {
                if (hardLowHealthSpecialCompleted)
                    SpawnCurvedStarBurst(-Mathf.Abs(hardSpecialStarCurveDegrees), hardSpecialStarSpeed);
                else
                    SpawnStarBurst();
            }
        }
        else
        {
            EnableOnly(kissRenderer);
            SetRendererStaticFirstFrame(kissRenderer);

            if (!isDead)
            {
                SpawnHeart();
                if (hardLowHealthSpecialCompleted)
                    SpawnHeart(hardSpecialSecondHeartSpeedMultiplier);
            }
        }

        float hold = Mathf.Max(0f, winkHoldDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        if (!isDead)
        {
            if (!inAngry)
            {
                if (previous != null && previous != winkRenderer && previous != kissRenderer)
                {
                    EnableOnly(previous);
                    if (previous == walkRenderer)
                        SetRendererAsLooping(walkRenderer, looping: true);
                }
                else
                {
                    EnableOnly(walkRenderer);
                    SetRendererAsLooping(walkRenderer, looping: true);
                }
            }
        }

        inWinkAttack = false;
    }

    void SpawnStarBurst()
    {
        Vector2 origin = transform.position;

        for (int i = 0; i < StarDirs.Length; i++)
        {
            Vector2 dir = StarDirs[i];
            Vector2 spawnPos = origin + dir * Mathf.Max(0f, starSpawnRadius);
            spawnPos = SnapToPixel(spawnPos);

            StarProjectile proj = Instantiate(starProjectilePrefab, spawnPos, Quaternion.identity);
            if (proj == null)
                continue;

            proj.damage = Mathf.Max(1, starDamage);
            proj.obstacleMask = starObstacleMask;
            proj.Initialize(dir, starSpeed, starLifeTime);
        }
    }

    void SpawnCurvedStarBurst(float angularSpeedDegrees, float burstSpeed)
    {
        Vector2 origin = transform.position;

        for (int i = 0; i < StarDirs.Length; i++)
        {
            Vector2 dir = StarDirs[i];
            Vector2 spawnPos = SnapToPixel(origin + dir * Mathf.Max(0f, starSpawnRadius));

            StarProjectile proj = Instantiate(starProjectilePrefab, spawnPos, Quaternion.identity);
            if (proj == null)
                continue;

            proj.damage = Mathf.Max(1, starDamage);
            proj.obstacleMask = starObstacleMask;
            proj.InitializeCurved(dir, Mathf.Max(0.01f, burstSpeed), starLifeTime, angularSpeedDegrees);
        }
    }

    void SpawnHeart(float speedMultiplier = 1f)
    {
        Vector2 origin = transform.position;
        Vector2 randomOffset = Random.insideUnitCircle * Mathf.Max(0f, heartSpawnRadius);
        Vector2 spawnPos = origin + randomOffset;
        spawnPos = SnapToPixel(spawnPos);

        var heart = Instantiate(heartPrefab, spawnPos, Quaternion.identity);
        if (heart == null)
            return;

        heart.SetBoss(this);

        heart.Initialize(
            speed: heartSpeed * Mathf.Max(0f, speedMultiplier),
            lifeTime: heartLifeTime,
            floatAmplitude: heartFloatAmplitude,
            floatFrequency: heartFloatFrequency
        );
    }

    public void NotifyHeartDestroyedByPlayer(MovementController playerWhoDestroyed)
    {
        if (isDead) return;
        if ((inHardLowHealthSpecial || hardLowHealthSpecialRoutine != null) && !hardSpecialCanEnterAngry) return;
        if (playerWhoDestroyed == null) return;
        if (playerWhoDestroyed.isDead || playerWhoDestroyed.IsEndingStage) return;

        EnterAngry(playerWhoDestroyed);
    }

    void EnterAngry(MovementController targetPlayer)
    {
        if (isDead) return;

        inAngry = true;
        inWinkAttack = false;

        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }

        if (angryRoutine != null)
            StopCoroutine(angryRoutine);

        angryRoutine = StartCoroutine(AngryRoutine(targetPlayer));
    }

    IEnumerator AngryRoutine(MovementController targetPlayer)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = SnapToPixel(rb.position);
            rb.MovePosition(rb.position);
        }

        if (movement != null)
            movement.enabled = false;

        AnimatedSpriteRenderer r = angryRenderer != null ? angryRenderer : walkRenderer;
        EnableOnly(r);
        SetRendererAsLooping(r, looping: true);

        float freeze = Mathf.Max(0f, angryFreezeSeconds);
        if (freeze > 0f)
            yield return new WaitForSeconds(freeze);

        float angrySpeed = movement != null ? Mathf.Max(0.01f, movement.speed) : 2.5f;
        float chaseSpeed = Mathf.Max(0.01f, angrySpeed * angryChaseSpeedMultiplier);
        float effectiveChaseSpeed = chaseSpeed;

        if (!isDead &&
            angryArcRadius > 0f &&
            targetPlayer != null &&
            !targetPlayer.isDead &&
            !targetPlayer.IsEndingStage)
        {
            yield return MoveFullCircleClockwiseTowardTarget(targetPlayer, angryArcRadius, angrySpeed);
        }

        float chaseDur = Mathf.Max(0f, angryChaseSeconds);
        float endTime = Time.time + chaseDur;

        Vector2 chaseDir;
        if (targetPlayer != null && rb != null)
        {
            Vector2 initialTo = (Vector2)targetPlayer.transform.position - rb.position;
            if (initialTo.sqrMagnitude > 0.0001f)
                chaseDir = initialTo.normalized;
            else
                chaseDir = Vector2.down;
        }
        else
        {
            chaseDir = Vector2.down;
        }

        float nextRetargetTime = 0f;
        Vector2 desiredDir = chaseDir;
        Vector2 continuousChasePosition = rb != null ? rb.position : (Vector2)transform.position;

        while (!isDead && Time.time < endTime)
        {
            if (targetPlayer == null || targetPlayer.isDead || targetPlayer.IsEndingStage)
                break;

            if (rb != null)
            {
                Vector2 pos = rb.position;

                if (Time.time >= nextRetargetTime)
                {
                    nextRetargetTime = Time.time + Mathf.Max(0.01f, angryRetargetInterval);

                    Vector2 to = (Vector2)targetPlayer.transform.position - pos;
                    if (to.sqrMagnitude > 0.0001f)
                        desiredDir = to.normalized;
                }

                float distanceToPlayer = Vector2.Distance(pos, (Vector2)targetPlayer.transform.position);
                effectiveChaseSpeed = ResolveAngryChaseSpeed(chaseSpeed, distanceToPlayer);

                float effectiveTurnSpeedDegrees = ResolveAngryChaseTurnSpeed(distanceToPlayer);
                float maxRadiansDelta = effectiveTurnSpeedDegrees * Mathf.Deg2Rad * Time.fixedDeltaTime;
                Vector3 v = Vector3.RotateTowards(chaseDir, desiredDir, maxRadiansDelta, 0f);
                chaseDir = (Vector2)v.normalized;

                Vector2 step = chaseDir * (effectiveChaseSpeed * Time.fixedDeltaTime);
                continuousChasePosition += step;
                ResolveAngryChaseBounds(ref continuousChasePosition, ref chaseDir, ref desiredDir);
                Vector2 next = SnapToPixel(continuousChasePosition);

                rb.MovePosition(next);
            }

            yield return new WaitForFixedUpdate();
        }

        if (!isDead)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = SnapToPixel(rb.position);
                rb.MovePosition(rb.position);
            }

            float stopAfter = Mathf.Max(0f, angryAfterChaseStopSeconds);

            if (stopAfter > 0f)
            {
                float half = stopAfter * 0.5f;

                if (half > 0f)
                    yield return new WaitForSeconds(half);

                if (!isDead)
                {
                    EnableOnly(walkRenderer);
                    SetRendererAsLooping(walkRenderer, looping: true);
                }

                if (half > 0f)
                    yield return new WaitForSeconds(half);
            }

            if (!isDead)
            {
                if (movement != null)
                    movement.enabled = true;
            }
        }

        inAngry = false;
        angryRoutine = null;
    }

    float ResolveAngryChaseSpeed(float baseChaseSpeed, float distanceToPlayer)
    {
        float slowDistance = Mathf.Max(7f, angryChaseCloseSlowDistance);
        if (slowDistance <= 0f || distanceToPlayer >= slowDistance)
            return baseChaseSpeed;

        float closeMultiplier = Mathf.Clamp(angryChaseCloseSpeedMultiplier, 0.45f, 1f);
        float t = Mathf.Clamp01(distanceToPlayer / slowDistance);
        float multiplier = Mathf.Lerp(closeMultiplier, 1f, t);
        return Mathf.Max(Mathf.Max(0.01f, angryChaseMinimumSpeed), baseChaseSpeed * multiplier);
    }

    float ResolveAngryChaseTurnSpeed(float distanceToPlayer)
    {
        float slowDistance = Mathf.Max(7f, angryChaseCloseSlowDistance);
        if (slowDistance <= 0f || distanceToPlayer >= slowDistance)
            return angryTurnSpeedDegrees;

        float closeMultiplier = Mathf.Clamp(angryChaseCloseTurnSpeedMultiplier, 0.1f, 0.45f);
        float t = Mathf.Clamp01(distanceToPlayer / slowDistance);
        float multiplier = Mathf.Lerp(closeMultiplier, 1f, t);
        return Mathf.Max(1f, angryTurnSpeedDegrees * multiplier);
    }

    bool ResolveAngryChaseBounds(ref Vector2 position, ref Vector2 chaseDir, ref Vector2 desiredDir)
    {
        if (movement == null || !movement.useBounds)
            return false;

        bool hitX = false;
        bool hitY = false;

        if (position.x < movement.minBounds.x)
        {
            position.x = movement.minBounds.x + (movement.minBounds.x - position.x);
            hitX = true;
        }
        else if (position.x > movement.maxBounds.x)
        {
            position.x = movement.maxBounds.x - (position.x - movement.maxBounds.x);
            hitX = true;
        }

        if (position.y < movement.minBounds.y)
        {
            position.y = movement.minBounds.y + (movement.minBounds.y - position.y);
            hitY = true;
        }
        else if (position.y > movement.maxBounds.y)
        {
            position.y = movement.maxBounds.y - (position.y - movement.maxBounds.y);
            hitY = true;
        }

        if (!hitX && !hitY)
            return false;

        if (hitX)
        {
            chaseDir.x *= -1f;
            desiredDir.x *= -1f;
        }

        if (hitY)
        {
            chaseDir.y *= -1f;
            desiredDir.y *= -1f;
        }

        position.x = Mathf.Clamp(position.x, movement.minBounds.x, movement.maxBounds.x);
        position.y = Mathf.Clamp(position.y, movement.minBounds.y, movement.maxBounds.y);

        chaseDir = chaseDir.sqrMagnitude > 0.0001f ? chaseDir.normalized : Vector2.down;
        desiredDir = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : chaseDir;
        return true;
    }

    IEnumerator MoveFullCircleClockwiseTowardTarget(MovementController target, float radius, float moveSpeed)
    {
        if (rb == null || target == null)
            yield break;

        radius = Mathf.Max(0.01f, radius);
        moveSpeed = Mathf.Max(0.01f, moveSpeed);

        Vector2 start = SnapToPixel(rb.position);
        Vector2 toTarget = (Vector2)target.transform.position - start;

        if (toTarget.sqrMagnitude < 0.0001f)
            yield break;

        Vector2 dir = toTarget.normalized;

        Vector2 center = SnapToPixel(start - dir * radius);
        Vector2 v0 = start - center;

        float angularSpeedDegrees = (moveSpeed / radius) * Mathf.Rad2Deg;
        float travelledDegrees = 0f;

        while (!isDead && travelledDegrees < 360f)
        {
            if (target == null || target.isDead || target.IsEndingStage)
                break;

            float stepDegrees = angularSpeedDegrees * Time.fixedDeltaTime;
            float nextTravelledDegrees = Mathf.Min(360f, travelledDegrees + stepDegrees);

            float angle = -nextTravelledDegrees;
            Vector2 p = center + Rotate(v0, angle);
            p = SnapToPixel(p);

            rb.MovePosition(p);

            travelledDegrees = nextTravelledDegrees;

            yield return new WaitForFixedUpdate();
        }

        if (!isDead && target != null && !target.isDead && !target.IsEndingStage)
        {
            rb.MovePosition(start);
        }
    }

    Vector2 SnapToPixel(Vector2 world)
    {
        int ppu = Mathf.Max(1, pixelsPerUnit);
        float s = 1f / ppu;
        world.x = Mathf.Round(world.x / s) * s;
        world.y = Mathf.Round(world.y / s) * s;
        return world;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    AnimatedSpriteRenderer GetCurrentRenderer()
    {
        if (walkRenderer != null && walkRenderer.enabled) return walkRenderer;
        if (angryRenderer != null && angryRenderer.enabled) return angryRenderer;
        if (winkRenderer != null && winkRenderer.enabled) return winkRenderer;
        if (kissRenderer != null && kissRenderer.enabled) return kissRenderer;
        if (hurtRenderer != null && hurtRenderer.enabled) return hurtRenderer;
        if (deathRenderer != null && deathRenderer.enabled) return deathRenderer;
        return null;
    }

    void EnableOnly(AnimatedSpriteRenderer target)
    {
        if (walkRenderer != null) walkRenderer.enabled = (target == walkRenderer);
        if (closedRenderer != null) closedRenderer.enabled = (target == closedRenderer);
        if (angryRenderer != null) angryRenderer.enabled = (target == angryRenderer);
        if (winkRenderer != null) winkRenderer.enabled = (target == winkRenderer);
        if (kissRenderer != null) kissRenderer.enabled = (target == kissRenderer);
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

    void SetRendererStaticFirstFrame(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        if (r.animationSprite != null && r.animationSprite.Length > 0)
        {
            r.CurrentFrame = 0;
            r.idleSprite = r.animationSprite[0];
        }

        r.idle = true;
        r.loop = false;
        r.RefreshFrame();
    }

    private void CreateEndStageRunnerAndSpawnMagnet()
    {
        var runnerGo = new GameObject("SunMask_EndStageAfterMagnetRunner");
        runnerGo.transform.position = transform.position;

        var runner = runnerGo.AddComponent<EndStageAfterMagnetRunner>();

        runner.magnetBomberPrefab = magnetBomberPrefab;
        runner.playMagnetBomberDeathOnSpawn = playMagnetBomberDeathOnSpawn;
        runner.magnetBomberDeathFallbackDuration = magnetBomberDeathFallbackDuration;

        runner.endStageDelayAfterMagnetDeath = endStageDelayAfterMagnetDeath;

        runner.CopyEndStageConfigFrom(bossEndSequence);
    }

    void DisableDamageCollisionsForDeath()
    {
        var colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (rb != null)
            rb.simulated = false;
    }

    public void KillByExplosion()
    {
        Kill();
    }
}
