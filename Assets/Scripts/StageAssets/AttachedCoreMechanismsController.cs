using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Keeps the same room objective alive from attachment through awakening.</summary>
[DefaultExecutionOrder(100)]
public sealed class AttachedCoreMechanismsController : MonoBehaviour
{
    [System.Serializable]
    public sealed class AnimationAttachment
    {
        public AnimatedSpriteRenderer animation;
        public bool mirrored;
        public Vector2[] frameOffsets = new Vector2[0];
    }

    public EnemyMovementController host;
    public SpriteRenderer attachedRenderer;
    public Sprite[] attachedFrames;
    public Sprite[] wakeUpFrames;
    public Sprite flightSprite;
    public SpriteRenderer shadowRenderer;
    public Vector3 backOffset = new Vector3(0f, 0.5f, 0f);
    public float frameSeconds = 0.07f;

    [Header("Attachment offsets per enemy animation frame (pixels)")]
    [Tooltip("Added to Back Offset. Element index matches the enemy's walking animation frame, including repeated frames.")]
    public Vector2[] upFrameOffsets = new Vector2[0];
    public Vector2[] downFrameOffsets = new Vector2[0];
    public Vector2[] leftFrameOffsets = new Vector2[0];
    public Vector2[] rightFrameOffsets = new Vector2[0];
    [Header("Additional host animations (attack, etc.)")]
    public AnimationAttachment[] additionalAnimations = new AnimationAttachment[0];
    [Min(1f)] public float pixelsPerUnit = 16f;

    [Header("Disconnect jump")]
    [Min(0.01f)] public float riseSeconds = 0.4f;
    [Tooltip("Final slow approach to the apex, after the initial rise.")]
    [Min(0f)] public float apexApproachSeconds = 0.1f;
    [Min(0.01f)] public float jumpHeightTiles = 2f;
    [Min(0.01f)] public float fallSeconds = 0.5f;
    [Min(0f)] public float landingHoldSeconds = 0.3f;
    [Min(0.01f)] public float wakeUpSeconds = 0.25f;

    CoreMechanismsDestructible core;
    CharacterHealth hostHealth;
    AnimatedSpriteRenderer normalAnimation;
    Collider2D[] coreColliders;
    Vector3 landingPosition;
    Vector3 flightOrigin;
    float routeStartTime;
    Collider2D roomBounds;
    bool[] originalTriggerStates;
    bool waitingForPlayerExit;
    Tilemap[] landingTilemaps;
    float jumpHeight;
    float elapsed;
    bool detached;
    bool finished;

    float TotalRiseSeconds => Mathf.Max(0.01f, riseSeconds) + Mathf.Max(0f, apexApproachSeconds);

    public bool IsGrounded => detached && elapsed >= TotalRiseSeconds + Mathf.Max(0.01f, fallSeconds);

    public Vector3 GroundPosition => detached ? landingPosition : host != null ? host.transform.position : transform.position;

    void OnEnable() => RefreshAttachedPose();

    /// <summary>Prepares the attached visual even while a room fade has frozen gameplay.</summary>
    public void RefreshAttachedPose()
    {
        if (detached || finished || host == null)
            return;
        Vector3 hover = host is SubmarineBubbleMovementController submarine ? submarine.AttachmentHoverOffset : Vector3.zero;
        transform.position = SnapToPixels(host.transform.position + GetAttachmentOffset() + hover);
        if (attachedRenderer != null && attachedFrames != null && attachedFrames.Length > 0)
            attachedRenderer.sprite = attachedFrames[(int)(elapsed / Mathf.Max(0.01f, frameSeconds)) % attachedFrames.Length];
    }

    void Start()
    {
        core = GetComponent<CoreMechanismsDestructible>();
        if (host != null)
            hostHealth = host.GetComponent<CharacterHealth>();
        Transform animationChild = transform.Find("Animation");
        normalAnimation = animationChild != null ? animationChild.GetComponent<AnimatedSpriteRenderer>() : null;
        coreColliders = GetComponents<Collider2D>();
        originalTriggerStates = new bool[coreColliders.Length];
        for (int i = 0; i < coreColliders.Length; i++)
            originalTriggerStates[i] = coreColliders[i].isTrigger;
        SetNormal(false);
        SetColliders(false);
        if (attachedRenderer != null)
            attachedRenderer.enabled = true;
        RefreshAttachedPose();
    }

    void LateUpdate()
    {
        UpdatePlayerExit();
        if (finished || core == null)
            return;
        if (core.IsDying)
        {
            transform.SetParent(null, true);
            if (attachedRenderer != null)
                attachedRenderer.enabled = false;
            if (shadowRenderer != null)
                shadowRenderer.enabled = false;
            finished = true;
            return;
        }
        if (!detached && (host == null || (hostHealth != null && hostHealth.IsDead)))
            Detach();
        if (!detached)
            RefreshAttachedPose();
        if (GamePauseController.IsPaused || Time.timeScale == 0f)
            return;
        elapsed += Time.deltaTime;
        if (!detached)
        {
            RefreshAttachedPose();
            return;
        }
        float rise = TotalRiseSeconds;
        float fall = Mathf.Max(0.01f, fallSeconds);
        if (elapsed < rise + fall)
        {
            if (!IsGroundOnlyAt(landingPosition))
            {
                float previousProgress = Mathf.Clamp01((elapsed - routeStartTime) / Mathf.Max(0.01f, rise + fall - routeStartTime));
                flightOrigin = Vector3.Lerp(flightOrigin, landingPosition, previousProgress);
                landingPosition = FindLandingPosition(landingPosition);
                routeStartTime = elapsed;
            }
            float height;
            if (elapsed < rise)
            {
                float t = Mathf.Clamp01(elapsed / rise);
                // A sine arc spreads deceleration across the entire ascent.
                // Unlike cubic ease-out, it keeps moving through the final pixels.
                height = jumpHeight * Mathf.Sin(t * Mathf.PI * 0.5f);
            }
            else
            {
                float t = Mathf.Clamp01((elapsed - rise) / fall);
                height = jumpHeight * (1f - t * t);
            }
            transform.position = landingPosition;
            if (attachedRenderer != null)
            {
                float progress = Mathf.Clamp01((elapsed - routeStartTime) / Mathf.Max(0.01f, rise + fall - routeStartTime));
                Vector3 groundPath = Vector3.Lerp(flightOrigin, landingPosition, progress);
                attachedRenderer.transform.position = SnapToPixels(groundPath + GetGroundVisualOffset() + Vector3.up * height);
                attachedRenderer.sprite = flightSprite;
            }
            return;
        }
        if (!IsGroundOnlyAt(landingPosition))
        {
            landingPosition = FindLandingPosition(landingPosition);
            if (!IsGroundOnlyAt(landingPosition))
                return;
        }
        transform.position = landingPosition;
        if (attachedRenderer != null)
            attachedRenderer.transform.localPosition = GetGroundVisualOffset();
        if (shadowRenderer != null)
            shadowRenderer.enabled = false;
        float wakeElapsed = elapsed - rise - fall - Mathf.Max(0f, landingHoldSeconds);
        if (wakeElapsed < 0f)
        {
            if (attachedRenderer != null)
                attachedRenderer.sprite = flightSprite;
            if (shadowRenderer != null)
                shadowRenderer.enabled = true;
            return;
        }
        float wakeDuration = Mathf.Max(0.01f, wakeUpSeconds);
        if (wakeUpFrames != null && wakeUpFrames.Length > 0 && wakeElapsed < wakeDuration)
        {
            int frame = Mathf.Min(wakeUpFrames.Length - 1, (int)(wakeElapsed / wakeDuration * wakeUpFrames.Length));
            if (attachedRenderer != null)
                attachedRenderer.sprite = wakeUpFrames[frame];
            return;
        }
        transform.position = landingPosition;
        if (attachedRenderer != null)
            attachedRenderer.enabled = false;
        SetNormal(true);
        SetColliders(true);
        BeginPlayerExit();
        finished = true;
    }

    public void Detach()
    {
        if (detached || finished)
            return;
        float tileSize = host != null ? Mathf.Max(0.01f, host.tileSize) : 1f;
        jumpHeight = tileSize * Mathf.Max(0.01f, jumpHeightTiles);
        landingPosition = host != null ? host.transform.position : transform.position - backOffset;
        landingPosition.x = Mathf.Round(landingPosition.x / tileSize) * tileSize;
        landingPosition.y = Mathf.Round(landingPosition.y / tileSize) * tileSize;
        flightOrigin = landingPosition;
        roomBounds = StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(landingPosition);
        if (!IsGroundOnlyAt(landingPosition))
            landingPosition = FindLandingPosition(landingPosition);
        // Preserve room-root ownership when the enemy hierarchy is removed.
        transform.SetParent(host != null ? host.transform.parent : null, true);
        transform.position = landingPosition;
        if (attachedRenderer != null)
        {
            attachedRenderer.transform.position = SnapToPixels(flightOrigin + GetGroundVisualOffset());
            attachedRenderer.sprite = flightSprite;
        }
        if (shadowRenderer != null)
        {
            shadowRenderer.transform.localPosition = GetGroundVisualOffset();
            shadowRenderer.enabled = true;
        }
        detached = true;
        elapsed = 0f;
        routeStartTime = 0f;
    }

    bool IsGroundOnlyAt(Vector3 position)
    {
        Tilemap ground = GameManager.Instance != null ? GameManager.Instance.groundTilemap : null;
        if (ground == null || !ground.HasTile(ground.WorldToCell(position)))
            return false;
        if (landingTilemaps == null)
        {
            Transform stage = ground.transform.parent != null ? ground.transform.parent : ground.transform;
            landingTilemaps = stage.GetComponentsInChildren<Tilemap>(true);
        }
        foreach (Tilemap map in landingTilemaps)
            if (map != null && map != ground && map.gameObject.activeInHierarchy && map.HasTile(map.WorldToCell(position)))
                return false;
        foreach (Collider2D obstacle in Physics2D.OverlapBoxAll(position, Vector2.one * 0.7f, 0f, LayerMask.GetMask("Stage", "Bomb")))
        {
            if (obstacle.isTrigger || obstacle.transform.IsChildOf(transform) ||
                (host != null && obstacle.transform.IsChildOf(host.transform)) ||
                obstacle.GetComponentInParent<Tilemap>() == ground)
                continue;
            return false;
        }
        return true;
    }

    Vector3 FindLandingPosition(Vector3 origin)
    {
        GameManager manager = GameManager.Instance;
        Tilemap ground = manager != null ? manager.groundTilemap : null;
        if (ground == null)
            return origin;
        Vector3 nearest = origin;
        float nearestDistance = float.PositiveInfinity;
        foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
        {
            if (!ground.HasTile(cell))
                continue;
            Vector3 candidate = ground.GetCellCenterWorld(cell);
            candidate.z = origin.z;
            float distance = (candidate - origin).sqrMagnitude;
            if (distance >= nearestDistance || (roomBounds != null && !roomBounds.OverlapPoint(candidate)) ||
                !IsGroundOnlyAt(candidate))
                continue;
            nearest = candidate;
            nearestDistance = distance;
        }
        return nearest;
    }

    bool HasOverlappingPlayer()
    {
        foreach (Collider2D collider in coreColliders)
        {
            Bounds bounds = collider.bounds;
            foreach (Collider2D other in Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, LayerMask.GetMask("Player")))
                if (other.GetComponentInParent<MovementController>() != null)
                    return true;
        }
        return false;
    }

    void BeginPlayerExit()
    {
        Physics2D.SyncTransforms();
        if (!HasOverlappingPlayer())
            return;
        waitingForPlayerExit = true;
        foreach (Collider2D collider in coreColliders)
            collider.isTrigger = true;
    }

    void UpdatePlayerExit()
    {
        if (!waitingForPlayerExit || coreColliders == null || HasOverlappingPlayer())
            return;
        for (int i = 0; i < coreColliders.Length; i++)
            coreColliders[i].isTrigger = originalTriggerStates[i];
        waitingForPlayerExit = false;
    }

    Vector3 GetGroundVisualOffset()
    {
        Transform normal = transform.Find("Animation");
        return normal != null ? normal.localPosition : new Vector3(0f, 0.25f, 0f);
    }

    Vector3 SnapToPixels(Vector3 position)
    {
        float ppu = Mathf.Max(1f, pixelsPerUnit);
        position.x = Mathf.Round(position.x * ppu) / ppu;
        position.y = Mathf.Round(position.y * ppu) / ppu;
        return position;
    }

    Vector3 GetAttachmentOffset()
    {
        foreach (AnimationAttachment entry in additionalAnimations)
        {
            if (entry == null || entry.animation == null || !entry.animation.enabled)
                continue;
            SpriteRenderer renderer = entry.animation.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.flipX != entry.mirrored)
                continue;
            int frame = entry.animation.CurrentFrame;
            return entry.frameOffsets != null && frame < entry.frameOffsets.Length
                ? backOffset + (Vector3)(entry.frameOffsets[frame] / Mathf.Max(1f, pixelsPerUnit)) : backOffset;
        }
        AnimatedSpriteRenderer animation = host.spriteDown;
        Vector2[] offsets = downFrameOffsets;
        if (host.spriteUp != null && host.spriteUp.enabled)
        {
            animation = host.spriteUp;
            offsets = upFrameOffsets;
        }
        else if (host.spriteRight != null && host.spriteRight.enabled)
        {
            animation = host.spriteRight;
            offsets = rightFrameOffsets;
        }
        else if (host.spriteLeft != null && host.spriteLeft.enabled)
        {
            animation = host.spriteLeft;
            var renderer = animation.GetComponent<SpriteRenderer>();
            offsets = renderer != null && renderer.flipX ? rightFrameOffsets : leftFrameOffsets;
        }
        if (animation == null || offsets == null || animation.CurrentFrame >= offsets.Length)
            return backOffset;
        return backOffset + (Vector3)(offsets[animation.CurrentFrame] / Mathf.Max(1f, pixelsPerUnit));
    }

    void SetNormal(bool visible)
    {
        if (normalAnimation == null)
            return;
        normalAnimation.enabled = visible;
        normalAnimation.GetComponent<SpriteRenderer>().enabled = visible;
    }

    void SetColliders(bool value)
    {
        if (coreColliders == null)
            return;
        foreach (Collider2D collider in coreColliders)
            collider.enabled = value;
    }
}
