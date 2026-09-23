using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class SnowManMovementController : JunctionTurningEnemyMovementController
{
    private const float JumpDuration = 1f;
    private const float JumpHeightInTiles = 3f;
    private const int SpinCount = 2;
    private static readonly Vector2[] SpinDirections =
        { Vector2.down, Vector2.right, Vector2.up, Vector2.left };

    private AnimatedSpriteRenderer[] walkingSprites;
    private Collider2D[] jumpColliders;
    private bool[] colliderStates;
    private Tilemap ground;
    private Tilemap[] tilemaps;
    private CharacterHealth jumpHealth;
    private bool jumping;
    private Vector2 jumpStart;
    private Vector2 jumpEnd;
    private float jumpStartedAt;
    private int spinStart;

    private bool CanBeginJump => isActiveAndEnabled && tilemaps != null && !isDead &&
        !jumping && !isInDamagedLoop && !GamePauseController.IsPaused &&
        direction != Vector2.zero &&
        !(TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned);

    protected override void Awake()
    {
        base.Awake();
        walkingSprites = new[] { spriteDown, spriteLeft, spriteUp, spriteRight };
        jumpColliders = GetComponentsInChildren<Collider2D>(true);
        colliderStates = new bool[jumpColliders.Length];
        jumpHealth = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        BombController.BombDetonating += OnBombDetonating;
    }

    private void OnDisable()
    {
        BombController.BombDetonating -= OnBombDetonating;
        if (jumping)
        {
            EndJump();
            SnapToGrid();
            targetTile = rb.position;
            UpdateSpriteDirection(direction);
        }
    }

    protected override void Start()
    {
        base.Start();
        tilemaps = FindObjectsByType<Tilemap>();
        ground = GameManager.Instance != null ? GameManager.Instance.groundTilemap : null;
        foreach (Tilemap map in tilemaps)
            if (ground == null && map.name == "Ground")
                ground = map;
    }

    private void OnBombDetonating(Vector2 origin, int radius)
    {
        if (!CanBeginJump)
            return;

        Vector2 delta = origin - rb.position;
        float forward = Vector2.Dot(delta, direction);
        float sideways = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        if (forward <= 0f || forward > (radius + 0.5f) * tileSize || sideways > tileSize * 0.4f)
            return;

        // Do not react to a bomb hidden behind stage geometry.
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(rb.position, origin, LayerMask.GetMask("Stage")))
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                return;

        TryBeginJump();
    }

    private bool TryBeginJump()
    {
        Vector2 gridStart = new(
            Mathf.Round(rb.position.x / tileSize) * tileSize,
            Mathf.Round(rb.position.y / tileSize) * tileSize);
        Vector2 destination = gridStart + direction * (2f * tileSize);
        if (!IsLandingAvailable(destination))
            return false;

        jumpStart = rb.position;
        jumpEnd = destination;
        jumpStartedAt = Time.time;
        jumping = true;
        isStuck = false;
        rb.linearVelocity = Vector2.zero;
        jumpHealth.SetExternalInvulnerability(true);
        for (int i = 0; i < jumpColliders.Length; i++)
        {
            colliderStates[i] = jumpColliders[i] != null && jumpColliders[i].enabled;
            if (jumpColliders[i] != null)
                jumpColliders[i].enabled = false;
        }
        spinStart = System.Array.IndexOf(SpinDirections, direction);
        foreach (AnimatedSpriteRenderer visual in walkingSprites)
            if (visual != null)
                visual.SetManualAnimationUpdate(true);
        ShowJump(0f);
        return true;
    }

    private bool IsLandingAvailable(Vector2 destination)
    {
        if (ground == null || !ground.HasTile(ground.WorldToCell(destination)))
            return false;

        foreach (Tilemap map in tilemaps)
        {
            if (map == null || map == ground)
                continue;
            bool blockedMap = map.name == "Destructibles" || map.name == "Indestructibles" ||
                map.name.IndexOf("hole", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                map.gameObject.tag == "Hole";
            if (blockedMap && map.HasTile(map.WorldToCell(destination)))
            {
                return false;
            }
        }

        foreach (Collider2D hit in Physics2D.OverlapBoxAll(destination, Vector2.one * (tileSize * 0.8f), 0f))
        {
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;
            Bomb bomb = hit.GetComponentInParent<Bomb>();
            if (bomb != null && bomb.HasExploded)
                continue;
            int layer = 1 << hit.gameObject.layer;
            if ((layer & (obstacleMask.value | bombLayerMask.value | enemyLayerMask.value)) != 0 ||
                hit.GetComponentInParent<CoreMechanismsDestructible>() != null)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsExplosionAhead(Collider2D explosion)
    {
        // An overlap with the next tile alone can also find a flame that is
        // beside or behind us. Require its actual center to be in our path.
        Vector2 delta = (Vector2)explosion.bounds.center - rb.position;
        float forward = Vector2.Dot(delta, direction);
        float sideways = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        if (forward <= tileSize * 0.1f || sideways > tileSize * 0.4f)
            return false;

        // This is preventive evasion, not a rescue from a flame already
        // touching the body (including a blast arriving from the side/rear).
        foreach (Collider2D body in jumpColliders)
            if (body != null && body.enabled && body.gameObject.activeInHierarchy &&
                body.Distance(explosion).isOverlapped)
                return false;

        return true;
    }

    protected override void FixedUpdate()
    {
        if (!jumping)
        {
            // Inspect the actual navigation target before the base controller
            // issues MovePosition, including explosions already active at spawn.
            if (CanBeginJump && Vector2.Dot(targetTile - rb.position, direction) > 0.01f)
            {
                Collider2D[] explosions = Physics2D.OverlapBoxAll(targetTile,
                    Vector2.one * (tileSize * 0.8f), 0f, LayerMask.GetMask("Explosion"));
                foreach (Collider2D explosion in explosions)
                {
                    if (explosion == null || !IsExplosionAhead(explosion))
                        continue;
                    TryBeginJump();
                    // If the landing is blocked, wait outside the flame and retry.
                    rb.linearVelocity = Vector2.zero;
                    return;
                }
            }
            base.FixedUpdate();
            return;
        }
        if (GamePauseController.IsPaused)
            return;

        float progress = Mathf.Clamp01((Time.time - jumpStartedAt) / JumpDuration);
        rb.MovePosition(Vector2.Lerp(jumpStart, jumpEnd, progress));
        ShowJump(progress);
        if (progress < 1f)
            return;

        rb.position = jumpEnd;
        EndJump();
        UpdateSpriteDirection(direction);
        DecideNextTile();
    }

    private void ShowJump(float progress)
    {
        foreach (AnimatedSpriteRenderer visual in walkingSprites)
            if (visual != null)
                visual.ClearExternalBase();
        int phaseCount = SpinDirections.Length * SpinCount;
        int phase = Mathf.Min(phaseCount - 1, Mathf.FloorToInt(progress * phaseCount));
        UpdateSpriteDirection(SpinDirections[(spinStart + phase) % SpinDirections.Length]);
        if (activeSprite == null)
            return;
        activeSprite.CurrentFrame = 0;
        activeSprite.RefreshFrame();
        activeSprite.SetExternalBaseOffsetFromInitial(Vector3.up *
            (Mathf.Sin(progress * Mathf.PI) * JumpHeightInTiles * tileSize));
    }

    private void EndJump()
    {
        jumping = false;
        jumpHealth.SetExternalInvulnerability(false);
        foreach (AnimatedSpriteRenderer visual in walkingSprites)
        {
            if (visual == null)
                continue;
            visual.ClearExternalBase();
            visual.SetManualAnimationUpdate(false);
        }
        for (int i = 0; i < jumpColliders.Length; i++)
            if (jumpColliders[i] != null)
                jumpColliders[i].enabled = colliderStates[i];
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!jumping)
            base.OnTriggerEnter2D(other);
    }

    protected override void Die()
    {
        if (jumping)
            EndJump();
        base.Die();
    }
}
