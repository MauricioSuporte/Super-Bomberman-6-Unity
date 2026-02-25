using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(BombController))]
[RequireComponent(typeof(BombKickAbility))]
public class BossBomberAI : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float maxChaseDistance = 20f;

    [Header("Thinking")]
    public float thinkIntervalSafe = 0.20f;
    public float thinkIntervalDanger = 0.05f;
    public float imminentWindowSeconds = 0.60f;
    public float reactionBuffer = 0.05f;
    public float safeTileMinTime = 0.35f;
    public int escapeLookaheadDepth = 7;

    [Header("Bomb Placement")]
    public float bombChainCooldown = 0.3f;
    public float safeDistanceAfterBomb = 3f;
    public int extraBombRangeTiles = 0;
    public bool allowPlaceBombInRange = true;
    public bool allowPlaceBombOnClearLine = true;

    [Header("Always Plant Near Player")]
    public bool alwaysPlantNearPlayer = true;
    public int alwaysPlantDistanceTiles = 2;
    public bool allowPlantEvenWithoutEscape = true;
    public float nearPlantMinCooldown = 0.35f;

    [Header("Retargeting")]
    public float retargetInterval = 0.25f;

    [Header("Kick Behavior")]
    [Range(0f, 1f)] public float opportunisticKickChance = 0.25f;
    public float opportunisticKickMaxBombAge = 0.9f;
    public float kickDecisionCooldown = 0.35f;
    public float minSafetyAfterKick = 1.5f;

    private AIMovementController movement;
    private BombController bomb;
    private BombKickAbility kickAbility;

    private float thinkTimer;
    private float retargetTimer;
    private Vector2 lastDirection = Vector2.zero;
    private bool isEvading;
    private float lastBombTime;
    private float lastKickDecisionTime;

    private int explosionLayer;
    private int explosionMask;
    private int bombLayer;
    private int bombMask;
    private int stageMask;

    private static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    void Awake()
    {
        movement = GetComponent<AIMovementController>();
        bomb = GetComponent<BombController>();
        kickAbility = GetComponent<BombKickAbility>();

        bomb.useAIInput = true;
        movement.isBoss = true;

        explosionLayer = LayerMask.NameToLayer("Explosion");
        explosionMask = (explosionLayer >= 0) ? (1 << explosionLayer) : 0;

        bombLayer = LayerMask.NameToLayer("Bomb");
        bombMask = (bombLayer >= 0) ? (1 << bombLayer) : 0;

        stageMask = LayerMask.GetMask("Stage");
    }

    void Start()
    {
        PickClosestAlivePlayer();
        retargetTimer = retargetInterval;
        thinkTimer = 0f;
        lastBombTime = -999f;
        lastKickDecisionTime = -999f;
    }

    void Update()
    {
        if (GamePauseController.IsPaused)
            return;

        if (movement == null || movement.isDead)
        {
            if (movement != null)
                movement.SetAIDirection(Vector2.zero);
            return;
        }

        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            PickClosestAlivePlayer();
            retargetTimer = retargetInterval;
        }

        Vector2 myTile = RoundToTile((Vector2)transform.position, movement.tileSize);
        Bomb[] bombsNow = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        bool dangerNow = IsTileDangerousNowOrSoon(myTile, bombsNow, 0f);
        float interval = dangerNow ? thinkIntervalDanger : thinkIntervalSafe;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            Think(myTile, bombsNow, dangerNow);
            thinkTimer = interval;
        }

        movement.SetAIDirection(lastDirection);
    }

    void PickClosestAlivePlayer()
    {
        Vector2 myPos = RoundToTile((Vector2)transform.position, movement.tileSize);

        Transform best = null;
        float bestDist = float.PositiveInfinity;

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

                Vector2 pPos = RoundToTile((Vector2)p.transform.position, movement.tileSize);
                float dist = Manhattan(myPos, pPos);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p.transform;
                }
            }

            target = best;
            return;
        }

        var players = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (!p.CompareTag("Player")) continue;
            if (!p.isActiveAndEnabled || !p.gameObject.activeInHierarchy) continue;
            if (p.isDead) continue;

            Vector2 pPos = RoundToTile((Vector2)p.transform.position, movement.tileSize);
            float dist = Manhattan(myPos, pPos);

            if (dist < bestDist)
            {
                bestDist = dist;
                best = p.transform;
            }
        }

        target = best;
    }

    void Think(Vector2 myTile, Bomb[] bombsNow, bool dangerNow)
    {
        if (movement == null || movement.isDead)
        {
            lastDirection = Vector2.zero;
            return;
        }

        if (IsDamagedPlaying())
        {
            lastDirection = Vector2.zero;
            return;
        }

        if (target != null && alwaysPlantNearPlayer)
        {
            Vector2 targetTile0 = RoundToTile((Vector2)target.position, movement.tileSize);
            float dist0 = Manhattan(myTile, targetTile0);

            if (!dangerNow && dist0 <= Mathf.Max(1, alwaysPlantDistanceTiles) + 0.01f)
            {
                if (TryPlaceBombAlwaysNearPlayer(myTile, targetTile0, bombsNow))
                    return;
            }
        }

        if (HasKickAbility())
        {
            if (TryTrappedKickIfNeeded(myTile, bombsNow))
                return;

            if (TryOpportunisticKickPlayerAdjacent(myTile, bombsNow, dangerNow))
                return;
        }

        if (dangerNow)
        {
            isEvading = true;
            lastDirection = GetEscapeStep_BFS(myTile, bombsNow, 0f);
            if (lastDirection == Vector2.zero && target != null)
            {
                Vector2 targetTile = RoundToTile((Vector2)target.position, movement.tileSize);
                lastDirection = GetBestStepAwayFromTarget(myTile, targetTile, bombsNow);
            }
            return;
        }

        if (isEvading)
        {
            if (IsTileDangerousNowOrSoon(myTile, bombsNow, 0f))
            {
                lastDirection = GetEscapeStep_BFS(myTile, bombsNow, 0f);
                if (lastDirection == Vector2.zero && target != null)
                {
                    Vector2 targetTile = RoundToTile((Vector2)target.position, movement.tileSize);
                    lastDirection = GetBestStepAwayFromTarget(myTile, targetTile, bombsNow);
                }
                return;
            }

            isEvading = false;
        }

        if (target == null)
        {
            lastDirection = WanderSafely(myTile, bombsNow);
            return;
        }

        Vector2 targetTile2 = RoundToTile((Vector2)target.position, movement.tileSize);
        float distToTarget = Manhattan(myTile, targetTile2);

        if (TryPlaceBombFromRangeIfGood(myTile, targetTile2, bombsNow, distToTarget))
            return;

        if (distToTarget <= 1.01f)
        {
            if (TryPlaceBombWithEscape(myTile, bombsNow))
                return;

            lastDirection = GetEscapeStep_BFS(myTile, bombsNow, 0f);
            if (lastDirection == Vector2.zero)
                lastDirection = GetBestStepAwayFromTarget(myTile, targetTile2, bombsNow);
            return;
        }

        if (allowPlaceBombOnClearLine && IsClearLine(myTile, targetTile2, out Vector2 lineDir, out float lineDist))
        {
            int myRadius = GetEffectiveMyBombRadius() + Mathf.Max(0, extraBombRangeTiles);

            if (lineDist <= myRadius + 0.01f)
            {
                if (TryPlaceBombWithEscape(myTile, bombsNow))
                    return;

                lastDirection = GetEscapeStep_BFS(myTile, bombsNow, 0f);
                if (lastDirection == Vector2.zero)
                    lastDirection = GetBestStepAwayFromTarget(myTile, targetTile2, bombsNow);
                return;
            }

            if (lineDist <= myRadius + 1f)
            {
                Vector2 step = lineDir;
                float eta = GetSingleStepTravelTimeSeconds();
                Vector2 next = myTile + step;

                if (!IsWalkableTile(next) || IsTileDangerousNowOrSoon(next, bombsNow, eta))
                {
                    lastDirection = GetEscapeStep_BFS(myTile, bombsNow, 0f);
                    if (lastDirection == Vector2.zero)
                        lastDirection = GetBestStepAwayFromTarget(myTile, targetTile2, bombsNow);
                    return;
                }

                lastDirection = step;
                return;
            }
        }

        if (distToTarget > maxChaseDistance)
        {
            Vector2 step = GetStepTowards(targetTile2 - myTile);
            Vector2 next = myTile + step;
            float eta = GetSingleStepTravelTimeSeconds();

            lastDirection = (step != Vector2.zero && IsWalkableTile(next) && !IsTileDangerousNowOrSoon(next, bombsNow, eta))
                ? step
                : WanderSafely(myTile, bombsNow);

            return;
        }

        Vector2 pathStep = GetChaseStep_AStar(myTile, targetTile2, bombsNow, 14);
        if (pathStep == Vector2.zero)
            pathStep = WanderSafely(myTile, bombsNow);

        lastDirection = pathStep;
    }

    bool TryPlaceBombAlwaysNearPlayer(Vector2 myTile, Vector2 targetTile, Bomb[] bombsNow)
    {
        if (movement == null || movement.isDead) return false;
        if (movement.IsDamagedVisualActive) return false;
        if (bomb == null) return false;
        if (bomb.BombsRemaining <= 0) return false;
        if (IsTileWithBomb(myTile)) return false;

        float cd = Mathf.Max(0.05f, nearPlantMinCooldown);
        if (Time.time - lastBombTime < cd) return false;

        Vector2 escape = GetEscapeStep_BFS(myTile, bombsNow, 0f);

        if (escape == Vector2.zero)
        {
            if (!allowPlantEvenWithoutEscape)
                return false;

            if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
                return false;

            lastBombTime = Time.time;

            Bomb[] bombsAfter0 = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isEvading = true;

            Vector2 away = GetBestStepAwayFromTarget(myTile, targetTile, bombsAfter0);
            if (away != Vector2.zero)
                lastDirection = away;
            else
                lastDirection = WanderSafely(myTile, bombsAfter0);

            return true;
        }

        float eta = GetSingleStepTravelTimeSeconds();
        float escapeSafety = ScoreTile(myTile + escape, bombsNow, eta);

        if (escapeSafety < minSafetyAfterKick)
        {
            if (!allowPlantEvenWithoutEscape)
                return false;

            if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
                return false;

            lastBombTime = Time.time;

            Bomb[] bombsAfter1 = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isEvading = true;

            Vector2 away = GetBestStepAwayFromTarget(myTile, targetTile, bombsAfter1);
            if (away != Vector2.zero)
                lastDirection = away;
            else
                lastDirection = WanderSafely(myTile, bombsAfter1);

            return true;
        }

        if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
            return false;

        lastBombTime = Time.time;

        Bomb[] bombsAfter = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        isEvading = true;
        lastDirection = GetEscapeStep_BFS(myTile, bombsAfter, 0f);

        if (lastDirection == Vector2.zero)
            lastDirection = GetBestStepAwayFromTarget(myTile, targetTile, bombsAfter);

        return true;
    }

    Vector2 GetBestStepAwayFromTarget(Vector2 myTile, Vector2 targetTile, Bomb[] bombsNow)
    {
        float stepTime = GetSingleStepTravelTimeSeconds();
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        Vector2 awayPrimary = GetStepTowards(myTile - targetTile);

        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 dir = (pass == 0 && i == 0) ? awayPrimary : Dirs[i];
                Vector2 n = myTile + dir;

                if (dir == Vector2.zero) continue;
                if (!IsWalkableTile(n)) continue;
                if (IsTileDangerousNowOrSoon(n, bombsNow, stepTime)) continue;

                float s = ScoreTile(n, bombsNow, stepTime);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = dir;
                }
            }

            if (best != Vector2.zero)
                return best;
        }

        return Vector2.zero;
    }

    bool TryPlaceBombFromRangeIfGood(Vector2 myTile, Vector2 targetTile, Bomb[] bombsNow, float distToTarget)
    {
        if (movement == null || movement.isDead) return false;
        if (!allowPlaceBombInRange)
            return false;

        int myRadius = GetEffectiveMyBombRadius() + Mathf.Max(0, extraBombRangeTiles);

        if (distToTarget > myRadius + 0.01f)
            return false;

        if (!IsPlaceBombAllowedNow(myTile))
            return false;

        Vector2 escape = GetEscapeStep_BFS(myTile, bombsNow, 0f);
        if (escape == Vector2.zero)
            return false;

        float escapeSafety = ScoreTile(myTile + escape, bombsNow, GetSingleStepTravelTimeSeconds());
        if (escapeSafety < minSafetyAfterKick)
            return false;

        if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
            return false;

        lastBombTime = Time.time;

        Bomb[] bombsAfter = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        isEvading = true;
        lastDirection = GetEscapeStep_BFS(myTile, bombsAfter, 0f);

        return true;
    }

    bool TryPlaceBombWithEscape(Vector2 myTile, Bomb[] bombsNow)
    {
        if (movement == null || movement.isDead) return false;

        if (!IsPlaceBombAllowedNow(myTile))
            return false;

        Vector2 escape = GetEscapeStep_BFS(myTile, bombsNow, 0f);
        if (escape == Vector2.zero)
            return false;

        float escapeSafety = ScoreTile(myTile + escape, bombsNow, GetSingleStepTravelTimeSeconds());
        if (escapeSafety < minSafetyAfterKick)
            return false;

        if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
            return false;

        lastBombTime = Time.time;

        Bomb[] bombsAfter = FindObjectsByType<Bomb>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        isEvading = true;
        lastDirection = GetEscapeStep_BFS(myTile, bombsAfter, 0f);
        return true;
    }

    bool IsPlaceBombAllowedNow(Vector2 myTile)
    {
        if (movement == null || movement.isDead) return false;
        if (movement.IsDamagedVisualActive) return false;
        if (bomb == null) return false;
        if (bomb.BombsRemaining <= 0) return false;
        if (Time.time - lastBombTime < bombChainCooldown) return false;
        if (IsTileWithBomb(myTile)) return false;
        return true;
    }

    bool HasKickAbility()
    {
        return kickAbility != null && kickAbility.IsEnabled;
    }

    bool TryOpportunisticKickPlayerAdjacent(Vector2 myTile, Bomb[] bombsNow, bool dangerNow)
    {
        if (movement == null || movement.isDead) return false;

        if (Time.time - lastKickDecisionTime < kickDecisionCooldown)
            return false;

        if (dangerNow)
            return false;

        if (Random.value > opportunisticKickChance)
            return false;

        Bomb bestAdjPlayerBomb = null;
        float bestPlaced = float.NegativeInfinity;

        for (int i = 0; i < bombsNow.Length; i++)
        {
            var b = bombsNow[i];
            if (b == null) continue;
            if (b.HasExploded) continue;
            if (!b.CanBeKicked) continue;
            if (b.Owner == null) continue;
            if (!b.Owner.CompareTag("Player")) continue;

            Vector2 bombTile = RoundToTile(b.GetLogicalPosition(), movement.tileSize);
            if (Manhattan(myTile, bombTile) != 1f)
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

        Vector2 bestBombTile = RoundToTile(bestAdjPlayerBomb.GetLogicalPosition(), movement.tileSize);
        Vector2 kickDir = bestBombTile - myTile;

        Vector2 escapeDir = GetEscapeStep_BFS(myTile, bombsNow, 0f);
        float escapeSafety = escapeDir == Vector2.zero
            ? ScoreTile(myTile, bombsNow, 0f)
            : ScoreTile(myTile + escapeDir, bombsNow, GetSingleStepTravelTimeSeconds());

        if (escapeSafety < minSafetyAfterKick)
            return false;

        lastKickDecisionTime = Time.time;
        isEvading = true;
        lastDirection = kickDir;
        return true;
    }

    bool TryTrappedKickIfNeeded(Vector2 myTile, Bomb[] bombsNow)
    {
        if (movement == null || movement.isDead) return false;

        if (Time.time - lastKickDecisionTime < kickDecisionCooldown)
            return false;

        Vector2 bestSafe = GetEscapeStep_BFS(myTile, bombsNow, 0f);
        if (bestSafe != Vector2.zero)
            return false;

        Bomb bestBomb = null;
        float bestTime = float.NegativeInfinity;
        Vector2 bestDir = Vector2.zero;

        for (int i = 0; i < bombsNow.Length; i++)
        {
            var b = bombsNow[i];
            if (b == null) continue;

            Vector2 bombTile = RoundToTile(b.GetLogicalPosition(), movement.tileSize);
            Vector2 delta = bombTile - myTile;

            if (Manhattan(myTile, bombTile) != 1f) continue;
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

    Vector2 WanderSafely(Vector2 myTile, Bomb[] bombsNow)
    {
        float stepTime = GetSingleStepTravelTimeSeconds();
        Vector2 best = Vector2.zero;
        float bestScore = ScoreTile(myTile, bombsNow, 0f);

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i];
            if (!IsWalkableTile(n))
                continue;

            if (IsTileDangerousNowOrSoon(n, bombsNow, stepTime))
                continue;

            float s = ScoreTile(n, bombsNow, stepTime);
            if (s > bestScore)
            {
                bestScore = s;
                best = Dirs[i];
            }
        }

        return best;
    }

    Vector2 GetEscapeStep_BFS(Vector2 myTile, Bomb[] bombsNow, float arrivalEtaBase)
    {
        float stepTime = GetSingleStepTravelTimeSeconds();

        var visited = new HashSet<Vector2>();
        var q = new Queue<Node>();

        visited.Add(myTile);

        Vector2 bestFirst = Vector2.zero;
        float bestScore = ScoreTile(myTile, bombsNow, arrivalEtaBase);

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i];
            if (!IsWalkableTile(n))
                continue;

            float eta = arrivalEtaBase + stepTime;
            if (IsTileDangerousNowOrSoon(n, bombsNow, eta))
                continue;

            visited.Add(n);
            q.Enqueue(new Node(n, 1, Dirs[i]));
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            float eta = arrivalEtaBase + cur.depth * stepTime;
            float timeToHit = GetMinTimeUntilBlastHitsTile(cur.pos, bombsNow);

            bool safeEnough = timeToHit >= eta + safeTileMinTime;
            float score = ScoreTile(cur.pos, bombsNow, eta);

            if (safeEnough && score > bestScore)
            {
                bestScore = score;
                bestFirst = cur.firstStep;

                if (cur.depth <= 2 && bestScore > 50f)
                    break;
            }

            if (cur.depth >= escapeLookaheadDepth)
                continue;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 nx = cur.pos + Dirs[i];
                if (visited.Contains(nx))
                    continue;

                if (!IsWalkableTile(nx))
                    continue;

                float nEta = arrivalEtaBase + (cur.depth + 1) * stepTime;
                if (IsTileDangerousNowOrSoon(nx, bombsNow, nEta))
                    continue;

                visited.Add(nx);
                q.Enqueue(new Node(nx, cur.depth + 1, cur.firstStep));
            }
        }

        return bestFirst;
    }

    Vector2 GetChaseStep_AStar(Vector2 start, Vector2 goal, Bomb[] bombsNow, int maxNodes)
    {
        float stepTime = GetSingleStepTravelTimeSeconds();

        var open = new List<AStarNode>(32);
        var bestG = new Dictionary<Vector2, float>(64);

        open.Add(new AStarNode(start, 0f, Heuristic(start, goal), Vector2.zero));
        bestG[start] = 0f;

        int expanded = 0;

        while (open.Count > 0 && expanded < Mathf.Max(8, maxNodes))
        {
            int bestIndex = 0;
            float bestF = open[0].f;

            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < bestF)
                {
                    bestF = open[i].f;
                    bestIndex = i;
                }
            }

            var cur = open[bestIndex];
            open.RemoveAt(bestIndex);

            expanded++;

            if (cur.pos == goal)
                return cur.firstStep;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 dir = Dirs[i];
                Vector2 nx = cur.pos + dir;

                if (!IsWalkableTile(nx))
                    continue;

                float eta = (cur.g + 1f) * stepTime;
                if (IsTileDangerousNowOrSoon(nx, bombsNow, eta))
                    continue;

                float g2 = cur.g + 1f;
                if (bestG.TryGetValue(nx, out float prev) && prev <= g2)
                    continue;

                bestG[nx] = g2;

                Vector2 first = cur.firstStep == Vector2.zero ? dir : cur.firstStep;
                float h = Heuristic(nx, goal);
                float risk = Mathf.Clamp01(1f / Mathf.Max(0.25f, GetMinTimeUntilBlastHitsTile(nx, bombsNow)));
                float f = g2 + h + (risk * 2.0f);

                open.Add(new AStarNode(nx, g2, f, first));
            }
        }

        Vector2 bestStep = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = start + Dirs[i];
            if (!IsWalkableTile(n))
                continue;

            if (IsTileDangerousNowOrSoon(n, bombsNow, stepTime))
                continue;

            float s = 1000f - Heuristic(n, goal) + ScoreTile(n, bombsNow, stepTime) * 0.01f;
            if (s > bestScore)
            {
                bestScore = s;
                bestStep = Dirs[i];
            }
        }

        return bestStep;
    }

    bool IsWalkableTile(Vector2 tileCenter)
    {
        if (IsTileWithExplosion(tileCenter))
            return false;

        float t = Mathf.Max(0.0001f, movement.tileSize);
        Vector2 size = Vector2.one * (t * 0.6f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, movement.obstacleMask);
        if (hits == null || hits.Length == 0)
            return true;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            if (h.gameObject == gameObject) continue;
            if (h.isTrigger) continue;
            return false;
        }

        return true;
    }

    bool IsTileDangerousNowOrSoon(Vector2 tileCenter, Bomb[] bombsNow, float arrivalEta)
    {
        if (IsTileWithExplosion(tileCenter))
            return true;

        float t = GetMinTimeUntilBlastHitsTile(tileCenter, bombsNow);
        return t <= arrivalEta + reactionBuffer + imminentWindowSeconds;
    }

    float GetMinTimeUntilBlastHitsTile(Vector2 tileCenter, Bomb[] bombsNow)
    {
        float best = 999f;

        for (int i = 0; i < bombsNow.Length; i++)
        {
            var b = bombsNow[i];
            if (b == null || b.HasExploded)
                continue;

            float remaining = GetBombRemainingFuseSecondsSafe(b);
            Vector2 bombTile = RoundToTile(b.GetLogicalPosition(), movement.tileSize);

            if (!IsTileInBlastLineWithBlocking(bombTile, tileCenter, GetBombExplosionRadiusSafe(b), out float dist))
                continue;

            if (dist <= GetBombExplosionRadiusSafe(b) + safeDistanceAfterBomb)
            {
                if (remaining < best)
                    best = remaining;
            }
        }

        return best;
    }

    bool IsTileInBlastLineWithBlocking(Vector2 bombTile, Vector2 tileCenter, int radius, out float linearDist)
    {
        Vector2 delta = tileCenter - bombTile;
        bool sameRow = Mathf.Abs(delta.y) < 0.001f;
        bool sameCol = Mathf.Abs(delta.x) < 0.001f;

        linearDist = 999f;
        if (!sameRow && !sameCol)
            return false;

        Vector2 dir = sameRow ? new Vector2(Mathf.Sign(delta.x), 0f) : new Vector2(0f, Mathf.Sign(delta.y));
        linearDist = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);

        if (linearDist > radius)
            return false;

        int steps = Mathf.RoundToInt(linearDist);
        Vector2 cur = bombTile;

        for (int i = 0; i < steps; i++)
        {
            cur += dir * movement.tileSize;
            if (cur == tileCenter)
                return true;

            if (IsBlastBlockedAt(cur))
                return false;
        }

        return true;
    }

    bool IsBlastBlockedAt(Vector2 tileCenter)
    {
        float t = Mathf.Max(0.0001f, movement.tileSize);
        Vector2 size = Vector2.one * (t * 0.55f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(tileCenter, size, 0f, stageMask);
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null) continue;
                if (h.isTrigger) continue;

                if (h.CompareTag("Destructibles") || h.CompareTag("Indestructibles"))
                    return true;
            }
        }

        if (bombMask != 0)
        {
            Collider2D bh = Physics2D.OverlapBox(tileCenter, size, 0f, bombMask);
            if (bh != null)
                return true;
        }

        return false;
    }

    float ScoreTile(Vector2 tileCenter, Bomb[] bombsNow, float arrivalEta)
    {
        float tHit = GetMinTimeUntilBlastHitsTile(tileCenter, bombsNow);
        if (tHit <= arrivalEta + reactionBuffer)
            return -999999f;

        float margin = tHit - arrivalEta;

        float bombProxPenalty = 0f;
        for (int i = 0; i < bombsNow.Length; i++)
        {
            var b = bombsNow[i];
            if (b == null || b.HasExploded) continue;

            Vector2 bt = RoundToTile(b.GetLogicalPosition(), movement.tileSize);
            float d = Manhattan(tileCenter, bt);
            bombProxPenalty += 1f / Mathf.Max(1f, d);
        }

        float openBonus = 0f;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = tileCenter + Dirs[i] * movement.tileSize;
            if (IsWalkableTile(n))
                openBonus += 0.25f;
        }

        float destructibleBonus = 0f;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 adj = tileCenter + Dirs[i] * movement.tileSize;

            float t = Mathf.Max(0.0001f, movement.tileSize);
            Vector2 size = Vector2.one * (t * 0.6f);

            Collider2D[] hits = Physics2D.OverlapBoxAll(adj, size, 0f, stageMask);

            if (hits != null)
            {
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h] != null && hits[h].CompareTag("Destructibles"))
                    {
                        destructibleBonus += 2.0f;
                    }
                }
            }
        }

        return (margin * 100f)
             + (openBonus * 5f)
             - (bombProxPenalty * 40f)
             + destructibleBonus;
    }

    bool IsTileWithExplosion(Vector2 tileCenter)
    {
        if (explosionMask == 0)
            return false;

        float t = Mathf.Max(0.0001f, movement.tileSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * (t * 0.55f), 0f, explosionMask) != null;
    }

    bool IsTileWithBomb(Vector2 tileCenter)
    {
        if (bombMask == 0)
            return false;

        float t = Mathf.Max(0.0001f, movement.tileSize);
        return Physics2D.OverlapBox(tileCenter, Vector2.one * (t * 0.55f), 0f, bombMask) != null;
    }

    bool IsClearLine(Vector2 from, Vector2 to, out Vector2 dir, out float distTiles)
    {
        dir = Vector2.zero;
        distTiles = 999f;

        Vector2 delta = to - from;
        bool sameRow = Mathf.Abs(delta.y) < 0.001f;
        bool sameCol = Mathf.Abs(delta.x) < 0.001f;

        if (!sameRow && !sameCol)
            return false;

        dir = sameRow ? new Vector2(Mathf.Sign(delta.x), 0f) : new Vector2(0f, Mathf.Sign(delta.y));
        float distWorld = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
        distTiles = distWorld / Mathf.Max(0.0001f, movement.tileSize);

        int steps = Mathf.RoundToInt(distTiles);
        Vector2 cur = from;

        for (int i = 0; i < steps - 1; i++)
        {
            cur += dir * movement.tileSize;
            if (IsBlastBlockedAt(cur))
                return false;
        }

        return true;
    }

    int GetEffectiveMyBombRadius()
    {
        if (bomb == null)
            return 2;

        return bomb.explosionRadius;
    }

    float GetSingleStepTravelTimeSeconds()
    {
        float spdTilesPerSec = Mathf.Max(0.0001f, movement.speed);
        return 1f / spdTilesPerSec;
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
        if (b.IsControlBomb) return 999f;
        return b.RemainingFuseSeconds;
    }

    int GetBombExplosionRadiusSafe(Bomb b)
    {
        if (b == null) return 2;
        BombController owner = b.Owner;
        if (owner == null) return 2;
        return owner.explosionRadius;
    }

    static Vector2 RoundToTile(Vector2 p, float tileSize)
    {
        float t = Mathf.Max(0.0001f, tileSize);
        return new Vector2(Mathf.Round(p.x / t) * t, Mathf.Round(p.y / t) * t);
    }

    static float Manhattan(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    static float Heuristic(Vector2 a, Vector2 b)
    {
        return Manhattan(a, b);
    }

    bool IsDamagedPlaying()
    {
        return movement != null && movement.IsDamagedVisualActive;
    }

    struct Node
    {
        public Vector2 pos;
        public int depth;
        public Vector2 firstStep;

        public Node(Vector2 p, int d, Vector2 first)
        {
            pos = p;
            depth = d;
            firstStep = first;
        }
    }

    struct AStarNode
    {
        public Vector2 pos;
        public float g;
        public float f;
        public Vector2 firstStep;

        public AStarNode(Vector2 p, float gCost, float fCost, Vector2 first)
        {
            pos = p;
            g = gCost;
            f = fCost;
            firstStep = first;
        }
    }
}
