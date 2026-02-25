using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(MovementControllerAI))]
[RequireComponent(typeof(MovementController))]
public class BossEscapeOnLastLife : MonoBehaviour
{
    [Header("Boss Refs")]
    [SerializeField] private MovementController boss;
    [SerializeField] private MovementControllerAI aiMove;
    [SerializeField] private MonoBehaviour bossAIToDisable;

    [Header("Escape")]
    [SerializeField] private Vector2 escapeTarget = new(-3f, 2f);
    [SerializeField] private float reachEpsilon = 0.06f;
    [SerializeField, Min(0f)] private float waitBeforeEscapeSeconds = 2f;

    [Header("Finish Behavior")]
    [SerializeField] private bool destroyBossGameObject = false;
    [SerializeField] private bool snapToGoalOnFinish = true;

    [Header("Pathfinding")]
    [SerializeField] private int maxNodes = 4096;
    [SerializeField] private int maxExpandSteps = 20000;

    [Header("Players Lock")]
    [SerializeField] private bool disablePlayerBombControllerWhileLocked = true;
    [SerializeField] private bool disablePlayerCollidersWhileLocked = true;

    [Header("Oscillation Protection")]
    [SerializeField] private float nearGoalWindowSeconds = 1.20f;
    [SerializeField] private float nearGoalDistance = 0.14f;
    [SerializeField] private float forceSnapIfOscillatingDistance = 0.25f;

    private CharacterHealth health;

    private bool escapeArmed;
    private bool escapeStarted;
    private bool escapeFinished;

    private readonly List<MovementController> players = new();
    private readonly List<BombController> playerBombs = new();
    private readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    private readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    private Vector2 dbgStart;
    private Vector2 dbgGoal;

    private bool dbgNearGoal;
    private float dbgNearGoalFirstTime;
    private int dbgNearGoalCrosses;

    private void Awake()
    {
        if (!boss) boss = GetComponent<MovementController>();
        if (!aiMove) aiMove = GetComponent<MovementControllerAI>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.HitInvulnerabilityEnded += OnHitInvulnerabilityEnded;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.HitInvulnerabilityEnded -= OnHitInvulnerabilityEnded;
        }

        UnlockPlayers();
    }

    private void OnDamaged(int amount)
    {
        if (escapeStarted) return;
        if (!health) return;

        if (health.life == 1)
            escapeArmed = true;
    }

    private void OnHitInvulnerabilityEnded()
    {
        if (escapeStarted) return;
        if (!escapeArmed) return;

        escapeStarted = true;
        StartCoroutine(EscapeRoutine());
    }

    private IEnumerator EscapeRoutine()
    {
        if (!boss) yield break;

        RefreshPlayersRefs();
        LockPlayers();

        LockBossForEscape();

        var gate = FindFirstObjectByType<EndStageGateAnimated>();
        if (gate != null)
            gate.ForceUnlock();

        if (waitBeforeEscapeSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < waitBeforeEscapeSeconds)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                if (boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;

                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;

                yield return null;
            }
        }

        BombController.ExplodeAllControlBombsInStage();
        yield return null;

        float tile = Mathf.Max(0.0001f, boss.tileSize);

        dbgStart = RoundToGrid(GetBossPos(), tile);
        dbgGoal = RoundToGrid(escapeTarget, tile);

        dbgNearGoal = false;
        dbgNearGoalCrosses = 0;
        dbgNearGoalFirstTime = -999f;

        if (IsAt(dbgGoal))
        {
            FinalizeEscape(dbgGoal);
            yield break;
        }

        List<Vector2> path = FindPathAStar(dbgStart, dbgGoal, tile);
        if (path == null || path.Count == 0)
        {
            yield return StartCoroutine(FallbackWalkTowards(dbgGoal, tile));
            if (!escapeFinished) FinalizeEscape(dbgGoal);
            yield break;
        }

        for (int i = 1; i < path.Count; i++)
        {
            yield return MoveToTile(path[i], tile);

            if (escapeFinished) yield break;

            if (IsAt(dbgGoal))
            {
                FinalizeEscape(dbgGoal);
                yield break;
            }
        }

        FinalizeEscape(dbgGoal);
    }

    private void FinalizeEscape(Vector2 goal)
    {
        if (escapeFinished) return;
        escapeFinished = true;

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
        if (boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;

        if (snapToGoalOnFinish && boss)
        {
            if (boss.Rigidbody != null)
                boss.Rigidbody.position = goal;
            else
                boss.transform.position = goal;
        }

        UnlockPlayers();

        if (destroyBossGameObject)
            Destroy(boss.gameObject);
        else
            boss.gameObject.SetActive(false);
    }

    private void LockBossForEscape()
    {
        if (bossAIToDisable) bossAIToDisable.enabled = false;
        if (aiMove) aiMove.enabled = true;

        boss.SetInputLocked(false, true);
        boss.SetExplosionInvulnerable(true);

        if (boss.Rigidbody != null)
            boss.Rigidbody.linearVelocity = Vector2.zero;

        if (health != null)
        {
            health.StopInvulnerability();
            health.SetExternalInvulnerability(true);
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
    }

    private IEnumerator MoveToTile(Vector2 tileCenter, float tile)
    {
        float maxTime = 6f;
        float t = 0f;

        Vector2 startPos = GetBossPos();
        Vector2 delta0 = tileCenter - startPos;

        bool vertical = Mathf.Abs(delta0.y) >= Mathf.Abs(delta0.x);
        Vector2 desiredDir = vertical
            ? new Vector2(0f, Mathf.Sign(delta0.y))
            : new Vector2(Mathf.Sign(delta0.x), 0f);

        if (desiredDir == Vector2.zero)
            desiredDir = vertical ? Vector2.up : Vector2.right;

        float targetCoord = vertical ? tileCenter.y : tileCenter.x;
        float dirSign = vertical ? desiredDir.y : desiredDir.x;

        float stepEps = Mathf.Max(reachEpsilon, tile * 0.08f);

        while (t < maxTime)
        {
            if (escapeFinished) yield break;

            if (GamePauseController.IsPaused)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetBossPos();
            float remaining = vertical ? (targetCoord - pos.y) : (targetCoord - pos.x);
            float absRemaining = Mathf.Abs(remaining);

            float distToGoal = Vector2.Distance(pos, dbgGoal);

            if (absRemaining <= stepEps ||
                Mathf.Sign(remaining) != Mathf.Sign(dirSign))
            {
                SnapBossTo(tileCenter);
                break;
            }

            if (distToGoal <= forceSnapIfOscillatingDistance &&
                IsOscillatingNow(Time.time, distToGoal))
            {
                SnapBossTo(dbgGoal);
                break;
            }

            if (aiMove) aiMove.SetAIDirection(desiredDir);

            t += Time.deltaTime;
            yield return null;
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
        yield return null;
    }

    private IEnumerator FallbackWalkTowards(Vector2 goal, float tile)
    {
        float maxTime = 8f;
        float t = 0f;

        while (t < maxTime)
        {
            if (escapeFinished) yield break;

            if (GamePauseController.IsPaused)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetBossPos();
            Vector2 delta = goal - pos;

            if (delta.sqrMagnitude <= (reachEpsilon * reachEpsilon))
                break;

            Vector2 dir = PickCardinal(delta);
            if (aiMove) aiMove.SetAIDirection(dir);

            t += Time.deltaTime;
            yield return null;
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
    }

    private bool IsOscillatingNow(float now, float distToGoal)
    {
        bool nowNear = distToGoal <= nearGoalDistance;

        if (nowNear != dbgNearGoal)
        {
            dbgNearGoal = nowNear;
            dbgNearGoalCrosses++;

            if (dbgNearGoalFirstTime < 0f ||
                (now - dbgNearGoalFirstTime) > nearGoalWindowSeconds)
                dbgNearGoalFirstTime = now;
        }

        if (distToGoal > forceSnapIfOscillatingDistance) return false;
        if (dbgNearGoalCrosses < 6) return false;

        return (now - dbgNearGoalFirstTime) <= nearGoalWindowSeconds;
    }

    private bool IsAt(Vector2 goal)
        => (goal - GetBossPos()).sqrMagnitude <= (reachEpsilon * reachEpsilon);

    private Vector2 GetBossPos()
        => boss.Rigidbody != null ? boss.Rigidbody.position : (Vector2)transform.position;

    private static Vector2 PickCardinal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private static Vector2 RoundToGrid(Vector2 p, float tile)
        => new Vector2(
            Mathf.Round(p.x / tile) * tile,
            Mathf.Round(p.y / tile) * tile);

    private void SnapBossTo(Vector2 worldPos)
    {
        if (boss.Rigidbody != null)
        {
            boss.Rigidbody.position = worldPos;
            boss.Rigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            boss.transform.position = worldPos;
        }
    }

    #region Players Lock (igual ao anterior)

    private void RefreshPlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var id in ids)
        {
            if (!id) continue;

            if (!id.TryGetComponent(out MovementController move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move) continue;
            if (!move.CompareTag("Player")) continue;
            if (move.isDead) continue;

            players.Add(move);

            if (move.TryGetComponent(out BombController bomb))
                playerBombs.Add(bomb);
        }
    }

    private void LockPlayers()
    {
        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        foreach (var p in players)
        {
            if (!p) continue;

            p.SetInputLocked(true, true);
            p.ApplyDirectionFromVector(Vector2.zero);
            p.SetExplosionInvulnerable(true);

            if (p.Rigidbody != null)
                p.Rigidbody.linearVelocity = Vector2.zero;

            if (p.TryGetComponent<CharacterHealth>(out var ph))
            {
                ph.StopInvulnerability();
                ph.SetExternalInvulnerability(true);
            }

            if (disablePlayerCollidersWhileLocked)
            {
                var col = p.GetComponent<Collider2D>();
                if (col)
                {
                    cachedPlayerColliders[p] = col;
                    cachedColliderEnabled[p] = col.enabled;
                    col.enabled = false;
                }
            }
        }

        if (disablePlayerBombControllerWhileLocked)
        {
            foreach (var bomb in playerBombs)
                if (bomb) bomb.enabled = false;
        }
    }

    private void UnlockPlayers()
    {
        foreach (var p in players)
        {
            if (!p) continue;

            p.SetInputLocked(false, true);
            p.SetExplosionInvulnerable(false);

            if (p.TryGetComponent<CharacterHealth>(out var ph))
                ph.SetExternalInvulnerability(false);
        }

        foreach (var kv in cachedPlayerColliders)
        {
            if (kv.Key && kv.Value &&
                cachedColliderEnabled.TryGetValue(kv.Key, out bool wasEnabled))
                kv.Value.enabled = wasEnabled;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        if (disablePlayerBombControllerWhileLocked)
        {
            foreach (var bomb in playerBombs)
                if (bomb) bomb.enabled = true;
        }
    }

    #endregion

    #region A* (igual ao anterior)

    private struct Node
    {
        public Vector2 Pos;
        public int Parent;
        public int G;
        public int F;
    }

    private List<Vector2> FindPathAStar(Vector2 start, Vector2 goal, float tile)
    {
        if (start == goal)
            return new List<Vector2> { start };

        var open = new List<int>();
        var nodes = new List<Node>();
        var openMap = new Dictionary<Vector2, int>();
        var closed = new HashSet<Vector2>();

        int Heur(Vector2 a, Vector2 b)
            => Mathf.Abs(Mathf.RoundToInt((a.x - b.x) / tile)) +
               Mathf.Abs(Mathf.RoundToInt((a.y - b.y) / tile));

        if (IsSolidAtWorld(goal))
            return null;

        nodes.Add(new Node { Pos = start, Parent = -1, G = 0, F = Heur(start, goal) });
        open.Add(0);
        openMap[start] = 0;

        int expanded = 0;

        while (open.Count > 0)
        {
            int bestOpenIndex = 0;
            int bestNodeIndex = open[0];
            int bestF = nodes[bestNodeIndex].F;

            for (int i = 1; i < open.Count; i++)
            {
                int ni = open[i];
                if (nodes[ni].F < bestF)
                {
                    bestF = nodes[ni].F;
                    bestNodeIndex = ni;
                    bestOpenIndex = i;
                }
            }

            open.RemoveAt(bestOpenIndex);
            openMap.Remove(nodes[bestNodeIndex].Pos);

            Vector2 cur = nodes[bestNodeIndex].Pos;
            if (cur == goal)
                return Reconstruct(nodes, bestNodeIndex);

            closed.Add(cur);

            expanded++;
            if (expanded > maxExpandSteps || nodes.Count > maxNodes)
                return null;

            Vector2[] neighbors =
            {
                cur + Vector2.up * tile,
                cur + Vector2.down * tile,
                cur + Vector2.left * tile,
                cur + Vector2.right * tile
            };

            foreach (var np in neighbors)
                ProcessNeighbor(np, bestNodeIndex, goal, tile, nodes, open, openMap, closed);
        }

        return null;
    }

    private static List<Vector2> Reconstruct(List<Node> nodes, int endIndex)
    {
        var path = new List<Vector2>();
        int cur = endIndex;
        while (cur >= 0)
        {
            path.Add(nodes[cur].Pos);
            cur = nodes[cur].Parent;
        }
        path.Reverse();
        return path;
    }

    private bool IsSolidAtWorld(Vector2 worldPos)
    {
        float tile = Mathf.Max(0.0001f, boss.tileSize);
        Vector2 size = Vector2.one * (tile * 0.6f);

        var hits = Physics2D.OverlapBoxAll(worldPos, size, 0f, boss.obstacleMask);
        foreach (var hit in hits)
        {
            if (!hit) continue;
            if (hit.isTrigger) continue;
            if (hit.gameObject == gameObject) continue;
            return true;
        }
        return false;
    }

    private void ProcessNeighbor(
        Vector2 np,
        int parent,
        Vector2 goal,
        float tile,
        List<Node> nodes,
        List<int> open,
        Dictionary<Vector2, int> openMap,
        HashSet<Vector2> closed)
    {
        if (closed.Contains(np)) return;
        if (IsSolidAtWorld(np)) return;

        int Heur(Vector2 a, Vector2 b)
            => Mathf.Abs(Mathf.RoundToInt((a.x - b.x) / tile)) +
               Mathf.Abs(Mathf.RoundToInt((a.y - b.y) / tile));

        int newG = nodes[parent].G + 1;

        if (openMap.TryGetValue(np, out int existing))
        {
            if (newG < nodes[existing].G)
            {
                var up = nodes[existing];
                up.G = newG;
                up.F = newG + Heur(np, goal);
                up.Parent = parent;
                nodes[existing] = up;
            }
            return;
        }

        int idx = nodes.Count;
        nodes.Add(new Node
        {
            Pos = np,
            Parent = parent,
            G = newG,
            F = newG + Heur(np, goal)
        });

        open.Add(idx);
        openMap[np] = idx;
    }

    #endregion
}