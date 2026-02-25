using System.Collections.Generic;
using UnityEngine;

public sealed class GridAStarPathfinder2D
{
    private struct Node
    {
        public Vector2 Pos;
        public int Parent;
        public int G;
        public int F;
    }

    public List<Vector2> FindPath(
        Vector2 start,
        Vector2 goal,
        float tile,
        LayerMask obstacleMask,
        GameObject selfToIgnore,
        int maxNodes,
        int maxExpandSteps,
        float overlapBoxScale = 0.6f)
    {
        if (tile <= 0f) tile = 0.0001f;

        if (start == goal)
        {
            return new List<Vector2> { start };
        }

        if (IsSolidAtWorld(goal, tile, obstacleMask, selfToIgnore, overlapBoxScale))
        {
            return null;
        }

        int Heur(Vector2 a, Vector2 b)
        {
            int dx = Mathf.Abs(Mathf.RoundToInt((a.x - b.x) / tile));
            int dy = Mathf.Abs(Mathf.RoundToInt((a.y - b.y) / tile));
            return dx + dy;
        }

        var open = new List<int>(128);
        var nodes = new List<Node>(256);
        var openMap = new Dictionary<Vector2, int>(256);
        var closed = new HashSet<Vector2>();

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
            {
                return Reconstruct(nodes, bestNodeIndex);
            }

            closed.Add(cur);

            expanded++;
            if (expanded > maxExpandSteps || nodes.Count > maxNodes)
            {
                return null;
            }

            Vector2[] neighbors =
            {
                cur + Vector2.up * tile,
                cur + Vector2.down * tile,
                cur + Vector2.left * tile,
                cur + Vector2.right * tile
            };

            for (int i = 0; i < neighbors.Length; i++)
            {
                ProcessNeighbor(
                    neighbors[i],
                    bestNodeIndex,
                    goal,
                    tile,
                    obstacleMask,
                    selfToIgnore,
                    overlapBoxScale,
                    nodes,
                    open,
                    openMap,
                    closed,
                    Heur);
            }
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

    private static void ProcessNeighbor(
        Vector2 np,
        int parent,
        Vector2 goal,
        float tile,
        LayerMask obstacleMask,
        GameObject selfToIgnore,
        float overlapBoxScale,
        List<Node> nodes,
        List<int> open,
        Dictionary<Vector2, int> openMap,
        HashSet<Vector2> closed,
        System.Func<Vector2, Vector2, int> heur)
    {
        if (closed.Contains(np)) return;
        if (IsSolidAtWorld(np, tile, obstacleMask, selfToIgnore, overlapBoxScale)) return;

        int newG = nodes[parent].G + 1;

        if (openMap.TryGetValue(np, out int existing))
        {
            if (newG < nodes[existing].G)
            {
                var up = nodes[existing];
                up.G = newG;
                up.F = newG + heur(np, goal);
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
            F = newG + heur(np, goal)
        });

        open.Add(idx);
        openMap[np] = idx;
    }

    private static bool IsSolidAtWorld(
        Vector2 worldPos,
        float tile,
        LayerMask obstacleMask,
        GameObject selfToIgnore,
        float overlapBoxScale)
    {
        Vector2 size = Vector2.one * (tile * Mathf.Clamp(overlapBoxScale, 0.1f, 1f));
        var hits = Physics2D.OverlapBoxAll(worldPos, size, 0f, obstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!hit) continue;
            if (hit.isTrigger) continue;
            if (selfToIgnore && hit.gameObject == selfToIgnore) continue;

            return true;
        }

        return false;
    }
}