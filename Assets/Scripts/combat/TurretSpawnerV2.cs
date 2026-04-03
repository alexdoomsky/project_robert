using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretSpawnerV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private TurnManagerV2 turnManager;

    [Header("Prefab")]
    [SerializeField] private TurretV2 turretPrefab;

    [Header("Spawn Rules")]
    [SerializeField] private int turretCount = 1;
    [SerializeField] private int minDistanceFromUnits = 4;
    [SerializeField] private int mustBeNearAsteroidRadius = 2;
    [SerializeField] private int attemptsPerTurret = 200;

    [Header("Wait / Order Fix")]
    [SerializeField] private bool waitForAsteroidsIfNeeded = true;
    [SerializeField] private bool waitForUnitsIfNeeded = true;
    [SerializeField] private float waitTimeoutSeconds = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool _spawned = false;

    // FIX: targetTurretCount должен жить в поле, а не в coroutine локальной переменной
    private int _targetTurretCount = 0;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();
    }

    private void Start()
    {
        StartCoroutine(CoSpawnWhenReady());
    }

    private IEnumerator CoSpawnWhenReady()
    {
        if (_spawned) yield break;

        // FIX: считаем и сохраняем сюда, чтобы SpawnTurrets() мог использовать
        _targetTurretCount = turretCount;
        var run = RunStateV2.Instance;
        if (run != null && run.pendingEncounterPreset != null)
            _targetTurretCount = run.pendingEncounterPreset.GetTurretCount(turretCount);

        if (_targetTurretCount <= 0) yield break;

        if (turretPrefab == null)
        {
            Debug.LogError("[TurretSpawnerV2] turretPrefab is not assigned");
            yield break;
        }

        float t0 = Time.realtimeSinceStartup;
        while (grid != null && !grid.IsReady)
        {
            if (Time.realtimeSinceStartup - t0 > waitTimeoutSeconds) break;
            yield return null;
        }

        yield return null;

        if (mustBeNearAsteroidRadius > 0 && waitForAsteroidsIfNeeded)
        {
            float a0 = Time.realtimeSinceStartup;
            while (FindObjectOfType<AsteroidObstacleV2>() == null)
            {
                if (Time.realtimeSinceStartup - a0 > waitTimeoutSeconds) break;
                yield return null;
            }
        }

        if (minDistanceFromUnits > 0 && waitForUnitsIfNeeded && turnManager != null)
        {
            float u0 = Time.realtimeSinceStartup;
            while (turnManager.Units == null || turnManager.Units.Count == 0)
            {
                if (Time.realtimeSinceStartup - u0 > waitTimeoutSeconds) break;
                yield return null;
            }
        }

        yield return null;

        SpawnTurrets();
        _spawned = true;
    }

    private void SpawnTurrets()
    {
        if (grid == null || !grid.IsReady)
        {
            Debug.LogWarning("[TurretSpawnerV2] Grid is not ready, spawn aborted.");
            return;
        }

        var allCells = new List<HexCellV2>(grid.GetAllCells());
        if (allCells.Count == 0)
        {
            Debug.LogWarning("[TurretSpawnerV2] No cells in grid.");
            return;
        }

        int spawned = 0;

        // FIX: используем поле _targetTurretCount
        for (int i = 0; i < _targetTurretCount; i++)
        {
            HexCellV2 cell = FindValidCell(allCells);
            if (cell == null)
            {
                if (debugLogs) Debug.LogWarning("[TurretSpawnerV2] No valid cell found for turret.");
                continue;
            }

            var turret = Instantiate(turretPrefab, cell.transform.position, Quaternion.identity);

            // важно: ставим occupant сразу, чтобы occupant блокировал следующее размещение
            bool ok = cell.TrySetOccupant(turret, true);
            if (!ok)
            {
                if (debugLogs) Debug.LogWarning($"[TurretSpawnerV2] Cell already occupied at ({cell.Col},{cell.Row}), turret destroyed.");
                Destroy(turret.gameObject);
                continue;
            }

            turret.SendMessage("ResolveCurrentCell", SendMessageOptions.DontRequireReceiver);

            spawned++;
            if (debugLogs) Debug.Log($"[TurretSpawnerV2] Spawned turret at ({cell.Col},{cell.Row})");
        }

        if (debugLogs) Debug.Log($"[TurretSpawnerV2] Spawned {spawned}/{_targetTurretCount} turrets.");
    }

    private HexCellV2 FindValidCell(List<HexCellV2> allCells)
    {
        for (int attempt = 0; attempt < attemptsPerTurret; attempt++)
        {
            var cell = allCells[Random.Range(0, allCells.Count)];
            if (cell == null) continue;

            if (!cell.walkable) continue;

            // важно: не ставим на занятые клетки
            if (cell.HasOccupant) continue;

            if (minDistanceFromUnits > 0 && turnManager != null)
            {
                if (IsTooCloseToAnyUnit(cell, minDistanceFromUnits))
                    continue;
            }

            if (mustBeNearAsteroidRadius > 0)
            {
                if (!IsNearAsteroid(cell, mustBeNearAsteroidRadius))
                    continue;
            }

            return cell;
        }

        return null;
    }

    private bool IsTooCloseToAnyUnit(HexCellV2 cell, int minDist)
    {
        var units = turnManager.Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.CurrentCell == null) continue;

            int dist = CombatSystemV2.HexDistance(cell, u.CurrentCell);
            if (dist < minDist) return true;
        }
        return false;
    }

    private bool IsNearAsteroid(HexCellV2 cell, int radius)
    {
        var cells = PathfinderV2.GetCellsInRange(cell, radius, grid);
        foreach (var c in cells)
        {
            if (c == null) continue;
            if (c.Occupant is AsteroidObstacleV2) return true;
        }
        return false;
    }
}
