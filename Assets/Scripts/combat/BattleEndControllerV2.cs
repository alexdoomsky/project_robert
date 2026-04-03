using System;
using System.Collections.Generic;
using UnityEngine;

public class BattleEndControllerV2 : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private BattleOutcomeUIV2 outcomeUI;
    [Tooltip("Optional: minimal report UI (single TMP_Text) that shows battle stats + reward breakdown.")]
    [SerializeField] private BattleReportUIV2 reportUI;

    [Header("Campaign")]
    [Tooltip("Optional. If set and RunStateV2 exists, battle end will return surviving units and apply rewards.")]
    [SerializeField] private BattleRewardsConfigV2 rewardsConfig;

    [Header("Options")]
    [SerializeField] private bool pauseTimeOnEnd = true;
    [SerializeField] private bool autoInitializeOnStart = false;

    private readonly HashSet<UnitV2> _alivePlayer = new();
    private readonly HashSet<UnitV2> _aliveEnemy = new();

    // For stats/rewards
    private readonly Dictionary<string, int> _playerSelected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _playerDeaths = new(StringComparer.Ordinal);

    private readonly Dictionary<UnitV2.EnemyArchetype, int> _enemyTotals = new();
    private readonly Dictionary<UnitV2.EnemyArchetype, int> _enemyDeaths = new();

    private bool _ended;
    private bool _trackingActive;

    private RunStateV2 _runState;
    private TurnManagerV2 _turnManager;
    private int _roundsEnded;

    [Serializable]
    public struct BattleReport
    {
        public bool victory;
        public int rounds;

        public Dictionary<UnitV2.EnemyArchetype, int> enemyTotals;
        public Dictionary<UnitV2.EnemyArchetype, int> enemyKilled;
        public Dictionary<UnitV2.EnemyArchetype, int> enemyKillReward;
        public int enemySalvageTotal;

        public Dictionary<string, int> playerSelected;
        public Dictionary<string, int> playerLost;
        public Dictionary<string, int> playerLossReward;
        public int playerSalvageTotal;

        public int baseReward;
        public int totalMaterials;
        public int healAwarded;
        public int dronesAwarded;
    }

    private void Awake()
    {
        if (outcomeUI == null)
            outcomeUI = FindObjectOfType<BattleOutcomeUIV2>(true);
        if (reportUI == null)
            reportUI = FindObjectOfType<BattleReportUIV2>(true);

        _runState = RunStateV2.Instance;
        if (_runState == null)
            _runState = FindObjectOfType<RunStateV2>();

        _turnManager = FindObjectOfType<TurnManagerV2>(true);
        if (_turnManager != null)
        {
            _turnManager.OnRoundEnded -= OnRoundEnded;
            _turnManager.OnRoundEnded += OnRoundEnded;
        }
    }

    private void Start()
    {
        if (autoInitializeOnStart)
            InitializeFromScene();
    }

    private void OnDestroy()
    {
        UnsubscribeAll();
        if (_turnManager != null)
            _turnManager.OnRoundEnded -= OnRoundEnded;
    }

    private void OnRoundEnded(int round)
    {
        _roundsEnded = round;
    }

    // Call AFTER you spawned all starting units.
    public void InitializeFromScene()
    {
        if (_ended) return;

        RebuildRosterFromScene();
        _trackingActive = true;
        EvaluateNow();
    }

    public void BeginTracking()
    {
        if (_ended) return;
        _trackingActive = true;

        // Snapshot campaign selection (if present) for end-of-battle reconciliation.
        _playerSelected.Clear();
        if (_runState != null)
        {
            var sel = _runState.GetLastBattleSelection();
            if (sel != null)
            {
                foreach (var kv in sel)
                    _playerSelected[kv.Key] = Mathf.Max(0, kv.Value);
            }
        }

        EvaluateNow();
    }

    public void RegisterUnit(UnitV2 unit)
    {
        if (unit == null) return;
        if (_ended) return;

        Subscribe(unit);

        // Track totals for report/rewards.
        if (unit.Team == UnitV2.Faction.Enemy)
        {
            var a = unit.Archetype;
            _enemyTotals[a] = _enemyTotals.TryGetValue(a, out var t) ? t + 1 : 1;
        }

        if (unit.IsAlive)
        {
            if (unit.Team == UnitV2.Faction.Player)
                _alivePlayer.Add(unit);
            else
                _aliveEnemy.Add(unit);
        }

        if (_trackingActive)
            EvaluateNow();
    }

    private void RebuildRosterFromScene()
    {
        UnsubscribeAll();
        _alivePlayer.Clear();
        _aliveEnemy.Clear();
        _enemyTotals.Clear();
        _enemyDeaths.Clear();

        var units = FindObjectsOfType<UnitV2>(true);
        foreach (var u in units)
        {
            if (u == null) continue;

            Subscribe(u);

            if (u.Team == UnitV2.Faction.Enemy)
            {
                var a = u.Archetype;
                _enemyTotals[a] = _enemyTotals.TryGetValue(a, out var t) ? t + 1 : 1;
            }

            if (!u.IsAlive) continue;

            if (u.Team == UnitV2.Faction.Player)
                _alivePlayer.Add(u);
            else
                _aliveEnemy.Add(u);
        }
    }

    private void Subscribe(UnitV2 u)
    {
        u.OnDied -= OnUnitDied;
        u.OnDied += OnUnitDied;
    }

    private void UnsubscribeAll()
    {
        foreach (var u in _alivePlayer)
            if (u != null) u.OnDied -= OnUnitDied;

        foreach (var u in _aliveEnemy)
            if (u != null) u.OnDied -= OnUnitDied;
    }

    private void OnUnitDied(UnitV2 unit)
    {
        if (_ended) return;
        if (!_trackingActive) return;
        if (unit == null) return;

        unit.OnDied -= OnUnitDied;

        // Stats: count deaths by side
        if (unit.Team == UnitV2.Faction.Enemy)
        {
            var a = unit.Archetype;
            _enemyDeaths[a] = _enemyDeaths.TryGetValue(a, out var d) ? d + 1 : 1;
        }
        else if (unit.Team == UnitV2.Faction.Player)
        {
            string id = GetCampaignUnitId(unit);
            if (!string.IsNullOrWhiteSpace(id))
                _playerDeaths[id] = _playerDeaths.TryGetValue(id, out var d) ? d + 1 : 1;
        }

        _alivePlayer.Remove(unit);
        _aliveEnemy.Remove(unit);

        EvaluateNow();
    }

    private string GetCampaignUnitId(UnitV2 unit)
    {
        var tag = unit != null ? unit.GetComponent<CampaignUnitTagV2>() : null;
        if (tag != null && !string.IsNullOrWhiteSpace(tag.unitId))
            return tag.unitId;
        return unit != null ? unit.name : string.Empty;
    }

    private void EvaluateNow()
    {
        if (_ended) return;
        if (!_trackingActive) return;

        _alivePlayer.RemoveWhere(u => u == null || !u.IsAlive);
        _aliveEnemy.RemoveWhere(u => u == null || !u.IsAlive);

        if (_alivePlayer.Count == 0 && _aliveEnemy.Count == 0)
        {
            EndBattle(defeat: true);
            return;
        }

        if (_alivePlayer.Count == 0)
        {
            EndBattle(defeat: true);
            return;
        }

        if (_aliveEnemy.Count == 0)
        {
            EndBattle(defeat: false);
            return;
        }
    }

    private void EndBattle(bool defeat)
    {
        _ended = true;

        if (pauseTimeOnEnd)
            Time.timeScale = 0f;

        bool victory = !defeat;

        // Campaign reconciliation + rewards + report
        BattleReport report = default;
        bool hasReport = false;

        if (_runState != null)
        {
            report = ApplyCampaignResults(victory);
            hasReport = true;
        }

        // Prefer single outcome panel (it can also render the report inside).
        if (outcomeUI != null)
        {
            if (hasReport)
            {
                outcomeUI.Show(victory, report);
            }
            else
            {
                // Still show the panel even if we don't have campaign report.
                outcomeUI.Show(victory, new BattleReport
                {
                    victory = victory,
                    rounds = _roundsEnded,
                    enemyTotals = new Dictionary<UnitV2.EnemyArchetype, int>(_enemyTotals),
                    enemyKilled = new Dictionary<UnitV2.EnemyArchetype, int>(_enemyDeaths),
                    enemyKillReward = new Dictionary<UnitV2.EnemyArchetype, int>(),
                    enemySalvageTotal = 0,
                    playerSelected = new Dictionary<string, int>(_playerSelected, StringComparer.Ordinal),
                    playerLost = new Dictionary<string, int>(StringComparer.Ordinal),
                    playerLossReward = new Dictionary<string, int>(StringComparer.Ordinal),
                    playerSalvageTotal = 0,
                    baseReward = 0,
                    totalMaterials = 0,
                    healAwarded = 0,
                    dronesAwarded = 0,
                });
            }
        }
        else
        {
            // Fallback: if someone still has standalone report UI in scene.
            if (hasReport && reportUI != null)
                reportUI.Show(report);
            else
                Debug.LogWarning("BattleEndControllerV2: outcomeUI not found.");
        }
    }

    private BattleReport ApplyCampaignResults(bool victory)
    {
        // 1) Determine survivors by scanning existing tagged player units at battle end.
        var survivorsById = new Dictionary<string, int>(StringComparer.Ordinal);
        var tags = FindObjectsOfType<CampaignUnitTagV2>(true);
        for (int i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            if (tag == null) continue;
            var unit = tag.GetComponent<UnitV2>();
            if (unit == null) continue;
            if (unit.Team != UnitV2.Faction.Player) continue;

            string id = string.IsNullOrWhiteSpace(tag.unitId) ? unit.name : tag.unitId;
            survivorsById[id] = survivorsById.TryGetValue(id, out var c) ? c + 1 : 1;
        }

        // 2) Add survivors back to global inventory (we consumed full selection on enter).
        var playerLost = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in _playerSelected)
        {
            string id = kv.Key;
            int selected = Mathf.Max(0, kv.Value);
            int survived = survivorsById.TryGetValue(id, out var s) ? s : 0;
            int lost = Mathf.Max(0, selected - survived);
            playerLost[id] = lost;

            if (survived > 0)
                _runState.AddUnit(id, survived);
        }

        // 3) Rewards
        int baseReward = 0;
        int enemySalvageTotal = 0;
        int playerSalvageTotal = 0;
        int totalMaterials = 0;
        int healAwarded = 0;
        int dronesAwarded = 0;

        var enemyKillReward = new Dictionary<UnitV2.EnemyArchetype, int>();
        var playerLossReward = new Dictionary<string, int>(StringComparer.Ordinal);

        if (rewardsConfig != null)
        {
            baseReward = victory ? rewardsConfig.baseWinMaterials : rewardsConfig.baseLoseMaterials;
            float mult = victory ? rewardsConfig.salvageMultiplierWin : rewardsConfig.salvageMultiplierLose;

            // Enemy kills
            foreach (var kv in _enemyDeaths)
            {
                int per = rewardsConfig.GetEnemySalvage(kv.Key);
                int reward = Mathf.RoundToInt(kv.Value * per * mult);
                enemyKillReward[kv.Key] = reward;
                enemySalvageTotal += reward;
            }

            // Player losses (based on fully destroyed units, not on death events)
            foreach (var kv in playerLost)
            {
                int per = rewardsConfig.GetPlayerSalvage(kv.Key);
                int reward = Mathf.RoundToInt(kv.Value * per * mult);
                playerLossReward[kv.Key] = reward;
                playerSalvageTotal += reward;
            }

            totalMaterials = baseReward + enemySalvageTotal + playerSalvageTotal;
            if (totalMaterials > 0)
                _runState.AddMaterials(totalMaterials);

            // Drops
            float healChance = victory ? rewardsConfig.healChanceWin : rewardsConfig.healChanceLose;
            if (rewardsConfig.healChargesAward > 0 && UnityEngine.Random.value <= healChance)
            {
                healAwarded = rewardsConfig.healChargesAward;
                _runState.AddHealCharges(healAwarded);
            }

            float droneChance = victory ? rewardsConfig.droneChanceWin : rewardsConfig.droneChanceLose;
            if (rewardsConfig.dronesAward > 0 && UnityEngine.Random.value <= droneChance)
            {
                dronesAwarded = rewardsConfig.dronesAward;
                _runState.AddDrones(dronesAwarded);
            }

            // Run HP penalty on defeat
            if (!victory && rewardsConfig.loseRunHpOnDefeat > 0)
                _runState.LoseRunHp(rewardsConfig.loseRunHpOnDefeat);
        }

        // Clear last selection after we reconciled.
        _runState.ClearLastBattleSelection();

        return new BattleReport
        {
            victory = victory,
            rounds = _roundsEnded,

            enemyTotals = new Dictionary<UnitV2.EnemyArchetype, int>(_enemyTotals),
            enemyKilled = new Dictionary<UnitV2.EnemyArchetype, int>(_enemyDeaths),
            enemyKillReward = enemyKillReward,
            enemySalvageTotal = enemySalvageTotal,

            playerSelected = new Dictionary<string, int>(_playerSelected, StringComparer.Ordinal),
            playerLost = playerLost,
            playerLossReward = playerLossReward,
            playerSalvageTotal = playerSalvageTotal,

            baseReward = baseReward,
            totalMaterials = totalMaterials,
            healAwarded = healAwarded,
            dronesAwarded = dronesAwarded,
        };
    }
}
