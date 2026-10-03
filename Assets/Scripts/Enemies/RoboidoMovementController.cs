using System.Collections.Generic;
using UnityEngine;

public sealed class RoboidoMovementController : JunctionTurningEnemyMovementController
{
    [Header("Roboido")]
    [SerializeField] private AnimatedSpriteRenderer wakeUp;
    [SerializeField] private AnimatedSpriteRenderer attackUp;
    [SerializeField] private AnimatedSpriteRenderer attackDown;
    [SerializeField] private AnimatedSpriteRenderer attackLeft;
    [SerializeField, Range(0f, 1f)] private float pursuitChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float attackChance = 0.2f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 2f;
    [SerializeField] private AudioClip destructionSfx;
    [SerializeField, Min(1f)] private float destructionSfxGain = 3f;

    private enum State { Sleeping, Waking, Walking, Attacking }
    private State state;
    private GameManager subscribedManager;
    private AnimatedSpriteRenderer sequence;
    private int sequenceFrame;
    private float frameElapsed;
    private float cooldownRemaining;
    private readonly List<Bomb> struckBombs = new();

    protected override void Awake()
    {
        base.Awake();
        SetWakeProtection(true);
        HideSpecialSprites();
        HideWalkingSprites();
        SetVisible(wakeUp, true);
        if (wakeUp != null)
        {
            wakeUp.idle = true;
            wakeUp.RefreshFrame();
            activeSprite = wakeUp;
        }
    }

    protected override void Start()
    {
        SnapToGrid();
        direction = Vector2.down;
        targetTile = rb.position;
        SubscribeToBlocks();
    }

    private void SubscribeToBlocks()
    {
        GameManager manager = GameManager.Instance;
        if (manager == subscribedManager) return;
        if (subscribedManager != null) subscribedManager.DestructibleDestroyed -= OnBlockDestroyed;
        subscribedManager = manager;
        if (subscribedManager != null) subscribedManager.DestructibleDestroyed += OnBlockDestroyed;
    }

    private void OnBlockDestroyed(Vector3Int cell)
    {
        if (state != State.Sleeping || subscribedManager == null || subscribedManager.destructibleTilemap == null) return;
        Vector3Int ownCell = subscribedManager.destructibleTilemap.WorldToCell(rb.position);
        Vector3Int delta = cell - ownCell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && delta.z == 0) Wake();
    }

    private void Wake()
    {
        if (isDead || state != State.Sleeping) return;
        state = State.Waking;
        BeginSequence(wakeUp);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (state == State.Sleeping) { Wake(); return; }
            if (state == State.Waking) return;
            base.OnTriggerEnter2D(other);
            return;
        }
        if (state == State.Walking) base.OnTriggerEnter2D(other);
    }

    protected override void FixedUpdate()
    {
        if (isDead || GamePauseController.IsPaused) return;
        if (isInDamagedLoop || (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.fixedDeltaTime);
        if (state == State.Sleeping)
        {
            rb.linearVelocity = Vector2.zero;
            SubscribeToBlocks();
            foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
            {
                if (!IsLivingPlayer(player)) continue;
                Vector2 offset = (Vector2)player.transform.position - rb.position;
                // A cardinal adjacent tile, including a player passing through it.
                if ((Mathf.Abs(offset.x) <= tileSize * 1.05f && Mathf.Abs(offset.y) < tileSize * 0.45f)
                    || (Mathf.Abs(offset.y) <= tileSize * 1.05f && Mathf.Abs(offset.x) < tileSize * 0.45f))
                {
                    Wake();
                    break;
                }
            }
            return;
        }
        if (state == State.Walking) { base.FixedUpdate(); return; }
        rb.linearVelocity = Vector2.zero;
        AdvanceSequence();
    }

    protected override void DecideNextTile()
    {
        if (state != State.Walking) return;
        AnimatedSpriteRenderer attack = direction == Vector2.up ? attackUp
            : direction == Vector2.down ? attackDown : attackLeft;
        if (cooldownRemaining <= 0f && attack != null && attack.animationSprite != null
            && attack.animationSprite.Length >= 2 && Random.value < attackChance)
        {
            state = State.Attacking;
            targetTile = rb.position;
            BeginSequence(attack);
            if (attack.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.flipX = direction == Vector2.right;
            return;
        }
        ChooseWalkingTile();
    }

    private void ChooseWalkingTile()
    {
        Vector2 incoming = direction;
        bool blocked = IsTileBlocked(rb.position + incoming * tileSize);
        int available = 0;
        foreach (Vector2 candidate in Dirs)
            if (!IsTileBlocked(rb.position + candidate * tileSize)) available++;
        base.DecideNextTile();
        // A surrounded Roboido must stay centered; pursuit cannot invent a path.
        if (available == 0) return;
        if ((!blocked && available < minAvailablePathsToTurn) || Random.value >= pursuitChance) return;
        Vector2 delta = Vector2.zero;
        float nearest = float.PositiveInfinity;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (!IsLivingPlayer(player)) continue;
            Vector2 offset = (Vector2)player.transform.position - rb.position;
            if (offset.sqrMagnitude >= nearest) continue;
            nearest = offset.sqrMagnitude;
            delta = offset;
        }
        if (float.IsPositiveInfinity(nearest)) return;
        Vector2 best = direction;
        float score = float.NegativeInfinity;
        foreach (Vector2 candidate in Dirs)
        {
            if (IsTileBlocked(rb.position + candidate * tileSize)) continue;
            if (preferTurnAtJunction && candidate == incoming) continue;
            float value = Vector2.Dot(candidate, delta);
            if (value <= score) continue;
            score = value;
            best = candidate;
        }
        if (float.IsNegativeInfinity(score)) return;
        direction = best;
        targetTile = rb.position + direction * tileSize;
        UpdateSpriteDirection(direction);
    }

    protected override bool IsTileBlocked(Vector2 tileCenter)
    {
        // Check tile data directly: collider geometry can lag behind tile edits
        // and scene collision masks must never allow walking through a block.
        GameManager manager = GameManager.Instance;
        var tiles = manager != null ? manager.destructibleTilemap : null;
        if (tiles != null && tiles.HasTile(tiles.WorldToCell(tileCenter))) return true;
        return base.IsTileBlocked(tileCenter);
    }

    private static bool IsLivingPlayer(PlayerIdentity player)
    {
        return player != null && player.gameObject.activeInHierarchy
            && player.TryGetComponent<CharacterHealth>(out var health) && health.life > 0;
    }

    private void BeginSequence(AnimatedSpriteRenderer animation)
    {
        HideWalkingSprites();
        HideSpecialSprites();
        sequence = animation;
        sequenceFrame = 0;
        frameElapsed = 0f;
        if (sequence == null) return;
        activeSprite = sequence;
        sequence.SetManualAnimationUpdate(true);
        sequence.loop = false;
        sequence.pingPong = false;
        sequence.idle = false;
        SetVisible(sequence, true);
        sequence.RestartAnimation();
    }

    private float FrameDuration()
    {
        if (sequence.frameDurations != null && sequence.frameDurations.Length == sequence.animationSprite.Length)
            return Mathf.Max(0.0001f, sequence.frameDurations[sequenceFrame]);
        return Mathf.Max(0.0001f, sequence.useSequenceDuration
            ? sequence.sequenceDuration / sequence.animationSprite.Length : sequence.animationTime);
    }

    private void AdvanceSequence()
    {
        if (sequence == null || sequence.animationSprite == null || sequence.animationSprite.Length == 0)
        {
            FinishSequence();
            return;
        }
        frameElapsed += Time.fixedDeltaTime;
        while (state != State.Walking && frameElapsed >= FrameDuration())
        {
            frameElapsed -= FrameDuration();
            sequenceFrame++;
            if (sequenceFrame >= sequence.animationSprite.Length) { FinishSequence(); break; }
            sequence.CurrentFrame = sequenceFrame;
            sequence.RefreshFrame();
            if (state == State.Attacking && sequenceFrame == 1) StrikeSides();
        }
    }

    private void FinishSequence()
    {
        bool wasAttack = state == State.Attacking;
        if (state == State.Waking) SetWakeProtection(false);
        HideSpecialSprites();
        sequence = null;
        state = State.Walking;
        cooldownRemaining = attackCooldown;
        if (!wasAttack) ChooseInitialDirection();
        UpdateSpriteDirection(direction);
        ChooseWalkingTile();
    }

    private void StrikeSides()
    {
        Vector2 side = direction.x == 0f ? Vector2.right : Vector2.up;
        StrikeTile(rb.position + side * tileSize);
        StrikeTile(rb.position - side * tileSize);
    }

    private void StrikeTile(Vector2 center)
    {
        GameManager manager = GameManager.Instance;
        var tiles = manager != null ? manager.destructibleTilemap : null;
        if (tiles != null)
        {
            Vector3Int cell = tiles.WorldToCell(center);
            if (tiles.HasTile(cell))
            {
                Vector3 spawnPosition = tiles.GetCellCenterWorld(cell);
                Destructible destructionPrefab = manager.GetDestructiblePrefab(tiles.GetTile(cell));
                if (destructionPrefab != null)
                    Instantiate(destructionPrefab, spawnPosition, Quaternion.identity, tiles.transform);
                GameObject hidden = manager.GetSpawnForDestroyedBlock(cell);
                tiles.SetTile(cell, null);
                if (destructionSfx != null && TryGetComponent<AudioSource>(out var source))
                    source.PlayOneShot(destructionSfx, Mathf.Clamp01(GameAudioSettings.ApplySfxVolume(1f) * Mathf.Max(1f, destructionSfxGain)));
                manager.OnDestructibleDestroyed(cell);
                if (hidden != null)
                {
                    manager.ReleasePendingHiddenItemCell(cell);
                    if (manager.TryReserveItemSpawnCell(cell))
                    {
                        GameObject spawned = Instantiate(hidden, spawnPosition, Quaternion.identity, tiles.transform);
                        manager.PrepareSpawnedHiddenObject(spawned, hidden, spawnPosition);
                    }
                }
            }
        }
        // Snapshot before detonation: exploding a bomb modifies ActiveBombs.
        struckBombs.Clear();
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.gameObject.activeInHierarchy) continue;
            Vector2 offset = bomb.GetLogicalPosition() - center;
            if (Mathf.Abs(offset.x) < tileSize * 0.5f && Mathf.Abs(offset.y) < tileSize * 0.5f) struckBombs.Add(bomb);
        }
        foreach (Bomb bomb in struckBombs)
        {
            if (bomb == null || bomb.HasExploded) continue;
            BombController detonator = bomb.Owner != null ? bomb.Owner : FindAnyObjectByType<BombController>();
            if (detonator != null) detonator.ExplodeBombChained(bomb.gameObject, center);
        }
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (!IsLivingPlayer(player)) continue;
            Vector2 offset = (Vector2)player.transform.position - center;
            if (Mathf.Abs(offset.x) < tileSize * 0.5f && Mathf.Abs(offset.y) < tileSize * 0.5f)
                player.GetComponent<CharacterHealth>().TakeDamage(1);
        }
    }

    private void HideWalkingSprites()
    {
        SetVisible(spriteUp, false);
        SetVisible(spriteDown, false);
        SetVisible(spriteLeft, false);
        SetVisible(spriteRight, false);
    }

    private void HideSpecialSprites()
    {
        SetVisible(wakeUp, false);
        SetVisible(attackUp, false);
        SetVisible(attackDown, false);
        SetVisible(attackLeft, false);
    }

    private static void SetVisible(AnimatedSpriteRenderer animation, bool visible)
    {
        if (animation == null) return;
        animation.enabled = visible;
        if (animation.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.enabled = visible;
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (state != State.Walking || isInDamagedLoop) return;
        base.UpdateSpriteDirection(dir);
        SetVisible(activeSprite, true);
    }

    protected override void Die()
    {
        HideSpecialSprites();
        HideWalkingSprites();
        base.Die();
    }

    private void SetWakeProtection(bool value)
    {
        if (TryGetComponent<CharacterHealth>(out var characterHealth))
            characterHealth.SetExternalInvulnerability(value);
    }

    private void OnEnable()
    {
        if (state == State.Sleeping || state == State.Waking) SetWakeProtection(true);
    }

    private void OnDisable() => SetWakeProtection(false);

    protected override void OnDestroy()
    {
        if (subscribedManager != null) subscribedManager.DestructibleDestroyed -= OnBlockDestroyed;
        base.OnDestroy();
    }
}
