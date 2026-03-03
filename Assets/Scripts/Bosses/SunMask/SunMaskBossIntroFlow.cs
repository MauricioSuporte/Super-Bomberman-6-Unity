using System.Collections;
using UnityEngine;

public class SunMaskBossIntroFlow : BossIntroFlowBase
{
    [Header("Boss")]
    [SerializeField] private SunMaskBoss boss;

    [Tooltip("Alvo (world position) onde o SunMaskBoss deve parar antes de iniciar o duelo.")]
    [SerializeField] private Transform bossStopPoint;

    [Tooltip("Tempo (segundos) para descer o boss até o ponto alvo.")]
    [SerializeField, Min(0f)] private float bossDescendSeconds = 1.0f;

    [Tooltip("Se true, trava o script do boss (SunMaskBoss.enabled=false) durante a intro.")]
    [SerializeField] private bool lockBossByDisablingBehaviour = true;

    [Header("Eyes Intro")]
    [SerializeField, Min(0f)] private float stopHoldSeconds = 0.5f;
    [SerializeField, Min(0f)] private float eyesSpinSeconds = 0.5f;

    [Header("Closed Blink (after eyes spin)")]
    [SerializeField, Min(0f)] private float closedHoldSeconds = 0.1f;
    [SerializeField, Min(0f)] private float postClosedHoldSeconds = 0.4f;

    private Rigidbody2D bossRb;
    private SunMaskEyesController bossEyes;

    private bool bossLockApplied;
    private bool bossWasEnabledBeforeLock;
    private bool bossMovementWasEnabledBeforeLock;

    protected override void Awake()
    {
        base.Awake();

        if (!boss)
            boss = FindAnyObjectByType<SunMaskBoss>();

        if (boss)
        {
            bossRb = boss.GetComponent<Rigidbody2D>();
            bossEyes = boss.GetComponentInChildren<SunMaskEyesController>(true);
        }
    }

    protected override IEnumerator FlowRoutine()
    {
        RefreshPlayersRefs();
        PushPlayersSafety();

        s_bossIntroRunningCount++;

        try
        {
            OnIntroStarted();

            RefreshPlayersRefs();
            MaintainPlayersSafety();
            LockPlayers(true);
            ForcePlayersIdleUp();
            LockBoss(true);

            HoldEyesDown();

            if (StageIntroTransition.Instance != null)
            {
                while (StageIntroTransition.Instance.IntroRunning)
                {
                    if (!enableFlow) yield break;

                    RefreshPlayersRefs();
                    MaintainPlayersSafety();
                    LockPlayers(true);
                    ForcePlayersIdleUp();
                    LockBoss(true);

                    HoldEyesDown();
                    yield return null;
                }
            }

            if (!enableFlow) yield break;

            if (boss && bossStopPoint)
                yield return DescendBossToPointRoutine(bossStopPoint.position);

            if (!enableFlow) yield break;

            if (stopHoldSeconds > 0f)
            {
                HoldEyesDown();
                yield return PauseAwareWait(stopHoldSeconds);
            }

            if (!enableFlow) yield break;

            // SPIN: durante o spin, o próprio EyesController controla a animação
            if (bossEyes != null && eyesSpinSeconds > 0f)
                yield return bossEyes.PlayIntroSpinCounterClockwise(eyesSpinSeconds);

            if (!enableFlow) yield break;

            // Depois do spin: manter olhos Down durante toda a sequência
            HoldEyesDown();

            // 0.1s Closed
            if (closedHoldSeconds > 0f)
            {
                SetBodyClosed(true);
                yield return PauseAwareWait(closedHoldSeconds);
            }

            // volta Walking + olhos Down por 0.4s
            SetBodyClosed(false);

            if (postClosedHoldSeconds > 0f)
            {
                HoldEyesDown();
                yield return PauseAwareWait(postClosedHoldSeconds);
            }

            ReleaseCombat();
        }
        finally
        {
            while (playersSafetyLocks > 0)
                PopPlayersSafety();

            LockPlayers(false);
            UnlockBoss();

            OnIntroFinished();
            s_bossIntroRunningCount = Mathf.Max(0, s_bossIntroRunningCount - 1);
        }
    }

    private void HoldEyesDown()
    {
        if (bossEyes != null)
            bossEyes.BeginIntroHoldDown();
    }

    private void SetBodyClosed(bool closed)
    {
        if (boss == null)
            return;

        // Desliga tudo do corpo e liga só o alvo
        if (boss.walkRenderer != null) boss.walkRenderer.enabled = false;
        if (boss.closedRenderer != null) boss.closedRenderer.enabled = false;
        if (boss.hurtRenderer != null) boss.hurtRenderer.enabled = false;
        if (boss.deathRenderer != null) boss.deathRenderer.enabled = false;

        AnimatedSpriteRenderer target = closed ? boss.closedRenderer : boss.walkRenderer;
        if (target == null)
            return;

        target.enabled = true;

        if (closed)
            SetRendererStaticFirstFrame(target);
        else
            SetRendererAsLooping(target, looping: true);

        if (target.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = true;

        target.RefreshFrame();
    }

    private IEnumerator PauseAwareWait(float seconds)
    {
        float t = 0f;
        float dur = Mathf.Max(0f, seconds);

        while (t < dur)
        {
            if (!enableFlow) yield break;

            if (!GamePauseController.IsPaused)
                t += Time.deltaTime;

            yield return null;
        }
    }

    private IEnumerator DescendBossToPointRoutine(Vector3 targetWorld)
    {
        if (!boss || !bossRb)
            yield break;

        float dur = Mathf.Max(0.001f, bossDescendSeconds);
        float elapsed = 0f;

        Vector2 start = bossRb.position;
        Vector2 end = new Vector2(targetWorld.x, targetWorld.y);

        bossRb.linearVelocity = Vector2.zero;

        while (elapsed < dur)
        {
            if (!enableFlow) yield break;

            RefreshPlayersRefs();
            MaintainPlayersSafety();
            LockPlayers(true);
            ForcePlayersIdleUp();
            LockBoss(true);

            HoldEyesDown();

            if (GamePauseController.IsPaused)
            {
                yield return null;
                continue;
            }

            float t = Mathf.Clamp01(elapsed / dur);
            bossRb.MovePosition(Vector2.Lerp(start, end, t));

            elapsed += Time.deltaTime;
            yield return null;
        }

        bossRb.MovePosition(end);
        bossRb.linearVelocity = Vector2.zero;
    }

    private static void SetRendererAsLooping(AnimatedSpriteRenderer r, bool looping)
    {
        if (r == null)
            return;

        r.idle = false;
        r.loop = looping;
        r.CurrentFrame = 0;
        r.RefreshFrame();
    }

    private static void SetRendererStaticFirstFrame(AnimatedSpriteRenderer r)
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

    protected override void LockBoss(bool locked)
    {
        if (!boss)
            boss = FindAnyObjectByType<SunMaskBoss>();

        if (!boss)
            return;

        if (!bossRb)
            bossRb = boss.GetComponent<Rigidbody2D>();

        if (locked)
        {
            if (bossLockApplied)
                return;

            bossLockApplied = true;

            if (bossRb)
                bossRb.linearVelocity = Vector2.zero;

            if (boss.movement)
            {
                bossMovementWasEnabledBeforeLock = boss.movement.enabled;
                boss.movement.enabled = false;
            }

            if (lockBossByDisablingBehaviour)
            {
                bossWasEnabledBeforeLock = boss.enabled;
                boss.enabled = false;
            }

            return;
        }

        UnlockBoss();
    }

    protected override void UnlockBoss()
    {
        if (!boss)
            boss = FindAnyObjectByType<SunMaskBoss>();

        if (!boss)
            return;

        if (!bossLockApplied)
            return;

        bossLockApplied = false;

        if (lockBossByDisablingBehaviour)
            boss.enabled = bossWasEnabledBeforeLock;

        if (boss.movement)
            boss.movement.enabled = bossMovementWasEnabledBeforeLock;

        if (!bossRb)
            bossRb = boss.GetComponent<Rigidbody2D>();

        if (bossRb)
            bossRb.linearVelocity = Vector2.zero;

        if (bossEyes != null)
            bossEyes.ClearOverrideIfIntro();
    }
}