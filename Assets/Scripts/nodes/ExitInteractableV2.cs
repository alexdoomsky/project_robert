using UnityEngine;

/// <summary>
/// Exit beacon interactable.
/// Always usable. Does NOT count towards completion.
///
/// The indicator will point to it only when all other objectives are completed
/// (handled by NearestInteractableIndicatorV2 + LocalZoneObjectiveTrackerV2).
/// </summary>
public sealed class ExitInteractableV2 : InteractableEventV2
{
    [Tooltip("Optional. If null, will find LocalZoneControllerV2 in scene.")]
    [SerializeField] private LocalZoneControllerV2 localZoneController;

    protected override void Awake()
    {
        base.Awake();
        if (localZoneController == null)
            localZoneController = FindFirstObjectByType<LocalZoneControllerV2>();
    }

    public override void Interact(PlayerInteractorV2 interactor)
    {
        // Do not call base.Interact: exit does not mark completion and should stay trackable/usable.
        if (localZoneController == null)
            localZoneController = FindFirstObjectByType<LocalZoneControllerV2>();

        if (localZoneController != null)
        {
            localZoneController.ExitLocalZone();
        }
        else
        {
            Debug.LogWarning("ExitInteractableV2: LocalZoneControllerV2 not found. Falling back to map.");
            var router = SceneRouterV2.Instance;
            if (router != null) router.LoadMap();
        }
    }
}
