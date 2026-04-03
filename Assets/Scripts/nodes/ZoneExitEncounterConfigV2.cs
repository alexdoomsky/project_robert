using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ZoneExitEncounterChanceV2
{
    public int zoneIndex;
    [Range(0f, 1f)] public float chanceLow;
    [Range(0f, 1f)] public float chanceMedium;
    [Range(0f, 1f)] public float chanceHigh;
}

[Serializable]
public struct ZoneThreatPresetPoolV2
{
    public int zoneIndex;
    public ThreatLevelV2 threat;
    public List<BattleEncounterPresetV2> presets;
}

/// <summary>
/// Inspector-driven chances to proc combat when exiting a LocalZone.
/// Also can provide encounter presets per zone + threat.
/// 
/// Compatibility notes:
/// - Some callers use GetChance(zoneIndex, ThreatLevelV2)
/// - Some callers use RollShouldStartCombat(...)
/// - RunState stores threat as string, so we provide overloads.
/// </summary>
[CreateAssetMenu(menuName = "V2/Exploration/Zone Exit Encounter Config", fileName = "ZoneExitEncounterConfigV2")]
public sealed class ZoneExitEncounterConfigV2 : ScriptableObject
{
    [Header("Chances")]
    public List<ZoneExitEncounterChanceV2> zones = new List<ZoneExitEncounterChanceV2>
    {
        new ZoneExitEncounterChanceV2 { zoneIndex = 0, chanceLow = 0.05f, chanceMedium = 0.10f, chanceHigh = 0.15f },
        new ZoneExitEncounterChanceV2 { zoneIndex = 1, chanceLow = 0.15f, chanceMedium = 0.25f, chanceHigh = 0.35f },
        new ZoneExitEncounterChanceV2 { zoneIndex = 2, chanceLow = 0.30f, chanceMedium = 0.45f, chanceHigh = 0.60f },
    };

    [Header("Encounter Presets")]
    [Tooltip("Optional pools. If empty or no match found, combat will use default spawner settings.")]
    public List<ZoneThreatPresetPoolV2> presetPools = new List<ZoneThreatPresetPoolV2>();

    public float GetChance(int zoneIndex, ThreatLevelV2 threat)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].zoneIndex != zoneIndex) continue;
            switch (threat)
            {
                case ThreatLevelV2.Low: return Mathf.Clamp01(zones[i].chanceLow);
                case ThreatLevelV2.Medium: return Mathf.Clamp01(zones[i].chanceMedium);
                case ThreatLevelV2.High: return Mathf.Clamp01(zones[i].chanceHigh);
                default: return Mathf.Clamp01(zones[i].chanceMedium);
            }
        }
        return 0f;
    }

    public float GetChance(int zoneIndex, string threat)
    {
        return GetChance(zoneIndex, ParseThreat(threat));
    }

    public bool RollShouldStartCombat(int zoneIndex, ThreatLevelV2 threat)
    {
        float chance = GetChance(zoneIndex, threat);
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    public bool RollShouldStartCombat(int zoneIndex, string threat)
    {
        return RollShouldStartCombat(zoneIndex, ParseThreat(threat));
    }

    public BattleEncounterPresetV2 PickPreset(int zoneIndex, ThreatLevelV2 threat)
    {
        if (presetPools == null || presetPools.Count == 0) return null;

        List<BattleEncounterPresetV2> pool = null;
        for (int i = 0; i < presetPools.Count; i++)
        {
            if (presetPools[i].zoneIndex != zoneIndex) continue;
            if (presetPools[i].threat != threat) continue;
            pool = presetPools[i].presets;
            break;
        }

        if (pool == null || pool.Count == 0) return null;

        // pick random non-null
        for (int guard = 0; guard < 20; guard++)
        {
            var p = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (p != null) return p;
        }

        return pool[0];
    }

    public BattleEncounterPresetV2 PickPreset(int zoneIndex, string threat)
    {
        return PickPreset(zoneIndex, ParseThreat(threat));
    }

    private static ThreatLevelV2 ParseThreat(string threat)
    {
        if (string.IsNullOrWhiteSpace(threat))
            return ThreatLevelV2.Medium;

        if (Enum.TryParse(threat, true, out ThreatLevelV2 parsed))
            return parsed;

        // Common aliases just in case
        string t = threat.Trim().ToLowerInvariant();
        if (t.Contains("low")) return ThreatLevelV2.Low;
        if (t.Contains("high")) return ThreatLevelV2.High;
        return ThreatLevelV2.Medium;
    }
}
