using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemMapViewV2 : MonoBehaviour
{
    [Header("Roots (UI)")]
    public RectTransform nodesRoot;
    public RectTransform linesRoot;

    [Header("Prefabs")]
    public NodeViewV2 nodePrefab;

    [Tooltip("UI prefab with RectTransform + Image. Image should be Tiled (dashed sprite).")]
    public Image dashedLinePrefab;

    [Header("Line Visuals")]
    [Tooltip("Thickness in UI pixels.")]
    public float lineThickness = 6f;

    [Tooltip("If true, lines will be rendered behind nodes by forcing sibling index.")]
    public bool linesBehindNodes = true;

    private readonly List<NodeViewV2> _spawnedNodes = new List<NodeViewV2>();
    private readonly List<Image> _spawnedLines = new List<Image>();

    public void Render(SystemMapControllerV2 controller, SystemMapControllerV2.MapData map)
    {
        Clear();
        if (controller == null || map == null) return;

        if (nodesRoot == null) nodesRoot = transform as RectTransform;
        if (linesRoot == null) linesRoot = transform as RectTransform;

        var progress = MapProgressV2.Instance;
        var selectable = progress != null ? progress.GetSelectable(map) : new HashSet<int>();

        // 1) Spawn nodes
        for (int i = 0; i < map.nodes.Count; i++)
        {
            var nd = map.nodes[i];
            if (nodePrefab == null) break;

            bool hide = progress != null && progress.ShouldHideNode(map, nd);
            if (hide) continue;

            var node = Instantiate(nodePrefab, nodesRoot);
            node.name = $"Node_{nd.id}_{nd.nodeType}";

            // position
            if (node.transform is RectTransform rt)
                rt.anchoredPosition = nd.uiPosition;
            else
                node.transform.localPosition = new Vector3(nd.uiPosition.x, nd.uiPosition.y, 0f);

            // icon
            Sprite icon = null;
            var typeCfg = controller.GetNodeTypeConfig(nd.nodeType);
            if (typeCfg != null) icon = typeCfg.icon;

            // bind click + icon
            node.Bind(nd.id, nd.layerIndex, icon, controller.SelectNode);

            // state
            NodeStateV2 state;
            if (progress != null && progress.IsVisited(nd.id))
                state = NodeStateV2.Visited;
            else if (selectable.Contains(nd.id))
                state = NodeStateV2.Selectable;
            else
                state = NodeStateV2.Locked;

            node.SetState(state);

            _spawnedNodes.Add(node);
        }

        // 2) Lines (only between visible nodes)
        if (dashedLinePrefab == null) return;

        // visible lookup
        var visiblePos = new Dictionary<int, Vector2>(_spawnedNodes.Count);
        for (int i = 0; i < _spawnedNodes.Count; i++)
        {
            var nv = _spawnedNodes[i];
            var nrt = nv.transform as RectTransform;
            visiblePos[nv.NodeId] = nrt != null
                ? nrt.anchoredPosition
                : new Vector2(nv.transform.localPosition.x, nv.transform.localPosition.y);
        }

        for (int i = 0; i < map.nodes.Count; i++)
        {
            var from = map.nodes[i];
            if (!visiblePos.ContainsKey(from.id)) continue;

            for (int k = 0; k < from.nextNodeIds.Count; k++)
            {
                int toId = from.nextNodeIds[k];
                if (!visiblePos.ContainsKey(toId)) continue;

                Vector2 a = visiblePos[from.id];
                Vector2 b = visiblePos[toId];

                var img = Instantiate(dashedLinePrefab, linesRoot);
                img.name = $"Link_{from.id}_to_{toId}";

                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 mid = (a + b) * 0.5f;
                Vector2 dir = (b - a);
                float length = dir.magnitude;

                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(length, Mathf.Max(1f, lineThickness));

                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);

                if (linesBehindNodes)
                    rt.SetAsFirstSibling();

                _spawnedLines.Add(img);
            }
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _spawnedNodes.Count; i++)
            if (_spawnedNodes[i] != null) Destroy(_spawnedNodes[i].gameObject);
        _spawnedNodes.Clear();

        for (int i = 0; i < _spawnedLines.Count; i++)
            if (_spawnedLines[i] != null) Destroy(_spawnedLines[i].gameObject);
        _spawnedLines.Clear();
    }
}
