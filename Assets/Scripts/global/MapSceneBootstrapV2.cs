using UnityEngine;

/// <summary>
/// Attach to the map scene root.
/// On scene open: ensures RunState has a seed and triggers SystemMapControllerV2.GenerateNewMap(seed).
/// </summary>
public sealed class MapSceneBootstrapV2 : MonoBehaviour
{
    [Header("Refs")]
    public SystemMapControllerV2 mapController;

    [Header("Seed")]
    [Tooltip("If true, always regenerate a new seed when entering the map scene (not recommended for campaign).")]
    public bool forceNewSeedOnEnter = false;

    private void Start()
    {
        if (mapController == null)
            mapController = FindObjectOfType<SystemMapControllerV2>();

        var run = RunStateV2.Instance;
        if (run == null) run = FindObjectOfType<RunStateV2>();

        if (run == null || mapController == null)
            return;

        if (forceNewSeedOnEnter)
            run.mapSeed = 0;

        int seed = run.EnsureMapSeed();
        mapController.GenerateNewMap(seed);
    }
}
