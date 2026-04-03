using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns placeholder interactables inside a trigger volume when entering a Local Zone.
/// World-space 3D (top-down), Spore-like.
/// </summary>
public sealed class LocalZoneSpawnerV2 : MonoBehaviour
{
    public enum MarkerType
    {
        ResourceCluster = 0,
        LoreEvent = 1,
        RandomEvent = 2,
        Combat = 3,
        Expedition = 4,
        Exit = 5,
    }

    [Header("Config")]
    public LocalZoneSpawnConfigV2 spawnConfig;

    public BoxCollider spawnArea;

    [Header("No-spawn zones")]
    public BoxCollider[] noSpawnZones;

    public Transform spawnedRoot;

    [Header("Prefabs")]
    public GameObject resourceClusterPrefab;
    public GameObject loreEventPrefab;
    public GameObject randomEventPrefab;
    public GameObject combatPrefab;
    public GameObject expeditionPrefab;

    [Header("Exit")]
    public GameObject exitPrefab;
    [Min(0f)] public float exitMinDistanceFromOtherMarkers = 8f;

    [Header("Placement")]
    [Min(0f)] public float minDistanceBetweenMarkers = 3.0f;
    [Min(1)] public int placementAttemptsPerMarker = 20;

    private readonly List<Vector3> _usedPositions = new(64);

    private void Awake()
    {
        if (spawnArea == null)
            spawnArea = GetComponentInChildren<BoxCollider>(true);
    }

    private void Start()
    {
        SpawnAll();
    }

    public void SpawnAll()
    {
        _usedPositions.Clear();

        var run = RunStateV2.Instance;
        if (run == null || spawnConfig == null || spawnArea == null)
            return;

        NodeTypeV2 nodeType = Enum.TryParse(run.currentNodeType, true, out NodeTypeV2 nt)
            ? nt
            : NodeTypeV2.Planet;

        ThreatLevelV2 threat = Enum.TryParse(run.currentThreat, true, out ThreatLevelV2 th)
            ? th
            : ThreatLevelV2.Low;

        var profile = spawnConfig.Pick(run.currentZoneIndex, nodeType, threat);

        int seed = run.mapSeed != 0 ? run.mapSeed : run.EnsureMapSeed();
        int.TryParse(run.currentNodeId, out int nodeSalt);
        var rng = new System.Random(seed ^ nodeSalt);

        SpawnGroup(rng, MarkerType.ResourceCluster, resourceClusterPrefab, profile.resourceClusters);
        SpawnGroup(rng, MarkerType.LoreEvent, loreEventPrefab, profile.loreEvents);
        SpawnGroup(rng, MarkerType.RandomEvent, randomEventPrefab, profile.randomEvents);
        SpawnGroup(rng, MarkerType.Combat, combatPrefab, profile.combats);

        if (nodeType == NodeTypeV2.Planet && profile.spawnExpeditionOnPlanet)
            SpawnSingle(rng, MarkerType.Expedition, expeditionPrefab);

        SpawnSingle(rng, MarkerType.Exit, exitPrefab);
    }

    private void SpawnGroup(System.Random rng, MarkerType type, GameObject prefab, LocalZoneSpawnConfigV2.SpawnRange rule)
    {
        if (prefab == null) return;
        int count = rule.RollCount(rng);
        for (int i = 0; i < count; i++)
            SpawnSingle(rng, type, prefab);
    }

    private void SpawnSingle(System.Random rng, MarkerType type, GameObject prefab)
    {
        if (prefab == null) return;

        float minDist = type == MarkerType.Exit
            ? Mathf.Max(minDistanceBetweenMarkers, exitMinDistanceFromOtherMarkers)
            : minDistanceBetweenMarkers;

        if (!TryPickPosition(rng, minDist, out Vector3 pos))
            return;

        var go = Instantiate(prefab, pos, Quaternion.identity, spawnedRoot != null ? spawnedRoot : transform);
        go.name = $"{type}_{go.name}";

        var tag = go.GetComponent<LocalZoneMarkerTagV2>() ?? go.AddComponent<LocalZoneMarkerTagV2>();
        tag.markerType = type;

        var marker = go.GetComponent<WorldInteractableMarkerV2>() ?? go.AddComponent<WorldInteractableMarkerV2>();
        marker.SetKind(MapKind(type));

        // IMPORTANT: grab tracker at spawn-time (not Awake)
        var tracker = LocalZoneObjectiveTrackerV2.Instance;
        if (tracker != null && type != MarkerType.Exit)
            tracker.RegisterObjective(marker);

        _usedPositions.Add(pos);
    }

    private static InteractableKindV2 MapKind(MarkerType type) => type switch
    {
        MarkerType.ResourceCluster => InteractableKindV2.Resource,
        MarkerType.LoreEvent => InteractableKindV2.Lore,
        MarkerType.RandomEvent => InteractableKindV2.RandomEvent,
        MarkerType.Combat => InteractableKindV2.Combat,
        MarkerType.Expedition => InteractableKindV2.Expedition,
        MarkerType.Exit => InteractableKindV2.Exit,
        _ => InteractableKindV2.Resource,
    };

    private bool TryPickPosition(System.Random rng, float minDist, out Vector3 pos)
    {
        Bounds b = spawnArea.bounds;

        for (int i = 0; i < placementAttemptsPerMarker; i++)
        {
            float x = Mathf.Lerp(b.min.x, b.max.x, (float)rng.NextDouble());
            float z = Mathf.Lerp(b.min.z, b.max.z, (float)rng.NextDouble());
            Vector3 candidate = new(x, b.center.y, z);

            if (IsInsideNoSpawnZone(candidate)) continue;
            if (!IsFarEnough(candidate, minDist)) continue;

            pos = candidate;
            return true;
        }

        pos = default;
        return false;
    }

    private bool IsInsideNoSpawnZone(Vector3 p)
    {
        if (noSpawnZones == null) return false;
        foreach (var c in noSpawnZones)
            if (c != null && c.bounds.Contains(p))
                return true;
        return false;
    }

    private bool IsFarEnough(Vector3 p, float minDist)
    {
        float sqr = minDist * minDist;
        foreach (var u in _usedPositions)
            if ((p - u).sqrMagnitude < sqr)
                return false;
        return true;
    }
}

public sealed class LocalZoneMarkerTagV2 : MonoBehaviour
{
    public LocalZoneSpawnerV2.MarkerType markerType;
}
