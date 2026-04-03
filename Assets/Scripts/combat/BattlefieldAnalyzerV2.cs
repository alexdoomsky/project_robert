using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattlefieldAnalyzerV2 : MonoBehaviour
{
    [Serializable]
    public struct CountBands
    {
        [Tooltip("1..low => 'мало'")]
        public int low;

        [Tooltip("low+1..mid => 'средне'")]
        public int mid;

        public string ToLabel(int count)
        {
            if (count <= 0) return "нет";
            if (count <= Mathf.Max(1, low)) return "мало";
            if (count <= Mathf.Max(low + 1, mid)) return "средне";
            return "много";
        }
    }

    [Header("Thresholds")]
    [SerializeField] private CountBands minesBands = new CountBands { low = 2, mid = 5 };
    [SerializeField] private CountBands turretsBands = new CountBands { low = 1, mid = 2 };
    [SerializeField] private CountBands genericEnemiesBands = new CountBands { low = 1, mid = 3 };
    [SerializeField] private CountBands orkCruiserBands = new CountBands { low = 1, mid = 2 };
    [SerializeField] private CountBands chaosRaiderBands = new CountBands { low = 1, mid = 2 };
    [SerializeField] private CountBands bulldogBands = new CountBands { low = 1, mid = 3 };

    [Header("Obstacles (density)")]
    [Tooltip("Если есть AsteroidObstacleV2 в сцене — считаем их. Иначе считаем по клеткам (walkable==false).")]
    [Range(0f, 1f)][SerializeField] private float obstacleLowDensity = 0.10f;
    [Range(0f, 1f)][SerializeField] private float obstacleMidDensity = 0.25f;

    [Serializable]
    public class EnemyGroup
    {
        public UnitV2.EnemyArchetype archetype;
        public int count;
        public string label;
    }

    [Serializable]
    public class Summary
    {
        public int minesCount;
        public string minesLabel;

        public int turretsCount;
        public string turretsLabel;

        public float obstaclesDensity;
        public string obstaclesLabel;

        public List<EnemyGroup> enemies = new();
    }

    public Summary Scan(HexGridV2 grid = null)
    {
        var s = new Summary();

        // Mines
        var mines = FindObjectsOfType<MineTrapV2>(true);
        s.minesCount = mines != null ? mines.Length : 0;
        s.minesLabel = minesBands.ToLabel(s.minesCount);

        // Turrets
        var turrets = FindObjectsOfType<TurretV2>(true);
        s.turretsCount = turrets != null ? turrets.Length : 0;
        s.turretsLabel = turretsBands.ToLabel(s.turretsCount);

        // Enemies by archetype
        var units = FindObjectsOfType<UnitV2>(true);
        var dict = new Dictionary<UnitV2.EnemyArchetype, int>();

        if (units != null)
        {
            foreach (var u in units)
            {
                if (u == null) continue;
                if (!u.IsAlive) continue;
                if (u.Team != UnitV2.Faction.Enemy) continue;

                var a = u.Archetype;
                if (!dict.ContainsKey(a)) dict[a] = 0;
                dict[a]++;
            }
        }

        s.enemies.Clear();
        foreach (var kv in dict.OrderByDescending(k => k.Value))
        {
            var eg = new EnemyGroup
            {
                archetype = kv.Key,
                count = kv.Value,
                label = GetEnemyBands(kv.Key).ToLabel(kv.Value)
            };
            s.enemies.Add(eg);
        }

        // Obstacles density
        int totalCells = FindGridCellCount(grid);
        totalCells = Mathf.Max(1, totalCells);

        int obstacleCount = 0;

        var obstacleComponents = FindObjectsOfType<AsteroidObstacleV2>(true);
        if (obstacleComponents != null && obstacleComponents.Length > 0)
        {
            obstacleCount = obstacleComponents.Length;
            s.obstaclesDensity = Mathf.Clamp01((float)obstacleCount / totalCells);
        }
        else
        {
            int unwalkable = FindUnwalkableCount(grid);
            s.obstaclesDensity = Mathf.Clamp01((float)unwalkable / totalCells);
        }

        if (s.obstaclesDensity <= 0f) s.obstaclesLabel = "нет";
        else if (s.obstaclesDensity <= obstacleLowDensity) s.obstaclesLabel = "мало";
        else if (s.obstaclesDensity <= obstacleMidDensity) s.obstaclesLabel = "средне";
        else s.obstaclesLabel = "много";

        return s;
    }

    private CountBands GetEnemyBands(UnitV2.EnemyArchetype a)
    {
        return a switch
        {
            UnitV2.EnemyArchetype.OrkCruiser => orkCruiserBands,
            UnitV2.EnemyArchetype.ChaosRaider => chaosRaiderBands,
            UnitV2.EnemyArchetype.Bulldog => bulldogBands,
            _ => genericEnemiesBands
        };
    }

    private int FindGridCellCount(HexGridV2 grid)
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (grid == null || !grid.IsReady) return 0;

        int count = 0;
        foreach (var c in grid.GetAllCells())
        {
            if (c != null) count++;
        }
        return count;
    }

    private int FindUnwalkableCount(HexGridV2 grid)
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (grid == null || !grid.IsReady) return 0;

        int unwalkable = 0;
        foreach (var c in grid.GetAllCells())
        {
            if (c == null) continue;
            if (!c.walkable) unwalkable++;
        }
        return unwalkable;
    }
}
