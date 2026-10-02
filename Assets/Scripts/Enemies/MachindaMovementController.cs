using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MachindaMovementController : JunctionTurningEnemyMovementController
{
    [Header("Machinda")]
    [SerializeField, Min(1f)] private float acceleratedSpeedMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float alignmentToleranceTiles = 0.15f;
    [SerializeField, Min(0.05f)] private float hopDuration = 0.25f;
    [SerializeField, Min(0f)] private float hopHeight = 0.5f;
    [SerializeField, Min(0f)] private float reconnectDelay = 1f;
    [SerializeField, Min(0.01f)] private float connectionSpinInterval = 0.06f;

    private const int MaximumChainLength = 8;
    private readonly List<Vector2> trail = new List<Vector2>();
    private List<MachindaMovementController> chain;
    private float normalSpeed;
    private float reconnectAt;
    private bool scattering;
    private bool connecting;
    private float hopElapsed;
    private Vector2 hopStart;
    private Vector2 hopEnd;
    private Vector2 scatterDirection;
    private float connectionDuration;
    private int connectionStartingDirection;
    private int scatterStartingDirection;
    private float scatterElapsed;
    private Collider2D scatterRoomBounds;
    private static readonly Vector2[] ClockwiseDirections = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private MovementController[] players = System.Array.Empty<MovementController>();
    private float nextPlayerScan;
    private Tilemap ground;
    private readonly Dictionary<Transform, Vector3> visualOrigins = new Dictionary<Transform, Vector3>();

    protected override void Awake()
    {
        base.Awake();
        normalSpeed = speed;
        foreach (var sprite in new[] { spriteUp, spriteDown, spriteLeft, spriteRight })
            if (sprite != null && !visualOrigins.ContainsKey(sprite.transform))
                visualOrigins.Add(sprite.transform, sprite.transform.localPosition);
    }

    protected override void Start()
    {
        var manager = FindAnyObjectByType<GameManager>();
        if (manager != null) ground = manager.groundTilemap;
        if (ground == null)
            foreach (var tilemap in FindObjectsByType<Tilemap>())
                if (tilemap.name == "Ground") { ground = tilemap; break; }
        base.Start();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;
        if (TryGetComponent<StunReceiver>(out var stun) && stun.IsStunned) return;
        if (scattering)
        {
            AdvanceHop();
            return;
        }
        if (connecting)
        {
            AdvanceConnectionHop();
            return;
        }
        if (chain != null)
        {
            var leader = chain[0];
            if (leader.HasConnectingMember()) return;
            if (leader != this)
            {
                FollowTrail(leader, chain.IndexOf(this) * leader.tileSize);
                return;
            }
        }
        UpdateSpeed();
        base.FixedUpdate();
    }

    private MovementController FindNearestPlayer(out bool accelerated)
    {
        MovementController nearest = null;
        float distance = float.PositiveInfinity;
        accelerated = false;
        if (Time.time >= nextPlayerScan)
        {
            players = FindObjectsByType<MovementController>();
            nextPlayerScan = Time.time + 0.25f;
        }
        foreach (var player in players)
        {
            if (player == null || !player.isActiveAndEnabled || (player.TryGetComponent<CharacterHealth>(out var hp) && hp.IsDead)) continue;
            Vector2 delta = (Vector2)player.transform.position - rb.position;
            accelerated |= Mathf.Abs(delta.x) <= alignmentToleranceTiles * tileSize ||
                           Mathf.Abs(delta.y) <= alignmentToleranceTiles * tileSize ||
                           (Mathf.Abs(delta.x) <= tileSize && Mathf.Abs(delta.y) <= tileSize);
            if (delta.sqrMagnitude < distance)
            {
                nearest = player;
                distance = delta.sqrMagnitude;
            }
        }
        return nearest;
    }

    private void UpdateSpeed()
    {
        FindNearestPlayer(out bool accelerated);
        speed = normalSpeed * (accelerated ? acceleratedSpeedMultiplier : 1f);
    }

    protected override void DecideNextTile()
    {
        RecordTrailTile();
        var player = FindNearestPlayer(out _);
        if (player != null)
        {
            Vector2 delta = (Vector2)player.transform.position - rb.position;
            Vector2 best = Vector2.zero;
            float bestDistance = delta.sqrMagnitude;
            foreach (var candidate in Dirs)
            {
                float distance = (delta - candidate * tileSize).sqrMagnitude;
                if (distance < bestDistance && !IsTileBlocked(rb.position + candidate * tileSize))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            if (best != Vector2.zero)
            {
                isStuck = false;
                direction = best;
                targetTile = rb.position + direction * tileSize;
                UpdateSpriteDirection(direction);
                return;
            }
        }
        base.DecideNextTile();
    }

    protected override bool IsTileBlocked(Vector2 center)
    {
        foreach (var hit in Physics2D.OverlapBoxAll(center, Vector2.one * tileSize * 0.8f, 0f, obstacleMask))
        {
            var machinda = hit.GetComponentInParent<MachindaMovementController>();
            if (machinda != null)
            {
                // Robots can reverse through one another to escape dead ends.
                continue;
            }
            if (hit.GetComponentInParent<StageAssets.IgluRoofController>() != null) continue;
            return true;
        }
        return false;
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (chain != null) ScatterChain();
            else if (!scattering) base.OnTriggerEnter2D(other);
            return;
        }
        if (scattering) return;
        var machinda = other.GetComponentInParent<MachindaMovementController>();
        if (machinda != null)
        {
            TryConnect(machinda);
            return;
        }
        if (chain != null && chain[0].HasConnectingMember()) return;
        if (chain == null || chain[0] == this) base.OnTriggerEnter2D(other);
    }

    private void TryConnect(MachindaMovementController other)
    {
        if (other == this || other.isDead || other.scattering || Time.time < reconnectAt || Time.time < other.reconnectAt) return;
        if (chain != null && chain == other.chain) return;
        // Contact anywhere in a chain sends the newcomer to its tail.
        if (chain != null)
        {
            if (other.chain == null) other.TryConnect(this);
            return;
        }
        var members = other.chain ?? new List<MachindaMovementController> { other };
        if (members.Count >= MaximumChainLength) return;
        var leader = members[0];
        if (leader.HasConnectingMember()) return;
        float tailDistance = members.Count * leader.tileSize;
        if (!leader.TryPrepareTail(tailDistance, out Vector2 tailPosition)) return;
        hopStart = rb.position;
        hopEnd = tailPosition;
        hopElapsed = 0f;
        connectionDuration = hopDuration * Mathf.Max(1f, Vector2.Distance(hopStart, hopEnd) / tileSize);
        connectionStartingDirection = System.Array.IndexOf(ClockwiseDirections, direction);
        if (connectionStartingDirection < 0) connectionStartingDirection = 0;
        connecting = true;
        members.Add(this);
        foreach (var member in members)
        {
            member.chain = members;
            member.GetComponent<CharacterHealth>().SetExternalInvulnerability(true);
        }
    }

    private bool HasConnectingMember()
    {
        if (chain == null) return false;
        foreach (var member in chain)
            if (member.connecting) return true;
        return false;
    }

    private void RecordTrailTile()
    {
        if (trail.Count == 0 || Vector2.Distance(trail[0], rb.position) > 0.01f)
            trail.Insert(0, rb.position);
        if (trail.Count > 64) trail.RemoveAt(trail.Count - 1);
    }

    private bool TryGetTrailPosition(float distance, out Vector2 destination)
    {
        destination = rb.position;
        foreach (var point in trail)
        {
            float segment = Vector2.Distance(destination, point);
            if (segment >= distance && segment > 0f)
            {
                destination = Vector2.MoveTowards(destination, point, distance);
                return true;
            }
            distance -= segment;
            destination = point;
        }
        return distance <= 0.01f;
    }

    private bool TryPrepareTail(float distance, out Vector2 destination)
    {
        // Keep the existing path when growing a chain, including its corners.
        // At spawn there may be too little history; extend its rear over free tiles.
        if (trail.Count == 0) trail.Add(rb.position);
        for (int attempt = 0; attempt < MaximumChainLength; attempt++)
        {
            if (TryGetTrailPosition(distance, out destination))
                return IsConnectionTileAvailable(destination);
            Vector2 end = trail[trail.Count - 1];
            Vector2 backward = trail.Count > 1 ? end - trail[trail.Count - 2] : -direction;
            backward = Mathf.Abs(backward.x) > Mathf.Abs(backward.y)
                ? (backward.x > 0f ? Vector2.right : Vector2.left)
                : (backward.y > 0f ? Vector2.up : Vector2.down);
            bool extended = false;
            foreach (var candidate in new[] { backward, Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                Vector2 next = end + candidate * tileSize;
                if (trail.Contains(next) || !IsConnectionTileAvailable(next)) continue;
                trail.Add(next);
                extended = true;
                break;
            }
            if (!extended) break;
        }
        destination = rb.position;
        return false;
    }

    private bool IsConnectionTileAvailable(Vector2 position)
    {
        if (ground == null || !ground.cellBounds.Contains(ground.WorldToCell(position)) || !ground.HasTile(ground.WorldToCell(position))) return false;
        if (IsTileBlocked(position)) return false;
        if (chain != null)
            foreach (var member in chain)
                if (Vector2.Distance(position, member.rb.position) < tileSize * 0.8f) return false;
        return Vector2.Distance(position, rb.position) >= tileSize * 0.8f;
    }

    private void AdvanceConnectionHop()
    {
        if (!connecting) return;
        hopElapsed = Mathf.Min(connectionDuration, hopElapsed + Time.fixedDeltaTime);
        float t = hopElapsed / connectionDuration;
        UpdateClockwiseSpin(hopElapsed, connectionStartingDirection);
        rb.position = Vector2.Lerp(hopStart, hopEnd, t);
        SetHopVisual(t);
        if (t >= 1f)
        {
            connecting = false;
            var leader = chain[0];
            targetTile = rb.position;
            UpdateSpriteDirection(leader.direction);
        }
    }

    private void FollowTrail(MachindaMovementController leader, float distance)
    {
        connecting = false;
        if (!leader.TryGetTrailPosition(distance, out Vector2 destination)) return;
        Vector2 delta = destination - rb.position;
        if (delta.sqrMagnitude > 0.0001f)
            UpdateSpriteDirection(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? Vector2.right : Vector2.left) : (delta.y > 0 ? Vector2.up : Vector2.down));
        rb.MovePosition(destination);
        SetHopVisual(1f);
    }

    private void ScatterChain()
    {
        var members = chain.ToArray();
        var leaderRoom = StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(chain[0].rb.position);
        foreach (var member in members)
        {
            member.chain = null;
            member.trail.Clear();
            member.scattering = true;
            member.connecting = false;
            member.scatterRoomBounds = StageAssets.World3RoomProgressionController.FindRoomBoundsContaining(member.rb.position) ?? leaderRoom;
            member.scatterElapsed = 0f;
            member.scatterStartingDirection = System.Array.IndexOf(ClockwiseDirections, member.direction);
            if (member.scatterStartingDirection < 0) member.scatterStartingDirection = 0;
            member.scatterDirection = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.value < 0.5f ? -1f : 1f);
            member.BeginScatterHop();
        }
    }

    private void BeginScatterHop()
    {
        hopStart = rb.position;
        Vector2 center = new Vector2(Mathf.Round(hopStart.x / tileSize), Mathf.Round(hopStart.y / tileSize)) * tileSize;
        hopEnd = center + scatterDirection * tileSize;
        if (scatterRoomBounds != null)
        {
            // Keep the room captured at separation, including its offset and scale.
            // Test axes independently so corners reflect both diagonal components.
            if (!IsInsideScatterRoom(center + Vector2.right * scatterDirection.x * tileSize))
                scatterDirection.x = -scatterDirection.x;
            if (!IsInsideScatterRoom(center + Vector2.up * scatterDirection.y * tileSize))
                scatterDirection.y = -scatterDirection.y;
            hopEnd = center + scatterDirection * tileSize;
            if (!IsInsideScatterRoom(hopEnd)) hopEnd = hopStart;
        }
        else if (ground == null)
        {
            // Without stage bounds, stay protected instead of jumping outside the map.
            hopEnd = center;
        }
        else
        {
            BoundsInt bounds = ground.cellBounds;
            Vector3Int cell = ground.WorldToCell(hopEnd);
            if (cell.x < bounds.xMin || cell.x >= bounds.xMax) scatterDirection.x = -scatterDirection.x;
            if (cell.y < bounds.yMin || cell.y >= bounds.yMax) scatterDirection.y = -scatterDirection.y;
            hopEnd = center + scatterDirection * tileSize;
            if (!bounds.Contains(ground.WorldToCell(hopEnd))) hopEnd = center;
        }
        hopElapsed = 0f;
    }

    private void AdvanceHop()
    {
        scatterElapsed += Time.fixedDeltaTime;
        UpdateClockwiseSpin(scatterElapsed, scatterStartingDirection);
        hopElapsed = Mathf.Min(hopDuration, hopElapsed + Time.fixedDeltaTime);
        float t = hopElapsed / hopDuration;
        rb.position = Vector2.Lerp(hopStart, hopEnd, t);
        SetHopVisual(t);
        if (t < 1f) return;
        Vector2 snappedLanding = new Vector2(Mathf.Round(hopEnd.x / tileSize), Mathf.Round(hopEnd.y / tileSize)) * tileSize;
        if (!IsInsideScatterRoom(snappedLanding) || ground == null || !ground.HasTile(ground.WorldToCell(hopEnd)))
        {
            BeginScatterHop();
            return;
        }
        // Landing must be clear of obstacles, robots, players and active fire.
        int mask = obstacleMask | enemyLayerMask | LayerMask.GetMask("Player", "Explosion");
        foreach (var hit in Physics2D.OverlapBoxAll(hopEnd, Vector2.one * tileSize * 0.8f, 0f, mask))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<StageAssets.IgluRoofController>() != null) continue;
            BeginScatterHop();
            return;
        }
        scattering = false;
        reconnectAt = Time.time + reconnectDelay;
        GetComponent<CharacterHealth>().SetExternalInvulnerability(false);
        SnapToGrid();
        DecideNextTile();
    }

    private void UpdateClockwiseSpin(float elapsed, int startingDirection)
    {
        int turn = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, connectionSpinInterval));
        UpdateSpriteDirection(ClockwiseDirections[(startingDirection + turn) % ClockwiseDirections.Length]);
    }

    private bool IsInsideScatterRoom(Vector2 position)
    {
        if (scatterRoomBounds == null) return true;
        // Include the enemy's entire collider, rather than only its center.
        var body = GetComponent<Collider2D>();
        Vector2 halfSize = body.bounds.extents;
        Vector2 centerOffset = (Vector2)body.bounds.center - rb.position;
        Vector2 center = position + centerOffset;
        foreach (var corner in new[]
        {
            center + new Vector2(-halfSize.x, -halfSize.y),
            center + new Vector2(-halfSize.x, halfSize.y),
            center + new Vector2(halfSize.x, -halfSize.y),
            center + new Vector2(halfSize.x, halfSize.y)
        })
        {
            if (scatterRoomBounds is BoxCollider2D box)
            {
                Vector2 local = (Vector2)box.transform.InverseTransformPoint(corner) - box.offset;
                if (Mathf.Abs(local.x) > box.size.x * 0.5f || Mathf.Abs(local.y) > box.size.y * 0.5f) return false;
            }
            else if (!scatterRoomBounds.OverlapPoint(corner)) return false;
        }
        return true;
    }

    private void SetHopVisual(float t)
    {
        foreach (var pair in visualOrigins)
            if (pair.Key != null) pair.Key.localPosition = pair.Value + Vector3.up * (Mathf.Sin(t * Mathf.PI) * hopHeight);
    }

    protected override void Die()
    {
        if (chain != null) ScatterChain();
        scattering = false;
        SetHopVisual(0f);
        base.Die();
    }

    private void OnDisable()
    {
        if (chain != null) ScatterChain();
        scattering = false;
        SetHopVisual(0f);
        if (TryGetComponent<CharacterHealth>(out var hp)) hp.SetExternalInvulnerability(false);
    }
}
