using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PathfinderV2
{
    public static List<HexCellV2> FindPath(HexCellV2 start, HexCellV2 goal, HexGridV2 grid)
    {
        if (start == null || goal == null || grid == null) return null;
        if (!goal.walkable) return null;
        if (goal.OccupantBlocksMovement) return null; // мина не блокирует, астероид блокирует

        var open = new List<HexCellV2> { start };
        var closed = new HashSet<HexCellV2>();

        var cameFrom = new Dictionary<HexCellV2, HexCellV2>();
        var g = new Dictionary<HexCellV2, int> { [start] = 0 };

        while (open.Count > 0)
        {
            HexCellV2 current = open
                .OrderBy(c => g.GetValueOrDefault(c, int.MaxValue) + CombatSystemV2.HexDistance(c, goal))
                .First();

            if (current == goal)
                return Reconstruct(cameFrom, start, goal);

            open.Remove(current);
            closed.Add(current);

            foreach (var n in current.GetNeighbors(grid))
            {
                if (n == null) continue;
                if (closed.Contains(n)) continue;
                if (!n.walkable) continue;
                if (n.OccupantBlocksMovement) continue;

                int tentative = g[current] + 1;

                if (!g.ContainsKey(n) || tentative < g[n])
                {
                    cameFrom[n] = current;
                    g[n] = tentative;
                    if (!open.Contains(n)) open.Add(n);
                }
            }
        }

        return null;
    }

    public static List<HexCellV2> GetReachableCells(HexCellV2 start, int movementPoints, HexGridV2 grid)
    {
        var res = new List<HexCellV2>();
        if (start == null || grid == null) return res;
        if (movementPoints <= 0) return res;

        var cost = new Dictionary<HexCellV2, int> { [start] = 0 };
        var q = new Queue<HexCellV2>();
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var n in cur.GetNeighbors(grid))
            {
                if (n == null) continue;
                if (!n.walkable) continue;
                if (n.OccupantBlocksMovement) continue;

                int nc = cost[cur] + 1;
                if (nc > movementPoints) continue;

                if (!cost.ContainsKey(n) || nc < cost[n])
                {
                    cost[n] = nc;
                    q.Enqueue(n);
                    if (n != start && !res.Contains(n)) res.Add(n);
                }
            }
        }

        return res;
    }

    // часто нужно для мин/взрывов/радиусов
    public static List<HexCellV2> GetCellsInRange(HexCellV2 center, int range, HexGridV2 grid)
    {
        var res = new List<HexCellV2>();
        if (center == null || grid == null || range < 0) return res;

        foreach (var c in grid.GetAllCells())
        {
            if (c == null) continue;
            int d = CombatSystemV2.HexDistance(center, c);
            if (d <= range) res.Add(c);
        }
        return res;
    }

    private static List<HexCellV2> Reconstruct(Dictionary<HexCellV2, HexCellV2> cameFrom, HexCellV2 start, HexCellV2 goal)
    {
        var path = new List<HexCellV2>();
        var cur = goal;
        path.Add(cur);
        while (cur != start && cameFrom.TryGetValue(cur, out var prev))
        {
            cur = prev;
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }
}
