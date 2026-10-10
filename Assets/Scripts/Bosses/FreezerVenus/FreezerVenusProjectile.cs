using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FreezerVenusProjectile : MonoBehaviour
{
    public enum AttackKind { Tornado, Ice, Doll }
    private FreezerVenusBoss owner;
    private AttackKind kind;
    private Sprite[] frames;
    private SpriteRenderer visual;
    private Rigidbody2D rb;
    private Vector2 direction;
    private Vector3 logicalPosition;
    private float elapsed;
    private float speed;
    private bool spent;
    private Vector3Int currentCell;
    private Vector3Int segmentCell;
    private Vector3 segmentEnd;
    private Vector3Int previousDirection;
    private bool travelling;
    private bool returningToCenter;
    private float turnPause;
    private Collider2D hitbox;
    private float walkTime;
    private float jumpTime;
    private bool invocationWalking;
    private static readonly int[] WalkSequence = { 0, 1, 0, 2 };
    private static readonly int[] JumpSequence = { 3, 4, 5, 6, 7, 6, 5, 4, 3, 9, 10, 11 };
    private const float JumpCycleSeconds = 0.2f;
    private const float InvocationInitialSpeed = 2.2f;
    private const float ObstaclePauseSeconds = 0.18f;
    public AttackKind Kind => kind;
    public float Speed => speed;

    public static FreezerVenusProjectile Create(FreezerVenusBoss owner, AttackKind kind,
        Sprite[] frames, Vector3 position, Vector2 direction)
    {
        var go = new GameObject("FreezerVenus " + kind);
        go.layer = LayerMask.NameToLayer("Projectile");
        position = kind == AttackKind.Tornado ? owner.Arena.Center(owner.Arena.NearestFloor(position)) :
            FreezerVenusArena.Snap(position);
        go.transform.position = position;
        var spriteObject = new GameObject("Sprite");
        spriteObject.layer = go.layer;
        spriteObject.transform.SetParent(go.transform, false);
        if (kind == AttackKind.Tornado) spriteObject.transform.localPosition = Vector3.up * 0.5f;
        var visual = spriteObject.AddComponent<SpriteRenderer>();
        visual.sortingLayerID = owner.body.sortingLayerID;
        visual.sortingOrder = owner.body.sortingOrder + 1;
        if (frames.Length > 0) visual.sprite = frames[0];
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        var collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        // SunMask's SunStarProjectile uses this same centered circular trigger.
        collider.radius = 0.3f;
        var projectile = go.AddComponent<FreezerVenusProjectile>();
        projectile.owner = owner;
        projectile.kind = kind;
        projectile.frames = frames;
        projectile.visual = visual;
        projectile.rb = rb;
        projectile.hitbox = collider;
        projectile.logicalPosition = position;
        projectile.currentCell = owner.Arena.Cell(position);
        projectile.direction = direction.normalized;
        projectile.speed = kind == AttackKind.Tornado ? 5f : kind == AttackKind.Ice ? 7.6f : InvocationInitialSpeed;
        if (kind == AttackKind.Doll)
        {
            projectile.invocationWalking = true;
            projectile.segmentCell = projectile.currentCell + Vector3Int.down;
            projectile.segmentEnd = owner.Arena.Center(projectile.segmentCell);
        }
        return projectile;
    }

    private void FixedUpdate()
    {
        if (spent || GamePauseController.IsPaused) return;
        if (owner == null || !owner.CombatActive) { Despawn(); return; }
        elapsed += Time.fixedDeltaTime;
        if (elapsed >= (kind == AttackKind.Tornado ? 7f : 6f)) { Despawn(); return; }
        if (kind != AttackKind.Doll && frames.Length > 0)
        {
            int frame = (int)(elapsed * 10f);
            if (kind == AttackKind.Tornado)
                frame = frame < 3 ? frame : 3 + (frame - 3) % 3;
            else frame %= frames.Length;
            visual.sprite = frames[Mathf.Min(frame, frames.Length - 1)];
        }
        if (kind == AttackKind.Ice)
            visual.transform.rotation = Quaternion.Euler(0f, 0f, 90f * (Mathf.FloorToInt(elapsed / 0.25f) % 4));
        if (kind == AttackKind.Doll)
        {
            MoveInvocation();
        }
        else if (kind == AttackKind.Tornado)
        {
            if (elapsed < 1f) return;
            MoveTornado();
        }
        else
        {
            logicalPosition += (Vector3)(direction * speed * Time.fixedDeltaTime);
            Vector3Int cell = owner.Arena.Cell(logicalPosition);
            if (kind == AttackKind.Ice && !owner.Arena.IsFloor(cell)) { Despawn(); return; }
            ApplyPosition();
        }
    }

    private void Update()
    {
        if (kind != AttackKind.Doll || spent || GamePauseController.IsPaused ||
            owner == null || !owner.CombatActive) return;
        int frame;
        if (invocationWalking)
        {
            walkTime += Time.deltaTime;
            frame = WalkSequence[Mathf.FloorToInt(walkTime / 0.1f) % WalkSequence.Length];
        }
        else
        {
            jumpTime = Mathf.Min(JumpCycleSeconds, jumpTime + Time.deltaTime);
            frame = JumpSequence[Mathf.Min(JumpSequence.Length - 1,
                Mathf.FloorToInt(jumpTime / JumpCycleSeconds * JumpSequence.Length))];
        }
        if (frame < frames.Length) visual.sprite = frames[frame];
    }

    private void MoveInvocation()
    {
        Vector3 next;
        if (invocationWalking)
            next = Vector3.MoveTowards(logicalPosition, segmentEnd, speed * Time.fixedDeltaTime);
        else
        {
            if (jumpTime < JumpCycleSeconds) return;
            speed = InvocationInitialSpeed * 3f;
            next = logicalPosition + Vector3.down * (speed * Time.fixedDeltaTime);
        }
        var cell = owner.Arena.Cell(next);
        if (!owner.Arena.IsStaticWalkable(cell))
        {
            FinishInvocation(false);
            return;
        }
        logicalPosition = next;
        ApplyPosition();
        if (invocationWalking && (logicalPosition - segmentEnd).sqrMagnitude < 0.000001f)
        {
            invocationWalking = false;
            if (frames.Length > 3) visual.sprite = frames[3];
        }
    }

    private void FinishInvocation(bool killedByExplosion)
    {
        if (spent) return;
        spent = true;
        hitbox.enabled = false;
        rb.linearVelocity = Vector2.zero;
        StartCoroutine(InvocationFinish(killedByExplosion));
    }

    private IEnumerator InvocationFinish(bool killedByExplosion)
    {
        Sprite[] finishFrames = killedByExplosion ? owner.invocationDeathFrames : owner.invocationCollisionFrames;
        if (killedByExplosion && frames.Length > 8)
        {
            for (int i = 0; i < 5; i++)
            {
                visual.sprite = frames[8];
                yield return FreezerVenusBoss.WaitUnpaused(0.1f);
            }
        }
        foreach (var frame in finishFrames)
        {
            visual.sprite = frame;
            yield return FreezerVenusBoss.WaitUnpaused(0.1f);
        }
        Destroy(gameObject);
    }

    private void MoveTornado()
    {
        if (turnPause > 0f) { turnPause = Mathf.Max(0f, turnPause - Time.fixedDeltaTime); return; }
        if (travelling && !returningToCenter && !owner.Arena.IsWalkable(segmentCell))
        {
            ReturnToCenter();
            return;
        }
        if (!travelling)
        {
            var target = owner.FindTarget(logicalPosition);
            if (target == null || !owner.Arena.TryNextStep(currentCell, owner.Arena.Cell(target.transform.position), out segmentCell))
                return;
            Vector3Int newDirection = segmentCell - currentCell;
            segmentEnd = owner.Arena.Center(segmentCell);
            travelling = true;
            returningToCenter = false;
            if (previousDirection != Vector3Int.zero && previousDirection != newDirection)
                turnPause = ObstaclePauseSeconds;
            previousDirection = newDirection;
            if (turnPause > 0f) return;
        }
        logicalPosition = Vector3.MoveTowards(logicalPosition, segmentEnd, speed * Time.fixedDeltaTime);
        ApplyPosition();
        if (logicalPosition != segmentEnd) return;
        if (!returningToCenter) currentCell = segmentCell;
        travelling = false;
        returningToCenter = false;
    }

    private void ReturnToCenter()
    {
        returningToCenter = true;
        segmentEnd = owner.Arena.Center(currentCell);
        turnPause = ObstaclePauseSeconds;
    }

    private void ApplyPosition()
    {
        Vector3 position = FreezerVenusArena.Snap(logicalPosition);
        transform.position = position;
        rb.position = position;
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (spent || GamePauseController.IsPaused) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            // Like SunMask stars (obstacleMask = 0), ice passes through explosions.
            if (kind == AttackKind.Doll) FinishInvocation(true);
            else if (kind != AttackKind.Ice) Despawn();
            return;
        }
        // The newborn tornado remains harmless for its one-second stationary wind-up.
        if (kind == AttackKind.Tornado && elapsed < 1f) return;
        if (FreezerVenusBoss.TryHitPlayer(other))
        {
            if (kind == AttackKind.Tornado) Despawn();
            return;
        }
        var bomb = other.GetComponentInParent<Bomb>();
        if (bomb != null && !bomb.HasExploded)
        {
            if (kind == AttackKind.Doll)
            {
                FinishInvocation(false);
                var manager = FindAnyObjectByType<GameManager>();
                float tileSize = Vector3.Distance(owner.Arena.Center(currentCell),
                    owner.Arena.Center(currentCell + Vector3Int.down));
                // Use BombKickAbility's blocking masks and kick parameters.
                bool kicked = bomb.StartKick(Vector2.down, tileSize, LayerMask.GetMask("Stage", "Enemy"),
                    manager != null ? manager.destructibleTilemap : null,
                    LayerMask.GetMask("Player", "Stage", "Bomb", "Enemy", "Louie"),
                    0.60f, 0.90f, false);
                if (kicked) owner.PlayInvocationKickSfx();
            }
            else if (kind == AttackKind.Tornado && travelling && !returningToCenter &&
                owner.Arena.Cell(bomb.transform.position) == segmentCell) ReturnToCenter();
        }
        else if (kind == AttackKind.Doll && other.gameObject.layer == LayerMask.NameToLayer("Stage"))
        {
            // Tile occupancy is checked every step; also catch the terminal wall's collider.
            if (!owner.Arena.IsStaticWalkable(owner.Arena.Cell(other.ClosestPoint(transform.position))))
                FinishInvocation(false);
        }
    }

    private void Despawn()
    {
        spent = true;
        Destroy(gameObject);
    }
}
