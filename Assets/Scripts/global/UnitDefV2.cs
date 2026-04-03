using UnityEngine;

[CreateAssetMenu(menuName = "V2/Run/Unit Def", fileName = "UnitDefV2")]
public sealed class UnitDefV2 : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id. Keep stable across builds.")]
    public string id;

    public string displayName;

    [TextArea(2, 6)]
    public string description;

    public Sprite icon;

    [Header("Category")]
    public UnitCategoryV2 category = UnitCategoryV2.Combat;

    [Header("Craft")]
    [Min(0)]
    public int costMaterials = 0;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name;

        if (costMaterials < 0) costMaterials = 0;
    }
}
