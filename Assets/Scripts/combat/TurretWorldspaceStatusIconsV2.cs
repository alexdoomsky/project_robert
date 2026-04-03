using UnityEngine;
using UnityEngine.UI;

public class TurretWorldspaceStatusIconsV2 : MonoBehaviour
{
    [SerializeField] private TurretV2 turret;

    [Header("Single slot")]
    [SerializeField] private GameObject slotRoot;
    [SerializeField] private Image icon;

    [Header("Icons")]
    [SerializeField] private Sprite dormantIcon;
    [SerializeField] private Sprite warmingUpIcon;

    private void Awake()
    {
        if (turret == null)
            turret = GetComponentInParent<TurretV2>();
    }

    private void OnEnable()
    {
        if (turret == null) return;
        turret.OnStateChanged += Refresh;
        Refresh(turret);
    }

    private void OnDisable()
    {
        if (turret != null)
            turret.OnStateChanged -= Refresh;
    }

    private void Refresh(TurretV2 t)
    {
        if (t == null || slotRoot == null || icon == null) return;

        switch (t.State)
        {
            case TurretV2.TurretState.Dormant:
                slotRoot.SetActive(true);
                icon.sprite = dormantIcon;
                break;

            case TurretV2.TurretState.WarmingUp:
                slotRoot.SetActive(true);
                icon.sprite = warmingUpIcon;
                break;

            default:
                slotRoot.SetActive(false);
                break;
        }
    }
}
