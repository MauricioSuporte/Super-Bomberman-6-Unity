using System.Collections.Generic;
using UnityEngine;

public sealed class WheelRobotMovementController : JunctionTurningEnemyMovementController
{
    private const float TurnSignalDuration = 1f;
    private const float SignalFrameDuration = 0.25f;
    private const float CoreDestructionDuration = 0.5f;

    [Header("Wheel Robot turn signal")]
    [SerializeField] private SpriteRenderer signalRenderer;
    [SerializeField] private Sprite upToLeftSignal;
    [SerializeField] private Sprite upToRightSignal;
    [SerializeField] private Sprite downToLeftSignal;
    [SerializeField] private Sprite downToRightSignal;
    [SerializeField] private Sprite leftToDownSignal;
    [SerializeField] private SpriteRenderer squishedPose;
    [SerializeField] private AnimatedSpriteRenderer coreDestruction;

    private Vector2 pendingDirection;
    private float signalElapsed;
    private bool signallingTurn;
    private Sprite activeSignal;
    private bool flipSignal;
    private readonly List<(GameObject visual, bool wasActive)> barrelHiddenMovementVisuals = new();
    private StunReceiver barrelStun;

    protected override void FixedUpdate()
    {
        if (!signallingTurn)
        {
            base.FixedUpdate();
            return;
        }

        if (isDead || isInDamagedLoop || (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = Vector2.zero;
        signalElapsed += Time.fixedDeltaTime;
        if (signalElapsed >= TurnSignalDuration)
        {
            EndTurnSignal();
            return;
        }

        UpdateSignalVisual();
    }

    protected override void DecideNextTile()
    {
        isStuck = false;
        List<Vector2> freeDirections = new(4);
        foreach (Vector2 candidate in Dirs)
        {
            if (!IsTileBlocked(rb.position + candidate * tileSize))
                freeDirections.Add(candidate);
        }

        if (freeDirections.Count == 0)
        {
            targetTile = rb.position;
            return;
        }

        Vector2 chosen = ChooseNextDirection(freeDirections);
        if (chosen == direction)
        {
            targetTile = rb.position + direction * tileSize;
            return;
        }

        if (chosen == -direction)
        {
            direction = chosen;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
            return;
        }

        BeginTurnSignal(chosen);
    }

    private Vector2 ChooseNextDirection(List<Vector2> freeDirections)
    {
        if (freeDirections.Count >= minAvailablePathsToTurn)
            return freeDirections[Random.Range(0, freeDirections.Count)];

        if (freeDirections.Contains(direction))
            return direction;

        return freeDirections[Random.Range(0, freeDirections.Count)];
    }

    private void BeginTurnSignal(Vector2 nextDirection)
    {
        signallingTurn = true;
        pendingDirection = nextDirection;
        signalElapsed = 0f;
        activeSignal = GetTurnSignal(direction, nextDirection, out flipSignal);
        targetTile = rb.position;
        UpdateSpriteDirection(direction);
        activeSprite.idle = true;
        UpdateSignalVisual();
    }

    private Sprite GetTurnSignal(Vector2 from, Vector2 to, out bool flip)
    {
        flip = false;
        if (from == Vector2.up)
            return to == Vector2.left ? upToLeftSignal : to == Vector2.right ? upToRightSignal : null;
        if (from == Vector2.down)
            return to == Vector2.left ? downToLeftSignal : to == Vector2.right ? downToRightSignal : null;
        if (from == Vector2.left && to == Vector2.down)
            return leftToDownSignal;
        if (from == Vector2.right && to == Vector2.down)
        {
            flip = true;
            return leftToDownSignal;
        }

        return null;
    }

    private void UpdateSignalVisual()
    {
        bool showArrow = activeSignal != null && Mathf.FloorToInt(signalElapsed / SignalFrameDuration) % 2 == 0;
        if (signalRenderer != null)
        {
            signalRenderer.sprite = activeSignal;
            signalRenderer.flipX = flipSignal;
            signalRenderer.enabled = showArrow;
        }

        if (activeSprite != null && activeSprite.TryGetComponent(out SpriteRenderer movementRenderer))
        {
            activeSprite.idle = true;
            movementRenderer.enabled = !showArrow;
        }
    }

    private void EndTurnSignal()
    {
        signallingTurn = false;
        if (signalRenderer != null)
            signalRenderer.enabled = false;
        if (activeSprite != null && activeSprite.TryGetComponent(out SpriteRenderer movementRenderer))
            movementRenderer.enabled = true;

        direction = pendingDirection;
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    public bool TryBarrelCrushStun(float seconds)
    {
        if (isDead || !TryGetComponent(out StunReceiver stun) || !stun.TryCrushStun(seconds, squishedPose))
            return false;

        barrelStun = stun;
        signallingTurn = false;
        if (signalRenderer != null)
            signalRenderer.enabled = false;
        HideMovementVisualForBarrel(spriteDown);
        HideMovementVisualForBarrel(spriteLeft);
        HideMovementVisualForBarrel(spriteUp);
        return true;
    }

    private void HideMovementVisualForBarrel(AnimatedSpriteRenderer animation)
    {
        if (animation == null || animation.gameObject == gameObject)
            return;

        GameObject visual = animation.gameObject;
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual == visual)
                return;

        barrelHiddenMovementVisuals.Add((visual, visual.activeSelf));
        visual.SetActive(false);
    }

    private void LateUpdate()
    {
        if (barrelHiddenMovementVisuals.Count == 0)
            return;

        if (isDead || barrelStun == null || !barrelStun.IsStunned)
            RestoreMovementAfterCrush();
    }

    private void RestoreMovementAfterCrush()
    {
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual != null)
                entry.visual.SetActive(entry.wasActive);
        barrelHiddenMovementVisuals.Clear();
        barrelStun = null;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!isDead && barrelStun != null && barrelStun.IsStunned)
            return;

        base.UpdateSpriteDirection(dir);
    }

    protected override void Die()
    {
        if (isDead)
            return;

        signallingTurn = false;
        if (signalRenderer != null)
            signalRenderer.enabled = false;
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
        RestoreMovementAfterCrush();
    }
}
