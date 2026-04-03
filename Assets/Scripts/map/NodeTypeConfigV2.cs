using UnityEngine;

[CreateAssetMenu(menuName = "V2/Exploration/Node Type Config", fileName = "NodeTypeConfigV2_")]
public sealed class NodeTypeConfigV2 : ScriptableObject
{
    public NodeTypeV2 nodeType;

    [Header("Presentation")]
    public Sprite icon;

    [TextArea(2, 6)]
    public string description;
}
