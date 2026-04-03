using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Compendium;

/// <summary>
/// Local Zone scene controller.
/// Handles:
/// - basic node info UI
/// - exit interaction
/// - lore unlock on exit (via CompendiumStateV2)
/// - optional combat on exit
/// </summary>
public sealed class LocalZoneControllerV2 : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text detailsText;
    public Button exitButton;

    [Header("Combat On Exit")]
    public ZoneExitEncounterConfigV2 exitEncounterConfig;

    [Header("Compendium On Exit")]
    public NodeExitLoreUnlockConfigV2 exitLoreUnlockConfig;
    public CompendiumUIV2 exitLoreUI;
    public bool showExitLorePopup = true;

    private void Awake()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(ExitLocalZone);
        }
    }

    private void Start()
    {
        var run = RunStateV2.Instance;
        if (run == null)
        {
            if (titleText != null) titleText.text = "Local Zone";
            if (detailsText != null) detailsText.text = "RunStateV2 not found";
            return;
        }

        if (titleText != null)
            titleText.text = $"Node {run.currentNodeId}";

        if (detailsText != null)
            detailsText.text =
                $"Zone: {run.currentZoneIndex + 1}\n" +
                $"Type: {run.currentNodeType}\n" +
                $"Threat: {run.currentThreat}";
    }

    public void ExitLocalZone()
    {
        var router = SceneRouterV2.Instance;
        var run = RunStateV2.Instance;

        if (router == null)
        {
            Debug.LogError("LocalZoneControllerV2: SceneRouterV2.Instance is null.");
            return;
        }

        // 1) Try unlock lore on exit (state-based, deterministic)
        TryUnlockExitLore();

        if (run == null || exitEncounterConfig == null)
        {
            router.LoadMap();
            return;
        }

        // 2) Roll combat on exit (this is ONLY for exit logic)
        bool startCombat = exitEncounterConfig.RollShouldStartCombat(
            run.currentZoneIndex,
            run.currentThreat
        );

        if (startCombat)
        {
            run.pendingEncounterPreset =
                exitEncounterConfig.PickPreset(run.currentZoneIndex, run.currentThreat);

            router.LoadCombat(router.mapSceneName);
        }
        else
        {
            router.LoadMap();
        }
    }

    private void TryUnlockExitLore()
    {
        if (exitLoreUnlockConfig == null) return;

        var state = CompendiumStateV2.Instance;
        if (state == null)
        {
            Debug.LogWarning("LocalZoneControllerV2: CompendiumStateV2 missing.");
            return;
        }

        if (!Enum.TryParse(RunStateV2.Instance.currentNodeType, true, out NodeTypeV2 nt))
            nt = NodeTypeV2.Planet;

        var pool = exitLoreUnlockConfig.GetPool(nt);
        if (pool == null || pool.Count == 0) return;

        var locked = new List<string>();
        for (int i = 0; i < pool.Count; i++)
        {
            string id = pool[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!state.IsUnlocked(id))
                locked.Add(id.Trim());
        }

        if (locked.Count == 0) return;

        string picked = locked[UnityEngine.Random.Range(0, locked.Count)];
        state.Unlock(picked);

        if (!showExitLorePopup) return;

        if (exitLoreUI == null)
            exitLoreUI = FindFirstObjectByType<CompendiumUIV2>(FindObjectsInactive.Include);

        if (exitLoreUI == null) return;

        var runtime = CompendiumRuntimeV2.Instance;
        if (runtime == null || runtime.Database == null)
        {
            exitLoreUI.Show("Lore unlocked", picked);
            return;
        }

        if (runtime.Database.TryGetArticle(picked, out var article) && article != null)
        {
            string title = string.IsNullOrWhiteSpace(article.title) ? picked : article.title;
            string body = BuildBody(article);
            exitLoreUI.Show(title, body);
        }
        else
        {
            exitLoreUI.Show("Lore unlocked", picked);
        }
    }

    private static string BuildBody(CompendiumArticle a)
    {
        if (a == null || a.blocks == null) return string.Empty;

        var sb = new StringBuilder(256);
        for (int i = 0; i < a.blocks.Count; i++)
        {
            var b = a.blocks[i];
            if (b == null || string.IsNullOrWhiteSpace(b.text)) continue;

            if (!string.IsNullOrWhiteSpace(b.kind) &&
                b.kind.StartsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\n").Append(b.text).Append("\n");
            }
            else
            {
                sb.Append(b.text).Append("\n\n");
            }
        }

        return CompendiumMarkupToTMP.ToTmpRichText(sb.ToString().Trim());
    }
}
