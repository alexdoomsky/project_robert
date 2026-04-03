using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class HoverUnitPanelV2 : MonoBehaviour
{
    public event Action OnCloseRequested;

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Pin UI")]
    [SerializeField] private Image pinProgressFill;
    [SerializeField] private GameObject pinnedBadge;

    [Header("Texts")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text moveText;
    [SerializeField] private TMP_Text actionsText;

    [Header("Turret extra")]
    [SerializeField] private TMP_Text turretStateText;

    [Header("Bars")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image energyFill;
    [SerializeField] private Image barrierFill;
    [SerializeField] private TMP_Text barrierText;

    [Header("Statuses")]
    [SerializeField] private Transform statusContainer;
    [SerializeField] private StatusIconViewV2 statusIconPrefab;
    [SerializeField] private StatusDatabaseV2 statusDatabase;

    private UnitV2 _unit;
    private TurretV2 _turret;
    private bool _pinned;

    private readonly List<StatusInfoV2> _statusBuffer = new();
    private readonly List<StatusIconViewV2> _spawned = new();

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleCloseClicked);
    }

    private void HandleCloseClicked()
    {
        OnCloseRequested?.Invoke();
    }

    public void ShowForUnit(UnitV2 unit)
    {
        ClearSubscriptions();

        _unit = unit;
        _turret = null;

        if (_unit == null) { Hide(); return; }

        if (root != null) root.SetActive(true);

        SubscribeUnit(_unit);
        Refresh();
    }

    public void ShowForTurret(TurretV2 turret)
    {
        ClearSubscriptions();

        _unit = null;
        _turret = turret;

        if (_turret == null) { Hide(); return; }

        if (root != null) root.SetActive(true);

        SubscribeTurret(_turret);
        Refresh();
    }

    public void Hide()
    {
        ClearSubscriptions();

        _unit = null;
        _turret = null;
        _pinned = false;

        ClearStatuses();

        if (root != null) root.SetActive(false);
    }

    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        if (pinnedBadge != null)
            pinnedBadge.SetActive(pinned);
    }

    public void SetPinProgress(float t)
    {
        if (pinProgressFill != null)
            pinProgressFill.fillAmount = Mathf.Clamp01(t);
    }

    public bool IsPointerOverPanel()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SubscribeUnit(UnitV2 u)
    {
        u.OnHPChanged += OnUnitChanged;
        u.OnEnergyChanged += OnUnitChanged;
        u.OnBarrierChanged += OnUnitChanged;
        u.OnStatusChanged += OnUnitChanged;
        u.OnDied += OnUnitChanged;
    }

    private void SubscribeTurret(TurretV2 t)
    {
        t.OnHPChanged += OnTurretChanged;
        t.OnEnergyChanged += OnTurretChanged;
        t.OnStateChanged += OnTurretChanged;
    }

    private void ClearSubscriptions()
    {
        if (_unit != null)
        {
            _unit.OnHPChanged -= OnUnitChanged;
            _unit.OnEnergyChanged -= OnUnitChanged;
            _unit.OnBarrierChanged -= OnUnitChanged;
            _unit.OnStatusChanged -= OnUnitChanged;
            _unit.OnDied -= OnUnitChanged;
        }

        if (_turret != null)
        {
            _turret.OnHPChanged -= OnTurretChanged;
            _turret.OnEnergyChanged -= OnTurretChanged;
            _turret.OnStateChanged -= OnTurretChanged;
        }
    }

    private void OnUnitChanged(UnitV2 _)
    {
        if (_unit == null) return;
        Refresh();
    }

    private void OnTurretChanged(TurretV2 _)
    {
        if (_turret == null) return;
        Refresh();
    }

    private void Refresh()
    {
        if (_unit != null)
            RefreshUnit();
        else if (_turret != null)
            RefreshTurret();
    }

    private void RefreshUnit()
    {
        if (nameText != null) nameText.text = SanitizeName(_unit.name);

        // HP
        if (hpText != null) hpText.text = $"{_unit.HP}/{_unit.MaxHP}";
        if (hpFill != null) hpFill.fillAmount = _unit.MaxHP > 0 ? (float)_unit.HP / _unit.MaxHP : 0f;

        // Energy
        if (_unit.MaxEnergy > 0)
        {
            if (energyText != null) { energyText.text = $"{_unit.Energy}/{_unit.MaxEnergy}"; energyText.gameObject.SetActive(true); }
            if (energyFill != null) { energyFill.fillAmount = (float)_unit.Energy / _unit.MaxEnergy; energyFill.gameObject.SetActive(true); }
        }
        else
        {
            if (energyText != null) energyText.gameObject.SetActive(false);
            if (energyFill != null) energyFill.gameObject.SetActive(false);
        }

        // Move/Actions
        if (moveText != null) { moveText.text = $"{_unit.MovementLeft}/{_unit.MaxMovePerTurn}"; moveText.gameObject.SetActive(true); }
        if (actionsText != null) { actionsText.text = $"{_unit.ActionsLeft}/{_unit.MaxActionsPerTurn}"; actionsText.gameObject.SetActive(true); }

        // turret-only text hidden
        if (turretStateText != null) turretStateText.gameObject.SetActive(false);

        // Barrier
        if (_unit.HasBarrier)
        {
            int cur = _unit.BarrierHP;
            int max = Mathf.Max(1, _unit.BarrierMaxHP);

            if (barrierFill != null) { barrierFill.fillAmount = (float)cur / max; barrierFill.gameObject.SetActive(true); }
            if (barrierText != null) { barrierText.text = $"{cur}/{_unit.BarrierMaxHP} ({_unit.BarrierRoundsLeft})"; barrierText.gameObject.SetActive(true); }
        }
        else
        {
            if (barrierFill != null) barrierFill.gameObject.SetActive(false);
            if (barrierText != null) barrierText.gameObject.SetActive(false);
        }

        DrawUnitStatuses();
    }

    private void RefreshTurret()
    {
        if (nameText != null) nameText.text = SanitizeName(_turret.name);

        // HP
        if (hpText != null) hpText.text = $"{_turret.HP}/{_turret.MaxHP}";
        if (hpFill != null) hpFill.fillAmount = _turret.MaxHP > 0 ? (float)_turret.HP / _turret.MaxHP : 0f;

        // Energy
        if (_turret.MaxEnergy > 0)
        {
            if (energyText != null) { energyText.text = $"{_turret.Energy}/{_turret.MaxEnergy}"; energyText.gameObject.SetActive(true); }
            if (energyFill != null) { energyFill.fillAmount = (float)_turret.Energy / _turret.MaxEnergy; energyFill.gameObject.SetActive(true); }
        }
        else
        {
            if (energyText != null) energyText.gameObject.SetActive(false);
            if (energyFill != null) energyFill.gameObject.SetActive(false);
        }

        // Move/Actions not applicable
        if (moveText != null) moveText.gameObject.SetActive(false);
        if (actionsText != null) actionsText.gameObject.SetActive(false);

        // Barrier not applicable (если не сделаешь в будущем)
        if (barrierFill != null) barrierFill.gameObject.SetActive(false);
        if (barrierText != null) barrierText.gameObject.SetActive(false);

        if (turretStateText != null)
        {
            turretStateText.gameObject.SetActive(true);
            turretStateText.text = _turret.State.ToString();
        }

        DrawTurretStatuses();
    }

    private void DrawUnitStatuses()
    {
        if (statusContainer == null || statusIconPrefab == null || statusDatabase == null) return;

        _unit.CollectStatusInfos(_statusBuffer);

        ClearStatuses();

        for (int i = 0; i < _statusBuffer.Count; i++)
        {
            var info = _statusBuffer[i];
            var def = statusDatabase.Get(info.id);
            if (def == null) continue;

            var view = Instantiate(statusIconPrefab, statusContainer);
            view.Bind(def, info);
            _spawned.Add(view);
        }
    }

    private void DrawTurretStatuses()
    {
        if (statusContainer == null || statusIconPrefab == null || statusDatabase == null) return;

        _statusBuffer.Clear();

        // Dormant / WarmingUp показываем, Controlled/Destroyed - можно не показывать
        if (_turret.State == TurretV2.TurretState.Dormant)
        {
            _statusBuffer.Add(new StatusInfoV2 { id = StatusId.Dormant, roundsLeft = 0, extraA = 0, extraB = 0 });
        }
        else if (_turret.State == TurretV2.TurretState.WarmingUp)
        {
            _statusBuffer.Add(new StatusInfoV2 { id = StatusId.WarmingUp, roundsLeft = _turret.WarmupRoundsLeft, extraA = 0, extraB = 0 });
        }

        ClearStatuses();

        for (int i = 0; i < _statusBuffer.Count; i++)
        {
            var info = _statusBuffer[i];
            var def = statusDatabase.Get(info.id);
            if (def == null) continue;

            var view = Instantiate(statusIconPrefab, statusContainer);
            view.Bind(def, info);
            _spawned.Add(view);
        }
    }

    private void ClearStatuses()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);

        _spawned.Clear();
    }

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw.Replace("(Clone)", "").Trim();
    }
}
