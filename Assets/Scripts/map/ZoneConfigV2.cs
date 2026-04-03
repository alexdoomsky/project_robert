using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WeightedNodeTypeV2
{
    public NodeTypeV2 type;
    [Min(0)] public int weight;
}

[CreateAssetMenu(menuName = "V2/Exploration/Zone Config", fileName = "ZoneConfigV2_")]
public sealed class ZoneConfigV2 : ScriptableObject
{
    [Header("Identity")]
    public int zoneIndex;
    public string displayName = "Zone";

    [Header("Theme")]
    public GameObject themePrefab;

    [Header("Map Layout")]
    [Min(1)] public int minLayers = 3;
    [Min(1)] public int maxLayers = 4;
    [Min(1)] public int minNodesPerLayer = 1;
    [Min(1)] public int maxNodesPerLayer = 3;

    [Header("Node Type Weights")]
    public List<WeightedNodeTypeV2> nodeTypeWeights = new List<WeightedNodeTypeV2>
    {
        new WeightedNodeTypeV2 { type = NodeTypeV2.Planet, weight = 60 },
        new WeightedNodeTypeV2 { type = NodeTypeV2.Wreck, weight = 25 },
        new WeightedNodeTypeV2 { type = NodeTypeV2.Anomaly, weight = 15 },
    };

    [Header("Constraints")]
    public bool mustHaveAtLeastOnePlanet = true;
    [Range(0, 10)] public int maxPlanetsInZone = 2;

    [Header("Threat")]
    public ThreatLevelV2 baseThreat = ThreatLevelV2.Low;
}
