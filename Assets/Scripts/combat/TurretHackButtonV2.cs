using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurretHackButtonV2 : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject root;

    [Header("How we pick target turret")]
    [Tooltip("Если true - выбираем турель с минимальной дистанцией до любого подходящего юнита.")]
    [SerializeField] private bool pickClosestTurret = true;

    private TurnManagerV2 turnManager;

    private TurretV2 _cachedTurret; // текущая “актуальная” турель под кнопку

    private void Awake()
    {
        turnManager = FindObjectOfType<TurnManagerV2>();

        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    private void Update()
    {
        if (root == null || turnManager == null)
            return;

        _cachedTurret = FindHackableTurret(out _);
        root.SetActive(_cachedTurret != null);
    }

    private void OnClick()
    {
        if (turnManager == null) return;

        // на клик заново находим (на случай, если за 1 кадр что-то поменялось)
        var turret = FindHackableTurret(out var candidates);
        if (turret == null) return;
        if (candidates.Count == 0) return;

        // выбираем рандомного юнита с действием
        var unit = candidates[Random.Range(0, candidates.Count)];
        turret.TryHack(unit);
    }

    private TurretV2 FindHackableTurret(out List<UnitV2> eligibleUnitsForThatTurret)
    {
        eligibleUnitsForThatTurret = new List<UnitV2>();

        var turrets = FindObjectsOfType<TurretV2>();
        if (turrets == null || turrets.Length == 0) return null;

        TurretV2 bestTurret = null;
        int bestDist = int.MaxValue;
        List<UnitV2> bestList = null;

        foreach (var t in turrets)
        {
            if (t == null) continue;
            if (t.State != TurretV2.TurretState.Dormant) continue;
            if (t.CurrentCell == null) continue;

            var list = GatherEligibleUnitsForTurret(t);
            if (list.Count == 0) continue;

            if (!pickClosestTurret)
            {
                eligibleUnitsForThatTurret = list;
                return t;
            }

            // считаем “насколько близко” лучший юнит к этой турели
            int localBest = int.MaxValue;
            foreach (var u in list)
            {
                if (u == null || u.CurrentCell == null) continue;
                int d = CombatSystemV2.HexDistance(u.CurrentCell, t.CurrentCell);
                if (d < localBest) localBest = d;
            }

            if (localBest < bestDist)
            {
                bestDist = localBest;
                bestTurret = t;
                bestList = list;
            }
        }

        if (bestTurret != null && bestList != null)
            eligibleUnitsForThatTurret = bestList;

        return bestTurret;
    }

    private List<UnitV2> GatherEligibleUnitsForTurret(TurretV2 turret)
    {
        var result = new List<UnitV2>();
        var units = turnManager.Units;

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (!u.IsAlive) continue;
            if (u.Team != UnitV2.Faction.Player) continue;
            if (u.ActionsLeft <= 0) continue;
            if (u.CurrentCell == null) continue;

            // используем правила турели (контроль-радиус и т.д.)
            if (turret.CanBeHackedBy(u))
                result.Add(u);
        }

        return result;
    }
}
