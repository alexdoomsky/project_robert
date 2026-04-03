using System;
using UnityEngine;

public class TurretV2 : MonoBehaviour, IDamageableV2, IHealableV2
{
    public enum TurretState
    {
        Dormant,
        WarmingUp,
        Controlled,
        Destroyed
    }

    [Header("State")]
    [SerializeField] private TurretState state = TurretState.Dormant;
    public TurretState State => state;

    [Header("Stats - HP")]
    [SerializeField] private int maxHP = 50;

    [Header("Stats - Battery (Energy)")]
    [Tooltip("0 = у турели нет батареи (полоса скрываетс€).")]
    [SerializeField] private int maxEnergy = 3;

    [Tooltip("Сколько энергии тратится за 1 выстрел.")]
    [SerializeField] private int energyPerShot = 1;

    [Header("Combat")]
    [SerializeField] private float rotateSpeed = 720f; // на будущее, если захочешь плавно
    [SerializeField] private int damage = 15;
    [SerializeField] private int attackRange = 3;

    [Header("Per-phase limit")]
    [Tooltip("Сколько выстрелов турель может сделать за фазу игрока.")]
    [SerializeField] private int shotsPerPlayerPhase = 1;

    [Header("Hack")]
    [SerializeField] private int controlRadius = 1;

    [SerializeField] private Transform visualRoot;

    public int MaxHP => Mathf.Max(0, maxHP);
    public int HP { get; private set; }

    public int MaxEnergy => Mathf.Max(0, maxEnergy);
    public int Energy { get; private set; }

    public int Damage => damage;
    public int AttackRange => attackRange;
    public int ControlRadius => controlRadius;

    public bool IsAlive => HP > 0;

    public HexCellV2 CurrentCell { get; private set; }

    private int _warmupRoundsLeft = 0;
    private int _shotsLeftThisPlayerPhase = 0;

    // НОВОЕ: UI нужно знать сколько осталось прогрева
    public int WarmupRoundsLeft => Mathf.Max(0, _warmupRoundsLeft);

    public event Action<TurretV2> OnHPChanged;
    public event Action<TurretV2> OnEnergyChanged;
    public event Action<TurretV2> OnStateChanged;

    private TurnManagerV2 _turnManager;
    private HexGridV2 _grid;

    private void Awake()
    {
        HP = MaxHP;
        Energy = MaxEnergy;

        _turnManager = FindObjectOfType<TurnManagerV2>();
        _grid = FindObjectOfType<HexGridV2>();

        ResolveCurrentCell();

        if (state == TurretState.Controlled)
            _shotsLeftThisPlayerPhase = Mathf.Max(0, shotsPerPlayerPhase);

        RaiseAllChanged();
    }

    private void OnEnable()
    {
        if (_turnManager != null)
        {
            _turnManager.OnRoundEnded += HandleRoundEnd;
            _turnManager.OnPhaseStarted += HandlePhaseStarted;
        }
    }

    private void OnDisable()
    {
        if (_turnManager != null)
        {
            _turnManager.OnRoundEnded -= HandleRoundEnd;
            _turnManager.OnPhaseStarted -= HandlePhaseStarted;
        }
    }

    private void HandlePhaseStarted(TurnManagerV2.Phase phase)
    {
        if (phase != TurnManagerV2.Phase.Player) return;
        _shotsLeftThisPlayerPhase = (state == TurretState.Controlled) ? Mathf.Max(0, shotsPerPlayerPhase) : 0;
    }

    private void ResolveCurrentCell()
    {
        if (_grid == null) return;

        HexCellV2 best = null;
        float bestD = float.PositiveInfinity;

        Vector3 p = transform.position;
        foreach (var c in _grid.GetAllCells())
        {
            if (c == null) continue;
            float d = (c.transform.position - p).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }

        CurrentCell = best;
    }

    private void RaiseAllChanged()
    {
        OnHPChanged?.Invoke(this);
        OnEnergyChanged?.Invoke(this);
        OnStateChanged?.Invoke(this);
    }

    public bool CanBeHackedBy(UnitV2 unit)
    {
        if (state != TurretState.Dormant) return false;
        if (unit == null || !unit.IsAlive) return false;
        if (unit.Team != UnitV2.Faction.Player) return false;
        if (unit.ActionsLeft <= 0) return false;

        if (unit.CurrentCell == null || CurrentCell == null)
            return false;

        int dist = CombatSystemV2.HexDistance(unit.CurrentCell, CurrentCell);
        return dist <= controlRadius;
    }

    public bool TryHack(UnitV2 unit)
    {
        if (!CanBeHackedBy(unit)) return false;

        unit.ConsumeAction();

        state = TurretState.WarmingUp;
        _warmupRoundsLeft = 1;
        _shotsLeftThisPlayerPhase = 0;

        OnStateChanged?.Invoke(this);
        return true;
    }

    private void HandleRoundEnd(int roundIndex)
    {
        if (state != TurretState.WarmingUp) return;

        _warmupRoundsLeft--;
        if (_warmupRoundsLeft <= 0)
        {
            state = TurretState.Controlled;
            _shotsLeftThisPlayerPhase = Mathf.Max(0, shotsPerPlayerPhase);
            OnStateChanged?.Invoke(this);
        }
    }

    private void FaceTowardsTargetCell(HexCellV2 targetCell)
    {
        if (targetCell == null) return;
        if (visualRoot == null) return;

        Vector3 dir = targetCell.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        // пошаговая тактика: поворачиваемся сразу.
        visualRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private bool HasEnergyForShot()
    {
        if (energyPerShot <= 0) return true;
        return Energy >= energyPerShot;
    }

    public bool CanFireNow()
    {
        if (state != TurretState.Controlled) return false;
        if (!IsAlive) return false;
        if (_shotsLeftThisPlayerPhase <= 0) return false;
        return HasEnergyForShot();
    }

    public bool TryFireAt(IDamageableV2 target, HexCellV2 targetCell, out string reason)
    {
        reason = null;

        if (!CanFireNow())
        {
            reason = "turret_cannot_fire";
            return false;
        }

        if (target == null || targetCell == null)
        {
            reason = "no_target";
            return false;
        }

        if (CurrentCell == null)
            ResolveCurrentCell();

        if (CurrentCell == null)
        {
            reason = "turret_cell_missing";
            return false;
        }

        int dist = CombatSystemV2.HexDistance(CurrentCell, targetCell);
        if (dist > attackRange)
        {
            reason = "out_of_range";
            return false;
        }

        _shotsLeftThisPlayerPhase = Mathf.Max(0, _shotsLeftThisPlayerPhase - 1);

        if (energyPerShot > 0)
        {
            Energy = Mathf.Max(0, Energy - energyPerShot);
            OnEnergyChanged?.Invoke(this);
        }

        FaceTowardsTargetCell(targetCell);

        // Visual feedback (tracer) to the center of the target cell.
        AttackVFXManagerV2.Instance?.PlayTracerFromTurret(this, targetCell);

        target.TakeDamage(damage, null);
        return true;
    }

    public void TakeDamage(int damageAmount, UnitV2 attacker = null)
    {
        if (state == TurretState.Destroyed) return;
        if (damageAmount <= 0) return;

        HP -= damageAmount;
        OnHPChanged?.Invoke(this);

        if (HP <= 0)
        {
            HP = 0;
            state = TurretState.Destroyed;
            OnStateChanged?.Invoke(this);
            Destroy(gameObject);
        }
    }

    public void RestoreHP(int amount)
    {
        if (amount <= 0) return;
        if (state == TurretState.Destroyed) return;

        int before = HP;
        HP = Mathf.Clamp(HP + amount, 0, MaxHP);
        if (HP != before)
            OnHPChanged?.Invoke(this);
    }
}
