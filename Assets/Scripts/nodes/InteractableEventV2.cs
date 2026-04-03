using UnityEngine;

/// <summary>
/// Base interactable placeholder.
///
/// For the demo it supports:
/// - prompt via PlayerInteractorV2
/// - optional completion tracking (progress X/Y)
/// - optional deactivation after interaction
///
/// Use subclasses for real logic (lore window, random event, combat trigger, etc.).
/// </summary>
[DisallowMultipleComponent]
public class InteractableEventV2 : MonoBehaviour, IInteractableV2
{
    [Header("UI")]
    [SerializeField] private string displayName = "Interactable";
    [SerializeField] private int priority = 0;

    [Tooltip("Optional marker (for kind/indicator filtering).")]
    [SerializeField] private WorldInteractableMarkerV2 marker;

    [Header("Completion")]
    [Tooltip("If true, interacting counts towards local-zone completion.")]
    [SerializeField] private bool countsTowardsCompletion = true;

    [Tooltip("If true, this marker will be removed from the nearest-interactable indicator after interaction.")]
    [SerializeField] private bool deactivateMarkerOnInteract = true;

    [Tooltip("If true, disables this object's trigger collider after interaction.")]
    [SerializeField] private bool disableTriggerOnInteract = true;

    [Tooltip("If true, deactivates the whole GameObject after interaction. This prevents stale UI prompts and removes it from tracking.")]
    [SerializeField] private bool deactivateGameObjectOnInteract = true;

    public string DisplayName => displayName;
    public int Priority => priority;
    public Transform Transform => transform;

    protected virtual void Awake()
    {
        if (marker == null)
        {
            marker = GetComponent<WorldInteractableMarkerV2>();
            if (marker == null) marker = GetComponentInChildren<WorldInteractableMarkerV2>();
            if (marker == null) marker = GetComponentInParent<WorldInteractableMarkerV2>();
        }
    }

    public virtual bool CanInteract(PlayerInteractorV2 interactor)
    {
        return true;
    }

    public virtual void Interact(PlayerInteractorV2 interactor)
    {
        // Placeholder behavior for now.
        Debug.Log($"[Interact] {DisplayName} ({gameObject.name})");

        if (countsTowardsCompletion)
        {
            var tracker = LocalZoneObjectiveTrackerV2.Instance;
            if (tracker != null && marker != null)
                tracker.MarkCompleted(marker);
        }

        if (deactivateMarkerOnInteract && marker != null)
            marker.SetActive(false);

        if (disableTriggerOnInteract)
        {
            var col = GetComponent<Collider>();
            if (col != null && col.isTrigger)
                col.enabled = false;
        }

        if (deactivateGameObjectOnInteract)
        {
            // Deactivate last so any logic above can run.
            gameObject.SetActive(false);
        }
    }
}
