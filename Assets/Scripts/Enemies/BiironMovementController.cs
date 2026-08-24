using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class BiironMovementController : JunctionTurningEnemyMovementController
{
    [Header("Jump Visuals")]
    [SerializeField] private AnimatedSpriteRenderer jumpUp;
    [SerializeField] private AnimatedSpriteRenderer jumpDown;
    [SerializeField] private AnimatedSpriteRenderer jumpLeft;
    [SerializeField] private AnimatedSpriteRenderer jumpRight;

    [Header("Jump Timing")]
    [SerializeField, Min(0.01f)] private float jumpDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float jumpArcHeightTiles = 0.75f;
    [SerializeField, Min(0.1f)] private float minJumpCooldown = 3f;
    [SerializeField, Min(0.1f)] private float maxJumpCooldown = 6f;

    [Header("Landing Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap destructiblesTilemap;
    [SerializeField] private Tilemap indestructiblesTilemap;

    private Collider2D bodyCollider;
    private CharacterHealth biironHealth;
    private Coroutine jumpRoutine;
    private float nextJumpTime;

    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();
        biironHealth = GetComponent<CharacterHealth>();
        SetAllJumpVisuals(false);
    }

    protected override void Start()
    {
        base.Start();
        ResolveLandingTilemaps();
        ScheduleNextJump();
    }

    protected override void FixedUpdate()
    {
        if (jumpRoutine != null)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        base.FixedUpdate();

        if (isDead || isInDamagedLoop || Time.time < nextJumpTime)
            return;

        // The jump always starts at a tile center, even when its cooldown
        // elapses while normal junction movement is between two cells.
        SnapToGrid();
        targetTile = rb != null ? rb.position : (Vector2)transform.position;

        if (TryChooseLanding(out Vector2 jumpDirection, out Vector2 landing))
        {
            jumpRoutine = StartCoroutine(JumpRoutine(jumpDirection, landing));
        }
        else
        {
            ScheduleNextJump();
        }
    }

    protected override void UpdateSpriteDirection(Vector2 newDirection)
    {
        if (jumpRoutine == null)
            base.UpdateSpriteDirection(newDirection);
    }

    protected override void Die()
    {
        StopJump();
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopJump();
        base.OnDestroy();
    }

    private IEnumerator JumpRoutine(Vector2 jumpDirection, Vector2 landing)
    {
        direction = jumpDirection;
        isStuck = false;

        Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
        AnimatedSpriteRenderer jumpVisual = GetJumpVisual(jumpDirection);
        SetOnlyJumpVisual(jumpVisual);

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        if (biironHealth != null)
            biironHealth.SetExternalInvulnerability(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        float duration = Mathf.Max(0.01f, jumpDuration);
        float elapsed = 0f;
        while (elapsed < duration && !isDead)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector2 groundPosition = Vector2.Lerp(start, landing, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * jumpArcHeightTiles * tileSize;
            transform.position = new Vector3(groundPosition.x, groundPosition.y + height, transform.position.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isDead)
        {
            transform.position = new Vector3(landing.x, landing.y, transform.position.z);
            if (rb != null)
            {
                rb.simulated = true;
                rb.position = landing;
                rb.linearVelocity = Vector2.zero;
            }

            if (bodyCollider != null)
                bodyCollider.enabled = true;

            if (biironHealth != null)
                biironHealth.SetExternalInvulnerability(false);

            SetAllJumpVisuals(false);
            jumpRoutine = null;
            UpdateSpriteDirection(direction);
            targetTile = landing;
            DecideNextTile();
            ScheduleNextJump();
            yield break;
        }

        if (rb != null)
            rb.simulated = true;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        SetAllJumpVisuals(false);
        if (biironHealth != null)
            biironHealth.SetExternalInvulnerability(false);
        jumpRoutine = null;
    }

    private bool TryChooseLanding(out Vector2 jumpDirection, out Vector2 landing)
    {
        ResolveLandingTilemaps();
        Vector2[] candidates = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var validDirections = new System.Collections.Generic.List<Vector2>(candidates.Length);
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;

        foreach (Vector2 candidate in candidates)
        {
            Vector2 destination = origin + candidate * tileSize * 2f;
            if (IsValidLandingTile(destination))
                validDirections.Add(candidate);
        }

        if (validDirections.Count == 0)
        {
            jumpDirection = Vector2.zero;
            landing = origin;
            return false;
        }

        jumpDirection = validDirections[Random.Range(0, validDirections.Count)];
        landing = origin + jumpDirection * tileSize * 2f;
        return true;
    }

    private bool IsValidLandingTile(Vector2 worldPosition)
    {
        if (groundTilemap == null || !groundTilemap.HasTile(groundTilemap.WorldToCell(worldPosition)))
            return false;

        return !HasTileAt(destructiblesTilemap, worldPosition) &&
               !HasTileAt(indestructiblesTilemap, worldPosition);
    }

    private void ResolveLandingTilemaps()
    {
        GameManager gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
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
            if (groundTilemap == null && tilemap.name == "Ground")
                groundTilemap = tilemap;
            else if (destructiblesTilemap == null && tilemap.name == "Destructibles")
                destructiblesTilemap = tilemap;
            else if (indestructiblesTilemap == null && tilemap.name == "Indestructibles")
                indestructiblesTilemap = tilemap;
        }
    }

    private static bool HasTileAt(Tilemap tilemap, Vector2 worldPosition)
        => tilemap != null && tilemap.HasTile(tilemap.WorldToCell(worldPosition));

    private AnimatedSpriteRenderer GetJumpVisual(Vector2 jumpDirection)
    {
        if (jumpDirection == Vector2.up)
            return jumpUp;
        if (jumpDirection == Vector2.down)
            return jumpDown;
        return jumpDirection == Vector2.left ? jumpLeft : jumpRight;
    }

    private void SetOnlyJumpVisual(AnimatedSpriteRenderer visibleVisual)
    {
        foreach (AnimatedSpriteRenderer renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            bool isVisible = renderer == visibleVisual;
            renderer.enabled = isVisible;
            if (renderer.TryGetComponent(out SpriteRenderer spriteRenderer))
                spriteRenderer.enabled = isVisible;
        }

        if (visibleVisual != null)
        {
            visibleVisual.loop = false;
            visibleVisual.idle = false;
            visibleVisual.RestartAnimation();
        }
    }

    private void SetAllJumpVisuals(bool enabled)
    {
        SetVisualEnabled(jumpUp, enabled);
        SetVisualEnabled(jumpDown, enabled);
        SetVisualEnabled(jumpLeft, enabled);
        SetVisualEnabled(jumpRight, enabled);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer visual, bool enabled)
    {
        if (visual == null)
            return;

        visual.enabled = enabled;
        if (visual.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

    private void StopJump()
    {
        if (jumpRoutine != null)
            StopCoroutine(jumpRoutine);

        jumpRoutine = null;
        if (rb != null)
            rb.simulated = true;
        if (bodyCollider != null)
            bodyCollider.enabled = false;
        if (biironHealth != null)
            biironHealth.SetExternalInvulnerability(false);
        SetAllJumpVisuals(false);
    }

    private void ScheduleNextJump()
    {
        float minimum = Mathf.Max(0.1f, minJumpCooldown);
        float maximum = Mathf.Max(minimum, maxJumpCooldown);
        nextJumpTime = Time.time + Random.Range(minimum, maximum);
    }
}
