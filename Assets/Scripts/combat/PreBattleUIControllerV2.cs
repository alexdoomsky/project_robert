using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-battle UI: shows battlefield summary and lets player select units to spawn.
///
/// Campaign integration:
/// - If RunStateV2 exists, it overrides UnitOption.availableCount from global inventory via UnitOption.unitId.
/// - On Start Battle, it consumes selected units from RunStateV2 and then asks UnitSpawnerV2 to spawn them.
/// </summary>
public class PreBattleUIControllerV2 : MonoBehaviour
{
    [Serializable]
    public class UnitOption
    {
        public UnitV2 prefab;
        public string displayName;

        [Header("Inventory")]
        [Tooltip("Id used in RunState inventory. If empty, prefab.name will be used.")]
        public string unitId;

        [Header("Card UI")]
        public Sprite cardSprite;

        [Min(0)] public int availableCount = 99;
        [Min(0)] public int maxPerBattle = 99;
        [HideInInspector] public int selected;
    }

    [Header("Refs")]
    [SerializeField] private UnitSpawnerV2 spawner;
    [SerializeField] private TurnManagerV2 turnManager;
    [SerializeField] private BattleEndControllerV2 battleEndController;
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private BattlefieldAnalyzerV2 analyzer;

    [Header("UI - Summary")]
    [SerializeField] private TMP_Text minesText;
    [SerializeField] private TMP_Text turretsText;
    [SerializeField] private TMP_Text obstaclesText;
    [SerializeField] private TMP_Text enemiesText;

    [Header("UI - Selection")]
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private UnitSelectRowV2 rowPrefab;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button closeButton;

    [Header("Rules")]
    [Min(0)][SerializeField] private int maxTotalUnits = 4;

    [Header("Options")]
    [SerializeField] private List<UnitOption> options = new();

    private readonly List<UnitSelectRowV2> _rows = new();
    private BattlefieldAnalyzerV2.Summary _summary;
    private RunStateV2 _runState;

    private void Awake()
    {
        if (spawner == null) spawner = FindObjectOfType<UnitSpawnerV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();
        if (battleEndController == null) battleEndController = FindObjectOfType<BattleEndControllerV2>(true);
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (analyzer == null) analyzer = FindObjectOfType<BattlefieldAnalyzerV2>();

        _runState = RunStateV2.Instance;
        if (_runState == null) _runState = FindObjectOfType<RunStateV2>();

        if (startBattleButton != null)
        {
            startBattleButton.onClick.RemoveAllListeners();
            startBattleButton.onClick.AddListener(OnStartBattleClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }
    }

    private void OnEnable()
    {
        StartCoroutine(CoInitAndShow());
    }

    private IEnumerator CoInitAndShow()
    {
        for (int i = 0; i < options.Count; i++)
            options[i].selected = 0;

        // wait until battlefield spawned (enemies/obstacles).
        if (spawner != null)
        {
            bool gotSignal = false;
            void OnSpawned() { gotSignal = true; }
            spawner.OnBattlefieldSpawned += OnSpawned;

            yield return null;
            yield return new WaitForEndOfFrame();

            spawner.OnBattlefieldSpawned -= OnSpawned;
        }
        else
        {
            yield return null;
        }

        // wait grid ready (just in case).
        float timeout = 2.0f;
        while (timeout > 0f && (grid == null || !grid.IsReady))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // scan battlefield summary
        if (analyzer != null)
            _summary = analyzer.Scan(grid);
        else
            _summary = new BattlefieldAnalyzerV2.Summary();

        ApplyInventoryCountsFromRunState();
        RenderSummary();
        BuildRows();
        RefreshAll();
    }

    private void ApplyInventoryCountsFromRunState()
    {
        if (_runState == null || options == null) return;
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt == null || opt.prefab == null) continue;
            string id = !string.IsNullOrWhiteSpace(opt.unitId) ? opt.unitId : opt.prefab.name;
            opt.availableCount = Mathf.Max(0, _runState.GetUnitCount(id));
        }
    }

    private void RenderSummary()
    {
        if (minesText != null)
            minesText.text = $"mines: {_summary.minesLabel}";

        if (turretsText != null)
            turretsText.text = $"turrets: {_summary.turretsLabel}";

        if (obstaclesText != null)
            obstaclesText.text = $"obstacles: {_summary.obstaclesLabel}";

        if (enemiesText != null)
        {
            if (_summary.enemies == null || _summary.enemies.Count == 0)
            {
                enemiesText.text = "enemies: none";
            }
            else
            {
                var parts = new List<string>();
                for (int i = 0; i < _summary.enemies.Count; i++)
                {
                    var e = _summary.enemies[i];
                    string name = EnemyTypeToName(e.archetype);
                    parts.Add($"{name}: {e.label}");
                }
                enemiesText.text = "enemies: " + string.Join(", ", parts);
            }
        }
    }

    private string EnemyTypeToName(UnitV2.EnemyArchetype a)
    {
        return a switch
        {
            UnitV2.EnemyArchetype.OrkCruiser => "Orks",
            UnitV2.EnemyArchetype.ChaosRaider => "Chaos",
            UnitV2.EnemyArchetype.Bulldog => "Bulldogs",
            _ => "Other"
        };
    }

    private void BuildRows()
    {
        // fallback: if options empty, build from spawner prefabs (debug).
        if ((options == null || options.Count == 0) && spawner != null)
        {
            options = spawner.PlayerUnitPrefabs
                .Where(p => p != null)
                .Select(p => new UnitOption
                {
                    prefab = p,
                    displayName = p.name,
                    unitId = p.name,
                    cardSprite = null,
                    availableCount = _runState != null ? _runState.GetUnitCount(p.name) : 99,
                    maxPerBattle = 99,
                    selected = 0
                })
                .ToList();
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }
        _rows.Clear();

        if (rowsRoot == null || rowPrefab == null || options == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            var opt = options[idx];
            if (opt == null || opt.prefab == null) continue;

            var row = Instantiate(rowPrefab, rowsRoot);

            row.Bind(
                title: string.IsNullOrWhiteSpace(opt.displayName) ? opt.prefab.name : opt.displayName,
                cardSprite: opt.cardSprite,
                getCountText: () =>
                {
                    int cap = Mathf.Min(options[idx].availableCount, options[idx].maxPerBattle);
                    return $"{options[idx].selected} / {cap}";
                },
                getOwnedText: () => $"owned: {Mathf.Max(0, options[idx].availableCount)}",
                onPlus: () => TryChange(idx, +1),
                onMinus: () => TryChange(idx, -1)
            );

            _rows.Add(row);
        }
    }

    private void TryChange(int idx, int delta)
    {
        if (idx < 0 || idx >= options.Count) return;

        int total = GetTotalSelected();

        var opt = options[idx];
        int cap = Mathf.Min(opt.availableCount, opt.maxPerBattle);

        if (delta > 0)
        {
            if (maxTotalUnits > 0 && total >= maxTotalUnits) return;
            if (opt.selected >= cap) return;
            opt.selected++;
        }
        else if (delta < 0)
        {
            if (opt.selected <= 0) return;
            opt.selected--;
        }

        RefreshAll();
    }

    private int GetTotalSelected()
    {
        if (options == null) return 0;
        int t = 0;
        for (int i = 0; i < options.Count; i++)
            t += Mathf.Max(0, options[i].selected);
        return t;
    }

    private void RefreshAll()
    {
        int total = GetTotalSelected();

        if (totalText != null)
        {
            if (maxTotalUnits > 0)
                totalText.text = $"squad: {total} / {maxTotalUnits}";
            else
                totalText.text = $"squad: {total}";
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            int idx = i;
            if (_rows[i] != null)
            {
                _rows[i].Refresh(
                    getCountText: () =>
                    {
                        int cap = Mathf.Min(options[idx].availableCount, options[idx].maxPerBattle);
                        return $"{options[idx].selected} / {cap}";
                    },
                    getOwnedText: () => $"{Mathf.Max(0, options[idx].availableCount)}"
                );
            }
        }

        if (startBattleButton != null)
            startBattleButton.interactable = total > 0 && (maxTotalUnits <= 0 || total <= maxTotalUnits);
    }

    private void OnStartBattleClicked()
    {
        var selection = BuildSelectionRequests();
        if (selection.Count == 0) return;

        // Consume inventory on enter (requested behavior).
        // We'll return survivors back to inventory on battle end.
        ConsumeSelectedFromRunState();

        // Store selection in RunState for end-of-battle reconciliation.
        StoreSelectionInRunState(selection);

        if (spawner != null)
            spawner.SpawnPlayerFromSelection(selection);

        if (battleEndController != null)
            battleEndController.BeginTracking();

        if (turnManager != null && !turnManager.HasStarted)
            turnManager.BeginBattle();

        gameObject.SetActive(false);
    }

    private void ConsumeSelectedFromRunState()
    {
        if (_runState == null || options == null) return;
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt == null || opt.prefab == null) continue;
            int n = Mathf.Max(0, opt.selected);
            if (n <= 0) continue;
            string id = !string.IsNullOrWhiteSpace(opt.unitId) ? opt.unitId : opt.prefab.name;
            _runState.TryRemoveUnit(id, n);
        }
    }

    private List<UnitSpawnerV2.UnitSpawnRequest> BuildSelectionRequests()
    {
        var list = new List<UnitSpawnerV2.UnitSpawnRequest>();
        if (options == null) return list;

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt == null || opt.prefab == null) continue;

            int n = Mathf.Max(0, opt.selected);
            if (n <= 0) continue;

            string id = !string.IsNullOrWhiteSpace(opt.unitId) ? opt.unitId : opt.prefab.name;
            list.Add(new UnitSpawnerV2.UnitSpawnRequest
            {
                prefab = opt.prefab,
                unitId = id,
                count = n,
            });
        }

        return list;
    }

    private void StoreSelectionInRunState(List<UnitSpawnerV2.UnitSpawnRequest> selection)
    {
        if (_runState == null || selection == null) return;

        var dict = new Dictionary<string, int>(32);
        for (int i = 0; i < selection.Count; i++)
        {
            var s = selection[i];
            if (s.prefab == null) continue;
            if (string.IsNullOrWhiteSpace(s.unitId)) continue;
            int n = Mathf.Max(0, s.count);
            if (n <= 0) continue;

            if (dict.TryGetValue(s.unitId, out int cur))
                dict[s.unitId] = cur + n;
            else
                dict[s.unitId] = n;
        }

        _runState.SetLastBattleSelection(dict);
    }
}
