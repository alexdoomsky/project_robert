using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HexCellV2 : MonoBehaviour
{
    [Header("Coords")]
    public int Col { get; private set; }
    public int Row { get; private set; }

    [Header("Flags")]
    public bool walkable = true;

    // Occupant может быть UnitV2 / AsteroidObstacleV2 / MineTrapV2 и тд
    public object Occupant { get; private set; }
    public bool OccupantBlocksMovement { get; private set; }
    public bool HasOccupant => Occupant != null;

    [Header("Visuals")]
    [SerializeField] private Renderer targetRenderer;

    private MaterialPropertyBlock _mpb;
    private bool _cachedOriginal;
    private Color _originalColor = Color.white;

    // highlight layers
    private bool _tempHighlight;
    private Color _tempColor;

    private bool _persistentHighlight;
    private Color _persistentColor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in/Standard

    public void Init(int col, int row)
    {
        Col = col;
        Row = row;
        name = $"Hex_{col}_{row}";

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _mpb ??= new MaterialPropertyBlock();

        CacheOriginalColor();
        ApplyCurrentColor();
    }

    private void CacheOriginalColor()
    {
        if (_cachedOriginal) return;
        if (targetRenderer == null) return;

        var mat = targetRenderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty(BaseColorId))
                _originalColor = mat.GetColor(BaseColorId);
            else if (mat.HasProperty(ColorId))
                _originalColor = mat.GetColor(ColorId);
        }

        _cachedOriginal = true;
    }

    public bool TrySetOccupant(object occupant, bool blocksMovement)
    {
        if (occupant == null) return false;

        if (Occupant != null && ReferenceEquals(Occupant, occupant))
        {
            OccupantBlocksMovement = blocksMovement;
            return true;
        }

        if (Occupant != null) return false;

        Occupant = occupant;
        OccupantBlocksMovement = blocksMovement;
        return true;
    }

    public void ClearOccupant(object occupant)
    {
        if (Occupant == null) return;
        if (!ReferenceEquals(Occupant, occupant)) return;

        Occupant = null;
        OccupantBlocksMovement = false;
    }

    // === TEMP HIGHLIGHT (старый контракт): hover/selection/path ===
    public void SetHighlight(Color color)
    {
        _tempHighlight = true;
        _tempColor = color;
        ApplyCurrentColor();
    }

    public void ResetHighlight()
    {
        _tempHighlight = false;
        ApplyCurrentColor();
    }

    // === PERSISTENT HIGHLIGHT: AoE preview, долгие маркеры и т.п. ===
    public void SetPersistentHighlight(Color color)
    {
        _persistentHighlight = true;
        _persistentColor = color;
        ApplyCurrentColor();
    }

    public void ResetPersistentHighlight()
    {
        _persistentHighlight = false;
        ApplyCurrentColor();
    }

    private void ApplyCurrentColor()
    {
        if (targetRenderer == null) return;

        CacheOriginalColor();

        Color c =
            _tempHighlight ? _tempColor :
            _persistentHighlight ? _persistentColor :
            _originalColor;

        _mpb ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_mpb);

        // красим и URP, и Standard
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);

        targetRenderer.SetPropertyBlock(_mpb);
    }

    // odd-r neighbors (pointy-top)
    public List<HexCellV2> GetNeighbors(HexGridV2 grid)
    {
        var res = new List<HexCellV2>(6);
        if (grid == null) return res;

        bool odd = (Row & 1) == 1;

        int[,] dirsEven = { { +1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, +1 }, { 0, +1 } };
        int[,] dirsOdd = { { +1, 0 }, { +1, -1 }, { 0, -1 }, { -1, 0 }, { 0, +1 }, { +1, +1 } };

        var dirs = odd ? dirsOdd : dirsEven;

        for (int i = 0; i < 6; i++)
        {
            int nc = Col + dirs[i, 0];
            int nr = Row + dirs[i, 1];

            if (grid.TryGetCell(nc, nr, out var n) && n != null)
                res.Add(n);
        }

        return res;
    }
}
