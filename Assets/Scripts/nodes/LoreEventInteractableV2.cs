using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Compendium;

/// <summary>
/// Local-zone interactable: unlocks one compendium entry from a pool and opens a simple popup.
/// Source of truth for unlocked entries: CompendiumStateV2 (DontDestroyOnLoad).
/// Uses CompendiumRuntimeV2.Database only to read article data.
/// </summary>
public sealed class LoreEventInteractableV2 : InteractableEventV2
{
    [Header("UI")]
    [SerializeField] private CompendiumUIV2 ui;

    [Header("Lore Pool")]
    [Tooltip("Possible compendium entry IDs that this event can unlock.")]
    [SerializeField] private List<string> unlockPool = new List<string>();

    [Tooltip("If true, even if nothing new is unlocked, will open a random already-unlocked entry from the pool.")]
    [SerializeField] private bool allowRepeatRead = true;

    protected override void Awake()
    {
        base.Awake();
        if (ui == null)
            ui = FindFirstObjectByType<CompendiumUIV2>(FindObjectsInactive.Include);
    }

    public override void Interact(PlayerInteractorV2 interactor)
    {
        // Mark as interacted (so it disappears from objective/arrow systems)
        base.Interact(interactor);

        if (ui == null)
        {
            Debug.LogError("LoreEventInteractableV2: CompendiumUIV2 not found/assigned.");
            return;
        }

        var runtime = CompendiumRuntimeV2.Instance;
        if (runtime == null || runtime.Database == null)
        {
            ui.Show("Compendium missing",
                "CompendiumRuntimeV2.Database is not initialized. Initialize it in your global scene/bootstrap.");
            return;
        }

        var state = EnsureCompendiumState();
        if (state == null)
        {
            ui.Show("Compendium missing", "CompendiumStateV2 is missing and could not be created.");
            return;
        }

        string pickedId = PickId(state);
        if (string.IsNullOrWhiteSpace(pickedId))
        {
            ui.Show("Nothing new", "No new lore found here.");
            return;
        }

        if (runtime.Database.TryGetArticle(pickedId, out var article) && article != null)
        {
            string title = string.IsNullOrWhiteSpace(article.title) ? pickedId : article.title;
            string body = BuildBody(article);

            ui.Show(title, body);
        }
        else
        {
            ui.Show("Unknown entry", pickedId);
        }
    }

    private string PickId(CompendiumStateV2 state)
    {
        if (unlockPool == null || unlockPool.Count == 0)
            return null;

        // Prefer locked entries (deterministic: 100% unlock when possible).
        var locked = new List<string>();
        for (int i = 0; i < unlockPool.Count; i++)
        {
            string id = unlockPool[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            id = id.Trim();

            if (!state.IsUnlocked(id))
                locked.Add(id);
        }

        if (locked.Count > 0)
        {
            string picked = locked[Random.Range(0, locked.Count)];
            state.Unlock(picked); // source of truth
            return picked;
        }

        if (!allowRepeatRead)
            return null;

        // Otherwise random from pool (read-only).
        var any = new List<string>();
        for (int i = 0; i < unlockPool.Count; i++)
        {
            string id = unlockPool[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            any.Add(id.Trim());
        }

        if (any.Count == 0) return null;
        return any[Random.Range(0, any.Count)];
    }

    private static CompendiumStateV2 EnsureCompendiumState()
    {
        if (CompendiumStateV2.Instance != null)
            return CompendiumStateV2.Instance;

        // If scene was launched directly and no bootstrap created it, create it now.
        var go = new GameObject("CompendiumStateV2(Auto)");
        return go.AddComponent<CompendiumStateV2>();
    }

    private static string BuildBody(CompendiumArticle a)
    {
        if (a == null) return string.Empty;
        if (a.blocks == null || a.blocks.Count == 0) return string.Empty;

        var sb = new StringBuilder(256);
        for (int i = 0; i < a.blocks.Count; i++)
        {
            var b = a.blocks[i];
            if (b == null || string.IsNullOrWhiteSpace(b.text)) continue;

            // minimal formatting: headings get extra newlines
            if (!string.IsNullOrWhiteSpace(b.kind) &&
                b.kind.StartsWith("h", System.StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\n");
                sb.Append(b.text);
                sb.Append("\n");
            }
            else
            {
                sb.Append(b.text);
                sb.Append("\n\n");
            }
        }

        return CompendiumMarkupToTMP.ToTmpRichText(sb.ToString().Trim());
    }
}
