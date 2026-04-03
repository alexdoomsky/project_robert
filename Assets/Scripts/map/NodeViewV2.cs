using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI representation of a system-map node.
/// - Hitbox handled by Button
/// - Icon set from NodeTypeConfigV2
/// - Overlays show Selectable / Visited
/// </summary>
public sealed class NodeViewV2 : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button hitButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image overlaySelectable;
    [SerializeField] private Image overlayVisited;

    public int NodeId { get; private set; }
    public int LayerIndex { get; private set; }

    private Action<int> _onClick;

    /// <summary>
    /// One-call bind for convenience.
    /// </summary>
    public void Bind(int nodeId, int layerIndex, Sprite icon, Action<int> onClick)
    {
        NodeId = nodeId;
        LayerIndex = layerIndex;

        if (iconImage != null)
            iconImage.sprite = icon;

        _onClick = onClick;

        if (hitButton != null)
        {
            hitButton.onClick.RemoveAllListeners();
            hitButton.onClick.AddListener(() => _onClick?.Invoke(NodeId));
        }
    }

    public void SetState(NodeStateV2 state)
    {
        bool isSelectable = state == NodeStateV2.Selectable;
        if (hitButton != null)
            hitButton.interactable = isSelectable;

        SetOverlay(overlaySelectable, state == NodeStateV2.Selectable);
        SetOverlay(overlayVisited, state == NodeStateV2.Visited);
    }

    private static void SetOverlay(Image img, bool on)
    {
        if (img == null) return;

        // Ensure overlays don't block clicks.
        img.raycastTarget = false;

        var c = img.color;
        c.a = on ? 1f : 0f;
        img.color = c;
    }
}
