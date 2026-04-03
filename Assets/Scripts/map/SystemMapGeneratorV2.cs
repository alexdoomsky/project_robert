using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemMapGeneratorV2 : MonoBehaviour
{
    [Serializable]
    public sealed class Node
    {
        public int id;
        public int zoneIndex;
        public int layerIndex;
        public int indexInLayer;
        public NodeTypeV2 nodeType;
        public ThreatLevelV2 threat;
        public Vector2 uiPos;
        public List<int> next = new List<int>();
    }

    [Serializable]
    public sealed class Zone
    {
        public int zoneIndex;
        public int startLayerIndex;
        public int layerCount;
        public GameObject themePrefab;
    }

    [Serializable]
    public sealed class Map
    {
        public int seed;
        public List<Zone> zones = new List<Zone>();
        public List<Node> nodes = new List<Node>();

        public Node GetNode(int id)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].id == id) return nodes[i];
            }
            return null;
        }
    }

    [Header("Configs")]
    public List<ZoneConfigV2> zoneConfigs = new List<ZoneConfigV2>();

    [Header("Layout")]
    [Min(10f)] public float layerSpacing = 220f; // horizontal distance
    [Min(10f)] public float laneSpacing = 140f;  // vertical distance
    [Tooltip("Max lanes used for positioning (visual only).")]
    [Range(2, 6)] public int maxLanes = 3;

    [Header("Debug")]
    public bool regenerateOnPlay = true;
    public int debugSeed = 12345;

    public Map CurrentMap { get; private set; }

    private System.Random _rng;

    private void Start()
    {
        if (regenerateOnPlay)
        {
            Generate(debugSeed);
        }
    }

    public Map Generate(int seed)
    {
        if (zoneConfigs == null || zoneConfigs.Count == 0)
        {
            Debug.LogError("SystemMapGeneratorV2: No zoneConfigs assigned.");
            return null;
        }

        _rng = new System.Random(seed);
        var map = new Map { seed = seed };

        int globalLayer = 0;
        int nodeId = 0;

        // For each zone, build layers and nodes.
        var nodesByLayer = new Dictionary<int, List<Node>>();

        for (int z = 0; z < zoneConfigs.Count; z++)
        {
            var cfg = zoneConfigs[z];
            int layerCount = NextIntInclusive(cfg.minLayers, cfg.maxLayers);

            var zone = new Zone
            {
                zoneIndex = cfg.zoneIndex,
                startLayerIndex = globalLayer,
                layerCount = layerCount,
                themePrefab = cfg.themePrefab
            };
            map.zones.Add(zone);

            // Track planet constraints per zone.
            int planetCount = 0;

            for (int li = 0; li < layerCount; li++)
            {
                int layerIndex = globalLayer + li;
                int nodeCount = NextIntInclusive(cfg.minNodesPerLayer, cfg.maxNodesPerLayer);
                nodeCount = Mathf.Clamp(nodeCount, 1, maxLanes);

                var layerNodes = new List<Node>(nodeCount);

                for (int ni = 0; ni < nodeCount; ni++)
                {
                    var type = PickNodeType(cfg, planetCount);
                    if (type == NodeTypeV2.Planet) planetCount++;

                    var threat = ComputeThreat(cfg.baseThreat, type);

                    var n = new Node
                    {
                        id = nodeId++,
                        zoneIndex = cfg.zoneIndex,
                        layerIndex = layerIndex,
                        indexInLayer = ni,
                        nodeType = type,
                        threat = threat,
                        uiPos = ComputeUiPos(layerIndex, ni, nodeCount)
                    };

                    layerNodes.Add(n);
                    map.nodes.Add(n);
                }

                nodesByLayer[layerIndex] = layerNodes;
            }

            // Enforce at least one planet per zone.
            if (cfg.mustHaveAtLeastOnePlanet)
            {
                bool hasPlanet = false;
                for (int i = 0; i < map.nodes.Count; i++)
                {
                    if (map.nodes[i].zoneIndex == cfg.zoneIndex && map.nodes[i].nodeType == NodeTypeV2.Planet)
                    {
                        hasPlanet = true;
                        break;
                    }
                }

                if (!hasPlanet)
                {
                    // Force a planet in a random layer/node in this zone.
                    int forcedLayer = zone.startLayerIndex + NextIntInclusive(0, zone.layerCount - 1);
                    var list = nodesByLayer[forcedLayer];
                    int pick = NextIntInclusive(0, list.Count - 1);
                    list[pick].nodeType = NodeTypeV2.Planet;
                    list[pick].threat = ComputeThreat(cfg.baseThreat, NodeTypeV2.Planet);
                }
            }

            // Enforce max planets per zone.
            if (cfg.maxPlanetsInZone >= 0)
            {
                var planets = new List<Node>();
                for (int i = 0; i < map.nodes.Count; i++)
                {
                    var n = map.nodes[i];
                    if (n.zoneIndex == cfg.zoneIndex && n.nodeType == NodeTypeV2.Planet)
                        planets.Add(n);
                }

                while (planets.Count > cfg.maxPlanetsInZone)
                {
                    // downgrade a random planet to another type
                    int idx = NextIntInclusive(0, planets.Count - 1);
                    var p = planets[idx];
                    var newType = PickNonPlanetType(cfg);
                    p.nodeType = newType;
                    p.threat = ComputeThreat(cfg.baseThreat, newType);
                    planets.RemoveAt(idx);
                }
            }

            globalLayer += layerCount;
        }

        // Add final node (single) at the end of the last zone.
        int finalLayer = globalLayer;
        var finalNode = new Node
        {
            id = nodeId++,
            zoneIndex = zoneConfigs[zoneConfigs.Count - 1].zoneIndex,
            layerIndex = finalLayer,
            indexInLayer = 0,
            nodeType = NodeTypeV2.Final,
            threat = ThreatLevelV2.High,
            uiPos = ComputeUiPos(finalLayer, 0, 1)
        };
        map.nodes.Add(finalNode);
        nodesByLayer[finalLayer] = new List<Node> { finalNode };

        // Connect layers within each zone + connect last zone to final.
        ConnectAllLayers(nodesByLayer);

        CurrentMap = map;
        return map;
    }

    private void ConnectAllLayers(Dictionary<int, List<Node>> nodesByLayer)
    {
        // Layers are consecutive from 0..max. Find max.
        int maxLayer = int.MinValue;
        foreach (var kv in nodesByLayer)
            maxLayer = Mathf.Max(maxLayer, kv.Key);

        for (int layer = 0; layer < maxLayer; layer++)
        {
            if (!nodesByLayer.TryGetValue(layer, out var fromLayer)) continue;
            if (!nodesByLayer.TryGetValue(layer + 1, out var toLayer)) continue;

            // First, ensure every "from" has at least one outgoing edge.
            for (int i = 0; i < fromLayer.Count; i++)
            {
                var from = fromLayer[i];
                int outCount = (toLayer.Count <= 1) ? 1 : NextIntInclusive(1, 2);
                for (int e = 0; e < outCount; e++)
                {
                    int targetIndex = PickTargetIndex(i, fromLayer.Count, toLayer.Count);
                    int targetId = toLayer[targetIndex].id;
                    if (!from.next.Contains(targetId)) from.next.Add(targetId);
                }
            }

            // Then, ensure every "to" has at least one incoming edge.
            var hasIncoming = new HashSet<int>();
            for (int i = 0; i < fromLayer.Count; i++)
            {
                foreach (var t in fromLayer[i].next)
                    hasIncoming.Add(t);
            }

            for (int ti = 0; ti < toLayer.Count; ti++)
            {
                int id = toLayer[ti].id;
                if (hasIncoming.Contains(id)) continue;

                // Attach this orphan to a random from.
                int fromIndex = NextIntInclusive(0, fromLayer.Count - 1);
                if (!fromLayer[fromIndex].next.Contains(id))
                    fromLayer[fromIndex].next.Add(id);
            }
        }
    }

    private Vector2 ComputeUiPos(int layerIndex, int indexInLayer, int countInLayer)
    {
        float x = layerIndex * layerSpacing;

        // Spread nodes vertically around 0.
        if (countInLayer <= 1) return new Vector2(x, 0f);

        float total = (countInLayer - 1) * laneSpacing;
        float y = (indexInLayer * laneSpacing) - (total * 0.5f);

        // Small jitter so it doesn't look like perfect graph paper.
        y += (float)(_rng.NextDouble() - 0.5) * (laneSpacing * 0.15f);

        return new Vector2(x, y);
    }

    private NodeTypeV2 PickNodeType(ZoneConfigV2 cfg, int currentPlanetCount)
    {
        // If we've reached max planets, disallow planets.
        bool allowPlanet = cfg.maxPlanetsInZone <= 0 || currentPlanetCount < cfg.maxPlanetsInZone;

        int totalWeight = 0;
        for (int i = 0; i < cfg.nodeTypeWeights.Count; i++)
        {
            var w = cfg.nodeTypeWeights[i];
            if (!allowPlanet && w.type == NodeTypeV2.Planet) continue;
            if (w.type == NodeTypeV2.Final) continue;
            totalWeight += Mathf.Max(0, w.weight);
        }

        if (totalWeight <= 0)
            return allowPlanet ? NodeTypeV2.Planet : NodeTypeV2.Wreck;

        int roll = NextIntInclusive(1, totalWeight);
        int acc = 0;

        for (int i = 0; i < cfg.nodeTypeWeights.Count; i++)
        {
            var w = cfg.nodeTypeWeights[i];
            if (!allowPlanet && w.type == NodeTypeV2.Planet) continue;
            if (w.type == NodeTypeV2.Final) continue;

            acc += Mathf.Max(0, w.weight);
            if (roll <= acc) return w.type;
        }

        return NodeTypeV2.Wreck;
    }

    private NodeTypeV2 PickNonPlanetType(ZoneConfigV2 cfg)
    {
        // Fallback order: Wreck -> Anomaly
        for (int i = 0; i < cfg.nodeTypeWeights.Count; i++)
        {
            if (cfg.nodeTypeWeights[i].type == NodeTypeV2.Wreck && cfg.nodeTypeWeights[i].weight > 0)
                return NodeTypeV2.Wreck;
        }
        for (int i = 0; i < cfg.nodeTypeWeights.Count; i++)
        {
            if (cfg.nodeTypeWeights[i].type == NodeTypeV2.Anomaly && cfg.nodeTypeWeights[i].weight > 0)
                return NodeTypeV2.Anomaly;
        }
        return NodeTypeV2.Wreck;
    }

    private ThreatLevelV2 ComputeThreat(ThreatLevelV2 baseThreat, NodeTypeV2 nodeType)
    {
        int t = (int)baseThreat;
        if (nodeType == NodeTypeV2.Anomaly) t += 1;
        if (nodeType == NodeTypeV2.Planet) t -= 1;
        t = Mathf.Clamp(t, (int)ThreatLevelV2.Low, (int)ThreatLevelV2.High);
        return (ThreatLevelV2)t;
    }

    private int PickTargetIndex(int fromIndex, int fromCount, int toCount)
    {
        if (toCount <= 1) return 0;

        // Bias toward keeping roughly parallel lanes (Slay the Spire feel).
        float normalized = (fromCount <= 1) ? 0.5f : (fromIndex / (float)(fromCount - 1));
        int center = Mathf.RoundToInt(normalized * (toCount - 1));

        int min = Mathf.Max(0, center - 1);
        int max = Mathf.Min(toCount - 1, center + 1);
        return NextIntInclusive(min, max);
    }

    private int NextIntInclusive(int min, int max)
    {
        if (max < min) (min, max) = (max, min);
        return _rng.Next(min, max + 1);
    }
}
