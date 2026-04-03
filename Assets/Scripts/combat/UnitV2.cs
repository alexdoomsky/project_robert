using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitV2 : MonoBehaviour, IDamageableV2, IHealableV2, IEnergyRestorableV2
{
    public enum Faction { Player, Enemy }

    // === NEW: Enemy archetype for AI turn order + behavior presets ===
    public enum EnemyArchetype { Generic, OrkCruiser, ChaosRaider, Bulldog }

    [Header("Faction")]
    [SerializeField] private Faction faction = Faction.Player;
    public Faction Team => faction;

    // === NEW ===
    [Header("AI (Enemy)")]
    [SerializeField] private EnemyArchetype enemyArchetype = EnemyArchetype.Generic;
    public EnemyArchetype Archetype => enemyArchetype;

    [Header("Stats - HP")]
    [SerializeField] private int maxHP = 10;

    [Header("Stats - Energy")]
    [Tooltip("0 = у юнита нет энергии (полоса скрыта).")]
    [SerializeField] private int maxEnergy = 0;

    [Header("Combat")]
    [SerializeField] private int damage = 3;
    [SerializeField] private int attackRange = 2;

    [Header("Turn")]
    [SerializeField] private int movementPoints = 4;
    [SerializeField] private int actionsPerTurn = 1;

    [Range(0f, 1f)]
    [SerializeField] private float dodgeChance = 0f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float rotateSpeed = 720f;

    [Header("Death Visual")]
    [SerializeField] private Renderer[] renderersToTint;
    [SerializeField] private Color destroyedTint = Color.black;

    [Header("Abilities")]
    public AbilityDefV2 DashAbility;
    public AbilityDefV2 SpecialAbility;

    [SerializeField] private Transform visualRoot;

    public HexCellV2 CurrentCell { get; private set; }

    public int MaxHP => Mathf.Max(0, maxHP);
    public int HP { get; private set; }

    public int MaxEnergy => Mathf.Max(0, maxEnergy);
    public int Energy { get; private set; }

    public int MovementLeft { get; private set; }
    public int ActionsLeft { get; private set; }

    public int Damage => damage;
    public int AttackRange => attackRange;

    public int MaxMovePerTurn => Mathf.Max(0, movementPoints);
    public int MaxActionsPerTurn => Mathf.Max(0, actionsPerTurn);

    public float DodgeChanceBase => Mathf.Clamp01(dodgeChance);

    public bool IsAlive => HP > 0;
    public bool CanAct => IsAlive && ActionsLeft > 0;

    public bool IsMoving { get; private set; }
    public bool IsDestroyedCorpse { get; private set; }

    public bool RollDodgeNegatesAttack => false;

    // === EVENTS ===
    public event Action<UnitV2> OnHPChanged;
    public event Action<UnitV2> OnEnergyChanged;
    public event Action<UnitV2> OnBarrierChanged;
    public event Action<UnitV2> OnDied;
    public event Action<UnitV2> OnDodged;

    public event Action<UnitV2> OnStatusChanged;

    // === ABILITY STATE ===
    private readonly Dictionary<AbilityDefV2, int> _cooldowns = new();

    // Dash
    private int _dashRoundsLeft;
    private float _dashMoveMultiplier = 1f;
    private float _dashDodgeMultiplier = 1f;

    private int _movementSpentThisTurn = 0;

    // Barrier
    private AbilityDefV2 _activeBarrier;
    private int _barrierHP;
    private int _barrierRoundsLeft;

    public bool HasBarrier => _activeBarrier != null && _barrierRoundsLeft > 0 && _barrierHP > 0;
    public int BarrierHP => Mathf.Max(0, _barrierHP);
    public int BarrierMaxHP => _activeBarrier != null ? Mathf.Max(0, _activeBarrier.barrierMaxHP) : 0;
    public int BarrierRoundsLeft => Mathf.Max(0, _barrierRoundsLeft);

    public bool HasDashBuff => _dashRoundsLeft > 0;
    public int DashRoundsLeft => Mathf.Max(0, _dashRoundsLeft);

    // Artillery
    private bool _isAiming;
    public bool IsAiming => _isAiming;

    // aiming rounds left живёт в PendingArtilleryStrikeV2
    public int AimingRoundsLeft
    {
        get
        {
            var p = GetComponent<PendingArtilleryStrikeV2>();
            return p != null ? Mathf.Max(0, p.RoundsLeft) : 0;
        }
    }

    // Corpse
    private int _corpseTurnsLeft = 0;

    private TurnManagerV2 _turnManager;

    public void BindTurnManager(TurnManagerV2 tm) => _turnManager = tm;

    // === NEW: planned mine-arming pause points (for AI telegraph) ===
    private HashSet<HexCellV2> _aiPauseCells;
    private float _aiPauseSeconds;

    /// <summary>
    /// Enemy AI can request a short pause when the unit steps onto certain cells (e.g., mine arming telegraph).
    /// </summary>
    public void SetAIPauseCells(HashSet<HexCellV2> cells, float pauseSeconds)
    {
        _aiPauseCells = (cells != null && cells.Count > 0) ? new HashSet<HexCellV2>(cells) : null;
        _aiPauseSeconds = Mathf.Max(0f, pauseSeconds);
    }

    private bool ShouldAIPauseOnCell(HexCellV2 cell)
    {
        if (Team != Faction.Enemy) return false;
        if (_aiPauseCells == null || cell == null) return false;
        return _aiPauseCells.Contains(cell);
    }

    public void Init(Faction team)
    {
        faction = team;
        HP = MaxHP;
        Energy = MaxEnergy;

        IsDestroyedCorpse = false;
        _corpseTurnsLeft = 0;

        _activeBarrier = null;
        _barrierHP = 0;
        _barrierRoundsLeft = 0;

        _dashRoundsLeft = 0;
        _dashMoveMultiplier = 1f;
        _dashDodgeMultiplier = 1f;

        _isAiming = false;

        // NEW: clear AI pauses on init
        _aiPauseCells = null;
        _aiPauseSeconds = 0f;

        ResetTurn();
        RaiseAllStatsChanged();
        OnBarrierChanged?.Invoke(this);
        OnStatusChanged?.Invoke(this);
    }

    public void ResetTurn()
    {
        if (!IsAlive) return;

        _movementSpentThisTurn = 0;

        // Aiming: юнит в “стане”
        if (_isAiming)
        {
            MovementLeft = 0;
            ActionsLeft = 0;
            _movementSpentThisTurn = movementPoints;
            return;
        }

        int totalMoveThisTurn = Mathf.RoundToInt(movementPoints * Mathf.Max(1f, _dashMoveMultiplier));
        MovementLeft = Mathf.Max(0, totalMoveThisTurn);
        ActionsLeft = actionsPerTurn;
    }

    public void ConsumeAction()
    {
        ActionsLeft = Mathf.Max(0, ActionsLeft - 1);
    }

    public bool CanUseAbility(AbilityDefV2 ability)
    {
        if (ability == null) return false;
        if (!CanAct) return false;
        if (ActionsLeft < ability.actionCost) return false;
        if (Energy < ability.energyCost) return false;
        if (_cooldowns.TryGetValue(ability, out int cd) && cd > 0) return false;
        if (_isAiming) return false;
        return true;
    }

    public void UseAbility(AbilityDefV2 ability, HexCellV2 targetCell, UnitV2 targetUnit)
    {
        if (!CanUseAbility(ability)) return;

        ConsumeAction();
        SpendEnergy(ability.energyCost);

        if (ability.cooldownRounds > 0)
            _cooldowns[ability] = ability.cooldownRounds;

        switch (ability.kind)
        {
            case AbilityKindV2.Dash:
                {
                    _dashRoundsLeft = Mathf.Max(0, ability.dashDurationRounds);
                    _dashMoveMultiplier = Mathf.Max(1f, ability.dashMoveMultiplier);
                    _dashDodgeMultiplier = Mathf.Max(1f, ability.dashDodgeMultiplier);

                    int totalMoveThisTurn = Mathf.RoundToInt(movementPoints * _dashMoveMultiplier);
                    int newMovementLeft = totalMoveThisTurn - _movementSpentThisTurn;
                    MovementLeft = Mathf.Max(0, newMovementLeft);

                    OnStatusChanged?.Invoke(this);
                    break;
                }

            case AbilityKindV2.Barrier:
                {
                    _activeBarrier = ability;
                    _barrierHP = Mathf.Max(0, ability.barrierMaxHP);
                    _barrierRoundsLeft = Mathf.Max(0, ability.barrierDurationRounds);
                    OnBarrierChanged?.Invoke(this);
                    OnStatusChanged?.Invoke(this);
                    break;
                }

            case AbilityKindV2.Heal:
                if (targetUnit != null)
                {
                    targetUnit.RestoreHP(ability.healHP);
                    targetUnit.RestoreEnergy(ability.restoreEnergy);
                }
                break;

            case AbilityKindV2.ArtilleryStrike:
                if (targetCell == null) return;

                SetAiming(true);

                var pending = gameObject.AddComponent<PendingArtilleryStrikeV2>();
                pending.Init(this, ability, targetCell);
                break;
        }
    }

    public void SetAiming(bool aiming)
    {
        if (_isAiming == aiming) return;

        _isAiming = aiming;

        if (_isAiming)
        {
            MovementLeft = 0;
            ActionsLeft = 0;
            _movementSpentThisTurn = movementPoints;
        }

        OnStatusChanged?.Invoke(this);
    }

    public void OnPhaseEnded()
    {
        if (IsDestroyedCorpse)
        {
            _corpseTurnsLeft--;
            if (_corpseTurnsLeft <= 0)
            {
                if (CurrentCell != null)
                    CurrentCell.ClearOccupant(this);
                Destroy(gameObject);
            }
            return;
        }

        foreach (var key in new List<AbilityDefV2>(_cooldowns.Keys))
            _cooldowns[key] = Mathf.Max(0, _cooldowns[key] - 1);

        bool beforeDash = HasDashBuff;
        bool beforeHasBarrier = HasBarrier;

        if (_dashRoundsLeft > 0)
        {
            _dashRoundsLeft--;
            if (_dashRoundsLeft == 0)
            {
                _dashMoveMultiplier = 1f;
                _dashDodgeMultiplier = 1f;
            }
        }

        if (_activeBarrier != null)
        {
            int beforeHP = _barrierHP;
            bool beforeHas = HasBarrier;

            _barrierRoundsLeft--;

            if (_barrierHP > 0 && _activeBarrier.barrierRegenPerRound > 0)
            {
                _barrierHP = Mathf.Min(_barrierHP + _activeBarrier.barrierRegenPerRound, _activeBarrier.barrierMaxHP);
            }

            if (_barrierRoundsLeft <= 0 || _barrierHP <= 0)
            {
                _activeBarrier = null;
                _barrierHP = 0;
                _barrierRoundsLeft = 0;
            }

            if (beforeHP != _barrierHP || beforeHas != HasBarrier)
                OnBarrierChanged?.Invoke(this);
        }

        if (beforeDash != HasDashBuff || beforeHasBarrier != HasBarrier)
            OnStatusChanged?.Invoke(this);
    }

    public bool TryEvadeAttack()
    {
        if (!IsAlive || dodgeChance <= 0f) return false;

        float mult = (_dashRoundsLeft > 0) ? Mathf.Max(1f, _dashDodgeMultiplier) : 1f;
        float finalChance = Mathf.Clamp01(dodgeChance * mult);

        bool dodged = UnityEngine.Random.value < finalChance;
        if (dodged) OnDodged?.Invoke(this);
        return dodged;
    }

    public void TakeDamage(int dmg, UnitV2 attacker = null)
    {
        if (!IsAlive && !IsDestroyedCorpse) return;
        if (dmg <= 0) return;

        bool beforeHasBarrier = HasBarrier;

        bool barrierWasHit = false;
        int beforeBarrierHP = _barrierHP;
        bool beforeHas = HasBarrier;

        if (_activeBarrier != null && _barrierHP > 0)
        {
            barrierWasHit = true;

            int shieldPart = Mathf.CeilToInt(dmg * Mathf.Clamp01(_activeBarrier.barrierAbsorbPercent));
            int hpPart = dmg - shieldPart;

            _barrierHP -= shieldPart;
            if (_barrierHP < 0) _barrierHP = 0;

            HP -= hpPart;
        }
        else
        {
            HP -= dmg;
        }

        OnHPChanged?.Invoke(this);

        if (barrierWasHit)
        {
            if (_activeBarrier != null && _barrierHP <= 0)
            {
                _activeBarrier = null;
                _barrierRoundsLeft = 0;
                _barrierHP = 0;
            }

            if (beforeBarrierHP != _barrierHP || beforeHas != HasBarrier)
                OnBarrierChanged?.Invoke(this);
        }

        if (beforeHasBarrier != HasBarrier)
            OnStatusChanged?.Invoke(this);

        if (HP <= 0)
            DieAsCorpseObstacle();
    }

    private void DieAsCorpseObstacle()
    {
        HP = 0;
        IsDestroyedCorpse = true;
        _corpseTurnsLeft = 1;
        OnDied?.Invoke(this);
        ApplyDestroyedTint();

        if (CurrentCell != null)
        {
            CurrentCell.ClearOccupant(this);
            CurrentCell.TrySetOccupant(this, true);
        }
    }

    public bool SpendEnergy(int amount)
    {
        if (amount <= 0) return true;
        if (MaxEnergy <= 0 || Energy < amount) return false;
        Energy -= amount;
        OnEnergyChanged?.Invoke(this);
        return true;
    }

    public void RestoreEnergy(int amount)
    {
        if (amount <= 0 || MaxEnergy <= 0) return;
        Energy = Mathf.Clamp(Energy + amount, 0, MaxEnergy);
        OnEnergyChanged?.Invoke(this);
    }

    public void RestoreHP(int amount)
    {
        if (amount <= 0 || !IsAlive) return;
        HP = Mathf.Clamp(HP + amount, 0, MaxHP);
        OnHPChanged?.Invoke(this);
    }

    private void RaiseAllStatsChanged()
    {
        OnHPChanged?.Invoke(this);
        OnEnergyChanged?.Invoke(this);
        OnBarrierChanged?.Invoke(this);
    }

    public bool PlaceOnCell(HexCellV2 cell)
    {
        if (cell == null || !cell.walkable || cell.OccupantBlocksMovement)
            return false;

        if (CurrentCell != null)
            CurrentCell.ClearOccupant(this);

        CurrentCell = cell;
        cell.TrySetOccupant(this, true);
        transform.position = cell.transform.position;
        return true;
    }

    public void StartMoveAlongPath(List<HexCellV2> path)
    {
        if (_isAiming) return;

        if (IsMoving || !IsAlive || path == null || path.Count < 2)
            return;
        StartCoroutine(CoMove(path));
    }

    public void FaceCell(HexCellV2 cell)
    {
        if (cell == null) return;
        FaceWorldPoint(cell.transform.position);
    }

    public void FaceWorldPoint(Vector3 worldPos)
    {
        if (visualRoot == null) return;

        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        visualRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private IEnumerator CoMove(List<HexCellV2> path)
    {
        IsMoving = true;

        for (int i = 1; i < path.Count && MovementLeft > 0; i++)
        {
            if (_isAiming) break;

            var next = path[i];
            if (!next.walkable || next.OccupantBlocksMovement) break;

            FaceCell(next);

            MovementLeft--;
            _movementSpentThisTurn++;

            Vector3 start = transform.position;
            Vector3 end = next.transform.position;
            float dist = Vector3.Distance(start, end);
            float t = 0f;

            while (t < 1f)
            {
                if (_isAiming) break;

                t += Time.deltaTime * (moveSpeed / Mathf.Max(0.01f, dist));
                transform.position = Vector3.Lerp(start, end, speedCurve.Evaluate(t));
                yield return null;
            }

            if (_isAiming) break;

            PlaceOnCell(next);

            // === NEW: AI telegraph pause on selected cells (mines etc.) ===
            if (_aiPauseSeconds > 0f && ShouldAIPauseOnCell(next))
                yield return new WaitForSeconds(_aiPauseSeconds);
        }

        IsMoving = false;

        // NEW: clear after finishing move so it doesn't leak into future moves
        _aiPauseCells = null;
        _aiPauseSeconds = 0f;
    }

    private void ApplyDestroyedTint()
    {
        if (renderersToTint == null || renderersToTint.Length == 0)
            renderersToTint = GetComponentsInChildren<Renderer>();

        foreach (var r in renderersToTint)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", destroyedTint);
            r.SetPropertyBlock(mpb);
        }
    }

    // ====== ДЛЯ UI: собрать статусы с подробностями ======
    public void CollectStatusInfos(List<StatusInfoV2> buffer)
    {
        buffer.Clear();

        if (IsAiming)
        {
            buffer.Add(new StatusInfoV2
            {
                id = StatusId.Aiming,
                roundsLeft = AimingRoundsLeft,
                extraA = 0,
                extraB = 0
            });
        }

        if (HasBarrier)
        {
            buffer.Add(new StatusInfoV2
            {
                id = StatusId.Barrier,
                roundsLeft = BarrierRoundsLeft,
                extraA = BarrierHP,
                extraB = BarrierMaxHP
            });
        }

        if (HasDashBuff)
        {
            buffer.Add(new StatusInfoV2
            {
                id = StatusId.Dash,
                roundsLeft = DashRoundsLeft,
                extraA = 0,
                extraB = 0
            });
        }
    }
}
