using UnityEngine;

public sealed class BakebakeMovementController : FlyMovimentController
{
    private const float CoreDestructionDuration = 0.5f;

    [Header("Bakebake phasing")]
    [SerializeField] private Sprite[] intangibleFrames;
    [SerializeField] private Vector2 tangibleDurationRange = new(3f, 6f);
    [SerializeField, Min(0.1f)] private float intangibleDuration = 2f;
    [SerializeField] private SpriteRenderer squishedPose;
    [SerializeField] private AnimatedSpriteRenderer coreDestruction;

    private Sprite[] tangibleFrames;
    private CharacterHealth phaseHealth;
    private Collider2D phaseCollider;
    private StunReceiver phaseStun;
    private float phaseRemaining;
    private bool intangible;
    private bool movementHiddenForCrush;
    private bool colliderEnabledBeforePhase;

    protected override void Awake()
    {
        base.Awake();
        phaseHealth = GetComponent<CharacterHealth>();
        phaseCollider = GetComponent<Collider2D>();
        phaseStun = GetComponent<StunReceiver>();
        tangibleFrames = spriteDown.animationSprite;
        phaseRemaining = NextTangibleDuration();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (phaseStun.IsStunned || isInDamagedLoop)
        {
            base.FixedUpdate();
            return;
        }

        RestoreMovementAfterCrush();
        phaseRemaining -= Time.fixedDeltaTime;
        if (phaseRemaining <= 0f)
        {
            SetIntangible(!intangible);
            phaseRemaining = intangible ? intangibleDuration : NextTangibleDuration();
        }
        base.FixedUpdate();
    }

    private float NextTangibleDuration()
    {
        float minimum = Mathf.Max(0.1f, tangibleDurationRange.x);
        return Random.Range(minimum, Mathf.Max(minimum, tangibleDurationRange.y));
    }

    private void SetIntangible(bool value)
    {
        if (value == intangible)
            return;

        intangible = value;
        phaseHealth.SetExternalInvulnerability(value);
        if (value)
        {
            colliderEnabledBeforePhase = phaseCollider.enabled;
            phaseCollider.enabled = false;
        }
        else
            phaseCollider.enabled = colliderEnabledBeforePhase;

        Sprite[] frames = value ? intangibleFrames : tangibleFrames;
        spriteDown.animationSprite = frames;
        spriteDown.idleSprite = frames[0];
        spriteDown.CurrentFrame = 0;
        spriteDown.GetComponent<SpriteRenderer>().sprite = frames[0];
    }

    public bool TryBarrelCrushStun(float seconds)
    {
        if (isDead || intangible || !phaseStun.TryCrushStun(seconds, squishedPose))
            return false;

        movementHiddenForCrush = true;
        spriteDown.gameObject.SetActive(false);
        return true;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (movementHiddenForCrush)
            return;
        base.UpdateSpriteDirection(dir);
    }

    private void RestoreMovementAfterCrush()
    {
        if (!movementHiddenForCrush)
            return;
        movementHiddenForCrush = false;
        spriteDown.gameObject.SetActive(true);
    }

    protected override void Die()
    {
        if (isDead)
            return;
        SetIntangible(false);
        RestoreMovementAfterCrush();
        base.Die();
        if (squishedPose != null)
            squishedPose.enabled = false;
    }

    protected override void OnDeathAnimationEnded()
    {
        if (coreDestruction == null || coreDestruction.animationSprite == null ||
            coreDestruction.animationSprite.Length == 0 || !coreDestruction.TryGetComponent(out SpriteRenderer renderer))
        {
            base.OnDeathAnimationEnded();
            return;
        }

        if (spriteDeath != null)
        {
            spriteDeath.enabled = false;
            if (spriteDeath.TryGetComponent(out SpriteRenderer deathRenderer))
                deathRenderer.enabled = false;
        }

        coreDestruction.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.flipX = false;
        renderer.flipY = false;
        coreDestruction.enabled = true;
        coreDestruction.idle = false;
        coreDestruction.loop = false;
        coreDestruction.useSequenceDuration = true;
        coreDestruction.sequenceDuration = CoreDestructionDuration;
        coreDestruction.CurrentFrame = 0;
        coreDestruction.RestartAnimation();
        Invoke(nameof(FinishCoreDestruction), CoreDestructionDuration);
    }

    private void FinishCoreDestruction()
    {
        if (coreDestruction == null)
        {
            base.OnDeathAnimationEnded();
            return;
        }

        coreDestruction.enabled = false;
        if (coreDestruction.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = false;
        base.OnDeathAnimationEnded();
    }

    private void OnDisable()
    {
        SetIntangible(false);
        RestoreMovementAfterCrush();
        phaseRemaining = NextTangibleDuration();
    }
}
