using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BombKickAbility))]
public class BossBomberAI : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Maximum Manhattan distance to keep chasing the target. If farther, the boss still moves toward but uses simpler logic.")]
    public float maxChaseDistance = 20f;

    [Header("Thinking")]
    [Tooltip("How often (seconds) the boss thinks when it is safe.")]
    public float thinkIntervalSafe = 0.20f;

    [Tooltip("How often (seconds) the boss thinks when it is in danger.")]
    public float thinkIntervalDanger = 0.05f;

    [Tooltip("Time window (seconds) to consider a tile dangerous even if the explosion is imminent.")]
    public float imminentWindowSeconds = 0.60f;

    [Tooltip("Additional reaction buffer (seconds) to avoid arriving exactly when an explosion starts.")]
    public float reactionBuffer = 0.05f;

    [Tooltip("Minimum time margin (seconds) a tile must remain safe after arrival to be considered a safe destination.")]
    public float safeTileMinTime = 0.35f;

    [Tooltip("BFS lookahead depth (in tiles) when searching for a safe escape route.")]
    public int escapeLookaheadDepth = 7;

    [Header("Bomb Distance Bias")]
    [Tooltip("Extra distance (in tiles) considered as unsafe beyond a bomb radius. Higher values make the boss flee earlier.")]
    public float safeDistanceAfterBomb = 3f;

    [Tooltip("Minimum time (seconds) between bomb placements for chaining.")]
    public float bombChainCooldown = 0.3f;

    [Header("Retargeting")]
    [Tooltip("How often (seconds) the boss re-evaluates the closest alive player.")]
    public float retargetInterval = 0.25f;

    [Header("Kick Behavior")]
    [Range(0f, 1f)]
    [Tooltip("Chance to opportunistically kick a nearby player bomb.")]
    public float opportunisticKickChance = 0.25f;

    [Tooltip("Maximum age (seconds) of a player bomb to be considered for an opportunistic kick.")]
    public float opportunisticKickMaxBombAge = 0.9f;

    [Tooltip("Cooldown (seconds) between kick decisions.")]
    public float kickDecisionCooldown = 0.35f;

    [Tooltip("Minimum safety score required after deciding to kick.")]
    public float minSafetyAfterKick = 1.5f;

    AIMovementController movement;
    BombController bomb;
    BombKickAbility kickAbility;

    float thinkTimer;
    float retargetTimer;
    Vector2 lastDirection = Vector2.zero;
    bool isEvading;
    float lastBombTime;
    float lastKickDecisionTime;

    void Awake()
    {
        movement = GetComponent<AIMovementController>();
        bomb = GetComponent<BombController>();
        kickAbility = GetComponent<BombKickAbility>();

        bomb.useAIInput = true;
        movement.isBoss = true;
    }

    void Start()
    {
        PickClosestAlivePlayer();
        retargetTimer = retargetInterval;
        thinkTimer = 0f;
    }

    void Update()
    {
        if (GamePauseController.IsPaused)
            return;

        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            PickClosestAlivePlayer();
            retargetTimer = retargetInterval;
        }

        Bomb[] bombs = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Vector2 myPos = RoundToTile(transform.position);

        bool dangerNow = IsTileDangerousNowOrSoon(myPos, bombs, 0f);
        float interval = dangerNow ? thinkIntervalDanger : thinkIntervalSafe;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            Think(bombs);
            thinkTimer = interval;
        }

        movement.SetAIDirection(lastDirection);
    }

    void PickClosestAlivePlayer()
    {
        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Vector2 myPos = RoundToTile(transform.position);

        Transform best = null;
        float bestDist = float.PositiveInfinity;

        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                if (!id.TryGetComponent<MovementController>(out MovementController p))
                    p = id.GetComponentInChildren<MovementController>(true);

                if (p == null) continue;
                if (!p.isActiveAndEnabled || !p.gameObject.activeInHierarchy) continue;
                if (p.isDead) continue;
                if (!p.CompareTag("Player")) continue;

                Vector2 pPos = RoundToTile(p.transform.position);

                float dist = Mathf.Abs(pPos.x - myPos.x) + Mathf.Abs(pPos.y - myPos.y);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p.transform;
                }
            }
        }
        else
        {
            var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (!p.CompareTag("Player")) continue;
                if (!p.isActiveAndEnabled || !p.gameObject.activeInHierarchy) continue;
                if (p.isDead) continue;

                Vector2 pPos = RoundToTile(p.transform.position);
                float dist = Mathf.Abs(pPos.x - myPos.x) + Mathf.Abs(pPos.y - myPos.y);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p.transform;
                }
            }
        }

        target = best;
    }

    void Think(Bomb[] bombs)
    {
        Vector2 myPos = RoundToTile(transform.position);

        bool inDanger = IsTileDangerousNowOrSoon(myPos, bombs, 0f);

        if (HasKickAbility())
        {
            if (TryTrappedKickIfNeeded(myPos, bombs))
                return;

            if (TryOpportunisticKickPlayerAdjacent(myPos, bombs, inDanger))
                return;
        }

        if (inDanger)
        {
            isEvading = true;
            lastDirection = GetEscapeStep_BFS(myPos, bombs);
            return;
        }

        if (isEvading)
        {
            if (IsTileDangerousNowOrSoon(myPos, bombs, 0f))
            {
                lastDirection = GetEscapeStep_BFS(myPos, bombs);
                return;
            }

            isEvading = false;
        }

        if (target == null)
        {
            lastDirection = GetBestDirectionAvoidingExplosionSmart(myPos, bombs);
            return;
        }

        Vector2 playerPos = RoundToTile(target.position);
        Vector2 delta = playerPos - myPos;

        float manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        if (manhattan > maxChaseDistance)
        {
            Vector2 dirFar = GetStepTowards(delta);
            Vector2 targetTileFar = myPos + dirFar;
            float eta = GetSingleStepTravelTimeSeconds();

            lastDirection = IsTileDangerousNowOrSoon(targetTileFar, bombs, eta)
                ? GetBestDirectionAvoidingExplosionSmart(myPos, bombs)
                : dirFar;
            return;
        }

        if ((sameRow || sameCol) && manhattan <= bomb.explosionRadius + 1)
        {
            if (manhattan > 1.01f)
            {
                Vector2 dirTo = GetStepTowards(delta);
                Vector2 targetTileTo = myPos + dirTo;
                float eta = GetSingleStepTravelTimeSeconds();

                lastDirection = IsTileDangerousNowOrSoon(targetTileTo, bombs, eta)
                    ? GetBestDirectionAvoidingExplosionSmart(myPos, bombs)
                    : dirTo;
                return;
            }

            TryPlaceBombChain(myPos);

            Bomb[] bombsNow = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isEvading = true;
            lastDirection = GetEscapeStep_BFS(myPos, bombsNow);
            return;
        }

        Vector2 dir = GetStepTowards(delta);
        Vector2 targetTile = myPos + dir;
        float eta2 = GetSingleStepTravelTimeSeconds();

        lastDirection = IsTileDangerousNowOrSoon(targetTile, bombs, eta2)
            ? GetBestDirectionAvoidingExplosionSmart(myPos, bombs)
            : dir;
    }

    bool HasKickAbility()
    {
        return kickAbility != null && kickAbility.IsEnabled;
    }

    bool TryOpportunisticKickPlayerAdjacent(Vector2 myPos, Bomb[] bombs, bool inDanger)
    {
        if (Time.time - lastKickDecisionTime < kickDecisionCooldown)
            return false;

        if (inDanger)
            return false;

        if (Random.value > opportunisticKickChance)
            return false;

        Bomb bestAdjPlayerBomb = null;
        float bestPlaced = float.NegativeInfinity;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null) continue;
            if (b.HasExploded) continue;
            if (!b.CanBeKicked) continue;
            if (b.Owner == null) continue;
            if (!b.Owner.CompareTag("Player")) continue;

            Vector2 bombPos = RoundToTile(b.GetLogicalPosition());
            Vector2 d = bombPos - myPos;

            float manhattan = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            if (manhattan != 1f)
                continue;

            float age = Time.time - b.PlacedTime;
            if (age > opportunisticKickMaxBombAge)
                continue;

            if (b.PlacedTime > bestPlaced)
            {
                bestPlaced = b.PlacedTime;
                bestAdjPlayerBomb = b;
            }
        }

        if (bestAdjPlayerBomb == null)
            return false;

        Vector2 bestBombPos2 = RoundToTile(bestAdjPlayerBomb.GetLogicalPosition());
        Vector2 kickDir = bestBombPos2 - myPos;

        Vector2 escapeDir = GetEscapeStep_BFS(myPos, bombs);
        float escapeSafety = escapeDir == Vector2.zero ? ScoreTile(myPos, bombs, 0f) : ScoreTile(myPos + escapeDir, bombs, GetSingleStepTravelTimeSeconds());

        if (escapeSafety < minSafetyAfterKick)
            return false;

        lastKickDecisionTime = Time.time;
        isEvading = true;
        lastDirection = kickDir;
        return true;
    }

    bool TryTrappedKickIfNeeded(Vector2 myPos, Bomb[] bombs)
    {
        if (Time.time - lastKickDecisionTime < kickDecisionCooldown)
            return false;

        Vector2 bestSafe = GetEscapeStep_BFS(myPos, bombs);
        if (bestSafe != Vector2.zero)
            return false;

        Bomb bestBomb = null;
        float bestTime = float.NegativeInfinity;
        Vector2 bestDir = Vector2.zero;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null) continue;

            Vector2 bombPos = RoundToTile(b.GetLogicalPosition());
            Vector2 delta = bombPos - myPos;
            float manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);

            if (manhattan != 1f) continue;
            if (b.HasExploded) continue;
            if (!b.CanBeKicked) continue;

            if (b.PlacedTime > bestTime)
            {
                bestTime = b.PlacedTime;
                bestBomb = b;
                bestDir = delta;
            }
        }

        if (bestBomb == null)
            return false;

        lastKickDecisionTime = Time.time;
        isEvading = true;
        lastDirection = bestDir;
        return true;
    }

    void TryPlaceBombChain(Vector2 myPos)
    {
        if (bomb == null) return;
        if (bomb.BombsRemaining <= 0) return;
        if (Time.time - lastBombTime < bombChainCooldown) return;
        if (IsTileWithBomb(myPos)) return;

        bomb.RequestBombFromAI();
        lastBombTime = Time.time;
    }

    Vector2 GetBestDirectionAvoidingExplosionSmart(Vector2 myPos, Bomb[] bombs)
    {
        Vector2 step = GetEscapeStep_BFS(myPos, bombs);
        return step;
    }

    Vector2 GetEscapeStep_BFS(Vector2 myPos, Bomb[] bombs)
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        float stepTime = GetSingleStepTravelTimeSeconds();

        var visited = new HashSet<Vector2>();
        var q = new Queue<(Vector2 pos, int depth, Vector2 firstStep)>();

        visited.Add(myPos);

        Vector2 bestFirst = Vector2.zero;
        float bestScore = -999999f;

        if (!IsTileDangerousNowOrSoon(myPos, bombs, 0f))
        {
            float stayScore = ScoreTile(myPos, bombs, 0f);
            bestFirst = Vector2.zero;
            bestScore = stayScore;
        }

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2 n = myPos + dirs[i];

            if (!IsWalkableTile(n))
                continue;

            float eta = stepTime;
            if (IsTileDangerousNowOrSoon(n, bombs, eta))
                continue;

            visited.Add(n);
            q.Enqueue((n, 1, dirs[i]));
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            float eta = cur.depth * stepTime;
            float timeToHit = GetMinTimeUntilBlastHitsTile(cur.pos, bombs);

            bool safeEnough = timeToHit >= eta + safeTileMinTime;
            float score = ScoreTile(cur.pos, bombs, eta);

            if (safeEnough && score > bestScore)
            {
                bestScore = score;
                bestFirst = cur.firstStep;

                if (cur.depth <= 2 && bestScore > 50f)
                    break;
            }

            if (cur.depth >= escapeLookaheadDepth)
                continue;

            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2 nx = cur.pos + dirs[i];
                if (visited.Contains(nx))
                    continue;

                if (!IsWalkableTile(nx))
                    continue;

                float nEta = (cur.depth + 1) * stepTime;
                if (IsTileDangerousNowOrSoon(nx, bombs, nEta))
                    continue;

                visited.Add(nx);
                q.Enqueue((nx, cur.depth + 1, cur.firstStep));
            }
        }

        return bestFirst;
    }

    bool IsWalkableTile(Vector2 tile)
    {
        if (IsTileWithExplosion(tile))
            return false;

        bool blocked = Physics2D.OverlapBox(
            tile,
            Vector2.one * (movement.tileSize * 0.6f),
            0f,
            movement.obstacleMask);

        return !blocked;
    }

    bool IsTileDangerousNowOrSoon(Vector2 tilePos, Bomb[] bombs, float arrivalEta)
    {
        if (IsTileWithExplosion(tilePos))
            return true;

        float timeToHit = GetMinTimeUntilBlastHitsTile(tilePos, bombs);

        return timeToHit <= arrivalEta + reactionBuffer + imminentWindowSeconds;
    }

    float GetMinTimeUntilBlastHitsTile(Vector2 tilePos, Bomb[] bombs)
    {
        float best = 999f;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null || b.HasExploded)
                continue;

            float remaining = GetBombRemainingFuseSecondsSafe(b);

            Vector2 bombTile = GetBombTilePosRoundedSafe(b);
            if (IsTileWithExplosion(bombTile))
                remaining = Mathf.Min(remaining, 0.08f);

            if (!IsTileInBlastLine(tilePos, b, out float linearDist))
                continue;

            int radius = GetBombExplosionRadiusSafe(b);
            if (linearDist <= radius + safeDistanceAfterBomb)
            {
                if (remaining < best)
                    best = remaining;
            }
        }

        return best;
    }

    bool IsTileInBlastLine(Vector2 tilePos, Bomb b, out float linearDist)
    {
        Vector2 bombPos = GetBombTilePosRoundedSafe(b);
        Vector2 delta = tilePos - bombPos;

        bool sameRow = Mathf.Abs(delta.y) < 0.1f;
        bool sameCol = Mathf.Abs(delta.x) < 0.1f;

        linearDist = 999f;
        if (!sameRow && !sameCol)
            return false;

        linearDist = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);

        int radius = GetBombExplosionRadiusSafe(b);
        return linearDist <= radius;
    }

    float ScoreTile(Vector2 tile, Bomb[] bombs, float arrivalEta)
    {
        float t = GetMinTimeUntilBlastHitsTile(tile, bombs);

        if (t <= arrivalEta + reactionBuffer)
            return -999999f;

        float linePenalty = 0f;

        for (int i = 0; i < bombs.Length; i++)
        {
            var b = bombs[i];
            if (b == null || b.HasExploded) continue;

            if (IsTileInBlastLine(tile, b, out float dist))
            {
                linePenalty += (10f / Mathf.Max(1f, dist));
            }
        }

        float margin = t - arrivalEta;
        return (margin * 100f) - (linePenalty * 5f);
    }

    float GetSingleStepTravelTimeSeconds()
    {
        float spd = Mathf.Max(0.0001f, movement.speed);
        float ts = Mathf.Max(0.0001f, movement.tileSize);
        return ts / spd;
    }

    bool IsTileWithExplosion(Vector2 tilePos)
    {
        int explosionLayer = LayerMask.NameToLayer("Explosion");
        int mask = 1 << explosionLayer;

        return Physics2D.OverlapBox(tilePos, Vector2.one * 0.4f, 0f, mask) != null;
    }

    bool IsTileWithBomb(Vector2 tilePos)
    {
        int bombLayer = LayerMask.NameToLayer("Bomb");
        int mask = 1 << bombLayer;

        return Physics2D.OverlapBox(tilePos, Vector2.one * 0.4f, 0f, mask) != null;
    }

    Vector2 RoundToTile(Vector2 p)
    {
        return new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
    }

    Vector2 GetStepTowards(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);

        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    float GetBombRemainingFuseSecondsSafe(Bomb b)
    {
        if (b == null) return 999f;
        if (b.HasExploded) return 0f;

        if (b.IsControlBomb)
            return 999f;

        BombController owner = b.Owner;
        if (owner == null) return 999f;

        float fuse = owner.bombFuseTime;
        float age = Time.time - b.PlacedTime;
        return Mathf.Max(0f, fuse - age);
    }

    int GetBombExplosionRadiusSafe(Bomb b)
    {
        if (b == null) return 2;
        BombController owner = b.Owner;
        if (owner == null) return 2;
        return owner.explosionRadius;
    }

    Vector2 GetBombTilePosRoundedSafe(Bomb b)
    {
        if (b == null)
            return Vector2.zero;

        Vector2 p = b.GetLogicalPosition();
        p.x = Mathf.Round(p.x);
        p.y = Mathf.Round(p.y);
        return p;
    }
}
