using System.Collections.Generic;
using UnityEngine;

public class AsteroidClusterSpawnerV2 : MonoBehaviour
{
    [Header("Refs")]
    public HexGridV2 grid;
    public GameObject asteroidPrefab;

    [Header("Cluster Settings")]
    [Min(1)] public int clusterCount = 4;
    [Min(1)] public int clusterMinSize = 3;
    [Min(1)] public int clusterMaxSize = 8;

    [Header("Shape")]
    [Range(0f, 1f)]
    [Tooltip("0 = ����� '����������' ��������, 1 = ����� '���������' (����� ���������� � ����� �����������)")]
    public float elongation = 0.6f;

    [Header("Edge Bias")]
    [Range(0f, 1f)]
    [Tooltip("����������� ������� ����� ����� � ������� ����� (col=0 ��� col=width-1)")]
    public float preferSideEdges = 0.7f;

    [Min(1)]
    [Tooltip("������ '������� ����', ���� ���� �������� ������ ��������� (� ��������)")]
    public int sideEdgeBandCols = 2;

    [Header("Forbidden")]
    public bool respectForbiddenRows = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private void Start()
    {
        if (!grid) grid = FindObjectOfType<HexGridV2>();
        if (grid == null)
        {
            Debug.LogError("AsteroidClusterSpawnerV2: grid not found.");
            return;
        }

        if (grid.IsReady) SpawnClusters();
        else grid.OnGridReady += SpawnClusters;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.OnGridReady -= SpawnClusters;
    }

    [ContextMenu("Spawn Asteroid Clusters")]
    public void SpawnClusters()
    {
        if (grid == null || !grid.IsReady)
        {
            Debug.LogError("AsteroidClusterSpawnerV2: grid not ready.");
            return;
        }
        if (asteroidPrefab == null)
        {
            Debug.LogError("AsteroidClusterSpawnerV2: asteroidPrefab not assigned.");
            return;
        }

        int targetClusterCount = clusterCount;
        int targetMinSize = clusterMinSize;
        int targetMaxSize = clusterMaxSize;

        var run = RunStateV2.Instance;
        if (run != null && run.pendingEncounterPreset != null)
        {
            var preset = run.pendingEncounterPreset;
            targetClusterCount = preset.GetClusterCount(clusterCount);
            targetMinSize = Mathf.Max(1, preset.clusterMinSize);
            targetMaxSize = Mathf.Max(targetMinSize, preset.clusterMaxSize);
        }


        int spawnedTotal = 0;

        for (int i = 0; i < targetClusterCount; i++)
        {
            int clusterSize = Random.Range(targetMinSize, targetMaxSize + 1);

            if (!TryPickClusterCenter(out var center))
            {
                if (debugLogs) Debug.LogWarning($"Cluster {i}: no valid center.");
                continue;
            }

            spawnedTotal += SpawnOneCluster(center, clusterSize);
        }

        if (debugLogs) Debug.Log($"Asteroids spawned total: {spawnedTotal}");
    }

    private bool TryPickClusterCenter(out HexCellV2 center)
    {
        center = null;

        // ��������� �������, ����� �� ��������� ���� ����� ��� ������
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int row = Random.Range(0, grid.Height);

            if (respectForbiddenRows && grid.IsForbiddenForHazards(row))
                continue;

            int col;

            bool pickEdge = Random.value < preferSideEdges;
            if (pickEdge)
            {
                bool left = Random.value < 0.5f;
                if (left) col = Random.Range(0, Mathf.Min(sideEdgeBandCols, grid.Width));
                else col = Random.Range(Mathf.Max(0, grid.Width - sideEdgeBandCols), grid.Width);
            }
            else
            {
                col = Random.Range(0, grid.Width);
            }

            if (!grid.TryGetCell(col, row, out var cell) || cell == null)
                continue;

            if (!cell.walkable) continue;
            if (cell.HasOccupant) continue;

            center = cell;
            return true;
        }

        return false;
    }

    private int SpawnOneCluster(HexCellV2 center, int size)
    {
        int spawned = 0;

        var frontier = new List<HexCellV2> { center };
        var used = new HashSet<HexCellV2> { center };

        // ������������ ������������: ����� ���� ���������� � ����� �����������
        HexCellV2 last = center;
        HexCellV2 lastPrev = null;

        while (frontier.Count > 0 && spawned < size)
        {
            // ���� ��������� ������ �� ��������
            int idx = Random.Range(0, frontier.Count);
            HexCellV2 current = frontier[idx];
            frontier.RemoveAt(idx);

            // ������� �������� � current
            if (CanPlaceOn(current))
            {
                var go = Instantiate(asteroidPrefab, current.transform.position, asteroidPrefab.transform.rotation, transform);
                go.transform.localScale = asteroidPrefab.transform.localScale;

                var obstacle = go.GetComponent<AsteroidObstacleV2>();
                if (obstacle == null) obstacle = go.AddComponent<AsteroidObstacleV2>();

                if (obstacle.PlaceOnCell(current))
                {
                    spawned++;
                    lastPrev = last;
                    last = current;
                }
                else
                {
                    Destroy(go);
                }
            }

            // ��������� ������� ��������
            var neighbors = current.GetNeighbors(grid);

            // ������������: ���� ��������, ��������� ������, ������� ���������� ����������� ����
            if (neighbors.Count > 0)
            {
                if (lastPrev != null && Random.value < elongation)
                {
                    Vector2 dir = new Vector2(last.Col - lastPrev.Col, last.Row - lastPrev.Row);

                    HexCellV2 best = null;
                    float bestScore = float.NegativeInfinity;

                    foreach (var n in neighbors)
                    {
                        if (n == null || used.Contains(n)) continue;
                        if (!CanPlaceOn(n)) continue;

                        Vector2 nd = new Vector2(n.Col - last.Col, n.Row - last.Row);
                        float score = Vector2.Dot(dir.normalized, nd.normalized); // ����� � 1 = ���������� �����
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = n;
                        }
                    }

                    if (best != null)
                    {
                        used.Add(best);
                        frontier.Add(best);
                    }
                }

                // ������� ���������� ��������� �������
                foreach (var n in neighbors)
                {
                    if (n == null || used.Contains(n)) continue;
                    if (!CanPlaceOn(n)) continue;

                    used.Add(n);
                    frontier.Add(n);
                }
            }
        }

        return spawned;
    }

    private bool CanPlaceOn(HexCellV2 cell)
    {
        if (cell == null) return false;
        if (!cell.walkable) return false;

        // �� ����� �� ����� ������ � �����, ���� ��������
        if (respectForbiddenRows && grid.IsForbiddenForHazards(cell.Row))
            return false;

        if (cell.HasOccupant) return false; // ����/����/��������
        return true;
    }
}
