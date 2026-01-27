using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DisguiseSpriteSet
{
    public AnimatedSpriteRenderer up;
    public AnimatedSpriteRenderer down;
    public AnimatedSpriteRenderer left;
    public AnimatedSpriteRenderer right;
}

[RequireComponent(typeof(SpriteRenderer))]
public class ChameleonMovementController : EnemyMovementController
{
    [Header("Chameleon Animation")]
    public SpriteRenderer spriteRenderer;
    public Sprite idleSprite;
    public Sprite[] blinkSprites;
    public float blinkMinInterval = 2f;
    public float blinkMaxInterval = 3f;
    public float blinkDuration = 0.2f;
    public int blinksToDisguise = 2;

    [Header("Disguise (Player Form)")]
    public DisguiseSpriteSet[] disguiseSets;
    public float disguiseMinDuration = 4f;
    public float disguiseMaxDuration = 5f;
    public float disguisedSpeed = 5f;

    [Header("Disguise Movement AI")]
    public float minDirectionChangeTime = 0.5f;
    public float maxDirectionChangeTime = 1.5f;

    [Header("Transform Blink")]
    public float transformDuration = 1f;
    public float transformBlinkInterval = 0.1f;

    Coroutine behaviourRoutine;
    bool isDisguised;
    bool isTransforming;
    bool isBlinking;

    Vector2 disguisedDirection = Vector2.zero;
    float directionChangeTimer;
    bool hasDisguisedInput;

    const float CenterEpsilon = 0.01f;
    float SlideDeadZone => tileSize * 0.25f;

    float originalSpeed;

    AnimatedSpriteRenderer activeDisguiseSprite;
    DisguiseSpriteSet currentDisguiseSet;

    Vector2 cachedNormalDirection;
    Vector2 cachedNormalTargetTile;
    bool hasCachedNormalState;

    protected override void Awake()
    {
        base.Awake();

        originalSpeed = speed;

        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        ForceNormalVisualState();
    }

    protected override void Start()
    {
        base.Start();

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        ForceNormalVisualState();
        StartBehaviourLoop();
    }

    void OnEnable()
    {
        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        ForceNormalVisualState();
        StartBehaviourLoop();
    }

    void OnDisable()
    {
        StopBehaviourLoop();
    }

    void LateUpdate()
    {
        if (isDead)
            return;

        if (!isDisguised && !isTransforming && !isBlinking)
            ForceNormalVisualState();
    }

    void Update()
    {
        if (isDead || isTransforming)
            return;

        if (isDisguised)
            UpdateDisguisedDirection();
    }

    protected override void FixedUpdate()
    {
        if (isDead || isTransforming)
            return;

        if (isDisguised)
            DoDisguisedMovement();
        else
            base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
    }

    void ForceNormalVisualState()
    {
        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        DisableBaseDirectionalSprites();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
        }
    }

    void DisableBaseDirectionalSprites()
    {
        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;

        if (spriteDamaged != null) spriteDamaged.enabled = false;

        if (spriteDeath != null && !isDead)
            spriteDeath.enabled = false;
    }

    void DisableAllAnimatedChildren()
    {
        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] == null) continue;
            anims[i].enabled = false;

            var sr = anims[i].GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }

    void StartBehaviourLoop()
    {
        if (behaviourRoutine != null)
            StopCoroutine(behaviourRoutine);

        if (gameObject.activeInHierarchy)
            behaviourRoutine = StartCoroutine(BehaviourLoop());
    }

    void StopBehaviourLoop()
    {
        if (behaviourRoutine != null)
        {
            StopCoroutine(behaviourRoutine);
            behaviourRoutine = null;
        }
    }

    IEnumerator BehaviourLoop()
    {
        while (!isDead)
        {
            int blinkCount = 0;

            while (blinkCount < blinksToDisguise && !isDead)
            {
                float wait = Random.Range(blinkMinInterval, blinkMaxInterval);
                yield return new WaitForSeconds(wait);

                yield return BlinkOnce();
                blinkCount++;
            }

            if (isDead)
                break;

            yield return DisguiseAsPlayer();
        }

        behaviourRoutine = null;
    }

    IEnumerator BlinkOnce()
    {
        if (!spriteRenderer || blinkSprites == null || blinkSprites.Length == 0)
            yield break;

        isBlinking = true;

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        DisableBaseDirectionalSprites();

        spriteRenderer.enabled = true;

        float duration = Mathf.Max(blinkDuration, 0.01f);
        float frameTime = duration / blinkSprites.Length;

        for (int i = 0; i < blinkSprites.Length; i++)
        {
            spriteRenderer.sprite = blinkSprites[i];
            yield return new WaitForSeconds(frameTime);
        }

        if (!isDisguised && idleSprite != null)
            spriteRenderer.sprite = idleSprite;

        isBlinking = false;
    }

    Sprite GetCurrentPlayerIdleSprite()
    {
        if (currentDisguiseSet == null)
            return null;

        if (currentDisguiseSet.down != null && currentDisguiseSet.down.idleSprite != null)
            return currentDisguiseSet.down.idleSprite;

        if (currentDisguiseSet.down != null)
        {
            var sr = currentDisguiseSet.down.GetComponent<SpriteRenderer>();
            if (sr != null)
                return sr.sprite;
        }

        return null;
    }

    IEnumerator TransformBlink(Sprite playerIdleSprite)
    {
        if (spriteRenderer == null || idleSprite == null || playerIdleSprite == null)
            yield break;

        isBlinking = true;

        float elapsed = 0f;
        bool useChameleon = true;

        while (elapsed < transformDuration)
        {
            spriteRenderer.sprite = useChameleon ? idleSprite : playerIdleSprite;
            useChameleon = !useChameleon;

            float wait = transformBlinkInterval;
            elapsed += wait;
            yield return new WaitForSeconds(wait);
        }

        spriteRenderer.sprite = idleSprite;
        isBlinking = false;
    }

    void CacheNormalMovementState()
    {
        cachedNormalDirection = (direction == Vector2.zero) ? Vector2.down : direction;
        cachedNormalTargetTile = targetTile;
        hasCachedNormalState = true;
    }

    void ResyncNormalMovementAfterDisguise()
    {
        if (rb == null)
            return;

        SnapToGrid();

        if (!hasCachedNormalState)
        {
            ChooseInitialDirection();
            targetTile = rb.position;
            DecideNextTile();
            return;
        }

        direction = cachedNormalDirection;
        targetTile = rb.position;
        DecideNextTile();
    }

    IEnumerator DisguiseAsPlayer()
    {
        if (disguiseSets == null || disguiseSets.Length == 0)
            yield break;

        CacheNormalMovementState();
        SnapToGrid();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        currentDisguiseSet = disguiseSets[Random.Range(0, disguiseSets.Length)];

        isTransforming = true;
        isDisguised = false;
        speed = 0f;

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        DisableBaseDirectionalSprites();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
        }

        disguisedDirection = Vector2.zero;
        hasDisguisedInput = false;

        Sprite playerIdle = GetCurrentPlayerIdleSprite();
        if (playerIdle != null)
            yield return TransformBlink(playerIdle);

        isTransforming = false;
        isDisguised = true;
        speed = disguisedSpeed;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        DisableBaseDirectionalSprites();

        SetDisguiseDirection(Vector2.down);

        disguisedDirection = Vector2.zero;
        hasDisguisedInput = false;
        ResetDirectionTimer();

        float disguiseTime = Random.Range(disguiseMinDuration, disguiseMaxDuration);
        float elapsed = 0f;

        while (elapsed < disguiseTime && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDisguised = false;
        isTransforming = true;
        speed = 0f;

        SnapToGrid();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        DisableBaseDirectionalSprites();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (idleSprite != null)
                spriteRenderer.sprite = idleSprite;
        }

        if (playerIdle != null)
            yield return TransformBlink(playerIdle);

        isTransforming = false;
        speed = originalSpeed;

        ForceNormalVisualState();
        ResyncNormalMovementAfterDisguise();
    }

    void DisableDisguiseSprites()
    {
        if (disguiseSets == null)
        {
            activeDisguiseSprite = null;
            currentDisguiseSet = null;
            return;
        }

        for (int i = 0; i < disguiseSets.Length; i++)
        {
            var set = disguiseSets[i];
            if (set == null) continue;

            DisableDisguiseSprite(set.up);
            DisableDisguiseSprite(set.down);
            DisableDisguiseSprite(set.left);
            DisableDisguiseSprite(set.right);
        }

        activeDisguiseSprite = null;
    }

    void DisableDisguiseSprite(AnimatedSpriteRenderer r)
    {
        if (r == null) return;
        r.enabled = false;

        var sr = r.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    void SetDisguiseDirection(Vector2 dir)
    {
        if (!isDisguised || currentDisguiseSet == null)
            return;

        AnimatedSpriteRenderer newSprite = activeDisguiseSprite;

        if (dir.y > 0.1f)
            newSprite = currentDisguiseSet.up;
        else if (dir.y < -0.1f)
            newSprite = currentDisguiseSet.down;
        else if (dir.x < -0.1f)
            newSprite = currentDisguiseSet.left;
        else if (dir.x > 0.1f)
            newSprite = currentDisguiseSet.right;

        if (newSprite != activeDisguiseSprite)
        {
            if (activeDisguiseSprite != null)
                DisableDisguiseSprite(activeDisguiseSprite);

            activeDisguiseSprite = newSprite;

            if (activeDisguiseSprite != null)
            {
                activeDisguiseSprite.enabled = true;
                activeDisguiseSprite.idle = false;

                var sr = activeDisguiseSprite.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = true;
            }
        }

        if (activeDisguiseSprite != null)
            activeDisguiseSprite.idle = dir == Vector2.zero;
    }

    void ResetDirectionTimer()
    {
        directionChangeTimer = Random.Range(minDirectionChangeTime, maxDirectionChangeTime);
    }

    void UpdateDisguisedDirection()
    {
        directionChangeTimer -= Time.deltaTime;

        if (!hasDisguisedInput || directionChangeTimer <= 0f)
        {
            var validDirs = new List<Vector2>();

            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = i == 0 ? Vector2.up : i == 1 ? Vector2.down : i == 2 ? Vector2.left : Vector2.right;
                Vector2 checkPos = rb.position + dir * tileSize;
                if (!IsBlockedDisguised(checkPos))
                    validDirs.Add(dir);
            }

            if (validDirs.Count == 0)
            {
                disguisedDirection = Vector2.zero;
                hasDisguisedInput = false;
            }
            else
            {
                disguisedDirection = validDirs[Random.Range(0, validDirs.Count)];
                hasDisguisedInput = true;
            }

            ResetDirectionTimer();
        }

        SetDisguiseDirection(disguisedDirection);
    }

    void DoDisguisedMovement()
    {
        if (!hasDisguisedInput || disguisedDirection == Vector2.zero)
        {
            SetDisguiseDirection(Vector2.zero);
            return;
        }

        float dt = Time.fixedDeltaTime;
        float moveSpeed = disguisedSpeed * dt;

        Vector2 position = rb.position;

        bool blockLeft = IsSolidAtDisguised(position + Vector2.left * (tileSize * 0.5f));
        bool blockRight = IsSolidAtDisguised(position + Vector2.right * (tileSize * 0.5f));
        bool blockUp = IsSolidAtDisguised(position + Vector2.up * (tileSize * 0.5f));
        bool blockDown = IsSolidAtDisguised(position + Vector2.down * (tileSize * 0.5f));

        bool movingVertical = Mathf.Abs(disguisedDirection.y) > 0.01f;
        bool movingHorizontal = Mathf.Abs(disguisedDirection.x) > 0.01f;

        if (movingVertical && blockLeft && blockRight)
        {
            float targetX = Mathf.Round(position.x / tileSize) * tileSize;
            position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed);
        }

        if (movingHorizontal && blockUp && blockDown)
        {
            float targetY = Mathf.Round(position.y / tileSize) * tileSize;
            position.y = Mathf.MoveTowards(position.y, targetY, moveSpeed);
        }

        Vector2 targetPosition = position + disguisedDirection * moveSpeed;

        if (!IsBlockedDisguised(targetPosition))
        {
            rb.MovePosition(targetPosition);
            return;
        }

        if (movingVertical)
        {
            float currentCenterX = Mathf.Round(position.x / tileSize) * tileSize;
            float offsetX = Mathf.Abs(position.x - currentCenterX);

            if (offsetX > SlideDeadZone)
                TrySlideHorizontally(position, moveSpeed);
        }
        else if (movingHorizontal)
        {
            float currentCenterY = Mathf.Round(position.y / tileSize) * tileSize;
            float offsetY = Mathf.Abs(position.y - currentCenterY);

            if (offsetY > SlideDeadZone)
                TrySlideVertically(position, moveSpeed);
        }
    }

    void TrySlideHorizontally(Vector2 position, float moveSpeed)
    {
        float leftCenter = Mathf.Floor(position.x / tileSize) * tileSize;
        float rightCenter = Mathf.Ceil(position.x / tileSize) * tileSize;

        Vector2 verticalStep = new Vector2(0f, disguisedDirection.y * moveSpeed);

        bool leftFree = !IsBlockedDisguised(new Vector2(leftCenter, position.y) + verticalStep);
        bool rightFree = !IsBlockedDisguised(new Vector2(rightCenter, position.y) + verticalStep);

        if (!leftFree && !rightFree)
            return;

        float targetX;

        if (leftFree && !rightFree)
            targetX = leftCenter;
        else if (rightFree && !leftFree)
            targetX = rightCenter;
        else
            targetX = Mathf.Abs(position.x - leftCenter) <= Mathf.Abs(position.x - rightCenter) ? leftCenter : rightCenter;

        if (Mathf.Abs(position.x - targetX) > CenterEpsilon)
        {
            float newX = Mathf.MoveTowards(position.x, targetX, moveSpeed);
            rb.MovePosition(new Vector2(newX, position.y));
        }
        else
        {
            Vector2 newPos = new Vector2(targetX, position.y) + verticalStep;
            if (!IsBlockedDisguised(newPos))
                rb.MovePosition(newPos);
        }
    }

    void TrySlideVertically(Vector2 position, float moveSpeed)
    {
        float bottomCenter = Mathf.Floor(position.y / tileSize) * tileSize;
        float topCenter = Mathf.Ceil(position.y / tileSize) * tileSize;

        Vector2 horizontalStep = new Vector2(disguisedDirection.x * moveSpeed, 0f);

        bool bottomFree = !IsBlockedDisguised(new Vector2(position.x, bottomCenter) + horizontalStep);
        bool topFree = !IsBlockedDisguised(new Vector2(position.x, topCenter) + horizontalStep);

        if (!bottomFree && !topFree)
            return;

        float targetY;

        if (bottomFree && !topFree)
            targetY = bottomCenter;
        else if (topFree && !bottomFree)
            targetY = topCenter;
        else
            targetY = Mathf.Abs(position.y - bottomCenter) <= Mathf.Abs(position.y - topCenter) ? bottomCenter : topCenter;

        if (Mathf.Abs(position.y - targetY) > CenterEpsilon)
        {
            float newY = Mathf.MoveTowards(position.y, targetY, moveSpeed);
            rb.MovePosition(new Vector2(position.x, newY));
        }
        else
        {
            Vector2 newPos = new Vector2(position.x, targetY) + horizontalStep;
            if (!IsBlockedDisguised(newPos))
                rb.MovePosition(newPos);
        }
    }

    bool IsSolidAtDisguised(Vector2 worldPosition)
    {
        Vector2 size = Vector2.one * (tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, size, 0f, obstacleMask);
        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    bool IsBlockedDisguised(Vector2 targetPosition)
    {
        Vector2 size;

        if (Mathf.Abs(disguisedDirection.x) > 0f)
            size = new Vector2(tileSize * 0.6f, tileSize * 0.2f);
        else
            size = new Vector2(tileSize * 0.2f, tileSize * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetPosition, size, 0f, obstacleMask);

        if (hits == null || hits.Length == 0)
            return false;

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            if (hit.gameObject == gameObject)
                continue;

            if (hit.isTrigger)
                continue;

            return true;
        }

        return false;
    }

    protected override void Die()
    {
        if (isDead)
            return;

        StopBehaviourLoop();

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        DisableAllAnimatedChildren();
        DisableDisguiseSprites();
        DisableBaseDirectionalSprites();

        base.Die();
    }
}
