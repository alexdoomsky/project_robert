using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI card for a unit or drones.
/// Uses UnitDefV2 and RunStateV2.
/// </summary>
public sealed class UnitCardViewV2 : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text countText;
    public TMP_Text costText;
    public Button craftButton;

    private UnitDefV2 _def;
    private bool _isDroneCard;
    private RunStateV2 _state;

    public void Bind(UnitDefV2 def, RunStateV2 state)
    {
        _def = def;
        _state = state;
        _isDroneCard = def != null && def.category == UnitCategoryV2.Drone;

        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(OnCraftClicked);
        }

        Refresh();
    }

    private void OnEnable()
    {
        if (_state != null)
            _state.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_state != null)
            _state.OnChanged -= Refresh;
    }

    private void OnCraftClicked()
    {
        if (_state == null || _def == null) return;

        if (_isDroneCard)
        {
            _state.TryCraftDrone(_def.costMaterials);
        }
        else
        {
            _state.TryCraftUnit(_def);
        }
    }

    public void Refresh()
    {
        if (_def == null) return;

        if (icon != null) icon.sprite = _def.icon;
        if (titleText != null) titleText.text = string.IsNullOrEmpty(_def.displayName) ? _def.name : _def.displayName;
        if (descriptionText != null) descriptionText.text = _def.description;

        if (costText != null)
            costText.text = $"{_def.costMaterials}";

        if (_state != null)
        {
            int count = _isDroneCard ? _state.drones : _state.GetUnitCount(_def.id);
            if (countText != null) countText.text = $"{count}";

            if (craftButton != null)
                craftButton.interactable = _state.materials >= Mathf.Max(0, _def.costMaterials);
        }
    }
}
