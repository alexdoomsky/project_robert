using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generates a 3-zone horizontal Slay-the-Spire style node map.
/// - 3 zones
/// - 3-4 layers per zone
/// - 1..3 nodes per layer
/// - Each zone: at least 1 Planet, at most 2 Planets (per your rules)
/// - Final node: single node at the end of Zone 3
///
/// This script is intentionally "data-driven": tune zones via ZoneConfigV2 assets.
/// Rendering is done by SystemMapViewV2.
/// </summary>
public sealed class SystemMapControllerV2 : MonoBehaviour
{
    [Serializable]
    public sealed class NodeData
    {
        public int id;
        public int zoneIndex;
        public int layerIndex;
        public int indexInLayer;
        public NodeTypeV2 nodeType;
        public ThreatLevelV2 threat;
        public Vector2 uiPosition;
        public List<int> nextNodeIds = new List<int>();
    }

    [Serializable]
    public sealed class MapData
    {
        public int seed;
        public List<int> zoneStartLayer = new List<int>();
        public List<int> zoneLayerCount = new List<int>();
        public List<NodeData> nodes = new List<NodeData>();

        public NodeData GetNode(int id)
        {
            // Nodes are stored by id order, but don't assume contiguous.
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == id) return nodes[i];
            return null;
        }
    }

    [Header("Configs")]
    [Tooltip("Exactly 3 zone configs (zoneIndex 0..2).")]
    public List<ZoneConfigV2> zones = new List<ZoneConfigV2>();

    [Header("Presentation")]
    public List<NodeTypeConfigV2> nodeTypeConfigs = new List<NodeTypeConfigV2>();

    [Header("Layout")]
    public float layerSpacing = 320f;   // horizontal distance between layers
    public float laneSpacing = 140f;    // vertical distance between nodes within a layer
    public float zoneGap = 220f;        // extra horizontal space between zones
    public float xJitter = 20f;
    public float yJitter = 18f;

    [Header("Events")]
    public UnityEvent<NodeData> onNodeSelected;

    [NonSerialized] public MapData map;

    private readonly Dictionary<NodeTypeV2, NodeTypeConfigV2> _typeCfg = new Dictionary<NodeTypeV2, NodeTypeConfigV2>();

    private void Awake()
    {
        _typeCfg.Clear();
        for (int i = 0; i < nodeTypeConfigs.Count; i++)
        {
            var cfg = nodeTypeConfigs[i];
            if (cfg == null) continue;
            _typeCfg[cfg.nodeType] = cfg;
        }
    }

    public NodeTypeConfigV2 GetNodeTypeConfig(NodeTypeV2 type)
    {
        _typeCfg.TryGetValue(type, out var cfg);
        return cfg;
    }

    public void GenerateNewMap(int seed)
    {
        if (zones == null || zones.Count != 3)
            throw new InvalidOperationException("SystemMapControllerV2 requires exactly 3 ZoneConfigV2 assets.");

        var rng = new System.Random(seed);
        map = new MapData { seed = seed };
        map.zoneStartLayer.Clear();
        map.zoneLayerCount.Clear();
        map.nodes.Clear();

        int globalLayerIndex = 0;
        int nextId = 1;
        float xCursor = 0f;

        // Create per-zone layers with node counts.
        var layers = new List<List<NodeData>>();

        for (int z = 0; z < 3; z++)
        {
            ZoneConfigV2 zc = zones[z];
            if (zc == null) throw new InvalidOperationException($"ZoneConfig at index {z} is null.");

            int layerCount = rng.Next(zc.minLayers, zc.maxLayers + 1);
            map.zoneStartLayer.Add(globalLayerIndex);
            map.zoneLayerCount.Add(layerCount);

            int planetsInZone = 0;

            // Pre-pick node counts per layer.
            for (int li = 0; li < layerCount; li++)
            {
                int nodesInLayer = rng.Next(zc.minNodesPerLayer, zc.maxNodesPerLayer + 1);

                // Make ends converge a bit: first and last layer biased smaller.
                if (li == 0) nodesInLayer = Mathf.Min(nodesInLayer, 2);
                if (li == layerCount - 1) nodesInLayer = Mathf.Min(nodesInLayer, 2);

                var layer = new List<NodeData>(nodesInLayer);
                layers.Add(layer);

                for (int ni = 0; ni < nodesInLayer; ni++)
                {
                    var node = new NodeData
                    {
                        id = nextId++,
                        zoneIndex = z,
                        layerIndex = globalLayerIndex,
                        indexInLayer = ni,
                        nodeType = PickNodeTypeWithConstraints(zc, rng, ref planetsInZone),
                        threat = ApplyThreatRules(zc, rng),
                    };

                    // If we reached max planets, ensure future picks avoid planets.
                    if (node.nodeType == NodeTypeV2.Planet) planetsInZone++;

                    node.uiPosition = ComputeNodePosition(rng, xCursor, layerSpacing, laneSpacing, nodesInLayer, ni);
                    layer.Add(node);
                    map.nodes.Add(node);
                }

                // Ensure at least 1 planet in zone (your rule) by forcing one in a random layer.
                if (li == layerCount - 1 && zc.mustHaveAtLeastOnePlanet && planetsInZone == 0)
                {
                    // force planet in this last layer on a random node
                    int idx = rng.Next(0, layer.Count);
                    layer[idx].nodeType = NodeTypeV2.Planet;
                }

                globalLayerIndex++;
                xCursor += layerSpacing;
            }

            xCursor += zoneGap; // gap before next zone starts
        }

        // Final node: single node at the end of zone 3.
        var finalLayer = new List<NodeData>(1);
        layers.Add(finalLayer);
        var finalNode = new NodeData
        {
            id = nextId++,
            zoneIndex = 2,
            layerIndex = globalLayerIndex,
            indexInLayer = 0,
            nodeType = NodeTypeV2.Final,
            threat = ThreatLevelV2.High,
            uiPosition = new Vector2(xCursor, 0f) + RandomJitter(rng),
        };
        finalLayer.Add(finalNode);
        map.nodes.Add(finalNode);

        // Build connections: each layer connects to next layer.
        for (int l = 0; l < layers.Count - 1; l++)
        {
            var a = layers[l];
            var b = layers[l + 1];
            ConnectLayers(rng, a, b);
        }

        // Notify view (if any) to render.
        var view = GetComponent<SystemMapViewV2>();
        if (view != null) view.Render(this, map);
    }

    public void SelectNode(int nodeId)
    {
        if (map == null) return;
        var node = map.GetNode(nodeId);
        if (node == null) return;
        onNodeSelected?.Invoke(node);
    }

    private NodeTypeV2 PickNodeTypeWithConstraints(ZoneConfigV2 zc, System.Random rng, ref int planetsInZone)
    {
        // Enforce max 2 planets.
        NodeTypeV2 picked = WeightedPick(zc.nodeTypeWeights, rng);
        if (picked == NodeTypeV2.Planet && planetsInZone >= zc.maxPlanetsInZone)
        {
            // Re-pick without planets.
            int guard = 0;
            while (picked == NodeTypeV2.Planet && guard++ < 50)
                picked = WeightedPick(zc.nodeTypeWeights, rng);
            if (picked == NodeTypeV2.Planet) picked = NodeTypeV2.Wreck;
        }
        return picked;
    }

    private ThreatLevelV2 ApplyThreatRules(ZoneConfigV2 zc, System.Random rng)
    {
        // Simple: base threat from zone, can be tuned later.
        // You said you'll tune via chances, so keep it predictable.
        return zc.baseThreat;
    }

    private static NodeTypeV2 WeightedPick(List<WeightedNodeTypeV2> weights, System.Random rng)
    {
        int total = 0;
        for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0, weights[i].weight);
        if (total <= 0) return NodeTypeV2.Planet;

        int r = rng.Next(0, total);
        int acc = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            acc += Mathf.Max(0, weights[i].weight);
            if (r < acc) return weights[i].type;
        }
        return weights[weights.Count - 1].type;
    }

    private Vector2 ComputeNodePosition(System.Random rng, float xCursor, float layerSpacing, float laneSpacing, int nodesInLayer, int indexInLayer)
    {
        float x = xCursor + RandomRange(rng, -xJitter, xJitter);

        // Center lanes around 0
        float centerOffset = (nodesInLayer - 1) * 0.5f;
        float y = (indexInLayer - centerOffset) * laneSpacing + RandomRange(rng, -yJitter, yJitter);

        return new Vector2(x, y);
    }

    private static void ConnectLayers(System.Random rng, List<NodeData> from, List<NodeData> to)
    {
        // Basic STS-style connection:
        // - Every node in 'from' connects to 1..2 nodes in 'to'
        // - Ensure every node in 'to' has at least one incoming connection

        // First pass: give each 'from' at least 1 connection
        for (int i = 0; i < from.Count; i++)
        {
            int t0 = ClampIndex((int)System.Math.Round(i * (to.Count - 1) / System.Math.Max(1.0, from.Count - 1)), 0, to.Count - 1);
            from[i].nextNodeIds.Add(to[t0].id);

            // Chance for second connection to neighboring node
            if (to.Count > 1 && rng.NextDouble() < 0.45)
            {
                int dir = rng.Next(0, 2) == 0 ? -1 : 1;
                int t1 = ClampIndex(t0 + dir, 0, to.Count - 1);
                if (t1 != t0) from[i].nextNodeIds.Add(to[t1].id);
            }
        }

        // Ensure every 'to' has at least one incoming
        var incoming = new Dictionary<int, int>();
        for (int j = 0; j < to.Count; j++) incoming[to[j].id] = 0;
        for (int i = 0; i < from.Count; i++)
            for (int k = 0; k < from[i].nextNodeIds.Count; k++)
                incoming[from[i].nextNodeIds[k]]++;

        for (int j = 0; j < to.Count; j++)
        {
            int id = to[j].id;
            if (incoming[id] > 0) continue;

            // Attach it to a random 'from' node
            int fi = rng.Next(0, from.Count);
            from[fi].nextNodeIds.Add(id);
            incoming[id] = 1;
        }

        // De-duplicate connections
        for (int i = 0; i < from.Count; i++)
        {
            var list = from[i].nextNodeIds;
            for (int a = 0; a < list.Count; a++)
            {
                for (int b = list.Count - 1; b > a; b--)
                {
                    if (list[b] == list[a]) list.RemoveAt(b);
                }
            }
        }
    }

    private static int ClampIndex(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    private static Vector2 RandomJitter(System.Random rng)
    {
        float jx = RandomRange(rng, -14f, 14f);
        float jy = RandomRange(rng, -10f, 10f);
        return new Vector2(jx, jy);
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        double t = rng.NextDouble();
        return (float)(min + (max - min) * t);
    }
}
