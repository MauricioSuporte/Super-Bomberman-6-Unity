using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StageAssets;

/// <summary>
/// Junction-turning submarine that periodically stops to fire a three-tile
/// bubble stream toward the nearest player.
/// </summary>
public sealed class SubmarineBubbleMovementController : JunctionTurningEnemyMovementController
{
    private const float WalkDuration = 7f;
    private const float AttackDuration = 5f;
    private const float BurstInterval = 0.25f;
    private const float BubbleTileTravelDelay = 0.08f;
    private const int MinimumBubblesPerTile = 2;
    private const int MaximumBubblesPerTile = 4;

    private enum BubbleState { Walking, Attacking }

    [Header("Bubble Attack")]
    [SerializeField] private AnimatedSpriteRenderer attackLeftVisual;
    [SerializeField] private AnimatedSpriteRenderer attackDownVisual;
    [SerializeField] private AnimatedSpriteRenderer attackUpVisual;

    [Header("Bubble Stream Visuals")]
    [SerializeField] private Stage33BubbleAnimationCatalog bubbleAnimationCatalog;
    [SerializeField, Min(0.01f)] private float bubbleFramesPerSecond = 12f;
    [SerializeField] private string bubbleSortingLayerName = "Default";
    [SerializeField] private int bubbleSortingOrder = 6;

    [Header("Bubble Attack Audio")]
    [SerializeField] private AudioClip bubbleAttackSfx;
    [SerializeField, Range(0f, 1f)] private float bubbleAttackSfxVolume = 1f;

    [Header("Bubble Push")]
    [SerializeField, Min(0.1f)] private float playerPushSpeed = 4f;

    [Header("Bubble Shadow")]
    [SerializeField, Min(0f)] private float hoverBaseHeight = 0.45f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.08f;
    [SerializeField, Min(0.01f)] private float hoverFrequency = 3f;
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Vector2 shadowScale = new(0.9f, 0.9f);
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;

    private BubbleState state;
    private float walkTimer;
    private GameObject shadow;
    private Sprite shadowSprite;
    private AudioSource audioSource;
    private readonly List<GameObject> activeBubbleParticles = new();
    private readonly HashSet<MovementController> pushedPlayers = new();

    protected override void Awake()
    {
        ConfigureRenderers();
        bubbleAnimationCatalog ??= Resources.Load<Stage33BubbleAnimationCatalog>("StageAssets/Stage33BubbleAnimations");
        audioSource = GetComponent<AudioSource>();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        CreateShadow();
        walkTimer = WalkDuration;
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (state == BubbleState.Attacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        base.FixedUpdate();

        if (isInDamagedLoop)
            return;

        walkTimer -= Time.fixedDeltaTime;
        if (walkTimer <= 0f)
            StartCoroutine(BubbleAttackRoutine());
    }

    protected override void Die()
    {
        DestroyShadow();
        ClearHoverOffset();
        HideAttackVisuals();
        ClearBubbleParticles();
        pushedPlayers.Clear();
        base.Die();
    }

    protected override void OnDestroy()
    {
        DestroyShadow();
        ClearBubbleParticles();
        pushedPlayers.Clear();

        if (shadowSprite != null)
        {
            Destroy(shadowSprite.texture);
            Destroy(shadowSprite);
        }

        base.OnDestroy();
    }

    private IEnumerator BubbleAttackRoutine()
    {
        state = BubbleState.Attacking;
        rb.linearVelocity = Vector2.zero;
        SnapToGrid();

        direction = GetNearestPlayerDirection();
        ShowAttackVisual(direction);
        if (audioSource != null && bubbleAttackSfx != null)
            GameAudioSettings.PlaySfx(audioSource, bubbleAttackSfx, bubbleAttackSfxVolume);

        float elapsed = 0f;
        while (!isDead && elapsed < AttackDuration)
        {
            yield return FireBubbleStream(direction);
            elapsed += BurstInterval;
        }

        if (isDead)
            yield break;

        HideAttackVisuals();
        state = BubbleState.Walking;
        walkTimer = WalkDuration;
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private Vector2 GetNearestPlayerDirection()
    {
        MovementController closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (MovementController player in FindObjectsByType<MovementController>())
        {
            if (player == null || player.isDead || player.IsEndingStage || !player.gameObject.activeInHierarchy)
                continue;

            float distance = ((Vector2)player.transform.position - rb.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player;
            }
        }

        if (closest == null)
            return direction == Vector2.zero ? Vector2.down : direction;

        Vector2 delta = (Vector2)closest.transform.position - rb.position;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x < 0f ? Vector2.left : Vector2.right;

        return delta.y < 0f ? Vector2.down : Vector2.up;
    }

    private IEnumerator FireBubbleStream(Vector2 attackDirection)
    {
        for (int distance = 1; distance <= 3; distance++)
        {
            Vector2 tile = rb.position + attackDirection * (tileSize * distance);
            SpawnBubbleBurstAt(tile, attackDirection);
            PushTargetsAtTile(tile, attackDirection);

            if (distance < 3)
                yield return new WaitForSeconds(BubbleTileTravelDelay);
        }

        float recovery = BurstInterval - BubbleTileTravelDelay * 2f;
        if (recovery > 0f)
            yield return new WaitForSeconds(recovery);
    }

    private void PushTargetsAtTile(Vector2 tile, Vector2 attackDirection)
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        LayerMask targetMask = LayerMask.GetMask("Player", "Bomb");

        foreach (Collider2D hit in Physics2D.OverlapBoxAll(tile, Vector2.one * (tileSize * 0.7f), 0f, targetMask))
        {
            if (hit == null)
                continue;

            Bomb bomb = hit.GetComponentInParent<Bomb>();
            if (bomb != null)
            {
                bomb.StartKick(attackDirection, tileSize, obstacleMask,
                    gameManager != null ? gameManager.destructibleTilemap : null);
                continue;
            }

            MovementController player = hit.GetComponentInParent<MovementController>();
            if (player == null || player.isDead || player.IsEndingStage || player.Rigidbody == null)
                continue;

            Vector2 destination = player.Rigidbody.position + attackDirection * tileSize;
            if (IsTileBlocked(destination))
                continue;

            if (pushedPlayers.Add(player))
                StartCoroutine(PushPlayerRoutine(player, attackDirection));
        }
    }

    private IEnumerator PushPlayerRoutine(MovementController player, Vector2 pushDirection)
    {
        float remainingDistance = tileSize;
        float speed = Mathf.Max(0.1f, playerPushSpeed);

        while (!isDead && player != null && !player.isDead && !player.IsEndingStage && remainingDistance > 0.001f)
        {
            if (player.Rigidbody == null)
                break;

            float step = Mathf.Min(speed * Time.fixedDeltaTime, remainingDistance);
            Vector2 next = player.Rigidbody.position + pushDirection * step;
            if (IsTileBlocked(next))
                break;

            // Match Magnet's movement: advance the Rigidbody one physics step
            // at a time, instead of changing the player's Transform instantly.
            player.Rigidbody.MovePosition(next);
            remainingDistance -= step;
            yield return new WaitForFixedUpdate();
        }

        if (player != null)
            pushedPlayers.Remove(player);
    }

    private void SpawnBubbleBurstAt(Vector2 tilePosition, Vector2 attackDirection)
    {
        Vector2 perpendicular = new(-attackDirection.y, attackDirection.x);
        int count = Random.Range(MinimumBubblesPerTile, MaximumBubblesPerTile + 1);

        for (int i = 0; i < count; i++)
        {
            Sprite[] sprites = GetRandomBubbleAnimation();
            if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                continue;

            Vector2 position = tilePosition - attackDirection * Random.Range(0.18f, 0.34f) +
                perpendicular * Random.Range(-0.22f, 0.22f);
            Vector2 velocity = attackDirection * Random.Range(1.4f, 2.3f) +
                perpendicular * Random.Range(-0.55f, 0.55f);
            StartCoroutine(AnimateBubbleParticle(position, velocity, sprites));
        }
    }

    private IEnumerator AnimateBubbleParticle(Vector2 startPosition, Vector2 velocity, Sprite[] sprites)
    {
        GameObject bubble = new("Submarine Bubble Stream");
        activeBubbleParticles.Add(bubble);
        SpriteRenderer renderer = bubble.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingLayerName = bubbleSortingLayerName;
        renderer.sortingOrder = bubbleSortingOrder;

        float frameDuration = 1f / Mathf.Max(0.01f, bubbleFramesPerSecond);
        float frameTimer = 0f;
        float elapsed = 0f;
        int frame = 0;
        Vector2 position = startPosition;

        while (frame < sprites.Length && bubble != null)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            frameTimer += deltaTime;
            velocity += Random.insideUnitCircle * (0.35f * deltaTime);
            position += velocity * deltaTime;
            bubble.transform.position = SnapToPixelGrid(position);

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frame++;
                if (frame >= sprites.Length)
                    break;

                renderer.sprite = sprites[frame];
                Color color = renderer.color;
                color.a = 1f - Mathf.Clamp01(elapsed / (frameDuration * sprites.Length));
                renderer.color = color;
            }

            yield return null;
        }

        activeBubbleParticles.Remove(bubble);
        if (bubble != null)
            Destroy(bubble);
    }

    private Sprite[] GetRandomBubbleAnimation()
    {
        Stage33BubbleAnimation[] animations = bubbleAnimationCatalog != null
            ? bubbleAnimationCatalog.animations
            : null;

        int validCount = 0;
        for (int i = 0; animations != null && i < animations.Length; i++)
        {
            if (animations[i]?.sprites?.Length > 0 && animations[i].sprites[0] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int selected = Random.Range(0, validCount);
        for (int i = 0; i < animations.Length; i++)
        {
            Sprite[] sprites = animations[i]?.sprites;
            if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                continue;

            if (selected-- == 0)
                return sprites;
        }

        return null;
    }

    private static Vector3 SnapToPixelGrid(Vector2 position)
    {
        const float pixelsPerUnit = 16f;
        return new Vector3(
            Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit,
            Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit,
            0f);
    }

    private void ClearBubbleParticles()
    {
        for (int i = 0; i < activeBubbleParticles.Count; i++)
        {
            if (activeBubbleParticles[i] != null)
                Destroy(activeBubbleParticles[i]);
        }

        activeBubbleParticles.Clear();
    }

    private void ConfigureRenderers()
    {
        spriteDown ??= transform.Find("Down")?.GetComponent<AnimatedSpriteRenderer>();
        spriteUp ??= transform.Find("Up")?.GetComponent<AnimatedSpriteRenderer>();
        spriteLeft ??= transform.Find("Left")?.GetComponent<AnimatedSpriteRenderer>();
        attackLeftVisual ??= transform.Find("Attack Left")?.GetComponent<AnimatedSpriteRenderer>();
        attackDownVisual ??= transform.Find("Attack Down")?.GetComponent<AnimatedSpriteRenderer>();
        attackUpVisual ??= transform.Find("Attack Up")?.GetComponent<AnimatedSpriteRenderer>();
        spriteDeath ??= transform.Find("Death")?.GetComponent<AnimatedSpriteRenderer>();

        if (spriteDeath != null)
        {
            spriteDeath.animationTime = 0.1f;
            spriteDeath.loop = false;
            spriteDeath.enabled = false;
        }
    }

    private void ShowAttackVisual(Vector2 attackDirection)
    {
        AnimatedSpriteRenderer target = attackDirection == Vector2.up ? attackUpVisual :
            attackDirection == Vector2.down ? attackDownVisual : attackLeftVisual;

        if (target == null)
            return;

        if (spriteUp != null) spriteUp.enabled = false;
        if (spriteDown != null) spriteDown.enabled = false;
        if (spriteLeft != null) spriteLeft.enabled = false;
        HideAttackVisuals();

        activeSprite = target;
        target.enabled = true;
        target.idle = false;
        target.loop = true;
        target.RestartAnimation();

        if (target.TryGetComponent(out SpriteRenderer renderer))
            renderer.flipX = attackDirection == Vector2.right;
    }

    private void HideAttackVisuals()
    {
        if (attackLeftVisual != null) attackLeftVisual.enabled = false;
        if (attackDownVisual != null) attackDownVisual.enabled = false;
        if (attackUpVisual != null) attackUpVisual.enabled = false;
    }

    private void LateUpdate()
    {
        UpdateShadowPosition();

        if (isDead || isInDamagedLoop || state != BubbleState.Walking)
        {
            ClearHoverOffset();
            return;
        }

        ApplyHoverOffset();
    }

    private void ApplyHoverOffset()
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (renderer != null && renderer != activeSprite)
                renderer.ClearExternalBase();
        }

        if (activeSprite == null)
            return;

        float height = hoverBaseHeight + Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * height);
    }

    private void ClearHoverOffset()
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            renderer?.ClearExternalBase();
    }

    private void CreateShadow()
    {
        if (shadow != null || isDead)
            return;

        shadow = new GameObject("SubmarineBubbleShadow");
        shadow.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = GetShadowSprite();
        renderer.color = shadowColor;
        renderer.sortingOrder = 4;

        if (activeSprite != null && activeSprite.TryGetComponent(out SpriteRenderer visualRenderer))
        {
            renderer.sortingLayerID = visualRenderer.sortingLayerID;
            renderer.sortingOrder = visualRenderer.sortingOrder - 1;
        }

        UpdateShadowPosition();
    }

    private void UpdateShadowPosition()
    {
        if (shadow == null)
            return;

        Vector3 position = transform.position + (Vector3)shadowOffset;
        shadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void DestroyShadow()
    {
        if (shadow != null)
            Destroy(shadow);
        shadow = null;
    }

    private Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
            return shadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = "SubmarineBubbleShadow"
        };

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
            texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
        }

        texture.Apply();
        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        shadowSprite.name = "SubmarineBubbleShadowSprite";
        return shadowSprite;
    }
}
