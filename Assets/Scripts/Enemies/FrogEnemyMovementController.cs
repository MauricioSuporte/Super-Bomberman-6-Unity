using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class FrogEnemyMovementController : EnemyMovementController
{
    private const float JumpDuration = 1.5f;
    private const float JumpStartAnimationDuration = 1f / 3f;
    private const float JumpEndAnimationDuration = 0.5f;
    private const float LandingIdleDuration = 0.25f;
    private const float JokeDuration = 1.5f;
    private const float JumpHeightInTiles = 2f;
    private const int JumpDistanceInTiles = 2;
    private const int JumpsPerJoke = 3;

    private enum FrogState
    {
        LandingIdle,
        Jumping,
        Joking,
    }

    [Header("Frog Visuals")]
    [SerializeField] private AnimatedSpriteRenderer idleSprite;
    [SerializeField] private AnimatedSpriteRenderer jumpSprite;
    [SerializeField] private AnimatedSpriteRenderer jokeSprite;

    [Header("Landing Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap destructiblesTilemap;
    [SerializeField] private Tilemap indestructiblesTilemap;

    private readonly List<Collider2D> jumpColliders = new();
    private readonly List<bool> jumpColliderStates = new();

    private FrogState state;
    private float stateTimer;
    private int completedJumps;
    private bool jokePending;
    private Vector2 jumpStart;
    private Vector2 jumpTarget;
    private float currentJumpArcHeight;
    private PlayerWaterSubmersionEffect waterSubmersion;
    private GameObject jumpShadow;
    private Sprite jumpShadowSprite;

    protected override void Awake()
    {
        ResolveVisuals();

        if (idleSprite != null)
        {
            spriteDown = idleSprite;
            spriteUp = null;
            spriteLeft = null;
        }

        base.Awake();
        activeSprite = idleSprite != null ? idleSprite : spriteDown;
    }

    protected override void Start()
    {
        SnapToGrid();
        ResolveLandingTilemaps();
        EnsureWaterSubmersion();

        state = FrogState.LandingIdle;
        stateTimer = LandingIdleDuration;
        ShowVisual(idleSprite);
        SetWaterSubmerged(true);
    }

    protected override void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (isDead || isInDamagedLoop || IsStunned())
            return;

        switch (state)
        {
            case FrogState.LandingIdle:
                UpdateLandingIdle();
                break;

            case FrogState.Jumping:
                UpdateJump();
                break;

            case FrogState.Joking:
                UpdateJoke();
                break;
        }
    }

    protected override void OnHitInvulnerabilityStarted(float seconds)
    {
        base.OnHitInvulnerabilityStarted(seconds);

        if (isInDamagedLoop)
        {
            SetWaterSubmerged(false);
            ShowVisual(spriteDamaged);
        }
    }

    protected override void OnHitInvulnerabilityEnded()
    {
        base.OnHitInvulnerabilityEnded();

        if (isDead || isInDamagedLoop)
            return;

        SetWaterSubmerged(IsWaterSubmergedState());
        RefreshVisualForCurrentState();
    }

    protected override void Die()
    {
        SetWaterSubmerged(false);
        DestroyJumpShadow();
        DisableAllColliders();
        HideAllVisualsExcept(spriteDeath);
        base.Die();
    }

    private void UpdateLandingIdle()
    {
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer > 0f)
            return;

        if (jokePending)
        {
            jokePending = false;
            state = FrogState.Joking;
            stateTimer = JokeDuration;
            ShowVisual(jokeSprite);
            return;
        }

        if (TryChooseLandingTile(out Vector2 target))
            BeginJump(target);
        else
            stateTimer = LandingIdleDuration;
    }

    private void BeginJump(Vector2 target)
    {
        jumpStart = rb.position;
        jumpTarget = target;
        state = FrogState.Jumping;
        stateTimer = 0f;
        currentJumpArcHeight = 0f;

        SetWaterSubmerged(false);
        DisableCollidersForJump();
        SpawnWaterEffect(jumpStart, FrogWaterJumpEffect.EffectType.ExitRipple);
        CreateJumpShadow(jumpStart);
        ShowJumpFrame(forward: true, 0f);
    }

    private void UpdateJump()
    {
        stateTimer += Time.fixedDeltaTime;
        float normalizedTime = Mathf.Clamp01(stateTimer / JumpDuration);
        float horizontalTime = EaseOutQuad(normalizedTime);
        currentJumpArcHeight = Mathf.Sin(normalizedTime * Mathf.PI) * JumpHeightInTiles * tileSize;
        Vector2 groundPosition = Vector2.Lerp(jumpStart, jumpTarget, horizontalTime);
        rb.MovePosition(groundPosition);
        UpdateJumpShadow(groundPosition);

        if (stateTimer <= JumpStartAnimationDuration)
            ShowJumpFrame(forward: true, stateTimer / JumpStartAnimationDuration);
        else if (stateTimer >= JumpDuration - JumpEndAnimationDuration)
            ShowJumpFrame(forward: false, (stateTimer - (JumpDuration - JumpEndAnimationDuration)) / JumpEndAnimationDuration);
        else
            ShowVisual(idleSprite);

        ApplyJumpArcToActiveVisual();

        if (stateTimer < JumpDuration)
            return;

        rb.position = jumpTarget;
        RestoreCollidersAfterJump();
        ClearJumpArc();
        DestroyJumpShadow();
        SpawnWaterEffect(jumpTarget, FrogWaterJumpEffect.EffectType.EntrySplash);

        completedJumps++;
        jokePending = completedJumps % JumpsPerJoke == 0;
        state = FrogState.LandingIdle;
        stateTimer = LandingIdleDuration;
        ShowVisual(idleSprite);
        SetWaterSubmerged(true);
    }

    private void UpdateJoke()
    {
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f)
        {
            state = FrogState.LandingIdle;
            stateTimer = 0f;
            ShowVisual(idleSprite);
        }
    }

    private bool TryChooseLandingTile(out Vector2 target)
    {
        List<Vector2> validDirections = new(Dirs.Length);

        foreach (Vector2 candidateDirection in Dirs)
        {
            Vector2 candidateTarget = rb.position + candidateDirection * tileSize * JumpDistanceInTiles;
            if (IsLandingTileAvailable(candidateTarget))
                validDirections.Add(candidateDirection);
        }

        if (validDirections.Count == 0)
        {
            target = rb.position;
            return false;
        }

        direction = validDirections[Random.Range(0, validDirections.Count)];
        target = rb.position + direction * tileSize * JumpDistanceInTiles;
        return true;
    }

    private bool IsLandingTileAvailable(Vector2 worldPosition)
    {
        if (groundTilemap == null)
            ResolveLandingTilemaps();

        if (groundTilemap == null || !groundTilemap.HasTile(groundTilemap.WorldToCell(worldPosition)))
            return false;

        if (HasTileAt(destructiblesTilemap, worldPosition) ||
            HasTileAt(indestructiblesTilemap, worldPosition))
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, Vector2.one * (tileSize * 0.8f), 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit.GetComponentInParent<CoreMechanismsDestructible>() != null)
                return false;
        }

        return true;
    }

    private static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition)
    {
        return tilemap != null && tilemap.GetTile(tilemap.WorldToCell(worldPosition)) != null;
    }

    private void ResolveVisuals()
    {
        idleSprite ??= FindChildAnimatedSprite("Idle");
        jumpSprite ??= FindChildAnimatedSprite("Jump");
        jokeSprite ??= FindChildAnimatedSprite("Joke");
    }

    private AnimatedSpriteRenderer FindChildAnimatedSprite(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<AnimatedSpriteRenderer>() : null;
    }

    private void ResolveLandingTilemaps()
    {
        GameManager gameManager = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (gameManager != null)
        {
            groundTilemap ??= gameManager.groundTilemap;
            destructiblesTilemap ??= gameManager.destructibleTilemap;
            indestructiblesTilemap ??= gameManager.indestructibleTilemap;
        }

        if (groundTilemap != null && destructiblesTilemap != null && indestructiblesTilemap != null)
            return;

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (tilemap == null)
                continue;

            if (groundTilemap == null && tilemap.name == "Ground")
                groundTilemap = tilemap;
            else if (destructiblesTilemap == null && tilemap.name == "Destructibles")
                destructiblesTilemap = tilemap;
            else if (indestructiblesTilemap == null && tilemap.name == "Indestructibles")
                indestructiblesTilemap = tilemap;
        }
    }

    private bool IsStunned()
    {
        return TryGetComponent(out StunReceiver stun) && stun != null && stun.IsStunned;
    }

    private void ShowJumpFrame(bool forward, float normalizedWindowTime)
    {
        if (jumpSprite == null || jumpSprite.animationSprite == null || jumpSprite.animationSprite.Length == 0)
        {
            ShowVisual(idleSprite);
            return;
        }

        ShowVisual(jumpSprite);
        jumpSprite.SetManualAnimationUpdate(true);

        int frameCount = jumpSprite.animationSprite.Length;
        int frame = Mathf.Min(frameCount - 1, Mathf.FloorToInt(Mathf.Clamp01(normalizedWindowTime) * frameCount));
        jumpSprite.CurrentFrame = forward ? frame : frameCount - 1 - frame;
        jumpSprite.RefreshFrame();
    }

    private void RefreshVisualForCurrentState()
    {
        switch (state)
        {
            case FrogState.Jumping:
                if (stateTimer <= JumpStartAnimationDuration)
                    ShowJumpFrame(forward: true, stateTimer / JumpStartAnimationDuration);
                else if (stateTimer >= JumpDuration - JumpEndAnimationDuration)
                    ShowJumpFrame(forward: false, (stateTimer - (JumpDuration - JumpEndAnimationDuration)) / JumpEndAnimationDuration);
                else
                    ShowVisual(idleSprite);
                break;

            case FrogState.Joking:
                ShowVisual(jokeSprite);
                break;

            default:
                ShowVisual(idleSprite);
                break;
        }
    }

    private void ShowVisual(AnimatedSpriteRenderer target)
    {
        if (target == null)
            return;

        HideAllVisualsExcept(target);
        target.enabled = true;
        target.idle = false;

        if (target != jumpSprite)
            target.SetManualAnimationUpdate(false);

        activeSprite = target;

        if (state == FrogState.Jumping)
            ApplyJumpArcToActiveVisual();
    }

    private void HideAllVisualsExcept(AnimatedSpriteRenderer target)
    {
        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite == null || animatedSprite == target)
                continue;

            animatedSprite.SetManualAnimationUpdate(false);
            animatedSprite.enabled = false;
        }
    }

    private void DisableCollidersForJump()
    {
        jumpColliders.Clear();
        jumpColliderStates.Clear();

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
        {
            if (collider == null)
                continue;

            jumpColliders.Add(collider);
            jumpColliderStates.Add(collider.enabled);
            collider.enabled = false;
        }
    }

    private void RestoreCollidersAfterJump()
    {
        for (int i = 0; i < jumpColliders.Count; i++)
        {
            if (jumpColliders[i] != null)
                jumpColliders[i].enabled = jumpColliderStates[i];
        }

        jumpColliders.Clear();
        jumpColliderStates.Clear();
    }

    private void DisableAllColliders()
    {
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    protected override void OnDestroy()
    {
        DestroyJumpShadow();

        if (jumpShadowSprite != null)
        {
            Destroy(jumpShadowSprite.texture);
            Destroy(jumpShadowSprite);
        }

        base.OnDestroy();
    }

    private static float EaseOutQuad(float time)
    {
        float inverseTime = 1f - Mathf.Clamp01(time);
        return 1f - inverseTime * inverseTime;
    }

    private void ApplyJumpArcToActiveVisual()
    {
        if (activeSprite != null)
            activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up * currentJumpArcHeight);
    }

    private void ClearJumpArc()
    {
        currentJumpArcHeight = 0f;

        foreach (AnimatedSpriteRenderer animatedSprite in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            if (animatedSprite != null)
                animatedSprite.ClearExternalBase();
        }
    }

    private void EnsureWaterSubmersion()
    {
        if (!TryGetComponent(out waterSubmersion))
            waterSubmersion = gameObject.AddComponent<PlayerWaterSubmersionEffect>();

        SetWaterSubmerged(false);
    }

    private void SetWaterSubmerged(bool submerged)
    {
        if (waterSubmersion != null)
            waterSubmersion.SetEffectSuppressed(!submerged);
    }

    private bool IsWaterSubmergedState()
    {
        return state == FrogState.LandingIdle || state == FrogState.Joking;
    }

    private void CreateJumpShadow(Vector2 position)
    {
        DestroyJumpShadow();

        jumpShadow = new GameObject("FrogJumpShadow");
        jumpShadow.transform.position = new Vector3(position.x, position.y, 0f);
        jumpShadow.transform.localScale = new Vector3(0.75f, 0.32f, 1f);

        SpriteRenderer shadowRenderer = jumpShadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetJumpShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);
        shadowRenderer.sortingOrder = 4;
    }

    private void UpdateJumpShadow(Vector2 position)
    {
        if (jumpShadow != null)
            jumpShadow.transform.position = new Vector3(position.x, position.y, 0f);
    }

    private void DestroyJumpShadow()
    {
        if (jumpShadow != null)
            Destroy(jumpShadow);

        jumpShadow = null;
    }

    private Sprite GetJumpShadowSprite()
    {
        if (jumpShadowSprite != null)
            return jumpShadowSprite;

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            name = "FrogJumpShadow"
        };

        Vector2 center = new(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Vector2 point = new((x - center.x) / 7.5f, (y - center.y) / 4.5f);
                texture.SetPixel(x, y, point.sqrMagnitude <= 1f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        jumpShadowSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        jumpShadowSprite.name = "FrogJumpShadowSprite";
        return jumpShadowSprite;
    }

    private void SpawnWaterEffect(Vector2 position, FrogWaterJumpEffect.EffectType effectType)
    {
        GameObject effectObject = new($"FrogWater{effectType}");
        effectObject.transform.position = new Vector3(position.x, position.y, 0f);

        int sortingLayerId = 0;
        if (groundTilemap != null && groundTilemap.TryGetComponent(out TilemapRenderer groundRenderer))
            sortingLayerId = groundRenderer.sortingLayerID;

        FrogWaterJumpEffect effect = effectObject.AddComponent<FrogWaterJumpEffect>();
        effect.Initialize(effectType, sortingLayerId, sortingOrder: 4);
    }
}
