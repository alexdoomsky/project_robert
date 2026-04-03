using UnityEngine;
using UnityEngine.UI;

public class UnitWorldspaceBarsV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UnitV2 unit;

    [Header("HP")]
    [SerializeField] private Image hpFill;

    [Header("Energy")]
    [Tooltip("Корневой объект полосы энергии (контейнер). Если MaxEnergy==0, он выключается целиком.")]
    [SerializeField] private GameObject energyRoot;
    [SerializeField] private Image energyFill;

    [Header("Barrier (Shield)")]
    [Tooltip("Корневой объект полосы щита. Выключается, если щита нет.")]
    [SerializeField] private GameObject barrierRoot;
    [SerializeField] private Image barrierFill;

    private void Awake()
    {
        if (unit == null)
            unit = GetComponentInParent<UnitV2>();
    }

    private void OnEnable()
    {
        if (unit == null) return;

        unit.OnHPChanged += HandleHPChanged;
        unit.OnEnergyChanged += HandleEnergyChanged;
        unit.OnBarrierChanged += HandleBarrierChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (unit == null) return;

        unit.OnHPChanged -= HandleHPChanged;
        unit.OnEnergyChanged -= HandleEnergyChanged;
        unit.OnBarrierChanged -= HandleBarrierChanged;
    }

    private void RefreshAll()
    {
        HandleHPChanged(unit);
        HandleEnergyChanged(unit);
        HandleBarrierChanged(unit);
    }

    private void HandleHPChanged(UnitV2 u)
    {
        if (hpFill == null) return;

        float max = Mathf.Max(1, u.MaxHP);
        hpFill.fillAmount = Mathf.Clamp01((float)u.HP / max);
    }

    private void HandleEnergyChanged(UnitV2 u)
    {
        bool hasEnergy = u.MaxEnergy > 0;

        if (energyRoot != null)
            energyRoot.SetActive(hasEnergy);

        if (!hasEnergy || energyFill == null)
            return;

        float max = Mathf.Max(1, u.MaxEnergy);
        energyFill.fillAmount = Mathf.Clamp01((float)u.Energy / max);
    }

    private void HandleBarrierChanged(UnitV2 u)
    {
        bool hasBarrier = u.HasBarrier && u.BarrierMaxHP > 0 && u.BarrierHP > 0;

        if (barrierRoot != null)
            barrierRoot.SetActive(hasBarrier);

        if (!hasBarrier || barrierFill == null)
            return;

        float max = Mathf.Max(1, u.BarrierMaxHP);
        barrierFill.fillAmount = Mathf.Clamp01((float)u.BarrierHP / max);
    }
}
