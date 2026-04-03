using System.Collections.Generic;

public static class InteractableRegistryV2
{
    private static readonly List<WorldInteractableMarkerV2> _items = new();

    public static IReadOnlyList<WorldInteractableMarkerV2> Items => _items;

    public static void Register(WorldInteractableMarkerV2 item)
    {
        if (item == null) return;
        if (_items.Contains(item)) return;
        _items.Add(item);
    }

    public static void Unregister(WorldInteractableMarkerV2 item)
    {
        if (item == null) return;
        _items.Remove(item);
    }
}
