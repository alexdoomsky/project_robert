using System;
using System.Collections.Generic;
using UnityEngine;

public class HexGridV2 : MonoBehaviour
{
    public event Action OnGridReady;

    [Header("Grid Size")]
    [Min(1)] public int width = 10;   // columns
    [Min(1)] public int height = 8;   // rows

    [Header("Hex")]
    public GameObject hexPrefab;
    [Min(0.01f)] public float hexSize = 1f;

    [Header("Prefab Rotation")]
    public float yRotationDegrees = 0f;

    [Header("Spawn Bands (by ROW)")]
    public int playerSpawnRows = 1;
    public int enemySpawnRows = 1;
    public int safeRowsFromSpawn = 2;

    private readonly Dictionary<(int col, int row), HexCellV2> cells = new();

    public int Width => width;
    public int Height => height;
    public bool IsReady { get; private set; }

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Grid V2")]
    public void Generate()
    {
        Clear();

        if (hexPrefab == null)
        {
            Debug.LogError("HexGridV2: hexPrefab not assigned");
            return;
        }

        Quaternion rot = Quaternion.Euler(0f, yRotationDegrees, 0f);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Vector3 pos = OffsetOddR_ToWorld_XUp(col, row);
                GameObject go = Instantiate(hexPrefab, pos, rot, transform);

                var cell = go.GetComponent<HexCellV2>();
                if (cell == null) cell = go.AddComponent<HexCellV2>();
                cell.Init(col, row);

                cells[(col, row)] = cell;
            }
        }

        IsReady = true;
        OnGridReady?.Invoke();
    }

    public void Clear()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        cells.Clear();
        IsReady = false;
    }

    // у тебя “вверх” по X: row -> X, col -> Z
    private Vector3 OffsetOddR_ToWorld_XUp(int col, int row)
    {
        float stepRow = 1.5f * hexSize;
        float stepCol = Mathf.Sqrt(3f) * hexSize;

        float x = stepRow * row;
        float z = stepCol * (col + ((row & 1) == 1 ? 0.5f : 0f));
        return new Vector3(x, 0f, z);
    }

    public bool TryGetCell(int col, int row, out HexCellV2 cell)
        => cells.TryGetValue((col, row), out cell);

    public IEnumerable<HexCellV2> GetAllCells() => cells.Values;

    public bool IsPlayerSpawnBand(int row) => row >= 0 && row < playerSpawnRows;
    public bool IsEnemySpawnBand(int row) => row < height && row >= height - enemySpawnRows;

    public bool IsForbiddenForHazards(int row)
    {
        if (row < playerSpawnRows + safeRowsFromSpawn) return true;
        if (row >= height - enemySpawnRows - safeRowsFromSpawn) return true;
        return false;
    }
}
