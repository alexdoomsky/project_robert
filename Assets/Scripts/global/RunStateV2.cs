using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent run/player state. Single source of truth.
/// Keep it dumb: data + simple rules (heal/craft/spend).
///
/// NOTE: This file intentionally contains some "glue" fields used to pass context between scenes
/// (map seed, selected node, last battle selection). It's still just data + small helper methods.
/// </summary>
public sealed class RunStateV2 : MonoBehaviour
{
    public static RunStateV2 Instance { get; private set; }

    public event Action OnChanged;

    [Header("Player")]
    [Min(1)] public int maxHp = 20;
    [Min(0)] public int currentHp = 20;

    [Header("Consumables")]
    [Min(0)] public int healCharges = 1;
    [Min(1)] public int healAmount = 5;

    [Header("Economy")]
    [Min(0)] public int materials = 0;
    [Min(0)] public int drones = 0;

    [Header("System Map")]
    [Tooltip("Seed used to generate the node map. 0 means 'not set yet'.")]
    public int mapSeed = 0;

    [Header("Selected Node (current)")]
    [Tooltip("Id of currently entered node. Used for LocalZone scene.")]
    public string currentNodeId;
    [Tooltip("Zone index of current node (0..2).")]
    public int currentZoneIndex = 0;
    [Tooltip("Type name of node (e.g. Planet/Event/Resource).")]
    public string currentNodeType;
    [Tooltip("Threat label or bucket (e.g. Low/Medium/High).")]
    public string currentThreat;

    [Header("System Map Progression")]
    [Tooltip("Id of the last visited node (numeric). 0 means none yet.")]
    public int lastVisitedNodeId = 0;

    [SerializeField] private List<int> _visitedNodeIds = new List<int>();
    private readonly HashSet<int> _visitedNodes = new HashSet<int>();

    [Header("Pending Combat")]
    [Tooltip("If set before loading Combat scene, spawners will use it to override battlefield contents.")]
    public BattleEncounterPresetV2 pendingEncounterPreset;

    // unitId -> count
    [SerializeField] private List<string> _unitIds = new List<string>();
    [SerializeField] private List<int> _unitCounts = new List<int>();
    private readonly Dictionary<string, int> _units = new Dictionary<string, int>(64, StringComparer.Ordinal);

    // Transient battle context (not serialized). We keep it in RunState because it survives scene loads.
    private Dictionary<string, int> _lastBattleSelection;

    [Header("Local Zone Progress (objectives)")]
    [Tooltip("Keys of completed interactables in local zones. Used to survive combat scene loads.")]
    [SerializeField] private List<string> _completedLocalObjectiveKeys = new List<string>();

    private readonly HashSet<string> _completedLocalObjectiveSet = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RebuildDictionaryFromSerializedLists();
        RebuildVisitedSet();
        RebuildLocalObjectiveSet();
        ClampBasics();
        RaiseChanged();
    }

    /// <summary>
    /// Ensure mapSeed is initialized. Call this before generating the map.
    /// </summary>
    public int EnsureMapSeed()
    {
        if (mapSeed != 0) return mapSeed;
        mapSeed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
        RaiseChanged();
        return mapSeed;
    }

    public void SetCurrentNode(string nodeId, int zoneIndex, string nodeType, string threat)
    {
        currentNodeId = nodeId;
        currentZoneIndex = zoneIndex;
        currentNodeType = nodeType;
        currentThreat = threat;
        RaiseChanged();
    }

    public void SetCurrentNode(SystemMapControllerV2.NodeData node)
    {
        if (node == null) return;
        SetCurrentNode(node.id.ToString(), node.zoneIndex, node.nodeType.ToString(), node.threat.ToString());
        lastVisitedNodeId = node.id;
    }

    public bool IsNodeVisited(int nodeId) => nodeId != 0 && _visitedNodes.Contains(nodeId);

    public void MarkNodeVisited(int nodeId)
    {
        if (nodeId == 0) return;
        if (_visitedNodes.Add(nodeId))
        {
            if (!_visitedNodeIds.Contains(nodeId)) _visitedNodeIds.Add(nodeId);
            lastVisitedNodeId = nodeId;
            RaiseChanged();
        }
    }

    public IReadOnlyCollection<int> GetVisitedNodes() => _visitedNodes;

    public void ClearPendingEncounterPreset()
    {
        pendingEncounterPreset = null;
    }

    private void RebuildVisitedSet()
    {
        _visitedNodes.Clear();
        if (_visitedNodeIds == null) return;
        for (int i = 0; i < _visitedNodeIds.Count; i++)
            _visitedNodes.Add(_visitedNodeIds[i]);
    }

    private void RebuildLocalObjectiveSet()
    {
        _completedLocalObjectiveSet.Clear();
        if (_completedLocalObjectiveKeys == null) return;
        for (int i = 0; i < _completedLocalObjectiveKeys.Count; i++)
        {
            var k = _completedLocalObjectiveKeys[i];
            if (!string.IsNullOrWhiteSpace(k))
                _completedLocalObjectiveSet.Add(k);
        }
    }

    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        if (healAmount < 1) healAmount = 1;
        if (currentHp < 0) currentHp = 0;
        if (currentHp > maxHp) currentHp = maxHp;
        if (materials < 0) materials = 0;
        if (drones < 0) drones = 0;
        if (healCharges < 0) healCharges = 0;
    }

    private void ClampBasics()
    {
        if (maxHp < 1) maxHp = 1;
        if (currentHp < 0) currentHp = 0;
        if (currentHp > maxHp) currentHp = maxHp;
        if (healCharges < 0) healCharges = 0;
        if (healAmount < 1) healAmount = 1;
        if (materials < 0) materials = 0;
        if (drones < 0) drones = 0;
    }

    private void RaiseChanged() => OnChanged?.Invoke();

    #region Public getters

    public int GetUnitCount(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId)) return 0;
        return _units.TryGetValue(unitId, out int c) ? c : 0;
    }

    public IReadOnlyDictionary<string, int> GetAllUnits() => _units;

    #endregion

    #region Run HP damage

    public void LoseRunHp(int amount)
    {
        if (amount <= 0) return;
        currentHp = Mathf.Max(0, currentHp - amount);
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
    }

    #endregion

    #region Healing

    public bool TryHeal()
    {
        if (healCharges <= 0) return false;
        if (currentHp >= maxHp) return false;

        healCharges -= 1;
        currentHp = Mathf.Min(maxHp, currentHp + healAmount);
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    public void AddHealCharges(int amount)
    {
        if (amount <= 0) return;
        healCharges += amount;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
    }

    #endregion

    #region Battle context

    /// <summary>
    /// Store selected units for the upcoming/active battle. Not persisted.
    /// </summary>
    public void SetLastBattleSelection(Dictionary<string, int> selection)
    {
        if (selection == null)
        {
            _lastBattleSelection = null;
            return;
        }

        _lastBattleSelection = new Dictionary<string, int>(selection, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, int> GetLastBattleSelection() => _lastBattleSelection;

    public void ClearLastBattleSelection() => _lastBattleSelection = null;

    #endregion

    #region Materials

    public void AddMaterials(int amount)
    {
        if (amount <= 0) return;
        materials += amount;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
    }

    public bool TrySpendMaterials(int amount)
    {
        if (amount <= 0) return true;
        if (materials < amount) return false;
        materials -= amount;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    #endregion

    #region Drones

    public bool TryCraftDrone(int costMaterials)
    {
        if (costMaterials < 0) costMaterials = 0;
        if (materials < costMaterials) return false;
        materials -= costMaterials;
        drones += 1;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    public void AddDrones(int amount)
    {
        if (amount <= 0) return;
        drones += amount;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
    }

    public bool TrySpendDrones(int amount)
    {
        if (amount <= 0) return true;
        if (drones < amount) return false;
        drones -= amount;
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    #endregion

    #region Units crafting

    public bool TryCraftUnit(UnitDefV2 def)
    {
        if (def == null) return false;
        if (def.category != UnitCategoryV2.Combat) return false;

        int cost = Mathf.Max(0, def.costMaterials);
        if (materials < cost) return false;

        materials -= cost;
        AddUnit(def.id, 1);
        ClampBasics();
        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    public void AddUnit(string unitId, int amount)
    {
        if (amount <= 0) return;
        if (string.IsNullOrWhiteSpace(unitId)) return;

        if (_units.TryGetValue(unitId, out int cur))
            _units[unitId] = cur + amount;
        else
            _units[unitId] = amount;

        SyncSerializedLists();
    }

    public bool TryRemoveUnit(string unitId, int amount)
    {
        if (amount <= 0) return true;
        if (string.IsNullOrWhiteSpace(unitId)) return false;

        if (!_units.TryGetValue(unitId, out int cur)) return false;
        if (cur < amount) return false;

        int next = cur - amount;
        if (next <= 0) _units.Remove(unitId);
        else _units[unitId] = next;

        SyncSerializedLists();
        RaiseChanged();
        return true;
    }

    #endregion

    #region Local Zone objective persistence

    /// <summary>
    /// Marks a local-zone interactable as completed. Key is derived from currentNodeId + marker identity.
    /// </summary>
    public void MarkLocalObjectiveCompleted(WorldInteractableMarkerV2 marker)
    {
        if (marker == null) return;
        string key = MakeLocalObjectiveKey(marker);
        if (string.IsNullOrWhiteSpace(key)) return;

        if (_completedLocalObjectiveSet.Add(key))
        {
            if (!_completedLocalObjectiveKeys.Contains(key))
                _completedLocalObjectiveKeys.Add(key);

            RaiseChanged();
        }
    }

    public bool IsLocalObjectiveCompleted(WorldInteractableMarkerV2 marker)
    {
        if (marker == null) return false;
        string key = MakeLocalObjectiveKey(marker);
        if (string.IsNullOrWhiteSpace(key)) return false;
        return _completedLocalObjectiveSet.Contains(key);
    }

    private string MakeLocalObjectiveKey(WorldInteractableMarkerV2 marker)
    {
        // If node id is missing, we can't safely persist this.
        if (string.IsNullOrWhiteSpace(currentNodeId))
            return null;

        // Use name + position as a stable-enough identity for a deterministic spawner.
        // (Prefab name includes (Clone) consistently; spawner also prefixes MarkerType_*)
        Vector3 p = marker.transform.position;
        int x = Mathf.RoundToInt(p.x * 100f);
        int z = Mathf.RoundToInt(p.z * 100f);

        return $"{currentNodeId}|{marker.gameObject.name}|{x}|{z}";
    }

    #endregion

    #region Serialization helpers

    private void RebuildDictionaryFromSerializedLists()
    {
        _units.Clear();

        int n = Mathf.Min(_unitIds.Count, _unitCounts.Count);
        for (int i = 0; i < n; i++)
        {
            string id = _unitIds[i];
            int c = _unitCounts[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (c <= 0) continue;

            if (_units.TryGetValue(id, out int cur))
                _units[id] = cur + c;
            else
                _units[id] = c;
        }

        SyncSerializedLists();
    }

    private void SyncSerializedLists()
    {
        _unitIds.Clear();
        _unitCounts.Clear();

        foreach (var kv in _units)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            if (kv.Value <= 0) continue;
            _unitIds.Add(kv.Key);
            _unitCounts.Add(kv.Value);
        }
    }

    #endregion
}
