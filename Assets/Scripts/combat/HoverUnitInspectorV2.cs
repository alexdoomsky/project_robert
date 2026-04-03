using UnityEngine;
using UnityEngine.EventSystems;

public class HoverUnitInspectorV2 : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask hoverLayerMask = ~0; // включи сюда и юнитов, и турели
    [SerializeField] private float maxRayDistance = 200f;

    [Header("UI")]
    [SerializeField] private HoverUnitPanelV2 panel;
    [SerializeField] private float pinHoldSeconds = 1.0f;

    private UnitV2 _hoveredUnit;
    private TurretV2 _hoveredTurret;

    private float _hoverTime;
    private bool _pinned;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (panel != null)
            panel.OnCloseRequested += ForceClose;
    }

    private void OnDestroy()
    {
        if (panel != null)
            panel.OnCloseRequested -= ForceClose;
    }

    private void Update()
    {
        if (panel == null || worldCamera == null)
            return;

        // если мышь на UI - не меняем world-hover (чтобы можно было наводить на панель/иконки)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // закрытие по клику вне панели, если закреплено
            if (_pinned && Input.GetMouseButtonDown(0) && !panel.IsPointerOverPanel())
                UnpinAndHide();

            return;
        }

        // если закреплено: не меняем цель от world-hover
        if (_pinned)
        {
            if (Input.GetMouseButtonDown(0) && !panel.IsPointerOverPanel())
                UnpinAndHide();
            return;
        }

        // ищем объект под мышью
        (UnitV2 unit, TurretV2 turret) = RaycastHoverTarget();

        // смена цели
        if (unit != _hoveredUnit || turret != _hoveredTurret)
        {
            _hoveredUnit = unit;
            _hoveredTurret = turret;
            _hoverTime = 0f;

            if (_hoveredUnit != null)
            {
                panel.ShowForUnit(_hoveredUnit);
                panel.SetPinProgress(0f);
            }
            else if (_hoveredTurret != null)
            {
                panel.ShowForTurret(_hoveredTurret);
                panel.SetPinProgress(0f);
            }
            else
            {
                panel.Hide();
            }
        }

        // прогресс закрепления
        if (_hoveredUnit != null || _hoveredTurret != null)
        {
            _hoverTime += Time.deltaTime;
            float t = Mathf.Clamp01(_hoverTime / Mathf.Max(0.01f, pinHoldSeconds));
            panel.SetPinProgress(t);

            if (t >= 1f)
                Pin();
        }
    }

    private (UnitV2, TurretV2) RaycastHoverTarget()
    {
        Ray r = worldCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(r, out var hit, maxRayDistance, hoverLayerMask, QueryTriggerInteraction.Ignore))
        {
            var unit = hit.collider.GetComponentInParent<UnitV2>();
            if (unit != null) return (unit, null);

            var turret = hit.collider.GetComponentInParent<TurretV2>();
            if (turret != null) return (null, turret);
        }

        return (null, null);
    }

    private void Pin()
    {
        if (_hoveredUnit == null && _hoveredTurret == null) return;

        _pinned = true;
        panel.SetPinned(true);
        panel.SetPinProgress(1f);
    }

    private void UnpinAndHide()
    {
        _pinned = false;
        _hoveredUnit = null;
        _hoveredTurret = null;
        _hoverTime = 0f;

        panel.SetPinned(false);
        panel.Hide();
    }

    private void ForceClose()
    {
        UnpinAndHide();
    }
}
