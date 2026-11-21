using UnityEngine;

/// <summary>
/// Todora patrols tile junctions and charges in a cardinal direction when it
/// detects an aligned player. Ice impulses are presentation-only: they never
/// participate in FixedUpdate, collisions or network simulation.
/// </summary>
public sealed class TodoraMovementController : JunctionTurningEnemyMovementController
{
    private const string DestructiblesTag = "Destructibles";
    private const string IndestructiblesTag = "Indestructibles";
    private const string CoreMechanismsName = "CoreMechanisms";

    [Header("Player Pursuit")]
    [SerializeField, Min(0.1f)] private float visionDistance = 10f;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField, Min(0.001f)] private float alignedToleranceTiles = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float scanBoxSizePercent = 0.6f;
    [SerializeField, Min(1f)] private float pursuitSpeedMultiplier = 3f;

    [Header("Walking Animation (1 - 2 - 3 - 2)")]
    [Tooltip("Duration in seconds for frames 1, 2 and the repeated frame 2.")]
    [SerializeField, Min(0.01f)] private float walkingFrameSeconds = 0.10f;
    [Tooltip("Duration in seconds for frame 3 (index 2 in the 1-2-3-2 sequence).")]
    [SerializeField, Min(0.01f)] private float walkingThirdFrameSeconds = 0.15f;

    [Header("Charge Sprites")]
    [SerializeField] private AnimatedSpriteRenderer chargeUp;
    [SerializeField] private AnimatedSpriteRenderer chargeDown;
    [SerializeField] private AnimatedSpriteRenderer chargeLeft;

    [Header("Ice Impulse (child templates)")]
    [Tooltip("IceUp child: Ice_1 ... Ice_4. Also used flipped vertically for Down.")]
    [SerializeField] private AnimatedSpriteRenderer iceUp;
    [Tooltip("IceLeft child: Ice_5 ... Ice_8. Also used flipped horizontally for Right.")]
    [SerializeField] private AnimatedSpriteRenderer iceLeft;
    [Tooltip("Distance from the Todora origin backwards to the tail tip, in tiles. " +
             "The impulse begins here rather than inside the body.")]
    [SerializeField, Min(0f)] private float iceTailDistanceTiles = 0.75f;

    [Header("SFX")]
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField, Range(0f, 1f)] private float chargeSfxVolume = 1f;

    private float patrolSpeed;
    private bool isPursuing;
    private AudioSource chargeAudioSource;

    // Walking frames are advanced only while rendering (never in predicted or
    // rollback physics ticks). The sprites are already ordered 1,2,3,2 in the prefab.
    private int walkingFrameIndex;
    private float walkingFrameElapsed;
    private bool walkingWasRendered;

    // Presentation only; not part of the predicted/rollback gameplay state.
    private bool chargeWasRendered;
    private Vector2 chargeStartPosition;
    private Vector2 chargeStartDirection;
    private SpriteRenderer iceUpInstance;
    private SpriteRenderer iceLeftInstance;
    private SpriteRenderer activeIceInstance;
    private Sprite[] activeIceFrames;
    private float activeIceFrameSeconds;
    private float activeIceElapsed;
    private bool activeIceUnscaledTime;
    private bool activeIceRespectPause;
    private Vector3 activeIceWorldPosition;

    protected override void Awake()
    {
        base.Awake();
        patrolSpeed = speed;
        chargeAudioSource = GetComponent<AudioSource>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");

        // The original IceUp/IceLeft children are templates. Their frames and
        // rendering settings are copied once to lightweight visual-only children.
        // No Instantiate/Destroy happens inside the predicted simulation loop.
        if (iceUp == null)
        {
            Transform t = transform.Find("IceUp");
            if (t != null) iceUp = t.GetComponent<AnimatedSpriteRenderer>();
        }

        if (iceLeft == null)
        {
            Transform t = transform.Find("IceLeft");
            if (t != null) iceLeft = t.GetComponent<AnimatedSpriteRenderer>();
        }

        iceUpInstance = CreateIceInstance(iceUp);
        iceLeftInstance = CreateIceInstance(iceLeft);

        // AnimatedSpriteRenderer has uniform frame timing; for this one enemy,
        // advance the configured walking frames manually so frame 3 lasts 150ms.
        SetManualWalkingAnimation(spriteUp);
        SetManualWalkingAnimation(spriteDown);
        SetManualWalkingAnimation(spriteLeft);
        DisableTodoraSprites();
    }

    protected override void FixedUpdate()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
            return;

        if (!isPursuing && TryGetPlayerDirection(out Vector2 playerDirection))
            StartCharge(playerDirection);

        if (isPursuing && HasBombAt(targetTile))
            EndCharge();
        else if (isPursuing && IsTileBlocked(targetTile))
            EndCharge();

        base.FixedUpdate();
    }

    // This runs only at render time, not for every predicted or replayed tick.
    // Consequently a rollback/resimulation cannot create duplicate ice objects
    // or replay charge audio once per simulated tick.
    private void LateUpdate()
    {
        bool chargingNow = isPursuing && !isDead;
        bool startedForPresentation = chargingNow && !chargeWasRendered;

        if (startedForPresentation)
        {
            BeginIceImpulse(chargeStartDirection, chargeStartPosition);
            GameAudioSettings.PlaySfx(chargeAudioSource, chargeSfx, chargeSfxVolume);
        }

        chargeWasRendered = chargingNow;

        AnimateWalking();

        if (!startedForPresentation)
            AnimateIceImpulse();
    }

    private void OnDisable()
    {
        StopIceImpulse();
        chargeWasRendered = false;
        walkingWasRendered = false;
        walkingFrameElapsed = 0f;
    }

    private static void SetManualWalkingAnimation(AnimatedSpriteRenderer sprite)
    {
        if (sprite != null)
            sprite.SetManualAnimationUpdate(true);
    }

    private void AnimateWalking()
    {
        AnimatedSpriteRenderer selected = activeSprite;
        bool isWalking = !isDead && !isInDamagedLoop && !isPursuing &&
                         selected != null && selected.enabled &&
                         (selected == spriteUp || selected == spriteDown || selected == spriteLeft);

        if (!isWalking || selected.animationSprite == null || selected.animationSprite.Length == 0)
        {
            walkingWasRendered = false;
            return;
        }

        int frameCount = selected.animationSprite.Length;
        if (!walkingWasRendered)
        {
            walkingFrameIndex = 0;
            walkingFrameElapsed = 0f;
            walkingWasRendered = true;
        }

        // Keep the same 1-2-3-2 phase on direction changes. Changes between
        // walking and charge always restart the walking cycle at frame 1.
        walkingFrameIndex %= frameCount;
        if (selected.CurrentFrame != walkingFrameIndex)
        {
            selected.CurrentFrame = walkingFrameIndex;
            selected.RefreshFrame();
        }

        if (selected.RespectGamePause && GamePauseController.IsPaused)
            return;

        walkingFrameElapsed += selected.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        int changedFrames = 0;
        // Frame index 2 is the third displayed sprite (150ms); all remaining
        // entries, including the repeated second frame, stay at 100ms.
        while (changedFrames < 120)
        {
            float duration = Mathf.Max(0.01f, walkingFrameIndex == 2
                ? walkingThirdFrameSeconds
                : walkingFrameSeconds);
            if (walkingFrameElapsed < duration)
                break;

            walkingFrameElapsed -= duration;
            walkingFrameIndex = (walkingFrameIndex + 1) % frameCount;
            changedFrames++;
        }

        if (changedFrames > 0)
        {
            selected.CurrentFrame = walkingFrameIndex;
            selected.RefreshFrame();
        }
    }

    protected override void DecideNextTile()
    {
        if (isPursuing)
        {
            Vector2 forwardTile = rb.position + direction * tileSize;
            if (IsTileBlocked(forwardTile))
            {
                EndCharge();
                base.DecideNextTile();
                return;
            }

            targetTile = forwardTile;
            return;
        }

        if (TryGetPlayerDirection(out Vector2 playerDirection))
        {
            StartCharge(playerDirection);
            return;
        }

        base.DecideNextTile();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isDead || isInDamagedLoop)
            return;

        AnimatedSpriteRenderer selected = isPursuing
            ? GetChargeSprite(dir)
            : GetWalkingSprite(dir);

        if (selected == null)
            return;

        ApplyDirectionalFlipAndPosition(selected, dir);

        if (activeSprite == selected && selected.enabled)
        {
            selected.idle = false;
            return;
        }

        int frame = activeSprite != null ? activeSprite.CurrentFrame : 0;
        DisableTodoraSprites();

        activeSprite = selected;
        SetSpriteEnabled(activeSprite, true);
        activeSprite.idle = false;
        activeSprite.loop = true;

        if (selected.animationSprite != null && selected.animationSprite.Length > 0)
        {
            selected.CurrentFrame = Mathf.Clamp(frame, 0, selected.animationSprite.Length - 1);
            selected.RefreshFrame();
        }
    }

    protected override void Die()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
            return;

        isPursuing = false;
        speed = patrolSpeed;
        StopIceImpulse();
        DisableTodoraSprites();
        base.Die();
    }

    private void StartCharge(Vector2 chargeDirection)
    {
        if (isPursuing)
            return;

        isPursuing = true;
        speed = patrolSpeed * pursuitSpeedMultiplier;
        direction = chargeDirection;
        targetTile = rb.position + direction * tileSize;
        chargeStartPosition = rb.position;
        chargeStartDirection = chargeDirection;
        UpdateSpriteDirection(direction);
        // VFX and SFX are triggered by LateUpdate, never during resimulation.
    }

    private void EndCharge()
    {
        if (!isPursuing)
            return;

        isPursuing = false;
        speed = patrolSpeed;
        UpdateSpriteDirection(direction);
    }

    private AnimatedSpriteRenderer GetWalkingSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return spriteUp;
        if (dir == Vector2.down) return spriteDown;
        if (dir == Vector2.left || dir == Vector2.right) return spriteLeft;
        return spriteDown;
    }

    private AnimatedSpriteRenderer GetChargeSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return chargeUp;
        if (dir == Vector2.down) return chargeDown;
        if (dir == Vector2.left || dir == Vector2.right) return chargeLeft;
        return chargeDown;
    }

    private void ApplyDirectionalFlipAndPosition(AnimatedSpriteRenderer selected, Vector2 dir)
    {
        if (selected.TryGetComponent<SpriteRenderer>(out SpriteRenderer spriteRenderer))
        {
            // Right always uses the Left frames, mirrored in-place.
            spriteRenderer.flipX = dir == Vector2.right;
            spriteRenderer.flipY = false;
        }

        // Suppress both the original local X bias and per-frame X offsets for
        // left/right animations, keeping them centered when mirrored.
        if (selected == spriteLeft || selected == chargeLeft)
            selected.SetRuntimeBaseLocalX(0f);
    }

    private SpriteRenderer CreateIceInstance(AnimatedSpriteRenderer source)
    {
        if (source == null)
            return null;

        SpriteRenderer templateRenderer = source.GetComponent<SpriteRenderer>();

        // Hide the author's templates; only their pooled child copies are shown.
        SetSpriteEnabled(source, false);

        if (templateRenderer == null || source.animationSprite == null || source.animationSprite.Length == 0)
            return null;

        GameObject visual = new GameObject(source.gameObject.name + "_Impulse");
        visual.transform.SetParent(transform, false);
        visual.layer = source.gameObject.layer;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = templateRenderer.sharedMaterial;
        renderer.color = templateRenderer.color;
        renderer.sortingLayerID = templateRenderer.sortingLayerID;
        renderer.sortingOrder = templateRenderer.sortingOrder;
        renderer.maskInteraction = templateRenderer.maskInteraction;
        renderer.sprite = source.animationSprite[0];
        renderer.enabled = false;
        return renderer;
    }

    private void BeginIceImpulse(Vector2 chargeDirection, Vector2 startPosition)
    {
        StopIceImpulse();

        bool isHorizontal = chargeDirection == Vector2.left || chargeDirection == Vector2.right;
        AnimatedSpriteRenderer template = isHorizontal ? iceLeft : iceUp;
        SpriteRenderer instance = isHorizontal ? iceLeftInstance : iceUpInstance;

        if (template == null || instance == null || template.animationSprite == null ||
            template.animationSprite.Length == 0)
            return;

        activeIceInstance = instance;
        activeIceFrames = template.animationSprite;
        activeIceElapsed = 0f;
        activeIceUnscaledTime = template.UseUnscaledTime;
        activeIceRespectPause = template.RespectGamePause;
        activeIceFrameSeconds = template.useSequenceDuration
            ? Mathf.Max(0.0001f, template.sequenceDuration / activeIceFrames.Length)
            : Mathf.Max(0.0001f, template.animationTime);

        // Leave a visual splash at the *start* of the dash, slightly behind
        // the direction of travel. This position is visual only.
        Vector2 icePosition = startPosition - chargeDirection * (tileSize * iceTailDistanceTiles);
        // Preserve the child template's vertical alignment, but never add an
        // X bias when mirroring the Left effect to the Right.
        Vector2 sourceBias = template.transform.localPosition;
        sourceBias.x = 0f;
        if (chargeDirection == Vector2.down)
            sourceBias.y = -sourceBias.y;
        icePosition += sourceBias;
        activeIceWorldPosition = new Vector3(icePosition.x, icePosition.y, transform.position.z);

        instance.flipX = chargeDirection == Vector2.right;
        instance.flipY = chargeDirection == Vector2.down;
        instance.sprite = activeIceFrames[0];
        instance.transform.position = activeIceWorldPosition;
        instance.enabled = true;
    }

    private void AnimateIceImpulse()
    {
        if (activeIceInstance == null || activeIceFrames == null || activeIceFrames.Length == 0)
            return;

        // Match the source AnimatedSpriteRenderer timing and pause settings.
        // No physics position or gameplay state changes.
        if (activeIceRespectPause && GamePauseController.IsPaused)
            return;

        activeIceElapsed += activeIceUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(activeIceElapsed / activeIceFrameSeconds);
        if (frameIndex >= activeIceFrames.Length)
        {
            StopIceImpulse();
            return;
        }

        activeIceInstance.sprite = activeIceFrames[frameIndex];

        // Keep the child anchored to the original splash world-position even
        // while its parent (Todora) moves away during the dash.
        activeIceInstance.transform.position = activeIceWorldPosition;
    }

    private void StopIceImpulse()
    {
        if (activeIceInstance != null)
            activeIceInstance.enabled = false;

        activeIceInstance = null;
        activeIceFrames = null;
        activeIceElapsed = 0f;
    }

    private bool TryGetPlayerDirection(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;

        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(visionDistance / tileSize));
        float boxSize = Mathf.Clamp(scanBoxSizePercent, 0.1f, 1f) * tileSize;
        float alignmentTolerance = Mathf.Max(0.001f, alignedToleranceTiles * tileSize);

        for (int directionIndex = 0; directionIndex < Dirs.Length; directionIndex++)
        {
            Vector2 scanDirection = Dirs[directionIndex];
            bool scansVertically = scanDirection == Vector2.up || scanDirection == Vector2.down;

            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + scanDirection * tileSize * step;

                // Obstacles end the scan before any player behind them can be detected.
                if (IsTileBlocked(tileCenter))
                    break;

                Collider2D[] playerHits = Physics2D.OverlapBoxAll(
                    tileCenter, Vector2.one * boxSize, 0f, playerLayerMask);

                for (int hitIndex = 0; hitIndex < playerHits.Length; hitIndex++)
                {
                    Collider2D playerCollider = playerHits[hitIndex];
                    if (playerCollider == null)
                        continue;

                    Vector2 playerPosition = playerCollider.attachedRigidbody != null
                        ? playerCollider.attachedRigidbody.position
                        : (Vector2)playerCollider.transform.position;

                    bool isAligned = scansVertically
                        ? Mathf.Abs(playerPosition.x - rb.position.x) <= alignmentTolerance
                        : Mathf.Abs(playerPosition.y - rb.position.y) <= alignmentTolerance;

                    if (!isAligned)
                        continue;

                    directionToPlayer = scanDirection;
                    return true;
                }
            }
        }

        return false;
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        if (!isPursuing)
            return base.IsTileBlocked(tileCenter);

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            tileCenter, Vector2.one * (tileSize * 0.8f), 0f, obstacleMask);

        int bombLayer = LayerMask.NameToLayer("Bomb");
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            Collider2D hit = hits[hitIndex];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            int hitLayer = hit.gameObject.layer;
            if (hitLayer == bombLayer || IsChargeBlocker(hit))
                return true;
        }

        return false;
    }

    private static bool IsChargeBlocker(Collider2D collider)
    {
        // Legacy stages can contain an Indestructibles tag not registered in
        // TagManager; keep the existing comparison behavior.
        string tag = collider.tag;
        return tag == DestructiblesTag ||
               tag == IndestructiblesTag ||
               collider.TryGetComponent<StageAssets.ExitBlocker>(out _) ||
               collider.TryGetComponent<StageAssets.World3RoomTwoTransition>(out _) ||
               collider.name.StartsWith(CoreMechanismsName, System.StringComparison.Ordinal);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
            return;

        if (isPursuing && other != null)
        {
            int otherLayer = other.gameObject.layer;
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            if (otherLayer == playerLayer || otherLayer == enemyLayer)
                return;

            if (otherLayer == LayerMask.NameToLayer("Bomb"))
                EndCharge();
        }

        base.OnTriggerEnter2D(other);
    }

    private void DisableTodoraSprites()
    {
        SetSpriteEnabled(spriteUp, false);
        SetSpriteEnabled(spriteDown, false);
        SetSpriteEnabled(spriteLeft, false);
        SetSpriteEnabled(chargeUp, false);
        SetSpriteEnabled(chargeDown, false);
        SetSpriteEnabled(chargeLeft, false);
    }

    private static void SetSpriteEnabled(AnimatedSpriteRenderer sprite, bool enabled)
    {
        if (sprite == null)
            return;

        sprite.enabled = enabled;

        // Make visibility consistent even before a child's OnDisable executes.
        if (sprite.TryGetComponent<SpriteRenderer>(out SpriteRenderer renderer))
            renderer.enabled = enabled;
    }
}
