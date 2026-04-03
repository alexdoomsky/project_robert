using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks node-map progression independent from combat/economy state.
///
/// Why separate from RunStateV2:
/// - avoids you constantly breaking combat code when we tweak map logic
/// - this is purely about what is selectable/hidden on the system map
///
/// Rules implemented:
/// 1) Strictly sequential: you can only click nodes that are directly connected from the current node.
/// 2) Visited nodes are hidden.
/// 3) If the current node had multiple outgoing connections (a split), once you pick one child,
///    all nodes on the other split branches become inactive until the first merge node (merge stays visible).
/// </summary>
public sealed class MapProgressV2 : MonoBehaviour
{
    public static MapProgressV2 Instance { get; private set; }

    [Header("Debug")]
    public bool debugLogs = false;

    // Visited node ids
    [SerializeField] private List<int> _visited = new List<int>();
    private readonly HashSet<int> _visitedSet = new HashSet<int>();

    // Current node id (-1 = before first move)
    public int CurrentNodeId { get; private set; } = -1;

    // Branch lock context
    private bool _branchLockActive;
    private int _branchLockStartLayer;
    private readonly HashSet<int> _branchForbidden = new HashSet<int>();
    private readonly HashSet<int> _branchMerge = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildVisitedSet();
    }

    private void RebuildVisitedSet()
    {
        _visitedSet.Clear();
        for (int i = 0; i < _visited.Count; i++)
            _visitedSet.Add(_visited[i]);
    }

    public bool IsVisited(int id) => _visitedSet.Contains(id);

    public void ResetProgress()
    {
        _visited.Clear();
        _visitedSet.Clear();
        CurrentNodeId = -1;
        ClearBranchLock();
    }

    public void MarkVisited(int id)
    {
        if (_visitedSet.Add(id))
            _visited.Add(id);
    }

    public IReadOnlyCollection<int> GetVisited() => _visited;

    public bool IsForbiddenByBranchLock(int nodeId) => _branchLockActive && _branchForbidden.Contains(nodeId);

    public bool IsMergeNode(int nodeId) => _branchLockActive && _branchMerge.Contains(nodeId);

    public void OnNodeChosen(SystemMapControllerV2.MapData map, int chosenNodeId)
    {
        if (map == null) return;

        var chosen = map.GetNode(chosenNodeId);
        if (chosen == null) return;

        // If we are entering a merge node, branch lock ends.
        if (_branchLockActive && _branchMerge.Contains(chosenNodeId))
        {
            if (debugLogs) Debug.Log($"[MapProgressV2] Reached merge node {chosenNodeId}. Clearing branch lock.");
            ClearBranchLock();
        }

        // Mark visited and advance.
        MarkVisited(chosenNodeId);

        int prevNodeId = CurrentNodeId;
        CurrentNodeId = chosenNodeId;

        // If we came from a split node, lock branch based on which child we picked.
        if (prevNodeId > 0)
        {
            var prev = map.GetNode(prevNodeId);
            if (prev != null && prev.nextNodeIds != null && prev.nextNodeIds.Count > 1)
            {
                // Split children
                var splitChildren = prev.nextNodeIds;
                // Only start lock if chosen node is one of the children
                bool isChild = false;
                for (int i = 0; i < splitChildren.Count; i++)
                    if (splitChildren[i] == chosenNodeId) { isChild = true; break; }

                if (isChild)
                {
                    StartBranchLock(map, prev, chosenNodeId);
                }
            }
        }
    }

    private void StartBranchLock(SystemMapControllerV2.MapData map, SystemMapControllerV2.NodeData splitNode, int chosenChildId)
    {
        ClearBranchLock();

        _branchLockActive = true;
        _branchLockStartLayer = splitNode.layerIndex;

        // Identify the "other" branch starts (other children of split)
        var otherStarts = new List<int>();
        for (int i = 0; i < splitNode.nextNodeIds.Count; i++)
        {
            int cid = splitNode.nextNodeIds[i];
            if (cid != chosenChildId) otherStarts.Add(cid);
        }

        // Compute descendants by layer for chosen branch and other branches.
        var chosenReach = CollectReachable(map, chosenChildId);
        var otherReach = new HashSet<int>();
        for (int i = 0; i < otherStarts.Count; i++)
        {
            var r = CollectReachable(map, otherStarts[i]);
            foreach (var id in r)
                otherReach.Add(id);
        }

        // Merge nodes are those reachable from both.
        _branchMerge.Clear();
        foreach (var id in chosenReach)
        {
            if (otherReach.Contains(id))
                _branchMerge.Add(id);
        }

        if (_branchMerge.Count == 0)
        {
            // No merge at all (rare). In that case lock all other branch nodes forever.
            _branchForbidden.Clear();
            foreach (var id in otherReach)
                _branchForbidden.Add(id);

            if (debugLogs) Debug.Log($"[MapProgressV2] Branch lock started at layer {_branchLockStartLayer}. No merge nodes. Forbidden={_branchForbidden.Count}");
            return;
        }

        // Find earliest merge layer index.
        int mergeLayer = int.MaxValue;
        foreach (var id in _branchMerge)
        {
            var n = map.GetNode(id);
            if (n != null)
                mergeLayer = Mathf.Min(mergeLayer, n.layerIndex);
        }

        // Forbidden: all nodes reachable from other branches with layer < mergeLayer.
        _branchForbidden.Clear();
        foreach (var id in otherReach)
        {
            var n = map.GetNode(id);
            if (n == null) continue;
            if (n.layerIndex < mergeLayer)
                _branchForbidden.Add(id);
        }

        // Also forbid the other children themselves (they are always before merge).
        for (int i = 0; i < otherStarts.Count; i++)
            _branchForbidden.Add(otherStarts[i]);

        if (debugLogs)
            Debug.Log($"[MapProgressV2] Branch lock started at layer {_branchLockStartLayer}. mergeLayer={mergeLayer} mergeNodes={_branchMerge.Count} forbidden={_branchForbidden.Count}");
    }

    private void ClearBranchLock()
    {
        _branchLockActive = false;
        _branchLockStartLayer = -1;
        _branchForbidden.Clear();
        _branchMerge.Clear();
    }

    private static HashSet<int> CollectReachable(SystemMapControllerV2.MapData map, int startId)
    {
        var visited = new HashSet<int>();
        var q = new Queue<int>();

        if (startId <= 0) return visited;

        visited.Add(startId);
        q.Enqueue(startId);

        while (q.Count > 0)
        {
            int id = q.Dequeue();
            var node = map.GetNode(id);
            if (node == null || node.nextNodeIds == null) continue;

            for (int i = 0; i < node.nextNodeIds.Count; i++)
            {
                int nx = node.nextNodeIds[i];
                if (nx <= 0) continue;
                if (visited.Add(nx))
                    q.Enqueue(nx);
            }
        }

        return visited;
    }

    /// <summary>
    /// Returns set of selectable nodes right now.
    /// - before first choice: all nodes on minimum layer
    /// - otherwise: only outgoing nodes of CurrentNodeId
    /// Applies branch lock (forbidden nodes are excluded, merge nodes allowed).
    /// Visited nodes excluded.
    /// </summary>
    public HashSet<int> GetSelectable(SystemMapControllerV2.MapData map)
    {
        var selectable = new HashSet<int>();
        if (map == null || map.nodes == null || map.nodes.Count == 0) return selectable;

        if (CurrentNodeId <= 0)
        {
            int minLayer = int.MaxValue;
            for (int i = 0; i < map.nodes.Count; i++)
                minLayer = Mathf.Min(minLayer, map.nodes[i].layerIndex);

            for (int i = 0; i < map.nodes.Count; i++)
            {
                var n = map.nodes[i];
                if (n.layerIndex != minLayer) continue;
                if (IsVisited(n.id)) continue;
                selectable.Add(n.id);
            }
            return selectable;
        }

        var cur = map.GetNode(CurrentNodeId);
        if (cur == null || cur.nextNodeIds == null) return selectable;

        for (int i = 0; i < cur.nextNodeIds.Count; i++)
        {
            int id = cur.nextNodeIds[i];
            if (id <= 0) continue;
            if (IsVisited(id)) continue;

            // Branch lock: forbid, but allow merge.
            if (IsForbiddenByBranchLock(id) && !IsMergeNode(id))
                continue;

            selectable.Add(id);
        }

        return selectable;
    }

    /// <summary>
    /// Should a node be hidden (inactive) on map UI.
    ///
    /// UX note: we now keep visited nodes visible and mark them with an overlay,
    /// so the only hard "hide" rule left is branch-lock pruning.
    /// </summary>
    public bool ShouldHideNode(SystemMapControllerV2.MapData map, SystemMapControllerV2.NodeData node)
    {
        if (node == null) return true;

        // Branch lock hides forbidden nodes (except merge nodes)
        if (_branchLockActive && _branchForbidden.Contains(node.id) && !_branchMerge.Contains(node.id))
            return true;

        return false;
    }
}
