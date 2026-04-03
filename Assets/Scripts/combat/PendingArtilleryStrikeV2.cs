using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PendingArtilleryStrikeV2 : MonoBehaviour
{
    private UnitV2 caster;
    private AbilityDefV2 ability;
    private HexCellV2 targetCell;

    private TurnManagerV2 turnManager;
    private HexGridV2 grid;

    private int roundsLeft;
    private bool exploded;

    private GameObject telegraphInstance;
    private TMP_Text telegraphTimerText;

    private readonly List<HexCellV2> highlightedCells = new();

    public int RoundsLeft => Mathf.Max(0, roundsLeft);

    public void Init(UnitV2 caster, AbilityDefV2 ability, HexCellV2 targetCell)
    {
        this.caster = caster;
        this.ability = ability;
        this.targetCell = targetCell;

        turnManager = FindObjectOfType<TurnManagerV2>();
        grid = FindObjectOfType<HexGridV2>();

        roundsLeft = Mathf.Max(0, ability != null ? ability.artilleryDelayRounds : 0);

        SpawnTelegraph();
        ApplyAoePreview();

        if (turnManager != null)
            turnManager.OnRoundEnded += OnRoundEnded;
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.OnRoundEnded -= OnRoundEnded;

        CleanupPreviewAndTelegraph();
    }

    private void OnRoundEnded(int round)
    {
        if (exploded) return;

        roundsLeft--;
        UpdateTelegraphText();

        if (roundsLeft <= 0)
            Explode();
    }

    private void SpawnTelegraph()
    {
        if (ability == null || ability.telegraphPrefab == null || targetCell == null)
            return;

        Vector3 pos = targetCell.transform.position;
        telegraphInstance = Instantiate(ability.telegraphPrefab, pos, Quaternion.identity);

        telegraphTimerText = telegraphInstance.GetComponentInChildren<TMP_Text>(true);
        UpdateTelegraphText();
    }

    private void UpdateTelegraphText()
    {
        if (telegraphTimerText == null) return;
        telegraphTimerText.text = Mathf.Max(0, roundsLeft).ToString();
    }

    private void ApplyAoePreview()
    {
        if (ability == null) return;
        if (grid == null || targetCell == null) return;
        if (!ability.showAoePreview) return;

        highlightedCells.Clear();

        var cells = PathfinderV2.GetCellsInRange(targetCell, Mathf.Max(0, ability.aoeRadius), grid);
        highlightedCells.AddRange(cells);

        Color c = ability.aoePreviewColor;

        foreach (var cell in highlightedCells)
        {
            if (cell == null) continue;
            cell.SetPersistentHighlight(c);
        }
    }

    private void RemoveAoePreview()
    {
        foreach (var cell in highlightedCells)
        {
            if (cell == null) continue;
            cell.ResetPersistentHighlight();
        }

        highlightedCells.Clear();
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (caster != null)
            caster.SetAiming(false);

        if (grid == null || targetCell == null || ability == null)
        {
            CleanupPreviewAndTelegraph();
            Destroy(this);
            return;
        }

        var aoeCells = PathfinderV2.GetCellsInRange(targetCell, Mathf.Max(0, ability.aoeRadius), grid);
        int dmg = Mathf.Max(0, ability.artilleryDamage);

        // FIX: не верим cell.Occupant, а верим UnitV2.CurrentCell
        var allUnits = FindObjectsOfType<UnitV2>(true);
        var allMines = FindObjectsOfType<MineTrapV2>(true);

        foreach (var cell in aoeCells)
        {
            if (cell == null) continue;

            // Units
            for (int i = 0; i < allUnits.Length; i++)
            {
                var u = allUnits[i];
                if (u == null) continue;
                if (!u.IsAlive) continue;
                if (u.CurrentCell == cell)
                    u.TakeDamage(dmg, caster);
            }

            // Mines (если хочешь, чтобы артиллерия могла взрывать мины)
            for (int i = 0; i < allMines.Length; i++)
            {
                var m = allMines[i];
                if (m == null) continue;
                if (m.CurrentCell == cell)
                    m.TakeDamage(dmg, caster);
            }
        }

        CleanupPreviewAndTelegraph();
        Destroy(this);
    }

    private void CleanupPreviewAndTelegraph()
    {
        RemoveAoePreview();

        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
            telegraphInstance = null;
        }
    }
}
