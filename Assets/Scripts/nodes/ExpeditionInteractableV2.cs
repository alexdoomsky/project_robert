using UnityEngine;

/// <summary>
/// Placeholder expedition interaction: opens a panel you can close.
/// </summary>
public sealed class ExpeditionInteractableV2 : InteractableEventV2
{
    [SerializeField] private ExpeditionPanelV2 panel;

    protected override void Awake()
    {
        base.Awake();
        if (panel == null) panel = FindFirstObjectByType<ExpeditionPanelV2>();
    }

    public override void Interact(PlayerInteractorV2 interactor)
    {
        if (panel != null) panel.Show();
        base.Interact(interactor);
    }
}
