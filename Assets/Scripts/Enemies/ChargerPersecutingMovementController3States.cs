using System.Collections.Generic;
using UnityEngine;

public sealed class ChargerPersecutingMovementController3States : ChargerPersecutingMovementController
{
    private enum VisualState
    {
        Walking = 0,
        Preparing = 1,
        Charging = 2
    }

    [Header("Walk Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer walkUp;
    [SerializeField] private AnimatedSpriteRenderer walkDown;
    [SerializeField] private AnimatedSpriteRenderer walkLeft;
    [SerializeField] private AnimatedSpriteRenderer walkRight;

    [Header("Prepare Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer prepareUp;
    [SerializeField] private AnimatedSpriteRenderer prepareDown;
    [SerializeField] private AnimatedSpriteRenderer prepareLeft;
    [SerializeField] private AnimatedSpriteRenderer prepareRight;

    [Header("Charge Sprites (4 dirs)")]
    [SerializeField] private AnimatedSpriteRenderer chargeUp;
    [SerializeField] private AnimatedSpriteRenderer chargeDown;
    [SerializeField] private AnimatedSpriteRenderer chargeLeft;
    [SerializeField] private AnimatedSpriteRenderer chargeRight;

    [Header("Charge Stop (after collision)")]
    [SerializeField, Min(0f)] private float chargeStopPauseSeconds = 0.5f;

    private VisualState visualState = VisualState.Walking;

    private bool preparing;
    private bool charging;
    private bool chargeStopping;

    private float timer;
    private float chargeStopTimer;

    private Vector2 chargeDir;
    private float baseSpd;

    private StunReceiver stun;
    private bool wasStun;

    private CharacterHealth healthRef;

    protected override void Awake()
    {
        base.Awake();
        DisableAllStateSprites();
    }

    protected override void OnDestroy()
    {
        UnhookHealth();
        base.OnDestroy();
    }

    protected override void Start()
    {
        baseSpd = speed;
        stun = GetComponent<StunReceiver>();
        wasStun = false;

        HookHealth();

        SnapToGrid();
        ChooseInitialDirection();
        SetVisualState(VisualState.Walking);
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        bool stunnedNow = stun != null && stun.IsStunned;

        if (stunnedNow)
        {
            wasStun = true;

            preparing = false;
            charging = false;
            chargeStopping = false;

            speed = baseSpd;

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (wasStun)
        {
            wasStun = false;

            speed = baseSpd;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                SnapToGrid();
            }

            DecideNextTile();
            return;
        }

        if (isInDamagedLoop)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        if (preparing)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            timer -= Time.fixedDeltaTime;

            if (timer <= 0f)
            {
                preparing = false;
                charging = true;
                chargeStopping = false;

                direction = chargeDir;
                SetVisualState(VisualState.Charging);
                UpdateSpriteDirection(direction);

                SnapToGrid();
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        if (chargeStopping)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            chargeStopTimer -= Time.fixedDeltaTime;

            if (chargeStopTimer <= 0f)
            {
                chargeStopping = false;
                charging = false;
                speed = baseSpd;

                SetVisualState(VisualState.Walking);
                UpdateSpriteDirection(direction);
                DecideNextTile();
            }

            return;
        }

        if (charging)
        {
            speed = baseSpd * chargeSpeedMultiplier;

            if (IsTileBlocked(targetTile))
            {
                SnapToGrid();
                StartChargeStopPause();
                return;
            }

            rb.MovePosition(Vector2.MoveTowards(rb.position, targetTile, speed * Time.fixedDeltaTime));

            if (ReachedTile())
            {
                SnapToGrid();
                targetTile = rb.position + direction * tileSize;
            }

            return;
        }

        speed = baseSpd;

        if (isStuck)
        {
            HandleStuckInternal();
            return;
        }

        if (HasBombAt(targetTile))
            HandleBombAhead();

        MoveTowardsTile();

        if (ReachedTile())
        {
            SnapToGrid();
            DecideNextTile();
        }
    }

    protected override void DecideNextTile()
    {
        if (preparing || charging || chargeStopping)
            return;

        if (stun != null && stun.IsStunned)
            return;

        if (isInDamagedLoop)
            return;

        if (TryGetPlayerDirectionChase(out Vector2 playerDir))
        {
            Vector2 forwardTile = rb.position + playerDir * tileSize;

            if (!IsTileBlocked(forwardTile))
            {
                direction = playerDir;

                if (MetalHornTryGetPlayerDirection(out Vector2 hornDir))
                {
                    preparing = true;
                    charging = false;
                    chargeStopping = false;

                    timer = chargePauseDuration;
                    chargeDir = hornDir;

                    direction = chargeDir;
                    SetVisualState(VisualState.Preparing);
                    UpdateSpriteDirection(direction);

                    if (rb != null)
                        rb.linearVelocity = Vector2.zero;

                    SnapToGrid();
                    return;
                }

                SetVisualState(VisualState.Walking);
                UpdateSpriteDirection(direction);
                targetTile = forwardTile;
                return;
            }
        }

        if (MetalHornTryGetPlayerDirection(out Vector2 hornOnlyDir))
        {
            preparing = true;
            charging = false;
            chargeStopping = false;

            timer = chargePauseDuration;
            chargeDir = hornOnlyDir;

            direction = chargeDir;
            SetVisualState(VisualState.Preparing);
            UpdateSpriteDirection(direction);

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            SnapToGrid();
            return;
        }

        SetVisualState(VisualState.Walking);

        isStuck = false;

        Vector2 forward = rb.position + direction * tileSize;

        if (!IsTileBlocked(forward))
        {
            targetTile = forward;
            UpdateSpriteDirection(direction);
            return;
        }

        var freeDirs = new List<Vector2>();

        foreach (var dir in Dirs)
        {
            if (dir == direction)
                continue;

            Vector2 check = rb.position + dir * tileSize;
            if (!IsTileBlocked(check))
                freeDirs.Add(dir);
        }

        if (freeDirs.Count == 0)
        {
            if (TryPickAnyFreeDirection(out var anyDir))
            {
                direction = anyDir;
                UpdateSpriteDirection(direction);
                targetTile = rb.position + direction * tileSize;
                return;
            }

            targetTile = rb.position;

            if (activeSprite != null)
            {
                activeSprite.enabled = true;
                activeSprite.idle = false;
            }

            isStuck = true;
            stuckTimer = recheckStuckEverySeconds;
            return;
        }

        direction = freeDirs[Random.Range(0, freeDirs.Count)];
        UpdateSpriteDirection(direction);
        targetTile = rb.position + direction * tileSize;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (isInDamagedLoop || isDead)
            return;

        AnimatedSpriteRenderer chosen = PickSprite(visualState, dir);

        if (chosen == null)
            chosen = PickSprite(VisualState.Walking, dir);

        if (chosen == null)
            chosen = walkDown != null ? walkDown : activeSprite;

        if (chosen == null)
            return;

        if (activeSprite == chosen && activeSprite.enabled)
        {
            activeSprite.idle = false;
            ForceFlipOff(activeSprite);
            return;
        }

        DisableAllStateSprites();
        activeSprite = chosen;
        activeSprite.enabled = true;
        activeSprite.idle = false;
        ForceFlipOff(activeSprite);
    }

    protected override void Die()
    {
        preparing = false;
        charging = false;
        chargeStopping = false;

        speed = baseSpd;

        DisableAllStateSprites();
        base.Die();
    }

    private void StartChargeStopPause()
    {
        charging = false;
        chargeStopping = true;

        speed = baseSpd;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        chargeStopTimer = chargeStopPauseSeconds;

        SetVisualState(VisualState.Charging);
        UpdateSpriteDirection(direction);
    }

    private void SetVisualState(VisualState state)
    {
        var previousState = visualState;
        visualState = state;

        if (previousState == VisualState.Walking && state == VisualState.Preparing)
        {
            AnimatedSpriteRenderer prepareSprite = PickSprite(VisualState.Preparing, direction);

            if (prepareSprite != null)
            {
                prepareSprite.CurrentFrame = 0;
                prepareSprite.idle = false;
                prepareSprite.RefreshFrame();
            }
        }
    }

    private AnimatedSpriteRenderer PickSprite(VisualState state, Vector2 dir)
    {
        if (dir == Vector2.up)
            return state == VisualState.Preparing ? prepareUp : state == VisualState.Charging ? chargeUp : walkUp;

        if (dir == Vector2.down)
            return state == VisualState.Preparing ? prepareDown : state == VisualState.Charging ? chargeDown : walkDown;

        if (dir == Vector2.left)
            return state == VisualState.Preparing ? prepareLeft : state == VisualState.Charging ? chargeLeft : walkLeft;

        if (dir == Vector2.right)
            return state == VisualState.Preparing ? prepareRight : state == VisualState.Charging ? chargeRight : walkRight;

        return null;
    }

    private void DisableAllStateSprites()
    {
        if (walkUp != null) walkUp.enabled = false;
        if (walkDown != null) walkDown.enabled = false;
        if (walkLeft != null) walkLeft.enabled = false;
        if (walkRight != null) walkRight.enabled = false;

        if (prepareUp != null) prepareUp.enabled = false;
        if (prepareDown != null) prepareDown.enabled = false;
        if (prepareLeft != null) prepareLeft.enabled = false;
        if (prepareRight != null) prepareRight.enabled = false;

        if (chargeUp != null) chargeUp.enabled = false;
        if (chargeDown != null) chargeDown.enabled = false;
        if (chargeLeft != null) chargeLeft.enabled = false;
        if (chargeRight != null) chargeRight.enabled = false;
    }

    private void ForceFlipOff(AnimatedSpriteRenderer r)
    {
        if (r == null)
            return;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.flipX = false;
    }

    private bool MetalHornTryGetPlayerDirection(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int mask = obstacleMask | playerLayerMask;

        foreach (var dir in dirs)
        {
            Vector2 origin = rb.position + dir * (tileSize * 0.5f);

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, visionDistance, mask);

            if (hit.collider == null)
                continue;

            if (((1 << hit.collider.gameObject.layer) & playerLayerMask) != 0)
            {
                dirToPlayer = dir;
                return true;
            }
        }

        return false;
    }

    private bool TryGetPlayerDirectionChase(out Vector2 dirToPlayer)
    {
        dirToPlayer = Vector2.zero;

        Vector2[] dirs =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right
        };

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(visionDistance / tileSize));
        Vector2 boxSize = Vector2.one * (tileSize * 0.8f);

        foreach (var dir in dirs)
        {
            for (int step = 1; step <= maxSteps; step++)
            {
                Vector2 tileCenter = rb.position + step * tileSize * dir;

                Collider2D playerHit = Physics2D.OverlapBox(tileCenter, boxSize, 0f, playerLayerMask);

                if (playerHit != null)
                {
                    dirToPlayer = dir;
                    return true;
                }

                if (IsTileBlocked(tileCenter))
                    break;
            }
        }

        return false;
    }

    private void HandleStuckInternal()
    {
        if (isDead || isInDamagedLoop)
        {
            isStuck = false;
            return;
        }

        if (activeSprite != null)
        {
            activeSprite.enabled = true;
            activeSprite.idle = false;
        }

        stuckTimer += Time.fixedDeltaTime;

        if (stuckTimer < recheckStuckEverySeconds)
            return;

        stuckTimer = 0f;

        if (TryPickAnyFreeDirection(out var newDir))
        {
            isStuck = false;
            direction = newDir;
            UpdateSpriteDirection(direction);
            targetTile = rb.position + direction * tileSize;
        }
        else
        {
            targetTile = rb.position;

            if (activeSprite != null)
            {
                activeSprite.enabled = true;
                activeSprite.idle = false;
            }
        }
    }

    private void HookHealth()
    {
        healthRef = GetComponent<CharacterHealth>();
        if (healthRef == null)
            return;

        healthRef.HitInvulnerabilityStarted += OnHealthInvulnStarted;
        healthRef.HitInvulnerabilityEnded += OnHealthInvulnEnded;
        healthRef.Died += OnHealthDiedInternal;
    }

    private void UnhookHealth()
    {
        if (healthRef == null)
            return;

        healthRef.HitInvulnerabilityStarted -= OnHealthInvulnStarted;
        healthRef.HitInvulnerabilityEnded -= OnHealthInvulnEnded;
        healthRef.Died -= OnHealthDiedInternal;
    }

    private void OnHealthInvulnStarted(float seconds)
    {
        DisableAllStateSprites();
    }

    private void OnHealthInvulnEnded()
    {
        if (isDead)
            return;

        UpdateSpriteDirection(direction);
    }

    private void OnHealthDiedInternal()
    {
        DisableAllStateSprites();
    }
}
