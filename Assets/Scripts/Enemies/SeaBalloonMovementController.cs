using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// First SeaBalloon phase: it travels submerged, therefore cannot receive
/// damage, and periodically stops to play its lookout animation.
/// </summary>
public sealed class SeaBalloonMovementController : JunctionTurningEnemyMovementController
{
    [Header("SeaBalloon Visuals")]
    [SerializeField] private AnimatedSpriteRenderer movementAnimation;
    [SerializeField] private AnimatedSpriteRenderer lookAnimation;
    [SerializeField] private AnimatedSpriteRenderer emergeAnimation;
    [SerializeField] private AnimatedSpriteRenderer surfaceWalkAnimation;
    [SerializeField] private SpriteRenderer shadowRenderer;

    [Header("Lookout")]
    [SerializeField, Min(0.01f)] private float movementBeforeLookSeconds = 10f;
    [SerializeField, Min(0.01f)] private float lookDuration = 0.5f;

    [Header("Emergence")]
    [SerializeField, Range(0f, 1f)] private float emergeChance = 0.5f;
    [SerializeField, Min(0.01f)] private float emergeDuration = 2f;
    [SerializeField, Min(0.01f)] private float surfaceWalkFrameDuration = 0.125f;
    [SerializeField, Min(0.01f)] private float surfaceRiseOrFallDuration = 0.25f;
    [SerializeField, Min(0f)] private float surfaceHoverHeight = 1f;
    [SerializeField, Min(0.01f)] private float surfaceWalkBeforeSubmergeSeconds = 10f;

    [Header("Submerged Teleport")]
    [SerializeField, Min(0.01f)] private float hiddenDuration = 1f;
    [SerializeField, Min(1)] private int teleportRangeTiles = 3;
    [SerializeField, Min(0f)] private float playerClearRadiusTiles = 2f;
    [SerializeField, Min(0.01f)] private float teleportRetrySeconds = 0.25f;
    [SerializeField] private LayerMask playerLayerMask;

    private CharacterHealth seaBalloonHealth;
    private Collider2D seaBalloonCollider;
    private Tilemap groundTilemap;
    private Tilemap destructiblesTilemap;
    private Tilemap indestructiblesTilemap;
    private Coroutine lookRoutine;
    private Coroutine surfaceWalkRoutine;
    private Coroutine returnToSubmergedRoutine;
    private bool looking;
    private bool emerging;
    private bool surfaceWalking;
    private int currentSurfaceWalkFrame;
    private float movementElapsed;
    private float surfaceWalkElapsed;

    protected override void Awake()
    {
        base.Awake();
        seaBalloonCollider = GetComponent<Collider2D>();

        if (playerLayerMask.value == 0)
            playerLayerMask = LayerMask.GetMask("Player");
    }

    protected override void Start()
    {
        base.Start();
        seaBalloonHealth = GetComponent<CharacterHealth>();
        SetSubmergedInvulnerability(true);

        activeSprite = movementAnimation != null ? movementAnimation : spriteDown;
        SetVisualEnabled(movementAnimation, true);
        SetVisualEnabled(lookAnimation, false);
        SetVisualEnabled(emergeAnimation, false);
        SetVisualEnabled(surfaceWalkAnimation, false);
        SetShadowVisible(false);
    }

    protected override void FixedUpdate()
    {
        if (!isDead && (looking || (!emerging && !surfaceWalking)))
            SetSubmergedInvulnerability(true);

        if (looking || emerging)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            return;
        }

        base.FixedUpdate();
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (looking || emerging || surfaceWalking)
            return;

        base.UpdateSpriteDirection(dir);
    }

    private void LateUpdate()
    {
        if (isDead || isInDamagedLoop || looking || emerging)
            return;

        if (surfaceWalking)
        {
            surfaceWalkElapsed += Time.deltaTime;
            if (surfaceWalkElapsed >= surfaceWalkBeforeSubmergeSeconds &&
                currentSurfaceWalkFrame != 2 &&
                returnToSubmergedRoutine == null)
            {
                SetSubmergedInvulnerability(true);
                returnToSubmergedRoutine = StartCoroutine(ReturnToSubmergedRoutine());
            }

            return;
        }

        movementElapsed += Time.deltaTime;
        if (movementElapsed >= movementBeforeLookSeconds)
            lookRoutine = StartCoroutine(LookRoutine());
    }

    protected override void Die()
    {
        StopLook();
        StopSurfaceWalk();
        StopReturnToSubmerged();
        SetSubmergedInvulnerability(false);
        base.Die();
    }

    protected override void OnDestroy()
    {
        StopLook();
        StopSurfaceWalk();
        StopReturnToSubmerged();
        base.OnDestroy();
    }

    private IEnumerator LookRoutine()
    {
        looking = true;
        SetSubmergedInvulnerability(true);
        SetCollisionEnabled(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            targetTile = rb.position;
        }

        if (Random.value < emergeChance)
        {
            yield return EmergeRoutine();
            yield break;
        }

        yield return PlayLookAnimation();

        if (isDead)
            yield break;

        HideForTeleport();
        yield return new WaitForSeconds(hiddenDuration);

        Vector2 destination = default;
        while (!isDead && !TryFindTeleportDestination(out destination))
            yield return new WaitForSeconds(teleportRetrySeconds);

        if (isDead)
            yield break;

        MoveToTeleportDestination(destination);
        yield return PlayLookAnimation();

        if (!isDead)
            FinishLook();
    }

    private IEnumerator PlayLookAnimation(bool reverse = false)
    {
        SetVisualEnabled(movementAnimation, false);
        SetVisualEnabled(lookAnimation, true);
        if (lookAnimation == null)
        {
            yield return new WaitForSeconds(lookDuration);
            yield break;
        }

        activeSprite = lookAnimation;
        lookAnimation.SetManualAnimationUpdate(true);
        lookAnimation.loop = false;
        lookAnimation.idle = false;

        Sprite[] frames = lookAnimation.animationSprite;
        if (frames == null || frames.Length == 0)
        {
            yield return new WaitForSeconds(lookDuration);
            lookAnimation.SetManualAnimationUpdate(false);
            yield break;
        }

        float frameDuration = lookDuration / frames.Length;
        int first = reverse ? frames.Length - 1 : 0;
        int last = reverse ? 0 : frames.Length - 1;
        int increment = reverse ? -1 : 1;
        for (int frame = first; ; frame += increment)
        {
            lookAnimation.CurrentFrame = frame;
            lookAnimation.RefreshFrame();
            yield return new WaitForSeconds(frameDuration);
            if (frame == last)
                break;
        }

        lookAnimation.SetManualAnimationUpdate(false);
    }

    private void FinishLook()
    {
        SetSubmergedInvulnerability(true);
        SetVisualEnabled(lookAnimation, false);
        activeSprite = movementAnimation != null ? movementAnimation : spriteDown;
        SetVisualEnabled(activeSprite, true);
        if (activeSprite != null)
        {
            activeSprite.RestartAnimation();
            activeSprite.idle = false;
        }

        SetCollisionEnabled(true);
        looking = false;
        lookRoutine = null;
        movementElapsed = 0f;
        DecideNextTile();
    }

    private IEnumerator EmergeRoutine()
    {
        emerging = true;
        SetSubmergedInvulnerability(false);
        SetCollisionEnabled(true);
        SetVisualEnabled(movementAnimation, false);
        SetVisualEnabled(lookAnimation, false);
        SetVisualEnabled(surfaceWalkAnimation, false);
        SetShadowVisible(false);

        if (emergeAnimation != null)
        {
            activeSprite = emergeAnimation;
            emergeAnimation.SetManualAnimationUpdate(true);
            emergeAnimation.idle = false;
            emergeAnimation.loop = false;

            Sprite[] frames = emergeAnimation.animationSprite;
            if (frames != null && frames.Length > 0)
            {
                SetVisualEnabled(emergeAnimation, true);
                float frameDuration = emergeDuration / frames.Length;
                for (int frame = 0; frame < frames.Length; frame++)
                {
                    emergeAnimation.CurrentFrame = frame;
                    emergeAnimation.RefreshFrame();
                    yield return new WaitForSeconds(frameDuration);
                }
            }
            else
            {
                yield return new WaitForSeconds(emergeDuration);
            }

            emergeAnimation.SetManualAnimationUpdate(false);
        }
        else
        {
            yield return new WaitForSeconds(emergeDuration);
        }

        if (!isDead)
            BeginSurfaceWalk();
    }

    private void BeginSurfaceWalk()
    {
        emerging = false;
        looking = false;
        lookRoutine = null;
        SetSubmergedInvulnerability(false);
        SetCollisionEnabled(true);
        SetVisualEnabled(emergeAnimation, false);
        SetVisualEnabled(movementAnimation, false);
        SetVisualEnabled(lookAnimation, false);
        SetVisualEnabled(surfaceWalkAnimation, true);

        activeSprite = surfaceWalkAnimation != null ? surfaceWalkAnimation : movementAnimation;
        surfaceWalking = true;
        currentSurfaceWalkFrame = 0;
        surfaceWalkElapsed = 0f;
        if (surfaceWalkRoutine != null)
            StopCoroutine(surfaceWalkRoutine);
        surfaceWalkRoutine = StartCoroutine(SurfaceWalkAnimationRoutine());
        DecideNextTile();
    }

    private IEnumerator SurfaceWalkAnimationRoutine()
    {
        if (surfaceWalkAnimation == null)
            yield break;

        Sprite[] frames = surfaceWalkAnimation.animationSprite;
        if (frames == null || frames.Length < 3)
            yield break;

        surfaceWalkAnimation.SetManualAnimationUpdate(true);
        surfaceWalkAnimation.idle = false;
        surfaceWalkAnimation.loop = false;
        while (surfaceWalking && !isDead)
        {
            ShowSurfaceFrame(0);
            yield return new WaitForSeconds(surfaceWalkFrameDuration);
            ShowSurfaceFrame(1);
            yield return new WaitForSeconds(surfaceWalkFrameDuration);

            ShowSurfaceFrame(2);
            SetShadowVisible(true);
            yield return HoverForSurfaceWalkFrame();
            surfaceWalkAnimation.ClearExternalBase();
            SetShadowVisible(false);
        }
    }

    private IEnumerator HoverForSurfaceWalkFrame()
    {
        float halfDuration = surfaceRiseOrFallDuration;
        float fullDuration = halfDuration * 2f;
        float elapsed = 0f;
        while (elapsed < fullDuration)
        {
            float normalized = elapsed / fullDuration;
            float height = Mathf.Sin(normalized * Mathf.PI) * surfaceHoverHeight * tileSize;
            float pixelStep = tileSize / 16f;
            if (pixelStep > 0f)
                height = Mathf.Round(height / pixelStep) * pixelStep;
            surfaceWalkAnimation.SetExternalBaseOffsetFromInitial(Vector3.up * height);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void ShowSurfaceFrame(int frame)
    {
        if (surfaceWalkAnimation == null)
            return;

        surfaceWalkAnimation.CurrentFrame = frame;
        surfaceWalkAnimation.RefreshFrame();
        currentSurfaceWalkFrame = frame;
        SetShadowVisible(frame == 2);
    }

    private void StopLook()
    {
        if (lookRoutine != null)
            StopCoroutine(lookRoutine);

        lookRoutine = null;
        looking = false;
        emerging = false;
        SetVisualEnabled(lookAnimation, false);
        SetVisualEnabled(emergeAnimation, false);
        SetShadowVisible(false);
    }

    private void StopSurfaceWalk()
    {
        if (surfaceWalkRoutine != null)
            StopCoroutine(surfaceWalkRoutine);

        surfaceWalkRoutine = null;
        surfaceWalking = false;
        if (surfaceWalkAnimation != null)
            surfaceWalkAnimation.ClearExternalBase();
        SetVisualEnabled(surfaceWalkAnimation, false);
        SetShadowVisible(false);
    }

    private IEnumerator ReturnToSubmergedRoutine()
    {
        SetSubmergedInvulnerability(true);
        StopSurfaceWalk();
        looking = true;
        SetCollisionEnabled(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            SnapToGrid();
            targetTile = rb.position;
        }

        yield return PlayLookAnimation();

        if (!isDead)
            FinishLook();

        returnToSubmergedRoutine = null;
    }

    private void StopReturnToSubmerged()
    {
        if (returnToSubmergedRoutine != null)
            StopCoroutine(returnToSubmergedRoutine);

        returnToSubmergedRoutine = null;
    }

    private void HideForTeleport()
    {
        SetVisualEnabled(movementAnimation, false);
        SetVisualEnabled(lookAnimation, false);
        SetShadowVisible(false);
        activeSprite = null;
    }

    private bool TryFindTeleportDestination(out Vector2 destination)
    {
        ResolveTilemaps();
        if (groundTilemap == null)
        {
            destination = default;
            return false;
        }

        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector3Int originCell = groundTilemap.WorldToCell(origin);
        var candidates = new List<Vector2>();

        for (int x = -teleportRangeTiles; x <= teleportRangeTiles; x++)
        {
            for (int y = -teleportRangeTiles; y <= teleportRangeTiles; y++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);
                if (distance == 0 || distance > teleportRangeTiles)
                    continue;

                Vector3Int cell = originCell + new Vector3Int(x, y, 0);
                if (!groundTilemap.HasTile(cell) ||
                    HasTileAt(destructiblesTilemap, cell) ||
                    HasTileAt(indestructiblesTilemap, cell))
                    continue;

                Vector2 candidate = groundTilemap.GetCellCenterWorld(cell);
                if (!HasPlayerNearby(candidate))
                    candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            destination = default;
            return false;
        }

        destination = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    private void MoveToTeleportDestination(Vector2 destination)
    {
        transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        if (rb != null)
        {
            rb.position = destination;
            rb.linearVelocity = Vector2.zero;
        }

        targetTile = destination;
    }

    private bool HasPlayerNearby(Vector2 position)
    {
        if (playerLayerMask.value == 0)
            return false;

        float radius = playerClearRadiusTiles * tileSize;
        return Physics2D.OverlapCircle(position, radius, playerLayerMask) != null;
    }

    private void ResolveTilemaps()
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

    private static bool HasTileAt(Tilemap tilemap, Vector3Int cell)
        => tilemap != null && tilemap.HasTile(cell);

    private void SetCollisionEnabled(bool enabled)
    {
        if (seaBalloonCollider != null)
            seaBalloonCollider.enabled = enabled;
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.gameObject.SetActive(visible);
            shadowRenderer.enabled = visible;
        }
    }

    private void SetSubmergedInvulnerability(bool value)
    {
        if (seaBalloonHealth != null)
            seaBalloonHealth.SetExternalInvulnerability(value);
    }

    private static void SetVisualEnabled(AnimatedSpriteRenderer animation, bool enabled)
    {
        if (animation == null)
            return;

        animation.enabled = enabled;
        if (animation.TryGetComponent(out SpriteRenderer renderer))
            renderer.enabled = enabled;
    }
}
