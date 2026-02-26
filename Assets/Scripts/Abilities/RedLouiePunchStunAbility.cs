using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
public class RedLouiePunchStunAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "RedLouiePunchStun";

    [SerializeField] private bool enabledAbility = true;

    public float punchCooldownSeconds = 0.25f;
    public float punchRange = 0.75f;
    public Vector2 punchBoxSize = new(0.65f, 0.45f);

    public float stunSeconds = 2f;

    public float successCooldownSeconds = 2.25f;

    public AudioClip punchSfx;
    [Range(0f, 1f)] public float punchSfxVolume = 1f;

    MovementController movement;
    Rigidbody2D rb;
    AudioSource audioSource;

    float nextAllowedTime;
    Coroutine routine;

    IRedLouiePunchExternalAnimator externalAnimator;

    bool deathCancelInProgress;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    void Awake()
    {
        movement = GetComponent<MovementController>();
        rb = movement != null ? movement.Rigidbody : GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
    }

    void OnDisable() => Cancel();
    void OnDestroy() => Cancel();

    public void SetExternalAnimator(IRedLouiePunchExternalAnimator animator)
    {
        externalAnimator = animator;
    }

    public void SetPunchSfx(AudioClip clip, float volume)
    {
        punchSfx = clip;
        punchSfxVolume = Mathf.Clamp01(volume);
    }

    void Update()
    {
        if (!enabledAbility)
            return;

        if (!CompareTag("Player"))
            return;

        if (movement == null || movement.isDead)
            return;

        if (Time.time < nextAllowedTime)
            return;

        if (GamePauseController.IsPaused ||
            ClownMaskBoss.BossIntroRunning ||
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null &&
             (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning)))
            return;

        var input = PlayerInputManager.Instance;
        int pid = movement.PlayerId;
        if (input == null || !input.GetDown(pid, PlayerAction.ActionC))
            return;

        if (routine != null)
            return;

        routine = StartCoroutine(PunchRoutine());
    }

    IEnumerator PunchRoutine()
    {
        if (movement == null)
        {
            routine = null;
            yield break;
        }

        Vector2 dir = movement.Direction != Vector2.zero ? movement.Direction : movement.FacingDirection;
        if (dir == Vector2.zero)
            dir = Vector2.down;

        externalAnimator?.Play(dir);

        movement.SetInputLocked(true, false);

        bool stunnedSomeone = TryHitEnemy(dir);

        if (stunnedSomeone)
        {
            if (audioSource != null && punchSfx != null)
                audioSource.PlayOneShot(punchSfx, punchSfxVolume);

            float extra = Mathf.Max(0.01f, successCooldownSeconds);
            nextAllowedTime = Mathf.Max(nextAllowedTime, Time.time + extra);
        }
        else
        {
            float baseCd = Mathf.Max(0.01f, punchCooldownSeconds);
            nextAllowedTime = Mathf.Max(nextAllowedTime, Time.time + baseCd);
        }

        float end = Time.time + Mathf.Max(0.01f, punchCooldownSeconds);
        while (Time.time < end)
        {
            if (!enabledAbility || movement == null || movement.isDead)
                break;

            yield return null;
        }

        if (!deathCancelInProgress)
        {
            externalAnimator?.Stop();

            if (movement != null)
                movement.SetInputLocked(false);
        }

        routine = null;
        deathCancelInProgress = false;
    }

    public void CancelPunchForDeath()
    {
        deathCancelInProgress = true;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (externalAnimator is RedLouiePunchAnimator anim && anim != null)
        {
            anim.CancelForDeath();
        }
        else if (externalAnimator is MonoBehaviour mb && mb != null)
        {
            mb.enabled = false;
        }

        externalAnimator = null;
    }

    bool TryHitEnemy(Vector2 dir)
    {
        int enemyMask = LayerMask.GetMask("Enemy");
        if (enemyMask == 0)
            return false;

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 ndir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.down;

        Vector2 center = origin + ndir * Mathf.Max(0.01f, punchRange);

        float w = Mathf.Max(0.05f, punchBoxSize.x);
        float h = Mathf.Max(0.05f, punchBoxSize.y);

        if (Mathf.Abs(ndir.x) > 0.01f)
            (w, h) = (Mathf.Max(w, h), Mathf.Min(w, h));
        else
            (w, h) = (Mathf.Min(w, h), Mathf.Max(w, h));

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyMask,
            useTriggers = true
        };

        Collider2D[] results = new Collider2D[8];
        int count = Physics2D.OverlapBox(center, new Vector2(w, h), 0f, filter, results);

        if (count <= 0)
            return false;

        bool stunnedAny = false;

        for (int i = 0; i < count; i++)
        {
            var hit = results[i];
            if (hit == null)
                continue;

            var receiver = hit.GetComponentInParent<StunReceiver>();
            if (receiver == null)
                receiver = hit.GetComponent<StunReceiver>();

            if (receiver != null)
            {
                receiver.Stun(stunSeconds);
                stunnedAny = true;
            }
        }

        return stunnedAny;
    }

    void Cancel()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        externalAnimator?.Stop();

        if (movement != null)
            movement.SetInputLocked(false);
    }

    public void Enable() => enabledAbility = true;

    public void Disable()
    {
        enabledAbility = false;
        Cancel();
    }
}