using UnityEngine;

public sealed class DespiderMovementController : JunctionTurningEnemyMovementController
{
    [SerializeField] private Vector2 attackIntervalSeconds = new(5f, 8f);
    [SerializeField, Min(0.01f)] private float turnStepSeconds = 0.15f;
    [SerializeField, Min(0f)] private float backswingSeconds = 0.5f;

    private static readonly Vector2[] TurnDirections = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private AnimatedSpriteRenderer[] webs;
    private AnimatedSpriteRenderer expand;
    private float nextAttackAt;
    private float nextPhaseAt;
    private int facingIndex;
    private int targetFacingIndex;
    private int shotIndex;
    private bool attacking;
    private bool waitingToFire;
    private bool recovering;

    protected override void Awake()
    {
        base.Awake();
        webs = new[] { FindVisual("WebUp"), FindVisual("WebRigth"), FindVisual("WebDown"), FindVisual("WebLeft") };
        expand = FindVisual("Expand");
        foreach (var web in webs) HideTemplate(web);
        HideTemplate(expand);
    }

    protected override void Start()
    {
        base.Start();
        ScheduleAttack();
    }

    protected override void DecideNextTile()
    {
        // The base movement calls this only on reaching a tile centre.
        if (Time.time >= nextAttackAt && webs != null && expand != null && TryFindTarget(out Vector2 delta))
        {
            shotIndex = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0f ? 1 : 3) : (delta.y > 0f ? 0 : 2);
            if (webs[shotIndex] != null)
            {
                targetFacingIndex = (shotIndex + 2) % 4;
                facingIndex = System.Array.IndexOf(TurnDirections, direction);
                if (facingIndex < 0) facingIndex = 0;
                attacking = true;
                waitingToFire = false;
                recovering = false;
                targetTile = rb.position;
                rb.linearVelocity = Vector2.zero;
                ShowFacing();
                nextPhaseAt = Time.time + turnStepSeconds;
                return;
            }
        }
        base.DecideNextTile();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;
        if (isInDamagedLoop || (TryGetComponent(out StunReceiver stun) && stun.IsStunned))
        {
            if (attacking)
            {
                attacking = false;
                ScheduleAttack();
                if (!isInDamagedLoop) UpdateSpriteDirection(direction);
            }
            base.FixedUpdate();
            return;
        }
        if (!attacking) { base.FixedUpdate(); return; }
        rb.linearVelocity = Vector2.zero;
        if (Time.time < nextPhaseAt) return;
        if (recovering)
        {
            recovering = false;
            attacking = false;
            ScheduleAttack();
            base.DecideNextTile();
            return;
        }
        if (waitingToFire)
        {
            DespiderWebProjectile.Launch(webs[shotIndex], expand, rb.position, TurnDirections[shotIndex], tileSize);
            waitingToFire = false;
            recovering = true;
            nextPhaseAt = Time.time + DespiderWebProjectile.WebLifetimeSeconds;
            return;
        }
        if (facingIndex != targetFacingIndex)
        {
            facingIndex = (facingIndex + 1) % 4;
            ShowFacing();
        }
        waitingToFire = facingIndex == targetFacingIndex;
        nextPhaseAt = Time.time + (waitingToFire ? backswingSeconds : turnStepSeconds);
    }

    private void ShowFacing()
    {
        direction = TurnDirections[facingIndex];
        UpdateSpriteDirection(direction);
        if (activeSprite != null) { activeSprite.idle = true; activeSprite.RefreshFrame(); }
    }

    private bool TryFindTarget(out Vector2 delta)
    {
        delta = Vector2.zero;
        float bestDistance = float.PositiveInfinity;
        foreach (var player in FindObjectsByType<MovementController>())
        {
            if (!player.isActiveAndEnabled || player.isDead || player.IsEndingStage || player.gameObject.layer != LayerMask.NameToLayer("Player")) continue;
            Vector2 candidate = (Vector2)player.transform.position - rb.position;
            if (candidate.sqrMagnitude >= bestDistance) continue;
            bestDistance = candidate.sqrMagnitude;
            delta = candidate;
        }
        return !float.IsPositiveInfinity(bestDistance);
    }

    private void ScheduleAttack() => nextAttackAt = Time.time + Random.Range(Mathf.Max(0.1f, attackIntervalSeconds.x), Mathf.Max(attackIntervalSeconds.x, attackIntervalSeconds.y));
    private AnimatedSpriteRenderer FindVisual(string childName) => transform.Find(childName)?.GetComponent<AnimatedSpriteRenderer>();
    private static void HideTemplate(AnimatedSpriteRenderer visual)
    {
        if (visual == null) return;
        visual.enabled = false;
        if (visual.TryGetComponent(out SpriteRenderer renderer)) renderer.enabled = false;
    }
}
