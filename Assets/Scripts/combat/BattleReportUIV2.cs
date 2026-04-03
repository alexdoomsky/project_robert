using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Minimal battle report presenter.
/// Drop it into victory/defeat roots and wire a single TMP_Text.
/// </summary>
public sealed class BattleReportUIV2 : MonoBehaviour
{
    [SerializeField] private TMP_Text reportText;

    public void Show(BattleEndControllerV2.BattleReport report)
    {
        if (reportText == null) return;

        var sb = new StringBuilder(512);
        sb.AppendLine(report.victory ? "RESULT: VICTORY" : "RESULT: DEFEAT");
        if (report.rounds >= 0) sb.AppendLine($"Rounds: {report.rounds}");
        sb.AppendLine();

        sb.AppendLine("ENEMIES:");
        if (report.enemyTotals.Count == 0)
        {
            sb.AppendLine("  none");
        }
        else
        {
            foreach (var kv in report.enemyTotals)
            {
                int killed = report.enemyKilled.TryGetValue(kv.Key, out var k) ? k : 0;
                int total = kv.Value;
                int reward = report.enemyKillReward.TryGetValue(kv.Key, out var r) ? r : 0;
                sb.AppendLine($"  {kv.Key}: {killed}/{total}  +{reward}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("YOUR UNITS (losses):");
        if (report.playerSelected.Count == 0)
        {
            sb.AppendLine("  none");
        }
        else
        {
            foreach (var kv in report.playerSelected)
            {
                int lost = report.playerLost.TryGetValue(kv.Key, out var l) ? l : 0;
                int total = kv.Value;
                int reward = report.playerLossReward.TryGetValue(kv.Key, out var r) ? r : 0;
                sb.AppendLine($"  {kv.Key}: {lost}/{total}  +{reward}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Base reward: {report.baseReward}");
        sb.AppendLine($"Salvage (enemies): {report.enemySalvageTotal}");
        sb.AppendLine($"Salvage (your losses): {report.playerSalvageTotal}");
        sb.AppendLine($"TOTAL materials: {report.totalMaterials}");

        if (report.healAwarded > 0)
            sb.AppendLine($"Bonus: +{report.healAwarded} heal");
        if (report.dronesAwarded > 0)
            sb.AppendLine($"Bonus: +{report.dronesAwarded} drone");

        reportText.text = sb.ToString();
    }
}
