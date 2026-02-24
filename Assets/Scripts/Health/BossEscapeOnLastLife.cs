using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AIMovementController))]
[RequireComponent(typeof(MovementController))]
public class BossEscapeOnLastLife : MonoBehaviour
{
    [Header("Boss Refs")]
    [SerializeField] private MovementController boss;
    [SerializeField] private AIMovementController aiMove;
    [SerializeField] private MonoBehaviour bossAIToDisable;

    [Header("Escape")]
    [SerializeField] private Vector2 escapeTarget = new(-3f, 2f);
    [SerializeField] private float reachEpsilon = 0.06f;

    [Header("Pathfinding")]
    [SerializeField] private int maxNodes = 4096;
    [SerializeField] private int maxExpandSteps = 20000;

    [Header("Players Lock")]
    [SerializeField] private bool disablePlayerBombControllerWhileLocked = true;
    [SerializeField] private bool disablePlayerCollidersWhileLocked = true;

    private CharacterHealth health;

    private bool escapeArmed;
    private bool escapeStarted;

    private readonly List<MovementController> players = new();
    private readonly List<BombController> playerBombs = new();
    private readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    private readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    private void Awake()
    {
        if (!boss) boss = GetComponent<MovementController>();
        if (!aiMove) aiMove = GetComponent<AIMovementController>();
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
        RefreshPlayersRefs();
        LockPlayers();

        LockBossForEscape();

        BombController.ExplodeAllControlBombsInStage();

        yield return null;

        float tile = Mathf.Max(0.0001f, boss.tileSize);

        Vector2 start = RoundToGrid(GetBossPos(), tile);
        Vector2 goal = RoundToGrid(escapeTarget, tile);

        List<Vector2> path = FindPathAStar(start, goal, tile);
        if (path == null || path.Count == 0)
        {
            yield return StartCoroutine(FallbackWalkTowards(goal, tile));
            Destroy(gameObject);
            yield break;
        }

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 next = path[i];
            yield return MoveToTile(next, tile);
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
        if (boss && boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;

        Destroy(gameObject);
    }

    private void LockBossForEscape()
    {
        if (bossAIToDisable) bossAIToDisable.enabled = false;

        if (aiMove) aiMove.enabled = true;

        if (boss)
        {
            boss.SetInputLocked(false, true);
            boss.SetExplosionInvulnerable(true);
            if (boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;
        }

        if (health != null)
        {
            health.StopInvulnerability();
            health.SetExternalInvulnerability(true);
        }
    }

    private IEnumerator MoveToTile(Vector2 tileCenter, float tile)
    {
        float maxTime = 6f;
        float t = 0f;

        while (t < maxTime)
        {
            if (GamePauseController.IsPaused)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetBossPos();
            Vector2 delta = tileCenter - pos;

            if (delta.sqrMagnitude <= (reachEpsilon * reachEpsilon))
                break;

            Vector2 dir = PickCardinal(delta);
            if (aiMove) aiMove.SetAIDirection(dir);

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

    private Vector2 GetBossPos()
        => boss && boss.Rigidbody != null ? boss.Rigidbody.position : (Vector2)transform.position;

    private static Vector2 PickCardinal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private static Vector2 RoundToGrid(Vector2 p, float tile)
        => new Vector2(Mathf.Round(p.x / tile) * tile, Mathf.Round(p.y / tile) * tile);

    #region Players Lock

    private void RefreshPlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
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

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
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
            for (int i = 0; i < playerBombs.Count; i++)
                if (playerBombs[i])
                    playerBombs[i].enabled = false;
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
            var p = kv.Key;
            var col = kv.Value;
            if (!p || !col) continue;

            if (cachedColliderEnabled.TryGetValue(p, out bool wasEnabled))
                col.enabled = wasEnabled;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        if (disablePlayerBombControllerWhileLocked)
        {
            for (int i = 0; i < playerBombs.Count; i++)
                if (playerBombs[i])
                    playerBombs[i].enabled = true;
        }
    }

    #endregion

    #region A* Grid

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

        var open = new List<int>(256);
        var nodes = new List<Node>(256);
        var openMap = new Dictionary<Vector2, int>(256);
        var closed = new HashSet<Vector2>();

        int Heur(Vector2 a, Vector2 b)
            => Mathf.Abs(Mathf.RoundToInt((a.x - b.x) / tile)) + Mathf.Abs(Mathf.RoundToInt((a.y - b.y) / tile));

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
                int f = nodes[ni].F;
                if (f < bestF)
                {
                    bestF = f;
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

            Vector2 n0 = cur + Vector2.up * tile;
            Vector2 n1 = cur + Vector2.down * tile;
            Vector2 n2 = cur + Vector2.left * tile;
            Vector2 n3 = cur + Vector2.right * tile;

            ProcessNeighbor(n0, bestNodeIndex, goal, tile, nodes, open, openMap, closed);
            ProcessNeighbor(n1, bestNodeIndex, goal, tile, nodes, open, openMap, closed);
            ProcessNeighbor(n2, bestNodeIndex, goal, tile, nodes, open, openMap, closed);
            ProcessNeighbor(n3, bestNodeIndex, goal, tile, nodes, open, openMap, closed);
        }

        return null;
    }

    private static List<Vector2> Reconstruct(List<Node> nodes, int endIndex)
    {
        var path = new List<Vector2>(64);
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
        if (hits == null || hits.Length == 0)
            return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!hit) continue;
            if (hit.isTrigger) continue;
            if (hit.gameObject == gameObject) continue;
            return true;
        }

        return false;
    }

    private void ProcessNeighbor(
        Vector2 np,
        int bestNodeIndex,
        Vector2 goal,
        float tile,
        List<Node> nodes,
        List<int> open,
        Dictionary<Vector2, int> openMap,
        HashSet<Vector2> closed)
    {
        if (closed.Contains(np))
            return;

        if (IsSolidAtWorld(np))
            return;

        int Heur(Vector2 a, Vector2 b)
            => Mathf.Abs(Mathf.RoundToInt((a.x - b.x) / tile)) + Mathf.Abs(Mathf.RoundToInt((a.y - b.y) / tile));

        int newG = nodes[bestNodeIndex].G + 1;

        if (openMap.TryGetValue(np, out int existingIndex))
        {
            if (newG < nodes[existingIndex].G)
            {
                var up = nodes[existingIndex];
                up.G = newG;
                up.F = newG + Heur(np, goal);
                up.Parent = bestNodeIndex;
                nodes[existingIndex] = up;
            }
            return;
        }

        int idx = nodes.Count;
        nodes.Add(new Node
        {
            Pos = np,
            Parent = bestNodeIndex,
            G = newG,
            F = newG + Heur(np, goal)
        });

        open.Add(idx);
        openMap[np] = idx;
    }

    #endregion
}