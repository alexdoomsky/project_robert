using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitSpawnerV2 : MonoBehaviour
{
    [Serializable]
    public struct UnitSpawnRequest
    {
        public UnitV2 prefab;
        [Tooltip("Id used by campaign inventory. If empty, prefab.name will be used.")]
        public string unitId;
        [Min(1)] public int count;
    }

    [Header("Refs")]
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private TurnManagerV2 turnManager;
    [SerializeField] private BattleEndControllerV2 battleEndController;

    [Header("Prefabs")]
    [SerializeField] private List<UnitV2> playerUnits = new();
    [SerializeField] private List<UnitV2> enemyUnits = new();

    [Header("Spawn bands (by rows)")]
    [Min(1)][SerializeField] private int enemySpawnBandRows = 2;
    [Min(1)][SerializeField] private int playerSpawnBandRows = 2;

    [Header("Boot")]
    [Tooltip("Если включено — игрок спавнится сразу (старое поведение). Если выключено — ждем pre-battle UI и SpawnPlayerFromSelection().")]
    [SerializeField] private bool autoSpawnPlayerUnitsOnStart = true;

    [Header("Rules")]
    [SerializeField] private bool randomizeCellsOnBand = true;
    [SerializeField] private bool debugLogs = true;

    public event Action OnBattlefieldSpawned;

    public IReadOnlyList<UnitV2> PlayerUnitPrefabs => playerUnits;

    public bool IsBattlefieldSpawned => _battlefieldSpawned;
    public bool IsPlayerSpawned => _playerSpawned;

    private bool _battlefieldSpawned;
    private bool _playerSpawned;
    private bool _trackingStarted;

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();
        if (battleEndController == null) battleEndController = FindObjectOfType<BattleEndControllerV2>(true);

        if (grid != null && !grid.IsReady)
            grid.OnGridReady += SpawnBattlefieldOnly;
        else
            SpawnBattlefieldOnly();
    }

    private void SpawnBattlefieldOnly()
    {
        if (grid == null || !grid.IsReady)
        {
            Debug.LogError("UnitSpawnerV2: grid is empty. Generate grid first.");
            return;
        }

        var run = RunStateV2.Instance;
        var preset = run != null ? run.pendingEncounterPreset : null;

        if (preset != null && preset.HasEnemyOverrides)
        {
            SpawnEnemyFromPreset(preset);
        }
        else
        {
            SpawnSide(enemyUnits, UnitV2.Faction.Enemy, isPlayer: false);
        }

        _battlefieldSpawned = true;
        OnBattlefieldSpawned?.Invoke();

        if (autoSpawnPlayerUnitsOnStart)
        {
            SpawnSide(playerUnits, UnitV2.Faction.Player, isPlayer: true);
            _playerSpawned = true;
            EnsureBeginTracking();
        }
    }

    public void SpawnPlayerFromSelection(List<UnitSpawnRequest> selection)
    {
        if (_playerSpawned) return;

        if (!_battlefieldSpawned)
            SpawnBattlefieldOnly();

        if (selection == null || selection.Count == 0)
        {
            Debug.LogWarning("UnitSpawnerV2: SpawnPlayerFromSelection got empty selection. Nothing spawned.");
            return;
        }

        SpawnSideFromRequests(selection, UnitV2.Faction.Player, isPlayer: true);
        _playerSpawned = true;
        EnsureBeginTracking();
    }

    public void SpawnPlayerFromSelection(List<UnitV2> selectedPrefabs)
    {
        if (selectedPrefabs == null) return;
        var req = new List<UnitSpawnRequest>(selectedPrefabs.Count);
        for (int i = 0; i < selectedPrefabs.Count; i++)
        {
            var p = selectedPrefabs[i];
            if (p == null) continue;
            req.Add(new UnitSpawnRequest { prefab = p, unitId = p.name, count = 1 });
        }
        SpawnPlayerFromSelection(req);
    }

    private void EnsureBeginTracking()
    {
        if (_trackingStarted) return;
        _trackingStarted = true;

        if (battleEndController != null)
            battleEndController.BeginTracking();
    }

    private void SpawnEnemyFromPreset(BattleEncounterPresetV2 preset)
    {
        // Expand preset entries into flat list (prefab + unitId)
        var expanded = new List<(GameObject prefabGO, string unitId)>(64);

        for (int i = 0; i < preset.enemySpawns.Count; i++)
        {
            var e = preset.enemySpawns[i];
            if (e.prefab == null) continue;

            int n = Mathf.Max(0, e.count);
            if (n <= 0) continue;

            string id = string.IsNullOrWhiteSpace(e.unitId) ? e.prefab.name : e.unitId;
            for (int k = 0; k < n; k++)
                expanded.Add((e.prefab, id));
        }

        if (expanded.Count == 0) return;

        var allCells = grid.GetAllCells().Where(c => c != null).ToList();
        if (allCells.Count == 0)
        {
            Debug.LogError("UnitSpawnerV2: grid has 0 cells.");
            return;
        }

        int minRow = allCells.Min(c => c.Row);
        int maxRow = allCells.Max(c => c.Row);
        int band = Mathf.Max(1, enemySpawnBandRows);

        int toRow = maxRow;
        int fromRow = Mathf.Max(minRow, maxRow - (band - 1));

        var bandCells = allCells
            .Where(c => c.Row >= fromRow && c.Row <= toRow)
            .Where(c => c.walkable && !c.OccupantBlocksMovement)
            .ToList();

        if (bandCells.Count == 0)
        {
            Debug.LogError($"UnitSpawnerV2: enemy spawn band has no free cells (rows {fromRow}..{toRow}).");
            return;
        }

        if (randomizeCellsOnBand)
            bandCells = bandCells.OrderBy(_ => UnityEngine.Random.value).ToList();

        int idx = 0;
        for (int i = 0; i < expanded.Count; i++)
        {
            if (idx >= bandCells.Count)
            {
                Debug.LogWarning($"UnitSpawnerV2: not enough spawn cells for Enemy. Needed {expanded.Count}, have {bandCells.Count}.");
                break;
            }

            var cell = bandCells[idx++];

            var go = Instantiate(expanded[i].prefabGO, transform);
            var unit = go.GetComponent<UnitV2>();
            if (unit == null)
            {
                Debug.LogError($"UnitSpawnerV2: preset enemy prefab '{expanded[i].prefabGO.name}' has no UnitV2 on root.");
                Destroy(go);
                continue;
            }

            unit.Init(UnitV2.Faction.Enemy);

            bool placed = unit.PlaceOnCell(cell);
            if (!placed)
            {
                Debug.LogWarning($"UnitSpawnerV2: failed to place {unit.name} at ({cell.Col},{cell.Row})");
                Destroy(unit.gameObject);
                continue;
            }

            if (turnManager != null)
                turnManager.RegisterUnit(unit);

            if (battleEndController != null)
                battleEndController.RegisterUnit(unit);

            if (debugLogs)
                Debug.Log($"[UnitSpawner] Spawned {unit.name} Enemy at ({cell.Col},{cell.Row}) rowsBand={fromRow}..{toRow} id={expanded[i].unitId}");
        }
    }

    private void SpawnSide(List<UnitV2> prefabs, UnitV2.Faction faction, bool isPlayer)
    {
        if (prefabs == null || prefabs.Count == 0) return;

        var allCells = grid.GetAllCells()
            .Where(c => c != null)
            .ToList();

        if (allCells.Count == 0)
        {
            Debug.LogError("UnitSpawnerV2: grid has 0 cells.");
            return;
        }

        int minRow = allCells.Min(c => c.Row);
        int maxRow = allCells.Max(c => c.Row);

        int band = Mathf.Max(1, isPlayer ? playerSpawnBandRows : enemySpawnBandRows);

        int fromRow, toRow;

        if (isPlayer)
        {
            fromRow = minRow;
            toRow = Mathf.Min(maxRow, minRow + (band - 1));
        }
        else
        {
            toRow = maxRow;
            fromRow = Mathf.Max(minRow, maxRow - (band - 1));
        }

        var bandCells = allCells
            .Where(c => c.Row >= fromRow && c.Row <= toRow)
            .Where(c => c.walkable && !c.OccupantBlocksMovement)
            .ToList();

        if (bandCells.Count == 0)
        {
            Debug.LogError($"UnitSpawnerV2: spawn band has no free cells for {faction} (rows {fromRow}..{toRow}).");
            return;
        }

        if (randomizeCellsOnBand)
            bandCells = bandCells.OrderBy(_ => UnityEngine.Random.value).ToList();

        int idx = 0;
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;

            if (idx >= bandCells.Count)
            {
                Debug.LogWarning($"UnitSpawnerV2: not enough spawn cells for {faction}. Needed {prefabs.Count}, have {bandCells.Count}.");
                break;
            }

            var cell = bandCells[idx++];

            var unit = Instantiate(prefab, transform);
            unit.Init(faction);

            if (faction == UnitV2.Faction.Player)
            {
                var tag = unit.GetComponent<CampaignUnitTagV2>();
                if (tag == null) tag = unit.gameObject.AddComponent<CampaignUnitTagV2>();
                tag.unitId = prefab != null ? prefab.name : unit.name;
            }

            bool placed = unit.PlaceOnCell(cell);
            if (!placed)
            {
                Debug.LogWarning($"UnitSpawnerV2: failed to place {unit.name} at ({cell.Col},{cell.Row})");
                Destroy(unit.gameObject);
                continue;
            }

            if (turnManager != null)
                turnManager.RegisterUnit(unit);

            if (battleEndController != null)
                battleEndController.RegisterUnit(unit);

            if (debugLogs)
                Debug.Log($"[UnitSpawner] Spawned {unit.name} {faction} at ({cell.Col},{cell.Row}) rowsBand={fromRow}..{toRow}");
        }
    }

    private void SpawnSideFromRequests(List<UnitSpawnRequest> requests, UnitV2.Faction faction, bool isPlayer)
    {
        if (requests == null || requests.Count == 0) return;

        var expanded = new List<(UnitV2 prefab, string unitId)>(64);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            if (r.prefab == null) continue;
            int n = Mathf.Max(0, r.count);
            if (n <= 0) continue;
            string id = string.IsNullOrWhiteSpace(r.unitId) ? r.prefab.name : r.unitId;
            for (int k = 0; k < n; k++)
                expanded.Add((r.prefab, id));
        }

        if (expanded.Count == 0) return;

        var allCells = grid.GetAllCells()
            .Where(c => c != null)
            .ToList();

        if (allCells.Count == 0)
        {
            Debug.LogError("UnitSpawnerV2: grid has 0 cells.");
            return;
        }

        int minRow = allCells.Min(c => c.Row);
        int maxRow = allCells.Max(c => c.Row);

        int band = Mathf.Max(1, isPlayer ? playerSpawnBandRows : enemySpawnBandRows);

        int fromRow, toRow;
        if (isPlayer)
        {
            fromRow = minRow;
            toRow = Mathf.Min(maxRow, minRow + (band - 1));
        }
        else
        {
            toRow = maxRow;
            fromRow = Mathf.Max(minRow, maxRow - (band - 1));
        }

        var bandCells = allCells
            .Where(c => c.Row >= fromRow && c.Row <= toRow)
            .Where(c => c.walkable && !c.OccupantBlocksMovement)
            .ToList();

        if (bandCells.Count == 0)
        {
            Debug.LogError($"UnitSpawnerV2: spawn band has no free cells for {faction} (rows {fromRow}..{toRow}).");
            return;
        }

        if (randomizeCellsOnBand)
            bandCells = bandCells.OrderBy(_ => UnityEngine.Random.value).ToList();

        int idx = 0;
        for (int i = 0; i < expanded.Count; i++)
        {
            var prefab = expanded[i].prefab;
            string unitId = expanded[i].unitId;

            if (idx >= bandCells.Count)
            {
                Debug.LogWarning($"UnitSpawnerV2: not enough spawn cells for {faction}. Needed {expanded.Count}, have {bandCells.Count}.");
                break;
            }

            var cell = bandCells[idx++];

            var unit = Instantiate(prefab, transform);
            unit.Init(faction);

            if (faction == UnitV2.Faction.Player)
            {
                var tag = unit.GetComponent<CampaignUnitTagV2>();
                if (tag == null) tag = unit.gameObject.AddComponent<CampaignUnitTagV2>();
                tag.unitId = unitId;
            }

            bool placed = unit.PlaceOnCell(cell);
            if (!placed)
            {
                Debug.LogWarning($"UnitSpawnerV2: failed to place {unit.name} at ({cell.Col},{cell.Row})");
                Destroy(unit.gameObject);
                continue;
            }

            if (turnManager != null)
                turnManager.RegisterUnit(unit);

            if (battleEndController != null)
                battleEndController.RegisterUnit(unit);

            if (debugLogs)
                Debug.Log($"[UnitSpawner] Spawned {unit.name} {faction} at ({cell.Col},{cell.Row}) rowsBand={fromRow}..{toRow} id={unitId}");
        }
    }
}
