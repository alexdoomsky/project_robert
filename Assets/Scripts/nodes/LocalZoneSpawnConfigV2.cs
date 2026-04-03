using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-only spawn rules for LocalZone scene.
/// One scene, different content driven by (zoneIndex, nodeType, threat).
/// </summary>
[CreateAssetMenu(menuName = "V2/Local Zone/Spawn Config", fileName = "LocalZoneSpawnConfigV2")]
public sealed class LocalZoneSpawnConfigV2 : ScriptableObject
{
    [Serializable]
    public struct SpawnRange
    {
        [Range(0f, 1f)] public float chance;
        [Min(0)] public int minCount;
        [Min(0)] public int maxCount;

        public int RollCount(System.Random rng)
        {
            if (chance <= 0f) return 0;
            if (chance >= 1f) return NextIntInclusive(rng, minCount, maxCount);

            // use double for System.Random
            if (rng.NextDouble() > chance) return 0;
            return NextIntInclusive(rng, minCount, maxCount);
        }

        private static int NextIntInclusive(System.Random rng, int min, int max)
        {
            if (max < min) max = min;
            if (min < 0) min = 0;
            if (max < 0) max = 0;
            // Next(maxExclusive)
            return (max == min) ? min : rng.Next(min, max + 1);
        }
    }

    [Serializable]
    public class Profile
    {
        [Header("Match")]
        [Tooltip("Zone index (0..2). -1 means any.")]
        public int zoneIndex = -1;

        [Tooltip("If false, nodeType is ignored.")]
        public bool useNodeType = true;
        public NodeTypeV2 nodeType = NodeTypeV2.Planet;

        [Tooltip("If true, threat must match.")]
        public bool useThreat = false;
        public ThreatLevelV2 threat = ThreatLevelV2.Low;

        [Header("Spawn Rules")]
        public SpawnRange resourceClusters;
        public SpawnRange loreEvents;
        public SpawnRange randomEvents;
        public SpawnRange combats;

        [Header("Expedition")]
        [Tooltip("If true, spawn exactly one expedition marker on Planet nodes.")]
        public bool spawnExpeditionOnPlanet = true;
    }

    [Tooltip("Ordered list. First match wins.")]
    public List<Profile> profiles = new List<Profile>();

    public Profile defaultProfile = new Profile
    {
        zoneIndex = -1,
        useNodeType = false,
        useThreat = false,
        spawnExpeditionOnPlanet = true,
        resourceClusters = new SpawnRange { chance = 1f, minCount = 1, maxCount = 3 },
        loreEvents = new SpawnRange { chance = 0.5f, minCount = 1, maxCount = 1 },
        randomEvents = new SpawnRange { chance = 0.5f, minCount = 1, maxCount = 2 },
        combats = new SpawnRange { chance = 0.5f, minCount = 1, maxCount = 1 },
    };

    public Profile Pick(int zoneIndex, NodeTypeV2 nodeType, ThreatLevelV2 threat)
    {
        for (int i = 0; i < profiles.Count; i++)
        {
            var p = profiles[i];
            if (p == null) continue;
            if (p.zoneIndex != -1 && p.zoneIndex != zoneIndex) continue;
            if (p.useNodeType && p.nodeType != nodeType) continue;
            if (p.useThreat && p.threat != threat) continue;
            return p;
        }
        return defaultProfile;
    }
}
