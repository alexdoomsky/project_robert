using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyTurnControllerV2 : MonoBehaviour
{
    [System.Serializable]
    public class BehaviorProfile
    {
        public UnitV2.EnemyArchetype archetype = UnitV2.EnemyArchetype.Generic;

        [Header("Distance preference (to nearest player)")]
        public int idealMinRange = 1;
        public int idealMaxRange = 2;

        [Header("Weights")]
        public float wKill = 10f;
        public float wDamage = 1f;
        public float wThreat = 0.25f;
        public float wDistanceBand = 0.5f;
        public float wApproach = 0.15f;
        public float wDanger = 0.5f;

        [Header("Mines")]
        public bool canUseMines = true;
        public bool canStayOnMineIfPlayerInBlast = false;
        public float wArmMineIfUseful = 2.5f;

        [Header("Search")]
        [Range(5, 80)] public int maxCandidateCells = 30;
    }

    [Header("Refs")]
    [SerializeField] private TurnManagerV2 turnManager;
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private BattleEndControllerV2 battleEndController;

    [Header("Timing")]
    [SerializeField] private float mineArmPauseSeconds = 0.6f;
    [SerializeField] private float perUnitDelaySeconds = 0.05f;

    [Header("Turn order")]
    [Tooltip("Порядок обработки архетипов в enemy-фазе. Оставшиеся (не перечисленные) идут в конец.")]
    [SerializeField]
    private List<UnitV2.EnemyArchetype> turnOrder = new()
    {
        UnitV2.EnemyArchetype.OrkCruiser,
        UnitV2.EnemyArchetype.Bulldog,
        UnitV2.EnemyArchetype.ChaosRaider,
        UnitV2.EnemyArchetype.Generic
    };

    [Header("Profiles")]
    [SerializeField] private List<BehaviorProfile> profiles = new();

    [Header("Raider reinforcements")]
    [SerializeField] private UnitV2 bulldogPrefab;
    [Min(0)][SerializeField] private int bulldogsToSpawn = 2;
    [Min(1)][SerializeField] private int reinforcementSearchRadius = 4;

    private readonly Dictionary<UnitV2.EnemyArchetype, BehaviorProfile> _profileMap = new();

    private readonly HashSet<UnitV2> _raidersThatCalledReinforcements = new();
    private readonly HashSet<UnitV2> _subscribedRaiders = new();

    private readonly Dictionary<HexCellV2, MineTrapV2> _mineByCell = new();

    private Coroutine _enemyRoutine;

    private void Awake()
    {
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (battleEndController == null) battleEndController = FindObjectOfType<BattleEndControllerV2>(true);

        RebuildProfileMap();
        RebuildMineCache();
        SubscribeToRaiders();
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.OnPhaseStarted += OnPhaseStarted;
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnPhaseStarted -= OnPhaseStarted;

        UnsubscribeAllRaiders();
    }

    private void RebuildProfileMap()
    {
        _profileMap.Clear();
        if (profiles == null) return;

        foreach (var p in profiles)
        {
            if (p == null) continue;
            _profileMap[p.archetype] = p;
        }
    }

    private BehaviorProfile GetProfile(UnitV2 unit)
    {
        if (unit == null) return null;

        if (_profileMap.Count != (profiles?.Count ?? 0))
            RebuildProfileMap();

        if (_profileMap.TryGetValue(unit.Archetype, out var p) && p != null)
            return p;

        return new BehaviorProfile
        {
            archetype = unit.Archetype,
            idealMinRange = (unit.Archetype == UnitV2.EnemyArchetype.ChaosRaider) ? 3 : 1,
            idealMaxRange = (unit.Archetype == UnitV2.EnemyArchetype.ChaosRaider) ? 6 : 2,
            canStayOnMineIfPlayerInBlast = (unit.Archetype == UnitV2.EnemyArchetype.OrkCruiser),
            canUseMines = true,
        };
    }

    private void RebuildMineCache()
    {
        _mineByCell.Clear();
        var mines = FindObjectsOfType<MineTrapV2>(true);
        foreach (var m in mines)
        {
            if (m == null) continue;
            if (m.CurrentCell == null) continue;
            _mineByCell[m.CurrentCell] = m;
        }
    }

    private void SubscribeToRaiders()
    {
        var raiders = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Enemy && u.Archetype == UnitV2.EnemyArchetype.ChaosRaider)
            .ToList();

        foreach (var r in raiders)
        {
            if (r == null) continue;
            if (_subscribedRaiders.Contains(r)) continue;

            r.OnHPChanged += OnRaiderHPChanged;
            _subscribedRaiders.Add(r);
        }
    }

    private void UnsubscribeAllRaiders()
    {
        foreach (var r in _subscribedRaiders)
        {
            if (r == null) continue;
            r.OnHPChanged -= OnRaiderHPChanged;
        }
        _subscribedRaiders.Clear();
    }

    private void OnRaiderHPChanged(UnitV2 raider)
    {
        TrySpawnRaiderReinforcementsFor(raider);
    }

    private void OnPhaseStarted(TurnManagerV2.Phase phase)
    {
        // FIX: подписки должны быть актуальны ДО того, как игрок начнёт наносить урон.
        // Значит обновляем их как минимум на Player и Enemy.
        if (phase == TurnManagerV2.Phase.Player || phase == TurnManagerV2.Phase.Enemy)
        {
            RebuildMineCache();
            SubscribeToRaiders();
        }

        if (phase != TurnManagerV2.Phase.Enemy) return;

        if (_enemyRoutine != null)
            StopCoroutine(_enemyRoutine);

        _enemyRoutine = StartCoroutine(CoRunEnemyPhase());
    }

    private IEnumerator CoRunEnemyPhase()
    {
        if (turnManager == null || grid == null)
        {
            turnManager?.EndPhase();
            yield break;
        }

        var allUnits = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Enemy)
            .ToList();

        allUnits.Sort((a, b) =>
        {
            int ia = IndexInTurnOrder(a.Archetype);
            int ib = IndexInTurnOrder(b.Archetype);
            if (ia != ib) return ia.CompareTo(ib);

            int da = DistanceToNearestPlayer(a);
            int db = DistanceToNearestPlayer(b);
            return da.CompareTo(db);
        });

        for (int i = 0; i < allUnits.Count; i++)
        {
            var enemy = allUnits[i];
            if (enemy == null || !enemy.IsAlive) continue;

            if (enemy.ActionsLeft <= 0 && enemy.MovementLeft <= 0)
                continue;

            yield return CoActWithUnit(enemy);

            if (perUnitDelaySeconds > 0f)
                yield return new WaitForSeconds(perUnitDelaySeconds);
        }

        turnManager.EndPhase();
    }

    private int IndexInTurnOrder(UnitV2.EnemyArchetype a)
    {
        int idx = (turnOrder != null) ? turnOrder.IndexOf(a) : -1;
        return idx >= 0 ? idx : 999;
    }

    private int DistanceToNearestPlayer(UnitV2 enemy)
    {
        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        if (enemy == null || enemy.CurrentCell == null || players.Count == 0) return 999;

        int best = 999;
        foreach (var p in players)
        {
            int d = CombatSystemV2.HexDistance(enemy.CurrentCell, p.CurrentCell);
            if (d < best) best = d;
        }
        return best;
    }

    private void TrySpawnRaiderReinforcementsFor(UnitV2 raider)
    {
        if (bulldogPrefab == null || bulldogsToSpawn <= 0) return;
        if (raider == null || !raider.IsAlive) return;
        if (raider.Team != UnitV2.Faction.Enemy) return;
        if (raider.Archetype != UnitV2.EnemyArchetype.ChaosRaider) return;

        if (_raidersThatCalledReinforcements.Contains(raider)) return;

        if (raider.HP <= Mathf.FloorToInt(raider.MaxHP * 0.5f))
        {
            SpawnBulldogsNear(raider);
            _raidersThatCalledReinforcements.Add(raider);
        }
    }

    private void SpawnBulldogsNear(UnitV2 raider)
    {
        if (grid == null) return;
        var origin = raider.CurrentCell;
        if (origin == null) return;

        RebuildMineCache();

        var picked = new List<HexCellV2>();
        int maxR = Mathf.Max(1, reinforcementSearchRadius);

        // 1) сначала пробуем “ближайшие кольца”
        for (int r = 1; r <= maxR && picked.Count < bulldogsToSpawn; r++)
        {
            foreach (var c in grid.GetAllCells())
            {
                if (c == null) continue;
                if (!c.walkable) continue;

                int d = CombatSystemV2.HexDistance(origin, c);
                if (d != r) continue;

                if (!IsCellFreeForBulldogSpawn(c)) continue;

                picked.Add(c);
                if (picked.Count >= bulldogsToSpawn) break;
            }
        }

        // 2) fallback: если рядом всё забито, добираем из любых клеток по дистанции
        if (picked.Count < bulldogsToSpawn)
        {
            var all = grid.GetAllCells()
                .Where(c => c != null && c.walkable && IsCellFreeForBulldogSpawn(c))
                .OrderBy(c => CombatSystemV2.HexDistance(origin, c))
                .ToList();

            foreach (var c in all)
            {
                if (picked.Count >= bulldogsToSpawn) break;
                if (picked.Contains(c)) continue;
                picked.Add(c);
            }
        }

        if (picked.Count == 0)
        {
            Debug.LogWarning("[EnemyTurnControllerV2] No free cells found for bulldog spawn.");
            return;
        }

        for (int i = 0; i < picked.Count; i++)
        {
            var cell = picked[i];
            var u = Instantiate(bulldogPrefab, transform);
            u.Init(UnitV2.Faction.Enemy);
            u.PlaceOnCell(cell);

            turnManager.RegisterUnit(u);
            battleEndController?.RegisterUnit(u);
        }
    }

    private bool IsCellFreeForBulldogSpawn(HexCellV2 cell)
    {
        if (cell == null) return false;

        if (_mineByCell.ContainsKey(cell)) return false;
        if (cell.OccupantBlocksMovement) return false;

        var allUnits = FindObjectsOfType<UnitV2>(true);
        for (int i = 0; i < allUnits.Length; i++)
        {
            var u = allUnits[i];
            if (u == null) continue;
            if (!u.IsAlive && !u.IsDestroyedCorpse) continue;
            if (u.CurrentCell == cell) return false;
        }

        return true;
    }

    private IEnumerator CoActWithUnit(UnitV2 enemy)
    {
        if (enemy == null || !enemy.IsAlive) yield break;

        var profile = GetProfile(enemy);

        HexCellV2 bestCell = enemy.CurrentCell;
        float bestScore = float.NegativeInfinity;
        HexCellV2 bestMinePauseCell = null;

        var candidates = BuildCandidateCells(enemy, profile);

        for (int i = 0; i < candidates.Count; i++)
        {
            var cell = candidates[i];
            if (cell == null) continue;

            List<HexCellV2> path = null;
            if (cell != enemy.CurrentCell)
            {
                path = PathfinderV2.FindPath(enemy.CurrentCell, cell, grid);
                if (path == null || path.Count < 2) continue;
            }

            var (_, attackScore) = EvaluateBestAttackFromCell(enemy, cell, profile);

            float score = attackScore;
            score += EvaluatePositioning(enemy, cell, profile);
            score -= EvaluateDanger(enemy, cell, profile);

            HexCellV2 minePause = null;
            if (profile.canUseMines && path != null)
            {
                minePause = FindFirstUsefulMineOnPath(path, profile);
                if (minePause != null)
                    score += profile.wArmMineIfUseful;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestMinePauseCell = minePause;
            }
        }

        if (bestCell != null && enemy.MovementLeft > 0 && bestCell != enemy.CurrentCell)
        {
            var path = PathfinderV2.FindPath(enemy.CurrentCell, bestCell, grid);
            if (path != null && path.Count >= 2)
            {
                if (bestMinePauseCell != null && mineArmPauseSeconds > 0f)
                    enemy.SetAIPauseCells(new HashSet<HexCellV2> { bestMinePauseCell }, mineArmPauseSeconds);
                else
                    enemy.SetAIPauseCells(null, 0f);

                enemy.StartMoveAlongPath(path);
                while (enemy.IsMoving)
                    yield return null;
            }
        }

        if (enemy.IsAlive && enemy.ActionsLeft > 0 && enemy.CurrentCell != null)
        {
            var (target, _) = EvaluateBestAttackFromCell(enemy, enemy.CurrentCell, profile);
            if (target != null)
                CombatSystemV2.TryPerformAttack(enemy, target, target.CurrentCell, out _);
        }
    }

    private List<HexCellV2> BuildCandidateCells(UnitV2 enemy, BehaviorProfile profile)
    {
        var res = new List<HexCellV2>();
        if (enemy == null || enemy.CurrentCell == null || grid == null) return res;

        res.Add(enemy.CurrentCell);

        int mp = Mathf.Max(0, enemy.MovementLeft);
        if (mp <= 0) return res;

        var reachable = PathfinderV2.GetReachableCells(enemy.CurrentCell, mp, grid);
        if (reachable == null || reachable.Count == 0) return res;

        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        if (players.Count == 0)
            return reachable.Take(profile.maxCandidateCells).ToList();

        UnitV2 nearest = players.OrderBy(p => CombatSystemV2.HexDistance(enemy.CurrentCell, p.CurrentCell)).FirstOrDefault();

        reachable.Sort((a, b) =>
        {
            int da = CombatSystemV2.HexDistance(a, nearest.CurrentCell);
            int db = CombatSystemV2.HexDistance(b, nearest.CurrentCell);

            int ba = BandPenalty(da, profile);
            int bb = BandPenalty(db, profile);

            int sa = ba * 100 + da;
            int sb = bb * 100 + db;
            return sa.CompareTo(sb);
        });

        int take = Mathf.Clamp(profile.maxCandidateCells, 5, 80);
        res.AddRange(reachable.Take(take));
        return res.Distinct().ToList();
    }

    private int BandPenalty(int distToPlayer, BehaviorProfile profile)
    {
        if (distToPlayer < profile.idealMinRange) return profile.idealMinRange - distToPlayer;
        if (distToPlayer > profile.idealMaxRange) return distToPlayer - profile.idealMaxRange;
        return 0;
    }

    private (UnitV2 target, float score) EvaluateBestAttackFromCell(UnitV2 enemy, HexCellV2 fromCell, BehaviorProfile profile)
    {
        if (enemy == null || fromCell == null) return (null, 0f);

        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        UnitV2 best = null;
        float bestScore = 0f;

        foreach (var p in players)
        {
            int dist = CombatSystemV2.HexDistance(fromCell, p.CurrentCell);
            if (dist > enemy.AttackRange) continue;

            float s = 0f;

            int dmg = enemy.Damage;
            int eff = Mathf.Min(dmg, p.HP);

            s += eff * profile.wDamage;

            if (dmg >= p.HP)
                s += profile.wKill;

            s += (p.Damage + p.AttackRange) * profile.wThreat;

            if (s > bestScore)
            {
                bestScore = s;
                best = p;
            }
        }

        return (best, bestScore);
    }

    private float EvaluatePositioning(UnitV2 enemy, HexCellV2 cell, BehaviorProfile profile)
    {
        if (enemy == null || cell == null) return 0f;

        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        if (players.Count == 0) return 0f;

        int nearest = 999;
        foreach (var p in players)
        {
            int d = CombatSystemV2.HexDistance(cell, p.CurrentCell);
            if (d < nearest) nearest = d;
        }

        int bandPenalty = BandPenalty(nearest, profile);
        float score = -bandPenalty * profile.wDistanceBand;

        score += Mathf.Max(0, 10 - nearest) * profile.wApproach;

        return score;
    }

    private float EvaluateDanger(UnitV2 enemy, HexCellV2 cell, BehaviorProfile profile)
    {
        if (enemy == null || cell == null) return 0f;

        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        if (players.Count == 0) return 0f;

        float danger = 0f;
        foreach (var p in players)
        {
            int reach = p.MaxMovePerTurn + p.AttackRange;
            int d = CombatSystemV2.HexDistance(p.CurrentCell, cell);
            if (d <= reach)
                danger += p.Damage;
        }

        return danger * profile.wDanger * 0.1f;
    }

    private HexCellV2 FindFirstUsefulMineOnPath(List<HexCellV2> path, BehaviorProfile profile)
    {
        if (path == null || path.Count < 2) return null;
        if (profile == null || !profile.canUseMines) return null;

        RebuildMineCache();

        for (int i = 1; i < path.Count; i++)
        {
            var c = path[i];
            if (c == null) continue;

            if (_mineByCell.TryGetValue(c, out var mine) && mine != null)
            {
                bool playerInBlast = IsAnyPlayerInBlastRadius(c, mine);

                if (profile.canStayOnMineIfPlayerInBlast && !playerInBlast)
                    continue;

                return c;
            }
        }

        return null;
    }

    private bool IsAnyPlayerInBlastRadius(HexCellV2 mineCell, MineTrapV2 mine)
    {
        if (mineCell == null || mine == null) return false;

        int range = mine.ExplosionRange;

        var players = FindObjectsOfType<UnitV2>(true)
            .Where(u => u != null && u.IsAlive && u.Team == UnitV2.Faction.Player && u.CurrentCell != null)
            .ToList();

        foreach (var p in players)
        {
            int d = CombatSystemV2.HexDistance(mineCell, p.CurrentCell);
            if (d <= range) return true;
        }
        return false;
    }
}
