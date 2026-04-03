using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MineSpawnerV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private TurnManagerV2 turnManager;

    [Header("Mine Prefab")]
    [SerializeField] private MineTrapV2 minePrefab;
    [SerializeField] private int mineCount = 5;

    [Header("Rules")]
    [SerializeField] private int mineNoMineRadius = 1; // � ������� ��������� �� ������ ���� ������ ����
    [SerializeField] private bool preferMiddle = true;

    [SerializeField] private bool debugLogs = true;

    private readonly List<MineTrapV2> mines = new();

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();

        if (grid != null && !grid.IsReady)
            grid.OnGridReady += SpawnMines;
        else
            SpawnMines();
    }

    private void SpawnMines()
    {
        if (grid == null || !grid.IsReady)
        {
            Debug.LogError("MineSpawnerV2: grid missing/empty");
            return;
        }

        if (minePrefab == null)
        {
            Debug.LogError("MineSpawnerV2: minePrefab not set");
            return;
        }

        foreach (var m in mines.Where(m => m != null)) Destroy(m.gameObject);
        mines.Clear();

        var allCells = grid.GetAllCells().Where(c => c != null).ToList();

        int targetMineCount = mineCount;
        var run = RunStateV2.Instance;
        if (run != null && run.pendingEncounterPreset != null)
            targetMineCount = run.pendingEncounterPreset.GetMineCount(mineCount);

        int placed = 0;
        int guard = 0;

        while (placed < targetMineCount && guard++ < 2000)
        {
            HexCellV2 cell = PickCell(allCells);
            if (cell == null) continue;

            // ��������� ������ �� Row (����� ������ ������/�����)
            if (grid.IsForbiddenForHazards(cell.Row)) continue;

            // ��������: ���� �� ������ ���������� � ������� ������ ������
            if (cell.HasOccupant) continue;

            // (��������, �� ��� �� ���� ������, ��� HasOccupant)
            if (!cell.walkable) continue;

            bool tooClose = mines.Any(m =>
                m != null && m.CurrentCell != null &&
                CombatSystemV2.HexDistance(m.CurrentCell, cell) <= mineNoMineRadius);

            if (tooClose) continue;

            var mine = Instantiate(minePrefab, transform);

            // PlaceOnCell ��� �������� occupant (� ���� ��� ��� �������)
            mine.PlaceOnCell(cell);

            // sanity: ���� ����� �� ������ ������ ������, �������
            if (mine.CurrentCell == null || mine.CurrentCell != cell)
            {
                Destroy(mine.gameObject);
                continue;
            }

            mines.Add(mine);
            placed++;

            if (debugLogs) Debug.Log($"[MineSpawner] placed {placed}/{targetMineCount} at ({cell.Col},{cell.Row})");
        }

        if (placed < targetMineCount)
            Debug.LogWarning($"[MineSpawner] placed only {placed}/{targetMineCount}");
    }

    private HexCellV2 PickCell(List<HexCellV2> allCells)
    {
        if (!preferMiddle)
            return allCells[Random.Range(0, allCells.Count)];

        float best = float.MinValue;
        HexCellV2 bestCell = null;

        for (int i = 0; i < 12; i++)
        {
            var c = allCells[Random.Range(0, allCells.Count)];

            float tRow = Mathf.InverseLerp(0, grid.Height - 1, c.Row);
            float distToMiddle = Mathf.Abs(tRow - 0.5f);
            float score = -distToMiddle;

            float tCol = Mathf.InverseLerp(0, grid.Width - 1, c.Col);
            float distToSide = Mathf.Min(tCol, 1f - tCol);
            score += (0.15f * (1f - distToSide));

            if (score > best)
            {
                best = score;
                bestCell = c;
            }
        }

        return bestCell;
    }
}
