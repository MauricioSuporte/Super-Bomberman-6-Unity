using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QueenOyobeeProjectile : MonoBehaviour
{
    private QueenOyobeeController owner;
    private SpriteRenderer visual;
    private Rigidbody2D rb;
    private CircleCollider2D hitbox;
    private MovementController target;
    private Vector2 position;
    private Vector2 direction;
    private Vector2 start;
    private Vector2 end;
    private Vector2[] crownPath;
    private float crownFlightSeconds;
    private bool groundSnow;
    private Vector2 groundDestination;
    private float releaseDelay;
    private float groundSpeed;
    private float landedSeconds;
    private bool pursuing = true;
    private bool enteredFloor;
    private bool landed;
    private float elapsed;
    private float orbitAngle;
    private bool crown;
    private bool spent;
    private float destructionElapsed;
    public const float SnowDestructionSeconds = 0.5f;
    private readonly HashSet<int> hitPlayers = new();
    public const float OrbitSeconds = 0.45f;
    public const float SnowOrbitRadius = 2.5f;
    public const float PursuingSnowReleaseSpacing = 0.18f;

    public static QueenOyobeeProjectile Create(QueenOyobeeController owner, bool crown, int index, Vector2[] path = null, float flightSeconds = 0f)
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return null;
        var go = new GameObject(crown ? "Queen crown" : "Queen snowflake");
        go.layer = LayerMask.NameToLayer("Projectile");
        var p = go.AddComponent<QueenOyobeeProjectile>();
        p.owner = owner;
        p.crown = crown;
        p.orbitAngle = index * 60f;
        if (!crown) p.releaseDelay = index * PursuingSnowReleaseSpacing;
        float initialAngle = p.orbitAngle * Mathf.Deg2Rad;
        p.position = crown ? (Vector2)owner.CrownOrigin :
            (Vector2)(owner.Boss.transform.position + Vector3.up * 0.5f) + new Vector2(Mathf.Cos(initialAngle), Mathf.Sin(initialAngle)) * SnowOrbitRadius;
        p.start = p.position;
        p.crownPath = path;
        p.crownFlightSeconds = flightSeconds > 0f ? flightSeconds : path != null ? CrownFlightSeconds(path) : 0.7f;
        var players = new List<MovementController>();
        foreach (var player in Object.FindObjectsByType<MovementController>())
            if (player != null && player.CompareTag("Player") && !player.isDead && !player.IsEndingStage && player.isActiveAndEnabled)
                players.Add(player);
        players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        if (players.Count > 0) p.target = players[index % players.Count];
        if (p.target == null) p.target = owner.Boss.FindTarget(p.position);
        Vector2 aim = p.target != null ? (Vector2)p.target.transform.position : p.start + Vector2.down * 5f;
        p.end = aim;
        if (crown) p.target = owner.Boss.FindTarget(p.position);
        if (crown && p.target != null) p.end = p.target.transform.position;
        p.visual = go.AddComponent<SpriteRenderer>();
        p.visual.sortingLayerID = owner.Boss.body.sortingLayerID;
        p.visual.sortingOrder = owner.Boss.body.sortingOrder + 2;
        p.visual.sprite = crown ? owner.crownFrames[0] : owner.snowFrames[index % owner.snowFrames.Length];
        p.rb = go.AddComponent<Rigidbody2D>();
        p.rb.bodyType = RigidbodyType2D.Kinematic;
        p.rb.gravityScale = 0f;
        p.rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        p.hitbox = go.AddComponent<CircleCollider2D>();
        p.hitbox.isTrigger = true;
        p.hitbox.radius = crown ? 0.45f : 0.22f;
        p.hitbox.enabled = crown;
        p.rb.position = FreezerVenusArena.Snap(p.position);
        go.transform.position = p.rb.position;
        Assets.Scripts.Netcode.BombermanNetworkManager.CoopTrackRenderer(p.visual);
        return p;
    }

    private void FixedUpdate()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;
        if (GamePauseController.IsPaused) return;
        if (owner == null || !owner.Boss.CombatActive) { Destroy(gameObject); return; }
        float dt = Time.fixedDeltaTime;
        if (spent)
        {
            destructionElapsed += dt;
            if (destructionElapsed >= SnowDestructionSeconds) { Destroy(gameObject); return; }
            SetDestructionFrame();
            return;
        }
        elapsed += dt;
        if (crown)
        {
            float t = Mathf.Clamp01(elapsed / crownFlightSeconds);
            Vector2 next = crownPath != null ? PathPoint(crownPath, t) : CirclePoint(start, end, t);
            // Sweep at sub-pixel intervals so a fast crown cannot skip a player collider.
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(position, next) * 16f));
            for (int sample = 0; sample <= samples; sample++)
                foreach (var other in Physics2D.OverlapCircleAll(Vector2.Lerp(position, next, sample / (float)samples), hitbox.radius)) Contact(other);
            position = next;
            if (t >= 1f) { Destroy(gameObject); return; }
            visual.sprite = owner.crownFrames[Mathf.FloorToInt(elapsed * 12f) % owner.crownFrames.Length];
        }
        else if (elapsed < OrbitSeconds + releaseDelay)
        {
            float angle = (orbitAngle + elapsed * 200f) * Mathf.Deg2Rad;
            position = (Vector2)(owner.Boss.transform.position + Vector3.up * 0.5f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SnowOrbitRadius;
        }
        else
        {
            if (!hitbox.enabled)
            {
                direction = target != null ? ((Vector2)target.transform.position - position).normalized : Vector2.down;
                hitbox.enabled = true;
                owner.PlayIceThrowOnce();
            }
            float flight = elapsed - OrbitSeconds - releaseDelay;
            if (groundSnow)
            {
                if (!landed)
                {
                    // Keep fractional progress; only the displayed/collider position snaps to pixels.
                    position = Vector2.MoveTowards(position, groundDestination, groundSpeed * dt);
                    if (position == groundDestination)
                    {
                        landed = true;
                        hitbox.radius = 0.38f;
                    }
                }
                else landedSeconds += dt;
                if (landedSeconds > 7f) { BeginSnowDestruction(); return; }
            }
            else
            {
                if (pursuing && target != null && !target.isDead && !target.IsEndingStage)
                {
                    Vector2 desired = ((Vector2)target.transform.position - position).normalized;
                    pursuing = CanSnowTurn(direction, desired);
                    if (pursuing)
                        direction = Quaternion.Euler(0f, 0f, Mathf.Clamp(Vector2.SignedAngle(direction, desired), -20f * dt, 20f * dt)) * direction;
                }
                position += direction * 4.5f * dt;
                bool overFloor = owner.Boss.Arena.IsFloor(owner.Boss.Arena.Cell(position));
                if (overFloor) enteredFloor = true;
                // The boss floats above the top floor row: let those shots enter the arena first.
                if (flight > 5f || (enteredFloor && !overFloor))
                { BeginSnowDestruction(); return; }
            }
            visual.sprite = owner.snowFrames[Mathf.FloorToInt(elapsed * 8f) % owner.snowFrames.Length];
        }
        rb.position = FreezerVenusArena.Snap(position);
        transform.position = rb.position;
    }

    private static bool TryFreezePlayer(MovementController player, float seconds)
    {
        if (player == null || player.isDead || player.IsEndingStage || player.explosionInvulnerable) return false;
        var health = player.GetComponent<CharacterHealth>();
        if (health != null && health.IsInvulnerable) return false;
        var freeze = player.GetComponent<QueenOyobeeFreeze>();
        if (freeze == null) freeze = player.gameObject.AddComponent<QueenOyobeeFreeze>();
        return freeze.TryFreeze(seconds);
    }

    public static bool CanSnowTurn(Vector2 heading, Vector2 desired)
        => desired != Vector2.zero && Mathf.Abs(Vector2.SignedAngle(heading, desired)) <= 55f;

    public static QueenOyobeeProjectile CreateGroundSnow(QueenOyobeeController owner, int index, Vector3Int cell)
    {
        var p = Create(owner, false, index);
        p.groundSnow = true;
        p.groundDestination = owner.Boss.Arena.Center(cell);
        p.releaseDelay = index * 0.11f;
        p.groundSpeed = 1.6f + index * 0.13f;
        p.orbitAngle += 30f;
        float angle = p.orbitAngle * Mathf.Deg2Rad;
        p.position = (Vector2)(owner.Boss.transform.position + Vector3.up * 0.5f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SnowOrbitRadius;
        p.rb.position = FreezerVenusArena.Snap(p.position);
        p.transform.position = p.rb.position;
        return p;
    }

    public static List<Vector3Int> GroundSnowDestinations(FreezerVenusArena arena, Vector2 origin)
    {
        var result = new List<Vector3Int>();
        if (arena.Ground == null) return result;
        var candidates = new List<Vector3Int>();
        foreach (var cell in arena.Ground.cellBounds.allPositionsWithin)
            if (arena.IsStaticWalkable(cell)) candidates.Add(cell);
        // Six angular sectors select distinct clear tiles across the arena, not one pile.
        for (int i = 0; i < 6 && candidates.Count > 0; i++)
        {
            float angle = (i * 60f + 30f) * Mathf.Deg2Rad;
            Vector2 desired = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (3f + i * 0.7f);
            candidates.Sort((a, b) => ((Vector2)arena.Center(a) - desired).sqrMagnitude.CompareTo(((Vector2)arena.Center(b) - desired).sqrMagnitude));
            result.Add(candidates[0]);
            candidates.RemoveAt(0);
        }
        return result;
    }

    public static float CrownFlightSeconds(Vector2[] path)
    {
        float length = 0f;
        for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);
        return Mathf.Max(0.7f, length / 19f);
    }

    public static Vector2[] ExpandCrownPath(FreezerVenusArena arena, Vector2[] path)
    {
        var expanded = new Vector2[path.Length];
        expanded[0] = path[0];
        expanded[expanded.Length - 1] = path[0];
        // sqrt(2) in both axes doubles nominal area; arena edges constrain the final extent.
        for (int i = 1; i < path.Length - 1; i++)
        {
            Vector2 position = path[0] + (path[i] - path[0]) * Mathf.Sqrt(2f);
            expanded[i] = arena.Center(arena.NearestFloor(position));
        }
        return expanded;
    }

    public static Vector2[] BuildCrownPath(QueenOyobeeController owner, Vector2 aim, int pattern)
    {
        Vector2 head = owner.CrownOrigin;
        Vector2 forward = (aim - head).normalized;
        if (forward == Vector2.zero) forward = Vector2.down;
        Vector2 side = new(-forward.y, forward.x);
        float width = pattern % 2 == 0 ? 2.5f : -2.5f;
        var points = new List<Vector2> { head };
        switch (pattern % 5)
        {
            case 0: points.Add(aim + side * width); points.Add(aim - side * width); break;
            case 1: points.Add(Vector2.Lerp(head, aim, 0.5f) + side * width); points.Add(aim); points.Add(Vector2.Lerp(head, aim, 0.5f) - side * width); break;
            case 2: points.Add(aim); points.Add(aim + side * width); points.Add(head + side * width); break;
            case 3: points.Add(aim + side * width); points.Add(Vector2.Lerp(head, aim, 0.45f)); points.Add(aim - side * width); break;
            default: points.Add(head + side * width); points.Add(aim - side * width); points.Add(aim + side * width); points.Add(head - side * width); break;
        }
        // Keep floor-level waypoints inside the arena; the crown always returns to its head.
        for (int i = 1; i < points.Count; i++)
            points[i] = owner.Boss.Arena.Center(owner.Boss.Arena.NearestFloor(points[i]));
        points.Add(head);
        return points.ToArray();
    }

    public static Vector2 PathPoint(Vector2[] path, float progress)
    {
        float length = 0f;
        for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);
        float distance = Mathf.Clamp01(progress) * length;
        for (int i = 1; i < path.Length; i++)
        {
            float segment = Vector2.Distance(path[i - 1], path[i]);
            if (distance <= segment) return Vector2.Lerp(path[i - 1], path[i], segment > 0f ? distance / segment : 0f);
            distance -= segment;
        }
        return path[path.Length - 1];
    }

    public static GameObject CreateRitual(QueenOyobeeController owner)
    {
        var ring = new Vector2[65];
        Vector2 center = owner.Boss.GroundPosition;
        for (int i = 0; i < ring.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            ring[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f;
        }
        return CreateTelegraph(owner, ring);
    }

    public static GameObject CreateTelegraph(QueenOyobeeController owner, Vector2[] path)
    {
        var go = new GameObject("Queen pixel trajectory");
        var marker = go.AddComponent<QueenOyobeePixelTrail>();
        marker.Initialize(owner, path);
        return go;
    }

    public static Vector2 CirclePoint(Vector2 head, Vector2 targetPosition, float progress)
    {
        Vector2 center = (head + targetPosition) * 0.5f;
        Vector2 radius = head - center;
        Vector2 perpendicular = new(-radius.y, radius.x);
        float angle = Mathf.Clamp01(progress) * Mathf.PI * 2f;
        return center + radius * Mathf.Cos(angle) + perpendicular * Mathf.Sin(angle);
    }

    private void OnTriggerEnter2D(Collider2D other) => Contact(other);
    private void OnTriggerStay2D(Collider2D other) => Contact(other);
    private void Contact(Collider2D other)
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;
        if (spent || !hitbox.enabled || GamePauseController.IsPaused) return;
        if (crown)
        {
            var hitPlayer = other.GetComponentInParent<MovementController>();
            if (hitPlayer == null) return;
            int id = hitPlayer.GetEntityId().GetHashCode();
            if (!hitPlayers.Contains(id) && FreezerVenusBoss.TryHitPlayer(other)) hitPlayers.Add(id);
            return;
        }
        var player = other.GetComponentInParent<MovementController>();
        if (player == null || !player.CompareTag("Player") || player.isDead || player.IsEndingStage) return;
        if (!player.InputLocked) TryFreezePlayer(player, 2f);
        // A collision consumes either snow type, even if this player resists freezing.
        BeginSnowDestruction();
    }

    private void BeginSnowDestruction()
    {
        if (spent || crown) return;
        spent = true;
        destructionElapsed = 0f;
        hitbox.enabled = false;
        rb.position = FreezerVenusArena.Snap(position);
        transform.position = rb.position;
        visual.color = Color.white;
        SetDestructionFrame();
    }

    private void SetDestructionFrame()
    {
        var frames = owner.snowDestructionFrames;
        if (frames == null || frames.Length == 0) return;
        int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(destructionElapsed * frames.Length / SnowDestructionSeconds));
        visual.sprite = frames[frame];
    }
}

// One point-filtered sprite rasterizes the exact flight path at the arena's 16 PPU.
public sealed class QueenOyobeePixelTrail : MonoBehaviour
{
    private Texture2D texture;
    private Sprite sprite;
    private SpriteRenderer visual;
    private float elapsed;

    public void Initialize(QueenOyobeeController owner, Vector2[] path)
    {
        Vector2 min = path[0], max = path[0];
        foreach (var point in path) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        min = new Vector2(Mathf.Floor(min.x * 16f), Mathf.Floor(min.y * 16f)) / 16f - Vector2.one * 0.125f;
        max += Vector2.one * 0.125f;
        int width = Mathf.CeilToInt((max.x - min.x) * 16f) + 1;
        int height = Mathf.CeilToInt((max.y - min.y) * 16f) + 1;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int segment = 1; segment < path.Length; segment++)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path[segment - 1], path[segment]) * 32f));
            for (int step = 0; step <= steps; step++)
            {
                Vector2 point = (Vector2.Lerp(path[segment - 1], path[segment], step / (float)steps) - min) * 16f;
                int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
                if (x < 1 || y < 1 || x >= width - 1 || y >= height - 1) continue;
                pixels[y * width + x] = new Color32(255, 225, 125, 210);
                if ((x + y) % 4 == 0)
                {
                    pixels[y * width + x - 1] = new Color32(40, 75, 105, 180);
                    pixels[(y - 1) * width + x] = new Color32(40, 75, 105, 180);
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, 16f);
        visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingLayerID = owner.Boss.body.sortingLayerID;
        visual.sortingOrder = owner.Boss.body.sortingOrder - 1;
        transform.position = min;
        Assets.Scripts.Netcode.BombermanNetworkManager.CoopTrackRenderer(visual);
    }

    private void Update()
    {
        if (GamePauseController.IsPaused || visual == null) return;
        elapsed += Time.deltaTime;
        visual.color = new Color(1f, 1f, 1f, 0.8f + Mathf.Sin(elapsed * 6f) * 0.15f);
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
