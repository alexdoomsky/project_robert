using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitWorldspaceStatusIconsV2 : MonoBehaviour
{
    [SerializeField] private UnitV2 unit;

    [Header("Slots (size must be 3)")]
    [SerializeField] private GameObject[] slotRoots;
    [SerializeField] private Image[] slotIcons;

    [System.Serializable]
    public struct StatusIconBinding
    {
        public StatusId status;
        public Sprite icon;
    }

    [Header("Icon bindings")]
    [SerializeField] private StatusIconBinding[] bindings;

    private readonly Dictionary<StatusId, Sprite> _iconMap = new();
    private readonly List<StatusId> _statuses = new(3);

    private void Awake()
    {
        if (unit == null)
            unit = GetComponentInParent<UnitV2>();

        _iconMap.Clear();
        foreach (var b in bindings)
        {
            if (b.icon != null)
                _iconMap[b.status] = b.icon;
        }
    }

    private void OnEnable()
    {
        if (unit == null) return;
        unit.OnStatusChanged += Refresh;
        Refresh(unit);
    }

    private void OnDisable()
    {
        if (unit != null)
            unit.OnStatusChanged -= Refresh;
    }

    private void Refresh(UnitV2 _)
    {
        _statuses.Clear();

        // priority order: Aiming > Barrier > Dash
        if (unit.IsAiming) _statuses.Add(StatusId.Aiming);
        if (unit.HasBarrier) _statuses.Add(StatusId.Barrier);
        if (unit.HasDashBuff) _statuses.Add(StatusId.Dash);

        int slots = Mathf.Min(slotRoots.Length, slotIcons.Length);

        for (int i = 0; i < slots; i++)
        {
            if (i < _statuses.Count && _iconMap.TryGetValue(_statuses[i], out var sprite) && sprite != null)
            {
                slotRoots[i].SetActive(true);
                slotIcons[i].sprite = sprite;
            }
            else
            {
                slotRoots[i].SetActive(false);
            }
        }

        // если кто-то в инспекторе сделал больше 3 слотов, лишние просто выключим
        for (int i = slots; i < slotRoots.Length; i++)
            slotRoots[i].SetActive(false);
    }
}
