using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnManagerV2 : MonoBehaviour
{
    public enum Phase { Player, Enemy }

    public event Action<Phase> OnPhaseStarted;
    public event Action<Phase> OnPhaseEnded;

    // Полный раунд = Player + Enemy. Срабатывает в конце Enemy-фазы.
    public event Action<int> OnRoundEnded;

    [SerializeField] private Phase currentPhase = Phase.Player;
    public Phase CurrentPhase => currentPhase;

    [Header("Boot")]
    [Tooltip("Если выключено, бой не стартует в Start(). Вызови BeginBattle() вручную (например, после pre-battle UI).")]
    [SerializeField] private bool autoStartOnStart = true;

    public bool HasStarted => _started;
    private bool _started;

    [SerializeField] private bool debugLogs = true;

    private readonly List<UnitV2> _units = new();
    public IReadOnlyList<UnitV2> Units => _units;

    private int _roundIndex = 0; // увеличиваем на каждом EndRound (после Enemy)

    public void RegisterUnit(UnitV2 unit)
    {
        if (unit == null) return;
        if (_units.Contains(unit)) return;

        _units.Add(unit);
        unit.BindTurnManager(this);
    }

    public void UnregisterUnit(UnitV2 unit)
    {
        if (unit == null) return;
        _units.Remove(unit);
    }

    public bool CanControl(UnitV2 unit)
    {
        if (unit == null) return false;
        return currentPhase == Phase.Player
            ? unit.Team == UnitV2.Faction.Player
            : unit.Team == UnitV2.Faction.Enemy;
    }

    private void Start()
    {
        if (autoStartOnStart)
            BeginBattle();
    }

    // NEW: ручной старт боя (для pre-battle)
    public void BeginBattle()
    {
        if (_started) return;
        _started = true;

        if (debugLogs) Debug.Log($"[Turn] BeginBattle (Phase={currentPhase})");
        BeginPhase(currentPhase);
    }

    public void EndPhase()
    {
        if (!_started) return;

        if (debugLogs) Debug.Log($"[Turn] EndPhase {currentPhase}");

        // Чистим null (юниты могли быть Destroy)
        CleanupNullUnits();

        OnPhaseEnded?.Invoke(currentPhase);

        // Трупы: тик каждый конец фазы
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u == null) continue;
            u.OnPhaseEnded();
        }

        // Если сейчас заканчивается Enemy-фаза, значит закончился полный раунд
        if (currentPhase == Phase.Enemy)
        {
            _roundIndex++;
            if (debugLogs) Debug.Log($"[Turn] RoundEnded {_roundIndex}");
            OnRoundEnded?.Invoke(_roundIndex);
        }

        // переключаем фазу
        currentPhase = (currentPhase == Phase.Player) ? Phase.Enemy : Phase.Player;
        BeginPhase(currentPhase);
    }

    private void BeginPhase(Phase phase)
    {
        if (!_started) return;

        if (debugLogs) Debug.Log($"[Turn] BeginPhase {phase}");

        CleanupNullUnits();

        // сброс ходовых ресурсов для стороны
        for (int i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u == null) continue;
            if (!u.IsAlive) continue;

            if (phase == Phase.Player && u.Team == UnitV2.Faction.Player) u.ResetTurn();
            if (phase == Phase.Enemy && u.Team == UnitV2.Faction.Enemy) u.ResetTurn();
        }

        OnPhaseStarted?.Invoke(phase);
    }

    private void CleanupNullUnits()
    {
        for (int i = _units.Count - 1; i >= 0; i--)
        {
            if (_units[i] == null) _units.RemoveAt(i);
        }
    }
}
