using UnityEngine;

public interface IInteractableV2
{
    string DisplayName { get; }
    bool CanInteract(PlayerInteractorV2 interactor);
    void Interact(PlayerInteractorV2 interactor);
    Transform Transform { get; }
    int Priority { get; } // если хочешь выбирать “важнее” при наложении
}
