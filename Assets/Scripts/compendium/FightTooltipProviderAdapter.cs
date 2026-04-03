using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Compendium
{
    /// <summary>
    /// Adapter between Compendium terms and your existing fight tooltip system:
    /// - Status tooltips are shown via TooltipSystemV2 using StatusDatabaseV2 + StatusDefV2.
    /// - Ability tooltips are shown via TooltipSystemV2 using AbilityDefV2 list.
    /// 
    /// Resolution order for term.tooltipKey:
    /// 1) StatusId enum name (e.g. "Barrier", "Aiming")
    /// 2) AbilityKindV2 enum name (e.g. "Barrier", "Dash")
    /// 3) AbilityDefV2.abilityName match (case-insensitive)
    /// </summary>
    public sealed class FightTooltipProviderAdapter : MonoBehaviour, ITermTooltipProvider
    {
        [Header("Status source")]
        [Tooltip("Status database asset (StatusDatabaseV2).")]
        public StatusDatabaseV2 statusDatabase;

        [Header("Ability source")]
        [Tooltip("List of AbilityDefV2 assets to resolve ability tooltips.")]
        public List<AbilityDefV2> abilityDefs = new();

        [Header("Formatting")]
        [Tooltip("If true, adds basic numeric lines to ability tooltip body.")]
        public bool includeAbilityNumbers = true;

        public void ShowTermTooltip(string tooltipKey)
        {
            Debug.Log($"[CompendiumTooltip] ShowTermTooltip key={tooltipKey}");

            if (string.IsNullOrWhiteSpace(tooltipKey))
                return;

            tooltipKey = tooltipKey.Trim();

            // 1) Try StatusId
            if (TryResolveStatus(tooltipKey, out var statusTitle, out var statusBody))
            {
                TooltipSystemV2.Show(statusTitle, statusBody);
                return;
            }

            // 2) Try AbilityKindV2
            if (TryResolveAbilityByKind(tooltipKey, out var abTitle, out var abBody))
            {
                TooltipSystemV2.Show(abTitle, abBody);
                return;
            }

            // 3) Try Ability by name
            if (TryResolveAbilityByName(tooltipKey, out abTitle, out abBody))
            {
                TooltipSystemV2.Show(abTitle, abBody);
                return;
            }

            // Nothing found -> silently hide (per your "don't spam user" philosophy)
            TooltipSystemV2.Hide();
        }

        public void HideTooltip()
        {
            TooltipSystemV2.Hide();
        }

        private bool TryResolveStatus(string key, out string title, out string body)
        {
            title = null;
            body = null;

            if (statusDatabase == null)
                return false;

            if (!Enum.TryParse<StatusId>(key, ignoreCase: true, out var statusId))
                return false;

            var def = statusDatabase.Get(statusId);
            if (def == null)
                return false;

            title = string.IsNullOrWhiteSpace(def.displayName) ? statusId.ToString() : def.displayName;

            // In combat you build body using StatusInfoV2 and template placeholders.
            // In compendium we don't have StatusInfoV2, so show raw template as a definition.
            // (Or you can later provide a "static description" field if you want.)
            body = def.descriptionTemplate ?? "";

            return true;
        }

        private bool TryResolveAbilityByKind(string key, out string title, out string body)
        {
            title = null;
            body = null;

            if (abilityDefs == null || abilityDefs.Count == 0)
                return false;

            if (!Enum.TryParse<AbilityKindV2>(key, ignoreCase: true, out var kind))
                return false;

            var ab = abilityDefs.FirstOrDefault(a => a != null && a.kind == kind);
            if (ab == null)
                return false;

            BuildAbilityTooltip(ab, out title, out body);
            return true;
        }

        private bool TryResolveAbilityByName(string key, out string title, out string body)
        {
            title = null;
            body = null;

            if (abilityDefs == null || abilityDefs.Count == 0)
                return false;

            var ab = abilityDefs.FirstOrDefault(a =>
                a != null && !string.IsNullOrWhiteSpace(a.abilityName) &&
                string.Equals(a.abilityName.Trim(), key, StringComparison.OrdinalIgnoreCase));

            if (ab == null)
                return false;

            BuildAbilityTooltip(ab, out title, out body);
            return true;
        }

        private void BuildAbilityTooltip(AbilityDefV2 ab, out string title, out string body)
        {
            title = string.IsNullOrWhiteSpace(ab.abilityName) ? ab.kind.ToString() : ab.abilityName;

            var desc = ab.description ?? "";
            if (!includeAbilityNumbers)
            {
                body = desc;
                return;
            }

            // Lightweight version of what AbilityPanelV2 displays, but in plain text.
            // You can format this later however you like.
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(desc))
                lines.Add(desc.Trim());

            // Energy
            if (ab.energyCost > 0)
                lines.Add($"Energy: {ab.energyCost}");

            // Cooldown
            if (ab.cooldownRounds > 0)
                lines.Add($"Cooldown: {ab.cooldownRounds}");

            // Range (skip self)
            bool isSelf = ab.targetMode == AbilityTargetModeV2.Self;
            bool hasAnyRange = ab.minRange > 0 || ab.maxRange > 0;
            if (!isSelf && hasAnyRange)
            {
                if (ab.minRange <= 0 && ab.maxRange > 0) lines.Add($"Range: {ab.maxRange}");
                else lines.Add($"Range: {ab.minRange}-{ab.maxRange}");
            }

            body = string.Join("\n", lines);
        }
    }
}
