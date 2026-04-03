using UnityEngine;
using UnityEngine.UI;

public class TurretWorldspaceBarsV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TurretV2 turret;

    [Header("HP")]
    [SerializeField] private Image hpFill;

    [Header("Energy")]
    [Tooltip("Корневой объект энергии (бар). Если MaxEnergy == 0, скрывается целиком.")]
    [SerializeField] private GameObject energyRoot;
    [SerializeField] private Image energyFill;

    private void Awake()
    {
        if (turret == null)
            turret = GetComponentInParent<TurretV2>();
    }

    private void OnEnable()
    {
        if (turret == null) return;

        turret.OnHPChanged += HandleHPChanged;
        turret.OnEnergyChanged += HandleEnergyChanged;

        HandleHPChanged(turret);
        HandleEnergyChanged(turret);
    }

    private void OnDisable()
    {
        if (turret == null) return;

        turret.OnHPChanged -= HandleHPChanged;
        turret.OnEnergyChanged -= HandleEnergyChanged;
    }

    private void HandleHPChanged(TurretV2 t)
    {
        if (t == null || hpFill == null) return;

        float max = Mathf.Max(1, t.MaxHP);
        hpFill.fillAmount = Mathf.Clamp01((float)t.HP / max);
    }

    private void HandleEnergyChanged(TurretV2 t)
    {
        if (t == null) return;

        bool hasEnergy = t.MaxEnergy > 0;

        if (energyRoot != null)
            energyRoot.SetActive(hasEnergy);

        if (!hasEnergy || energyFill == null)
            return;

        float max = Mathf.Max(1, t.MaxEnergy);
        energyFill.fillAmount = Mathf.Clamp01((float)t.Energy / max);
    }
}
