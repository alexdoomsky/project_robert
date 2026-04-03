using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "V2/Combat/Battle Rewards Config", fileName = "BattleRewardsConfigV2")]
public sealed class BattleRewardsConfigV2 : ScriptableObject
{
    [Header("Base reward (materials)")]
    [Min(0)] public int baseWinMaterials = 30;
    [Min(0)] public int baseLoseMaterials = 10;

    [Header("Run HP (global player HP)")]
    [Tooltip("How much global run HP is lost on defeat. 0 disables the penalty.")]
    [Min(0)] public int loseRunHpOnDefeat = 3;

    [Header("Salvage multipliers")]
    [Min(0f)] public float salvageMultiplierWin = 1.0f;
    [Min(0f)] public float salvageMultiplierLose = 0.5f;

    [Header("Enemy salvage values")]
    public List<EnemySalvageEntry> enemySalvage = new();

    [Header("Player unit salvage values (by unitId)")]
    public List<UnitIdSalvageEntry> playerUnitSalvage = new();

    [Header("Drops")]
    [Range(0f, 1f)] public float healChanceWin = 0.25f;
    [Range(0f, 1f)] public float healChanceLose = 0.10f;
    [Min(0)] public int healChargesAward = 1;

    [Range(0f, 1f)] public float droneChanceWin = 0.10f;
    [Range(0f, 1f)] public float droneChanceLose = 0.00f;
    [Min(0)] public int dronesAward = 1;

    [Serializable]
    public struct EnemySalvageEntry
    {
        public UnitV2.EnemyArchetype archetype;
        [Min(0)] public int salvageValue;
    }

    [Serializable]
    public struct UnitIdSalvageEntry
    {
        public string unitId;
        [Min(0)] public int salvageValue;
    }

    public int GetEnemySalvage(UnitV2.EnemyArchetype a)
    {
        for (int i = 0; i < enemySalvage.Count; i++)
            if (enemySalvage[i].archetype == a)
                return Mathf.Max(0, enemySalvage[i].salvageValue);
        return 0;
    }

    public int GetPlayerSalvage(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId)) return 0;
        for (int i = 0; i < playerUnitSalvage.Count; i++)
            if (string.Equals(playerUnitSalvage[i].unitId, unitId, StringComparison.Ordinal))
                return Mathf.Max(0, playerUnitSalvage[i].salvageValue);
        return 0;
    }
}
