using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AbilityPanelV2 : MonoBehaviour
{
    public static AbilityPanelV2 Instance { get; private set; }

    [Header("Buttons")]
    public Button dashButton;
    public Button specialButton;

    [Header("Text")]
    public TMP_Text dashLabel;
    public TMP_Text specialLabel;

    [Header("Tooltip / Description Panel (optional)")]
    public GameObject tooltipRoot;
    public TMP_Text tooltipTitle;
    public TMP_Text tooltipBody;

    [Header("Tooltip value texts (numbers only)")]
    public TMP_Text tooltipEnergyValue;
    public TMP_Text tooltipCooldownValue;
    public TMP_Text tooltipRangeValue;

    [Header("Tooltip blocks (to hide layout slots)")]
    public GameObject energyBlock;
    public GameObject cooldownBlock;
    public GameObject rangeBlock;

    [Header("Auto refresh")]
    [SerializeField] private float refreshInterval = 0.1f;

    private UnitV2 _currentUnit;
    private float _nextRefreshTime;

    private void Awake()
    {
        Instance = this;
        SetupHover(dashButton, isDash: true);
        SetupHover(specialButton, isDash: false);
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void Update()
    {
        if (refreshInterval > 0f && Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + refreshInterval;

        var ctrl = UnitMovementControllerV2.Instance;
        UnitV2 selected = ctrl != null ? ctrl.SelectedUnit : null;

        if (!ReferenceEquals(selected, _currentUnit))
        {
            Refresh(selected);
            HideTooltip();
            return;
        }

        if (_currentUnit != null)
            Refresh(_currentUnit);
    }

    public void Refresh(UnitV2 unit)
    {
        _currentUnit = unit;

        if (unit == null)
        {
            SetButtonVisibleAndState(dashButton, dashLabel, null, false);
            SetButtonVisibleAndState(specialButton, specialLabel, null, false);
            return;
        }

        // Dash
        var dash = unit.DashAbility;
        if (dash == null)
        {
            // нет абилки -> кнопки нет
            SetButtonVisibleAndState(dashButton, dashLabel, null, false);
        }
        else
        {
            bool usable = unit.CanUseAbility(dash);
            SetButtonVisibleAndState(dashButton, dashLabel, dash, usable);
        }

        // Special
        var spec = unit.SpecialAbility;
        if (spec == null)
        {
            SetButtonVisibleAndState(specialButton, specialLabel, null, false);
        }
        else
        {
            bool usable = unit.CanUseAbility(spec);
            SetButtonVisibleAndState(specialButton, specialLabel, spec, usable);
        }
    }

    /// <summary>
    /// Если ability == null -> полностью прячем кнопку.
    /// Если ability != null -> показываем кнопку, меняем текст, interactable зависит от usable.
    /// </summary>
    private void SetButtonVisibleAndState(Button btn, TMP_Text label, AbilityDefV2 ability, bool usable)
    {
        if (btn == null) return;

        bool visible = ability != null;
        btn.gameObject.SetActive(visible);

        if (!visible)
            return;

        btn.interactable = usable;

        if (label != null)
            label.text = SafeAbilityName(ability);
    }

    public void OnDashPressed()
    {
        if (_currentUnit == null) return;
        var ab = _currentUnit.DashAbility;
        if (ab == null) return;
        UnitMovementControllerV2.Instance.RequestUseAbility(ab);
    }

    public void OnSpecialPressed()
    {
        if (_currentUnit == null) return;
        var ab = _currentUnit.SpecialAbility;
        if (ab == null) return;
        UnitMovementControllerV2.Instance.RequestUseAbility(ab);
    }

    // =========================
    // Hover / Tooltip
    // =========================

    private void SetupHover(Button btn, bool isDash)
    {
        if (btn == null) return;

        var trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null) trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            if (_currentUnit == null) { HideTooltip(); return; }

            AbilityDefV2 ab = isDash ? _currentUnit.DashAbility : _currentUnit.SpecialAbility;
            if (ab == null) { HideTooltip(); return; } // кнопка может быть скрыта, но на всякий

            bool usable = _currentUnit.CanUseAbility(ab);
            ShowTooltip(ab, usable);
        });

        AddTrigger(trigger, EventTriggerType.PointerExit, _ => HideTooltip());
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action?.Invoke(data));
        trigger.triggers.Add(entry);
    }

    private void ShowTooltip(AbilityDefV2 ability, bool usable)
    {
        if (tooltipRoot == null) return;

        tooltipRoot.SetActive(true);

        if (tooltipTitle != null)
            tooltipTitle.text = SafeAbilityName(ability) + (usable ? "" : " (недоступно)");

        string desc =
            ReadStringMember(ability, "description") ??
            ReadStringMember(ability, "desc") ??
            ReadStringMember(ability, "tooltip") ??
            "";

        if (tooltipBody != null)
            tooltipBody.text = desc;

        // Energy cost (hide if <=0 or missing)
        int? energy =
            ReadIntMember(ability, "energyCost") ??
            ReadIntMember(ability, "EnergyCost") ??
            ReadIntMember(ability, "costEnergy") ??
            ReadIntMember(ability, "manaCost");

        bool showEnergy = energy.HasValue && energy.Value > 0;
        SetBlockVisible(energyBlock, tooltipEnergyValue, showEnergy, showEnergy ? energy.Value.ToString() : "");

        // Cooldown (hide if <=0 or missing)
        int? cd =
            ReadIntMember(ability, "cooldownRounds") ??
            ReadIntMember(ability, "cooldown") ??
            ReadIntMember(ability, "CooldownRounds");

        bool showCd = cd.HasValue && cd.Value > 0;
        SetBlockVisible(cooldownBlock, tooltipCooldownValue, showCd, showCd ? cd.Value.ToString() : "");

        // Range (hide when target=self OR no range values)
        int? minR =
            ReadIntMember(ability, "minRange") ??
            ReadIntMember(ability, "MinRange");
        int? maxR =
            ReadIntMember(ability, "maxRange") ??
            ReadIntMember(ability, "MaxRange");

        bool isSelf = false;
        try { isSelf = ability.targetMode == AbilityTargetModeV2.Self; }
        catch { isSelf = false; }

        bool hasAnyRange = (minR.HasValue && minR.Value > 0) || (maxR.HasValue && maxR.Value > 0);
        bool showRange = !isSelf && hasAnyRange;

        string rangeText = "";
        if (showRange)
        {
            int a = Mathf.Max(0, minR ?? 0);
            int b = Mathf.Max(0, maxR ?? 0);

            if (a <= 0 && b > 0) rangeText = b.ToString();
            else rangeText = $"{a}-{b}";
        }

        SetBlockVisible(rangeBlock, tooltipRangeValue, showRange, rangeText);
    }

    private void SetBlockVisible(GameObject block, TMP_Text valueText, bool visible, string value)
    {
        if (block != null)
            block.SetActive(visible);

        if (valueText != null)
            valueText.text = visible ? value : "";
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    private string SafeAbilityName(AbilityDefV2 ability)
    {
        try
        {
            return string.IsNullOrWhiteSpace(ability.abilityName) ? ability.name : ability.abilityName;
        }
        catch
        {
            return ability != null ? ability.name : "";
        }
    }

    // =========================
    // Reflection helpers
    // =========================

    private static string ReadStringMember(object obj, string member)
    {
        if (obj == null || string.IsNullOrEmpty(member)) return null;
        var t = obj.GetType();

        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(string))
            return p.GetValue(obj) as string;

        var f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(string))
            return f.GetValue(obj) as string;

        return null;
    }

    private static int? ReadIntMember(object obj, string member)
    {
        if (obj == null || string.IsNullOrEmpty(member)) return null;
        var t = obj.GetType();

        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null && (p.PropertyType == typeof(int) || p.PropertyType == typeof(short)))
        {
            object v = p.GetValue(obj);
            if (v != null) return Convert.ToInt32(v);
        }

        var f = t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(short)))
        {
            object v = f.GetValue(obj);
            if (v != null) return Convert.ToInt32(v);
        }

        return null;
    }
}
