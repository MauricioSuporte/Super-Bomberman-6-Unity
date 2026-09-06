using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Junction-turning robot that advances exactly one tile per jump.
/// The visual lift is applied to the active directional child so its physics
/// body and its shadow always remain on the logical ground tile.
/// </summary>
public sealed class HopRobotMovementController : JunctionTurningEnemyMovementController
{
    private const float CoreDestructionDuration = 0.5f;

    [Header("Hop timing")]
    [SerializeField, Min(0f)] private float idleSeconds = 1f;
    [SerializeField, Min(0.01f)] private float preparationSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float jumpSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float landingSeconds = 0.2f;
    [SerializeField, Min(0f)] private float jumpHeight = 1f;

    [Header("Directional poses: base, preparation, air")]
    [SerializeField] private Sprite[] upPoses;
    [SerializeField] private Sprite[] downPoses;
    [SerializeField] private Sprite[] leftPoses;

    [Header("Hop shadow")]
    [SerializeField] private Sprite hopShadowSprite;
    [SerializeField] private Color shadowColor = Color.white;
    [SerializeField] private Vector2 shadowScale = Vector2.one;
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;

    [Header("Barrel and destruction")]
    [SerializeField] private SpriteRenderer squishedPose;
    [SerializeField] private AnimatedSpriteRenderer coreDestruction;

    private enum HopPhase { Idle, Preparing, Airborne, Landing }
    private HopPhase phase;
    private float phaseElapsed;
    private Vector2 jumpStart;
    private GameObject shadow;
    private StunReceiver barrelStun;
    private readonly List<(GameObject visual, bool wasActive)> barrelHiddenMovementVisuals = new();

    protected override void Start()
    {
        SnapToGrid();
        ChooseInitialDirection();
        UpdateSpriteDirection(direction);
        CreateShadow();
        BeginPhase(HopPhase.Idle);
    }

    protected override void FixedUpdate()
    {
        if (isDead || isInDamagedLoop || (TryGetComponent(out StunReceiver stun) && stun.IsStunned))
        {
            StopMotion();
            return;
        }

        phaseElapsed += Time.fixedDeltaTime;
        switch (phase)
        {
            case HopPhase.Idle:
                if (phaseElapsed >= idleSeconds)
                {
                    DecideNextTile();
                    if (targetTile == rb.position)
                        BeginPhase(HopPhase.Idle);
                    else
                        BeginPhase(HopPhase.Preparing);
                }
                break;

            case HopPhase.Preparing:
                if (phaseElapsed >= preparationSeconds)
                {
                    jumpStart = rb.position;
                    BeginPhase(HopPhase.Airborne);
                }
                break;

            case HopPhase.Airborne:
                float progress = Mathf.Clamp01(phaseElapsed / jumpSeconds);
                rb.MovePosition(Vector2.Lerp(jumpStart, targetTile, progress));
                ApplyJumpOffset(Mathf.Sin(progress * Mathf.PI) * jumpHeight);
                UpdateShadow();
                if (progress >= 1f)
                {
                    rb.position = targetTile;
                    SnapToGrid();
                    BeginPhase(HopPhase.Landing);
                }
                break;

            case HopPhase.Landing:
                if (phaseElapsed >= landingSeconds)
                    BeginPhase(HopPhase.Idle);
                break;
        }
    }

    private void BeginPhase(HopPhase next)
    {
        phase = next;
        phaseElapsed = 0f;
        switch (next)
        {
            case HopPhase.Idle:
                ClearJumpOffset();
                SetShadowVisible(false);
                SetPose(0);
                break;
            case HopPhase.Preparing:
            case HopPhase.Landing:
                ClearJumpOffset();
                SetShadowVisible(false);
                SetPose(1);
                break;
            case HopPhase.Airborne:
                SetShadowVisible(true);
                SetPose(2);
                UpdateShadow();
                break;
        }
    }

    private void SetPose(int poseIndex)
    {
        Sprite[] poses = direction == Vector2.up ? upPoses :
                        direction == Vector2.down ? downPoses : leftPoses;
        if (activeSprite == null || poses == null || poseIndex >= poses.Length || poses[poseIndex] == null)
            return;

        activeSprite.enabled = true;
        activeSprite.idleSprite = poses[poseIndex];
        activeSprite.animationSprite = new[] { poses[poseIndex] };
        activeSprite.idle = true;
        activeSprite.CurrentFrame = 0;
        activeSprite.RefreshFrame();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!isDead && barrelStun != null && barrelStun.IsStunned)
            return;

        base.UpdateSpriteDirection(dir);
        SetPose(phase == HopPhase.Airborne ? 2 : phase == HopPhase.Idle ? 0 : 1);
    }

    private void ApplyJumpOffset(float height)
    {
        if (activeSprite != null)
            activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * height);
    }

    private void ClearJumpOffset()
    {
        foreach (AnimatedSpriteRenderer animated in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            if (animated != null) animated.ClearExternalBase();
    }

    private void CreateShadow()
    {
        if (shadow != null || hopShadowSprite == null)
            return;

        shadow = new GameObject("HopShadow");
        shadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);
        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = hopShadowSprite;
        renderer.color = shadowColor;
        if (spriteDown != null && spriteDown.TryGetComponent(out SpriteRenderer body))
        {
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder - 1;
        }
        SetShadowVisible(false);
    }

    private void UpdateShadow()
    {
        if (shadow == null) return;
        Vector3 position = transform.position + (Vector3)shadowOffset;
        shadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadow != null) shadow.SetActive(visible);
    }

    private void StopMotion()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        ClearJumpOffset();
        SetShadowVisible(false);
    }

    public bool TryBarrelCrushStun(float seconds)
    {
        if (isDead || !TryGetComponent(out StunReceiver stun) || !stun.TryCrushStun(seconds, squishedPose))
            return false;

        barrelStun = stun;
        StopMotion();
        HideMovementVisual(spriteUp);
        HideMovementVisual(spriteDown);
        HideMovementVisual(spriteLeft);
        return true;
    }

    private void HideMovementVisual(AnimatedSpriteRenderer animation)
    {
        if (animation == null || animation.gameObject == gameObject) return;
        GameObject visual = animation.gameObject;
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual == visual) return;
        barrelHiddenMovementVisuals.Add((visual, visual.activeSelf));
        visual.SetActive(false);
    }

    private void LateUpdate()
    {
        if (barrelHiddenMovementVisuals.Count > 0 && (isDead || barrelStun == null || !barrelStun.IsStunned))
            RestoreMovementVisuals();
    }

    private void RestoreMovementVisuals()
    {
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual != null) entry.visual.SetActive(entry.wasActive);
        barrelHiddenMovementVisuals.Clear();
        barrelStun = null;
    }

    protected override void Die()
    {
        if (isDead) return;
        RestoreMovementVisuals();
        StopMotion();
        if (shadow != null) Destroy(shadow);
        shadow = null;
        base.Die();
        if (squishedPose != null) squishedPose.enabled = false;
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
            if (spriteDeath.TryGetComponent(out SpriteRenderer deathRenderer)) deathRenderer.enabled = false;
        }
        coreDestruction.gameObject.SetActive(true);
        renderer.enabled = true;
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
        if (coreDestruction == null) { base.OnDeathAnimationEnded(); return; }
        coreDestruction.enabled = false;
        if (coreDestruction.TryGetComponent(out SpriteRenderer renderer)) renderer.enabled = false;
        base.OnDeathAnimationEnded();
    }

    private void OnDisable() => RestoreMovementVisuals();

    protected override void OnDestroy()
    {
        if (shadow != null) Destroy(shadow);
        base.OnDestroy();
    }
}
