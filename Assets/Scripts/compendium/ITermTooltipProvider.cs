namespace Compendium
{
    // Adapter to YOUR existing tooltip system.
    public interface ITermTooltipProvider
    {
        void ShowTermTooltip(string tooltipKey);
        void HideTooltip();
    }
}
