using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
public class GreenLouieDashAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "GreenLouieDash";

    [SerializeField] private bool enabledAbility = true;

    public float dashSpeedMultiplier = 3.5f;

    public AudioClip dashSfx;
    [Range(0f, 1f)] public float dashSfxVolume = 1f;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    Vector2 lastFacingDir = Vector2.down;
    Coroutine routine;

    float originalSpeed;
    bool dashActive;

    IGreenLouieDashExternalAnimator externalAnimator;

    int bombLayer;

    public bool DashActive => dashActive;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : null;
        audioSource = GetComponent<AudioSource>();

        bombLayer = LayerMask.NameToLayer("Bomb");
    }

    void OnDisable() => CancelDash();
    void OnDestroy() => CancelDash();

    public void SetExternalAnimator(IGreenLouieDashExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    public void SetDashSfx(AudioClip clip, float volume)
    {
        dashSfx = clip;
        dashSfxVolume = Mathf.Clamp01(volume);
    }

    void Update()
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

        if (movement.Direction != Vector2.zero)
            lastFacingDir = movement.Direction;

        var input = PlayerInputManager.Instance;
        if (input == null)
            return;

        if (!input.GetDown(PlayerAction.ActionC))
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        if (movement == null || rb == null)
        {
            routine = null;
            yield break;
        }

        Vector2 dir = lastFacingDir == Vector2.zero ? Vector2.down : lastFacingDir;

        dashActive = true;
        originalSpeed = movement.speed;

        if (audioSource != null && dashSfx != null)
            audioSource.PlayOneShot(dashSfx, dashSfxVolume);

        movement.speed *= dashSpeedMultiplier;
        movement.SetInputLocked(true, false);

        externalAnimator?.Play(dir);

        try
        {
            while (true)
            {
                if (!enabledAbility || movement == null || movement.isDead)
                    break;

                Vector2 nextPos = rb.position + movement.speed * Time.fixedDeltaTime * dir;

                if (IsBlocked(nextPos, dir))
                    break;

                rb.MovePosition(nextPos);
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            dashActive = false;

            externalAnimator?.Stop();

            if (movement != null)
            {
                movement.speed = originalSpeed;
                movement.SetInputLocked(false);
            }

            routine = null;
        }
    }

    void CancelDash()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (dashActive)
        {
            dashActive = false;

            externalAnimator?.Stop();

            if (movement != null)
            {
                movement.speed = originalSpeed;
                movement.SetInputLocked(false);
            }
        }
    }

    bool HasDestructiblePassEnabled()
    {
        if (!TryGetComponent<AbilitySystem>(out var abilitySystem) || abilitySystem == null)
            return false;

        abilitySystem.RebuildCache();
        return abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
    }

    bool HasBombPassEnabled()
    {
        if (PlayerPersistentStats.CanPassBombs)
            return true;

        if (!TryGetComponent<AbilitySystem>(out var abilitySystem) || abilitySystem == null)
            return false;

        abilitySystem.RebuildCache();
        return abilitySystem.IsEnabled(BombPassAbility.AbilityId);
    }

    bool IsBlocked(Vector2 targetPos, Vector2 dir)
    {
        if (movement == null)
            return true;

        Vector2 size = Mathf.Abs(dir.x) > 0.01f
            ? new Vector2(movement.tileSize * 0.6f, movement.tileSize * 0.2f)
            : new Vector2(movement.tileSize * 0.2f, movement.tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPos, size, 0f);

        if (hits == null || hits.Length == 0)
            return false;

        bool canPassDestructibles = HasDestructiblePassEnabled();
        bool canPassBombs = HasBombPassEnabled();

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            if (canPassDestructibles && hit.CompareTag("Destructibles"))
                continue;

            if (canPassBombs && hit.gameObject.layer == bombLayer)
                continue;

            return true;
        }

        return false;
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        CancelDash();
    }
}
