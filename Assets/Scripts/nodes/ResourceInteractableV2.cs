using UnityEngine;

/// <summary>
/// Gives materials on interaction. Amount is rolled from [minMaterials, maxMaterials]. (inclusive)
/// </summary>
public sealed class ResourceInteractableV2 : InteractableEventV2
{
    [Header("Reward")]
    [Min(0)] public int minMaterials = 10;
    [Min(0)] public int maxMaterials = 25;

    public override void Interact(PlayerInteractorV2 interactor)
    {
        var run = RunStateV2.Instance;
        if (run != null)
        {
            int min = Mathf.Min(minMaterials, maxMaterials);
            int max = Mathf.Max(minMaterials, maxMaterials);
            int amount = Random.Range(min, max + 1);
            run.AddMaterials(amount);
        }

        base.Interact(interactor);
    }
}
