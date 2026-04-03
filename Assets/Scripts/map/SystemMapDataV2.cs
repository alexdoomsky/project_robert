using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ZoneRuntimeDataV2
{
    public int zoneIndex;
    public int startLayerIndex;
    public int layerCount;
}

[Serializable]
public sealed class NodeRuntimeDataV2
{
    public int id;
    public int zoneIndex;
    public int layerIndex;
    public int indexInLayer;

    public NodeTypeV2 nodeType;
    public ThreatLevelV2 threat;

    // UI/runtime placement for the map view.
    public Vector2 mapPosition;

    // Edges to nodes in the next layer only.
    public List<int> nextNodeIds = new List<int>();
}

[Serializable]
public sealed class SystemMapDataV2
{
    public int seed;
    public List<ZoneRuntimeDataV2> zones = new List<ZoneRuntimeDataV2>();
    public List<NodeRuntimeDataV2> nodes = new List<NodeRuntimeDataV2>();

    public NodeRuntimeDataV2 GetNodeById(int id)
    {
        // For simplicity we keep id == index.
        if (id < 0 || id >= nodes.Count) return null;
        return nodes[id];
    }
}
