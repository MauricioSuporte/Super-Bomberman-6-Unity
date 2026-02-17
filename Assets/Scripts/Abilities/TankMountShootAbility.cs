using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class TankMountShootAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "TankMountShoot";

    [SerializeField] private bool enabledAbility = true;

    [Header("Input")]
    [SerializeField] private bool lockInputWhileShooting = true;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float shootLockSeconds = 0.25f;
    private readonly float cooldownSeconds = 10f;

    [Header("Cooldown Tint (Tank Sprites)")]
    [SerializeField] private bool tintTankOnCooldown = true;
    [SerializeField] private Color cooldownTintColor = new(8f, 0.2f, 0.2f, 1f);

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private bool autoLoadProjectileFromResources = true;
    [SerializeField] private string projectileResourcesPath = "Tank/Shot";

    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField] private Vector2 projectileLocalOffset = new(0f, 0.05f);
    [SerializeField] private LayerMask projectileHitMask;

    [Header("SFX")]
    [SerializeField] private AudioClip shotSfx;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 1f;

    private MovementController movement;
    private Rigidbody2D rb;
    private Coroutine routine;

    private Vector2 lastFacingDir = Vector2.down;
    private bool running;
    private float cooldownUntil;

    private ITankMountShootExternalAnimator externalAnimator;
    private AudioSource audioSource;

    private MountVisualController cachedTankVisual;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = GetComponentInParent<AudioSource>();

        if (projectileHitMask.value == 0)
            projectileHitMask = LayerMask.GetMask("Bomb", "Stage", "Enemy", "Player", "Water", "Explosion");

        TryAutoLoadProjectilePrefab();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            TryAutoLoadProjectilePrefab();
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();

    public void SetExternalAnimator(ITankMountShootExternalAnimator animator)
    {
        externalAnimator = animator;
        cachedTankVisual = null;

        var v = ResolveTankVisual();
        if (v != null && tintTankOnCooldown)
            v.SetExternalTint(false, cooldownTintColor, 1f);
    }

    public void SetShotSfx(AudioClip clip, float volume)
    {
        shotSfx = clip;
        shotVolume = Mathf.Clamp01(volume);
    }

    private void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (movement == null || movement.isDead || movement.InputLocked)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        TickCooldownTint();

        if (movement.Direction != Vector2.zero)
            lastFacingDir = movement.Direction;

        if (Time.time < cooldownUntil)
            return;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        int pid = movement.PlayerId;
        if (!input.GetDown(pid, PlayerAction.ActionC))
            return;

        if (!IsMountedOnTank())
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShootRoutine());
    }

    private IEnumerator ShootRoutine()
    {
        if (running)
        {
            routine = null;
            yield break;
        }

        if (movement == null || rb == null)
        {
            routine = null;
            yield break;
        }

        running = true;

        Vector2 dir = lastFacingDir == Vector2.zero ? Vector2.down : Cardinalize(lastFacingDir);

        if (lockInputWhileShooting)
            movement.SetInputLocked(true, false);

        movement.ForceFacingDirection(dir);

        try
        {
            externalAnimator?.Play(dir);

            StopMotionNow();

            SpawnProjectile(dir);

            if (shotSfx != null)
            {
                var src = audioSource != null ? audioSource : FindAnyPlayerAudioSource();
                if (src != null)
                    src.PlayOneShot(shotSfx, shotVolume);
            }

            cooldownUntil = Time.time + Mathf.Max(0f, cooldownSeconds);

            var v = ResolveTankVisual();
            if (v != null && tintTankOnCooldown)
                v.SetExternalTint(true, cooldownTintColor, 0f);

            float t = Mathf.Max(0f, shootLockSeconds);
            if (t > 0f)
                yield return new WaitForSecondsRealtime(t);
        }
        finally
        {
            externalAnimator?.Stop();

            if (movement != null && lockInputWhileShooting)
                movement.SetInputLocked(false);

            running = false;
            routine = null;
        }
    }

    private void TickCooldownTint()
    {
        if (!tintTankOnCooldown)
            return;

        var v = ResolveTankVisual();
        if (v == null)
            return;

        if (!IsMountedOnTank())
        {
            v.SetExternalTint(false, cooldownTintColor, 1f);
            return;
        }

        float remaining = cooldownUntil - Time.time;

        if (remaining <= 0f || cooldownSeconds <= 0f)
        {
            v.SetExternalTint(false, cooldownTintColor, 1f);
            return;
        }

        float normalized = 1f - Mathf.Clamp01(remaining / cooldownSeconds);
        v.SetExternalTint(true, cooldownTintColor, normalized);
    }

    private MountVisualController ResolveTankVisual()
    {
        if (cachedTankVisual != null)
            return cachedTankVisual;

        if (!TryGetComponent<PlayerMountCompanion>(out var mount) || mount == null)
            return null;

        if (mount.GetMountedLouieType() != MountedType.Tank)
            return null;

        var visuals = GetComponentsInChildren<MountVisualController>(true);
        for (int i = 0; i < visuals.Length; i++)
        {
            var v = visuals[i];
            if (v == null)
                continue;

            cachedTankVisual = v;
            break;
        }

        return cachedTankVisual;
    }

    private void StopMotionNow()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (movement != null)
            movement.SetInactivityMountedDownOverride(false);
    }

    private void TryAutoLoadProjectilePrefab()
    {
        if (projectilePrefab != null)
            return;

        if (!autoLoadProjectileFromResources)
            return;

        if (string.IsNullOrWhiteSpace(projectileResourcesPath))
            return;

        projectilePrefab = Resources.Load<GameObject>(projectileResourcesPath);
    }

    private void SpawnProjectile(Vector2 dir)
    {
        TryAutoLoadProjectilePrefab();

        if (projectilePrefab == null || rb == null || movement == null)
            return;

        float tileSize = Mathf.Max(0.1f, movement.tileSize);

        Vector2 spawn = rb.position + dir * tileSize + projectileLocalOffset;

        var go = Instantiate(projectilePrefab, spawn, Quaternion.identity);
        if (go == null)
            return;

        var proj = go.GetComponent<TankShot>();
        if (proj == null)
            proj = go.AddComponent<TankShot>();

        proj.Init(dir, projectileSpeed, projectileHitMask, gameObject);
    }

    private bool IsMountedOnTank()
    {
        if (!TryGetComponent<PlayerMountCompanion>(out var mount) || mount == null)
            return false;

        return mount.GetMountedLouieType() == MountedType.Tank;
    }

    private static Vector2 Cardinalize(Vector2 v)
    {
        if (v == Vector2.zero)
            return Vector2.down;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private static AudioSource FindAnyPlayerAudioSource()
    {
        var all = FindObjectsByType<BombController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var bc = all[i];
            if (bc == null || !bc.CompareTag("Player"))
                continue;

            if (bc.playerAudioSource != null)
                return bc.playerAudioSource;
        }

        return null;
    }

    private void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        externalAnimator?.Stop();

        if (movement != null && lockInputWhileShooting)
            movement.SetInputLocked(false);

        var v = ResolveTankVisual();
        if (v != null && tintTankOnCooldown)
            v.SetExternalTint(false, cooldownTintColor, 1f);

        running = false;
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        Cancel();
    }
}
