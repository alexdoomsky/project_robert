using UnityEngine;
using UnityEngine.Events;

public sealed class SystemMapFlowV2 : MonoBehaviour
{
    [SerializeField] private SystemMapControllerV2 mapController;

    private UnityAction<SystemMapControllerV2.NodeData> _listener;

    private void Awake()
    {
        if (mapController == null)
            mapController = FindObjectOfType<SystemMapControllerV2>();

        _listener = OnNodeSelected;
    }

    private void OnEnable()
    {
        if (mapController != null && mapController.onNodeSelected != null)
            mapController.onNodeSelected.AddListener(_listener);
    }

    private void OnDisable()
    {
        if (mapController != null && mapController.onNodeSelected != null)
            mapController.onNodeSelected.RemoveListener(_listener);
    }

    private void OnNodeSelected(SystemMapControllerV2.NodeData node)
    {
        if (mapController == null || mapController.map == null || node == null) return;

        // Update map progression
        var progress = MapProgressV2.Instance;
        if (progress != null)
            progress.OnNodeChosen(mapController.map, node.id);

        // Persist node info for local zone (existing logic)
        var state = RunStateV2.Instance;
        if (state != null)
            state.SetCurrentNode(node.id.ToString(),node.layerIndex,node.nodeType.ToString(),node.threat.ToString());

        var router = SceneRouterV2.Instance;
        if (router != null)
            router.LoadLocalZone();

        // When we come back to map scene, MapSceneBootstrap should re-render using MapProgressV2 state.
    }
}
