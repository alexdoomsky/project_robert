using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-only preset used to override combat scene spawners.
/// Assign these assets in ZoneExitEncounterConfigV2 to control battlefield contents.
/// </summary>
[CreateAssetMenu(menuName = "V2/Combat/Battle Encounter Preset", fileName = "BattleEncounterPresetV2")]
public sealed class BattleEncounterPresetV2 : ScriptableObject
{
    [Header("Asteroid Clusters")]
    [Min(0)] public int clusterCount = -1;
    [Min(1)] public int clusterMinSize = 3;
    [Min(1)] public int clusterMaxSize = 8;

    [Header("Mines")]
    [Min(0)] public int mineCount = -1;

    [Header("Turrets")]
    [Min(0)] public int turretCount = -1;

    [Serializable]
    public struct EnemySpawnEntry
    {
        [Tooltip("Prefab root GameObject that has UnitV2 on it.")]
        public GameObject prefab;

        [Tooltip("Id used by campaign inventory. If empty, prefab.name will be used.")]
        public string unitId;

        [Min(1)] public int count;
    }

    [Header("Enemy Units")]
    [Tooltip("If not empty, overrides UnitSpawnerV2 enemy list for this battle.")]
    public List<EnemySpawnEntry> enemySpawns = new List<EnemySpawnEntry>();

    public bool HasEnemyOverrides => enemySpawns != null && enemySpawns.Count > 0;

    public int GetClusterCount(int fallback) => clusterCount >= 0 ? clusterCount : fallback;
    public int GetMineCount(int fallback) => mineCount >= 0 ? mineCount : fallback;
    public int GetTurretCount(int fallback) => turretCount >= 0 ? turretCount : fallback;
}
