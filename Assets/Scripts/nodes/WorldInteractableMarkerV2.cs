using UnityEngine;

public enum InteractableKindV2
{
    Resource,
    Lore,
    RandomEvent,
    Combat,
    Expedition,
    Exit
}

public sealed class WorldInteractableMarkerV2 : MonoBehaviour
{
    [SerializeField] private InteractableKindV2 kind = InteractableKindV2.Resource;

    [Tooltip("If false, this marker will not be considered for indicator.")]
    [SerializeField] private bool isActive = true;

    [Tooltip("Optional: if > 0, can be used later to prefer certain kinds.")]
    [SerializeField] private int priority = 0;

    public InteractableKindV2 Kind => kind;
    public bool IsActive => isActive;
    public int Priority => priority;

    private void OnEnable()
    {
        InteractableRegistryV2.Register(this);
    }

    private void OnDisable()
    {
        InteractableRegistryV2.Unregister(this);
    }

    public void SetActive(bool active)
    {
        isActive = active;
    }

    public void SetKind(InteractableKindV2 newKind)
    {
        kind = newKind;
    }
}
