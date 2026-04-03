using UnityEngine;

/// <summary>
/// Simple tag component to link an in-battle UnitV2 instance with campaign inventory id.
/// Player units spawned from pre-battle selection get this tag.
/// </summary>
public sealed class CampaignUnitTagV2 : MonoBehaviour
{
    [Tooltip("Id used in RunStateV2 inventory.")]
    public string unitId;
}
