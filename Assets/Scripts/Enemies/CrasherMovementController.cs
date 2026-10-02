using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CrasherMovementController : JunctionTurningEnemyMovementController
{
    [Header("Crasher")]
    [SerializeField, Range(0f, 1f)] private float pursuitChance = 0.25f;
    [SerializeField] private AnimatedSpriteRenderer attackDown;
    [SerializeField] private AnimatedSpriteRenderer attackLeft;
    [SerializeField] private AnimatedSpriteRenderer wreckage;

    [SerializeField, Min(1f)] private float bombVisionDistance = 4f;
    [SerializeField] private AudioClip destructionSfx;

    private AnimatedSpriteRenderer attackSprite;
    private Tilemap attackTilemap;
    private Vector3Int attackCell;
    private Vector2 nextDirection;
    private Vector2 nextTarget;
    private float frameElapsed;
    private int attackFrame;
    private bool attacking;
    private bool destroyedBlock;

    protected override void Awake()
    {
        base.Awake();
        SetVisible(attackDown, false);
        SetVisible(attackLeft, false);
        SetVisible(wreckage, false);
    }

    protected override void DecideNextTile()
    {
        if (attacking) return;
        if (TryFleeVisibleBomb()) return;
        Vector2 incoming = direction;
        Tilemap tiles = GameManager.Instance != null ? GameManager.Instance.destructibleTilemap : null;
        Vector3Int cell = tiles != null ? tiles.WorldToCell(rb.position + incoming * tileSize) : default;
        bool hitBlock = incoming != Vector2.up && tiles != null && tiles.HasTile(cell)
            && IsTileBlocked(rb.position + incoming * tileSize);

        base.DecideNextTile();
        // Bias only decisions where the junction controller would allow a turn.
        int available = 0;
        foreach (Vector2 dir in Dirs)
            if (!IsTileBlocked(rb.position + dir * tileSize)) available++;
        if ((hitBlock || available >= minAvailablePathsToTurn) && Random.value < pursuitChance)
            PreferNearestPlayer(incoming);

        AnimatedSpriteRenderer animation = incoming.x == 0f ? attackDown : attackLeft;
        if (!hitBlock || animation == null || animation.animationSprite == null || animation.animationSprite.Length < 5)
            return;

        nextDirection = direction;
        nextTarget = targetTile;
        direction = incoming;
        targetTile = rb.position;
        attackTilemap = tiles;
        attackCell = cell;
        attackSprite = animation;
        attacking = true;
        destroyedBlock = false;
        attackFrame = 0;
        frameElapsed = 0f;
        rb.linearVelocity = Vector2.zero;
        SetVisible(spriteUp, false);
        SetVisible(spriteDown, false);
        SetVisible(spriteLeft, false);
        SetVisible(spriteRight, false);
        attackSprite.SetManualAnimationUpdate(true);
        attackSprite.loop = false;
        attackSprite.pingPong = false;
        attackSprite.idle = false;
        SetVisible(attackSprite, true);
        attackSprite.RestartAnimation();
        attackSprite.GetComponent<SpriteRenderer>().flipX = incoming == Vector2.right;
    }

    private bool TryFindVisibleBomb(out Vector2 bombPosition)
    {
        bombPosition = Vector2.zero;
        float nearest = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.gameObject.activeInHierarchy) continue;
            Vector2 position = bomb.GetLogicalPosition();
            Vector2 delta = position - rb.position;
            float distance = delta.magnitude;
            if (distance > bombVisionDistance || distance >= nearest) continue;
            float tolerance = tileSize * 0.2f;
            if (Mathf.Abs(delta.x) > tolerance && Mathf.Abs(delta.y) > tolerance) continue;
            Vector2 sightDirection = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? Vector2.right : Vector2.left)
                : (delta.y >= 0f ? Vector2.up : Vector2.down);
            bool blocked = false;
            for (float step = tileSize; step < distance - tileSize * 0.5f; step += tileSize)
            {
                if (!IsTileBlocked(rb.position + sightDirection * step)) continue;
                blocked = true;
                break;
            }
            if (blocked) continue;
            nearest = distance;
            bombPosition = position;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    private bool TryFleeVisibleBomb()
    {
        if (!TryFindVisibleBomb(out Vector2 bombPosition)) return false;
        Vector2 bestDirection = Vector2.zero;
        float bestDistance = float.NegativeInfinity;
        // Keep the current direction when two escape paths are equally good.
        for (int index = -1; index < Dirs.Length; index++)
        {
            Vector2 candidate = index < 0 ? direction : Dirs[index];
            Vector2 destination = rb.position + candidate * tileSize;
            if (IsTileBlocked(destination) || HasBombAt(destination)) continue;
            float distance = (destination - bombPosition).sqrMagnitude;
            if (distance <= bestDistance) continue;
            bestDistance = distance;
            bestDirection = candidate;
        }
        if (bestDirection == Vector2.zero) return false;
        if (attacking) EndAttack();
        direction = bestDirection;
        targetTile = rb.position + direction * tileSize;
        isStuck = false;
        UpdateSpriteDirection(direction);
        return true;
    }
    private void PreferNearestPlayer(Vector2 incoming)
    {
        Vector2 delta = Vector2.zero;
        float nearest = float.PositiveInfinity;
        foreach (PlayerIdentity player in PlayerIdentity.ActivePlayers)
        {
            if (player == null || !player.gameObject.activeInHierarchy) continue;
            if (!player.TryGetComponent<CharacterHealth>(out var playerHealth) || playerHealth.life <= 0) continue;
            Vector2 offset = (Vector2)player.transform.position - rb.position;
            if (offset.sqrMagnitude >= nearest) continue;
            nearest = offset.sqrMagnitude;
            delta = offset;
        }
        if (float.IsPositiveInfinity(nearest)) return;
        Vector2 best = direction;
        float score = float.NegativeInfinity;
        foreach (Vector2 dir in Dirs)
        {
            if (IsTileBlocked(rb.position + dir * tileSize)) continue;
            if (preferTurnAtJunction && dir == incoming) continue;
            float candidate = Vector2.Dot(dir, delta);
            if (candidate <= score) continue;
            score = candidate;
            best = dir;
        }
        direction = best;
        targetTile = rb.position + best * tileSize;
        UpdateSpriteDirection(best);
    }

    protected override void FixedUpdate()
    {
        if (isDead || GamePauseController.IsPaused) return;
        if (!isInDamagedLoop && !(TryGetComponent<StunReceiver>(out var receiver) && receiver.IsStunned))
        {
            Vector2 center = new(Mathf.Round(rb.position.x / tileSize) * tileSize, Mathf.Round(rb.position.y / tileSize) * tileSize);
            if (Vector2.Distance(rb.position, center) < 0.01f) TryFleeVisibleBomb();
        }
        if (!attacking) { base.FixedUpdate(); return; }
        rb.linearVelocity = Vector2.zero;
        if (isInDamagedLoop || (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned)) return;
        frameElapsed += Time.fixedDeltaTime;
        while (attacking && frameElapsed >= FrameDuration(attackFrame))
        {
            frameElapsed -= FrameDuration(attackFrame);
            attackFrame++;
            if (attackFrame >= attackSprite.animationSprite.Length)
            {
                EndAttack();
                break;
            }
            attackSprite.CurrentFrame = attackFrame;
            attackSprite.RefreshFrame();
            if (attackFrame == 4 && !destroyedBlock)
            {
                destroyedBlock = true;
                DestroyBlock();
            }
        }
    }

    private float FrameDuration(int frame)
    {
        if (attackSprite.frameDurations != null && attackSprite.frameDurations.Length == attackSprite.animationSprite.Length)
            return Mathf.Max(0.0001f, attackSprite.frameDurations[frame]);
        return Mathf.Max(0.0001f, attackSprite.useSequenceDuration
            ? attackSprite.sequenceDuration / attackSprite.animationSprite.Length : attackSprite.animationTime);
    }

    private void DestroyBlock()
    {
        if (attackTilemap == null || !attackTilemap.HasTile(attackCell)) return;
        Vector3 center = attackTilemap.GetCellCenterWorld(attackCell);
        GameManager manager = GameManager.Instance;
        GameObject hidden = manager != null ? manager.GetSpawnForDestroyedBlock(attackCell) : null;
        attackTilemap.SetTile(attackCell, null);
        if (destructionSfx != null && TryGetComponent<AudioSource>(out var source))
            GameAudioSettings.PlaySfx(source, destructionSfx);
        if (manager != null) manager.OnDestructibleDestroyed(attackCell);
        if (hidden != null && manager != null)
        {
            manager.ReleasePendingHiddenItemCell(attackCell);
            if (manager.TryReserveItemSpawnCell(attackCell))
            {
                GameObject spawned = Instantiate(hidden, center, Quaternion.identity, attackTilemap.transform);
                manager.PrepareSpawnedHiddenObject(spawned, hidden, center);
            }
        }
        if (wreckage == null) return;
        for (int i = 0; i < 5; i++)
        {
            GameObject fragment = Instantiate(wreckage.gameObject, center, Quaternion.identity);
            fragment.SetActive(true);
            AnimatedSpriteRenderer animation = fragment.GetComponent<AnimatedSpriteRenderer>();
            animation.enabled = true;
            animation.idle = false;
            animation.loop = true;
            animation.RestartAnimation();
            float angle = (i + Random.value) * Mathf.PI * 2f / 5f;
            fragment.AddComponent<CrasherWreckage>().Initialize(
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2f, 4f));
        }
    }

    private static void SetVisible(AnimatedSpriteRenderer animation, bool visible)
    {
        if (animation == null) return;
        animation.enabled = visible;
        if (animation.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.enabled = visible;
    }

    private void EndAttack()
    {
        attacking = false;
        SetVisible(attackDown, false);
        SetVisible(attackLeft, false);
        direction = nextDirection;
        targetTile = nextTarget;
        isStuck = targetTile == rb.position;
        stuckTimer = recheckStuckEverySeconds;
        UpdateSpriteDirection(direction);
    }

    protected override void Die()
    {
        attacking = false;
        SetVisible(attackDown, false);
        SetVisible(attackLeft, false);
        base.Die();
    }

    private void OnDisable()
    {
        if (attacking && !isDead && rb != null) EndAttack();
    }
}
