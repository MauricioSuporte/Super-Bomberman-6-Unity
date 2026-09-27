using UnityEngine;

/// <summary>
/// A junction-turning enemy that evades an explosion with a vertical jump and
/// can be damaged only during its landing pause.
/// </summary>
public class PenguinMovementController : JunctionTurningEnemyMovementController
{
    private enum PenguinState
    {
        Walking,
        Jumping,
        Resting
    }

    [Header("Jump Visuals")]
    [SerializeField] protected AnimatedSpriteRenderer jumpUp;
    [SerializeField] protected AnimatedSpriteRenderer jumpDown;
    [SerializeField] protected AnimatedSpriteRenderer jumpLeft;
    [SerializeField] protected AnimatedSpriteRenderer jumpRight;

    [Header("Jump Timing")]
    [SerializeField, Min(0.01f)] protected float jumpDurationSeconds = 2f;
    [SerializeField, Min(0f)] protected float jumpHeightTiles = 3f;
    [SerializeField, Min(1)] protected int pixelsPerUnit = 16;
    [SerializeField, Min(0.01f)] protected float landingPauseSeconds = 2f;

    [Header("Jump Shadow")]
    [SerializeField] private SpriteRenderer jumpShadow;

    private CharacterHealth penguinHealth;
    private PenguinState state;
    private float stateElapsed;

    protected override void Awake()
    {
        base.Awake();

        penguinHealth = GetComponent<CharacterHealth>();
        if (jumpShadow == null)
        {
            Transform shadowTransform = transform.Find("Shadow");
            if (shadowTransform != null)
                jumpShadow = shadowTransform.GetComponent<SpriteRenderer>();
        }

        SetJumpShadowEnabled(false);
        SetJumpVisualsEnabled(false);
    }

    protected override void Start()
    {
        base.Start();

        // CharacterHealth initializes its renderer cache in Awake. Start runs
        // after every Awake on the GameObject has completed.
        penguinHealth?.SetExternalInvulnerability(true);
    }

    protected override void FixedUpdate()
    {
        if (state == PenguinState.Walking)
        {
            base.FixedUpdate();
            return;
        }

        if (isDead)
            return;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        stateElapsed += Time.fixedDeltaTime;

        if (state == PenguinState.Jumping)
        {
            ApplyJumpArc(stateElapsed / Mathf.Max(0.01f, jumpDurationSeconds));
            if (stateElapsed >= jumpDurationSeconds)
                BeginLandingPause();
            return;
        }

        if (stateElapsed >= landingPauseSeconds)
            ResumeWalking();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || other == null)
            return;

        if (other.gameObject.layer != LayerMask.NameToLayer("Explosion"))
        {
            base.OnTriggerEnter2D(other);
            return;
        }

        if (state == PenguinState.Resting)
            penguinHealth?.TakeDamage(1, fromExplosion: true);
        else if (state == PenguinState.Walking)
            TryEvadeExplosion();
    }

    /// <summary>
    /// Starts this enemy's normal explosion evasion when it is able to do so.
    /// External hazards can use this to react before they spawn a shared
    /// explosion hitbox.
    /// </summary>
    public bool TryEvadeExplosion()
    {
        if (isDead || state != PenguinState.Walking)
            return false;

        BeginJump();
        return true;
    }

    protected override void Die()
    {
        penguinHealth?.SetExternalInvulnerability(false);
        TryTakeExplosionDamageAtLanding();
        SetJumpShadowEnabled(false);
        ClearJumpArc();
        base.Die();
    }

    protected override void OnDestroy()
    {
        SetJumpShadowEnabled(false);
        base.OnDestroy();
    }

    private void BeginJump()
    {
        state = PenguinState.Jumping;
        stateElapsed = 0f;
        targetTile = rb != null ? rb.position : (Vector2)transform.position;
        penguinHealth?.SetExternalInvulnerability(true);

        ShowJumpVisual(direction);
        SetJumpShadowEnabled(true);
    }

    protected virtual void BeginLandingPause()
    {
        // Resume only from a tile center, just like Eskimo's recovery. This
        // keeps the next target aligned with the grid and prevents a visible
        // snap when normal junction movement takes control again.
        SnapToGrid();
        targetTile = rb != null ? rb.position : (Vector2)transform.position;

        state = PenguinState.Resting;
        stateElapsed = 0f;
        ClearJumpArc();
        SetJumpVisualsEnabled(false);
        SetJumpShadowEnabled(false);

        UpdateSpriteDirection(direction);
        if (activeSprite != null)
        {
            activeSprite.loop = true;
            activeSprite.idle = false;
        }

        penguinHealth?.SetExternalInvulnerability(false);
    }

    protected virtual void ResumeWalking()
    {
        state = PenguinState.Walking;
        stateElapsed = 0f;
        penguinHealth?.SetExternalInvulnerability(true);
        targetTile = rb != null ? rb.position : (Vector2)transform.position;
        base.DecideNextTile();
    }

    private void TryTakeExplosionDamageAtLanding()
    {
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        if (explosionLayer < 0)
            return;

        Collider2D explosion = Physics2D.OverlapCircle(
            transform.position,
            tileSize * 0.3f,
            1 << explosionLayer);
        if (explosion != null)
            penguinHealth?.TakeDamage(1, fromExplosion: true);
    }

    protected virtual void ShowJumpVisual(Vector2 jumpDirection)
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        if (spriteRight != null) spriteRight.enabled = false;

        AnimatedSpriteRenderer selected = jumpDirection == Vector2.up ? jumpUp :
            jumpDirection == Vector2.down ? jumpDown :
            jumpDirection == Vector2.right && jumpRight != null ? jumpRight : jumpLeft;

        SetJumpVisualsEnabled(false);
        if (selected == null)
            return;

        selected.enabled = true;
        selected.idle = false;
        selected.loop = true;
        activeSprite = selected;

        if (selected.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = jumpDirection == Vector2.right && selected == jumpLeft;
    }

    protected virtual void SetJumpVisualsEnabled(bool enabled)
    {
        if (jumpUp != null) jumpUp.enabled = enabled;
        if (jumpDown != null) jumpDown.enabled = enabled;
        if (jumpLeft != null) jumpLeft.enabled = enabled;
        if (jumpRight != null) jumpRight.enabled = enabled;
    }

    protected virtual void ApplyJumpArc(float progress)
    {
        if (activeSprite == null)
            return;

        float height = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * jumpHeightTiles * tileSize;
        float pixelPerfectHeight = Mathf.Round(height * Mathf.Max(1, pixelsPerUnit)) / Mathf.Max(1, pixelsPerUnit);
        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * pixelPerfectHeight);
    }

    protected virtual void ClearJumpArc()
    {
        if (jumpUp != null) jumpUp.ClearExternalBase();
        if (jumpDown != null) jumpDown.ClearExternalBase();
        if (jumpLeft != null) jumpLeft.ClearExternalBase();
        if (jumpRight != null) jumpRight.ClearExternalBase();
    }

    private void SetJumpShadowEnabled(bool enabled)
    {
        if (jumpShadow != null)
            jumpShadow.enabled = enabled;
    }
}
