using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitMovementControllerV2 : MonoBehaviour
{
    public static UnitMovementControllerV2 Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private TurnManagerV2 turnManager;

    [Header("Layers")]
    [SerializeField] private LayerMask hexLayerMask = ~0;
    [SerializeField] private LayerMask unitLayerMask = ~0;      // сюда должны входить и юниты, и турели
    [SerializeField] private LayerMask obstacleLayerMask = ~0;

    [Header("Highlight Colors (Inspector)")]
    [SerializeField] private Color reachableColor = Color.green;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color pathColor = Color.cyan;
    [SerializeField] private Color attackRangeColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Header("Ability Highlight")]
    [SerializeField] private Color abilityRangeColor = new Color(0.7f, 0.4f, 1f, 0.8f);

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    public UnitV2 SelectedUnit => selectedUnit;
    public TurretV2 SelectedTurret => selectedTurret;

    private UnitV2 selectedUnit;
    private TurretV2 selectedTurret;

    private HexCellV2 hoveredCell;
    private HexCellV2 lastHoveredCell;

    private readonly HashSet<HexCellV2> reachable = new();
    private readonly HashSet<HexCellV2> attackCells = new();
    private readonly HashSet<HexCellV2> turretAttackCells = new();
    private readonly List<HexCellV2> pathPreview = new();
    private readonly HashSet<HexCellV2> highlightedLastFrame = new();

    // === ability targeting (только для юнита) ===
    private bool abilityTargeting;
    private AbilityDefV2 activeAbility;
    private readonly HashSet<HexCellV2> abilityCells = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MoveCtrl] Duplicate UnitMovementControllerV2 found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (gameCamera == null) gameCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();

        if (grid != null && !grid.IsReady)
            grid.OnGridReady += OnGridReady;
        else
            OnGridReady();
    }

    private void OnGridReady()
    {
        if (debugLogs) Debug.Log("[MoveCtrl] Grid ready");
        RebuildHighlights();
    }

    private void Update()
    {
        if (grid == null || !grid.IsReady) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI()) return;

            if (abilityTargeting)
                HandleAbilityLeftClick();
            else
                HandleLeftClick();
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (IsPointerOverUI()) return;

            if (abilityTargeting)
                CancelAbilityTargeting();
            else
                HandleRightClick();
        }

        UpdateHoveredCellAndPathPreview();
    }

    // === API для UI (AbilityPanelV2) ===
    public void RequestUseAbility(AbilityDefV2 ability)
    {
        if (ability == null) return;
        BeginAbilityTargeting(ability);
    }

    public void BeginAbilityTargeting(AbilityDefV2 ability)
    {
        if (selectedUnit == null) return;
        if (ability == null) return;

        // если вдруг была выбрана турель, способности не для неё
        selectedTurret = null;

        // мгновенные режимы
        if (ability.targetMode == AbilityTargetModeV2.None || ability.targetMode == AbilityTargetModeV2.Self)
        {
            selectedUnit.UseAbility(ability, null, selectedUnit);
            RebuildHighlights();
            return;
        }

        if (!selectedUnit.CanUseAbility(ability)) return;

        abilityTargeting = true;
        activeAbility = ability;
        RebuildHighlights();
    }

    public void CancelAbilityTargeting()
    {
        abilityTargeting = false;
        activeAbility = null;
        abilityCells.Clear();
        RebuildHighlights();
    }

    private void HandleAbilityLeftClick()
    {
        if (selectedUnit == null || activeAbility == null)
        {
            CancelAbilityTargeting();
            return;
        }

        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        // 1) target unit (например хил на союзника)
        if (Physics.Raycast(ray, out var hitU, 1000f, unitLayerMask))
        {
            var unit = hitU.collider.GetComponentInParent<UnitV2>();
            if (unit != null)
            {
                if (activeAbility.targetMode == AbilityTargetModeV2.AllyUnit)
                {
                    if (unit.Team != selectedUnit.Team) return;
                    if (!IsInAbilityRange(selectedUnit, unit.CurrentCell, activeAbility)) return;

                    selectedUnit.UseAbility(activeAbility, null, unit);
                    CancelAbilityTargeting();
                    return;
                }
            }
        }

        // 2) target cell (артиллерия)
        if (Physics.Raycast(ray, out var hitH, 1000f, hexLayerMask))
        {
            var cell = hitH.collider.GetComponentInParent<HexCellV2>();
            if (cell == null) return;

            if (activeAbility.targetMode == AbilityTargetModeV2.Cell)
            {
                if (!IsInAbilityRange(selectedUnit, cell, activeAbility)) return;

                selectedUnit.UseAbility(activeAbility, cell, null);
                CancelAbilityTargeting();
                return;
            }
        }
    }

    private bool IsInAbilityRange(UnitV2 caster, HexCellV2 cell, AbilityDefV2 ability)
    {
        if (caster == null || caster.CurrentCell == null || cell == null || ability == null) return false;
        int d = CombatSystemV2.HexDistance(caster.CurrentCell, cell);
        return d >= ability.minRange && d <= ability.maxRange;
    }

    private void UpdateHoveredCellAndPathPreview()
    {
        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        HexCellV2 newHovered = null;
        if (Physics.Raycast(ray, out var hitH, 1000f, hexLayerMask))
            newHovered = hitH.collider.GetComponentInParent<HexCellV2>();

        if (newHovered == hoveredCell) return;

        hoveredCell = newHovered;

        // если hover реально поменялся - обновляем предпросмотр пути
        if (hoveredCell != lastHoveredCell)
        {
            lastHoveredCell = hoveredCell;
            UpdatePathPreviewFromHover();
            RebuildHighlights();
        }
    }

    private void UpdatePathPreviewFromHover()
    {
        pathPreview.Clear();

        if (abilityTargeting) return; // в режиме таргетинга абилки путь не нужен
        if (selectedUnit == null) return;
        if (!CanControl(selectedUnit)) return;
        if (selectedUnit.IsMoving) return;
        if (selectedUnit.CurrentCell == null) return;
        if (hoveredCell == null) return;

        // подсвечиваем путь только если клетка реально достижима
        if (!reachable.Contains(hoveredCell)) return;

        var path = PathfinderV2.FindPath(selectedUnit.CurrentCell, hoveredCell, grid);
        if (path == null || path.Count == 0) return;

        // не красим стартовую клетку как "путь", только шаги
        for (int i = 1; i < path.Count; i++)
            if (path[i] != null) pathPreview.Add(path[i]);
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool CanControl(UnitV2 unit)
    {
        if (unit == null || turnManager == null) return false;
        if (turnManager.CurrentPhase != TurnManagerV2.Phase.Player) return false;
        return unit.Team == UnitV2.Faction.Player;
    }

    private bool CanControlTurret(TurretV2 turret)
    {
        if (turret == null || turnManager == null) return false;
        if (turnManager.CurrentPhase != TurnManagerV2.Phase.Player) return false;
        return turret.State == TurretV2.TurretState.Controlled && turret.IsAlive;
    }

    private void HandleLeftClick()
    {
        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        // 1) клик по юниту/турели => select
        if (Physics.Raycast(ray, out var hitU, 1000f, unitLayerMask))
        {
            var unit = hitU.collider.GetComponentInParent<UnitV2>();
            if (unit != null && CanControl(unit))
            {
                SelectUnit(unit);
                return;
            }

            var turret = hitU.collider.GetComponentInParent<TurretV2>();
            if (turret != null)
            {
                SelectTurret(turret);
                return;
            }
        }

        // 2) клик по клетке => move (только если выбран юнит)
        if (selectedUnit != null && CanControl(selectedUnit) && !selectedUnit.IsMoving)
        {
            if (Physics.Raycast(ray, out var hitH, 1000f, hexLayerMask))
            {
                var cell = hitH.collider.GetComponentInParent<HexCellV2>();
                if (cell == null) return;

                if (reachable.Contains(cell))
                {
                    var path = PathfinderV2.FindPath(selectedUnit.CurrentCell, cell, grid);
                    if (path != null)
                    {
                        selectedUnit.StartMoveAlongPath(path);
                        // после старта движения путь на hover уже не нужен
                        pathPreview.Clear();
                        RebuildHighlights();
                    }
                    return;
                }
            }
        }

        // пустой клик
        ClearSelection();
    }

    private void HandleRightClick()
    {
        // если выбрана турель - пытаемся стрелять турелью
        if (selectedTurret != null)
        {
            HandleRightClickTurretFire();
            return;
        }

        // иначе обычная атака юнитом
        HandleRightClickAttack();
    }

    private void HandleRightClickTurretFire()
    {
        if (!CanControlTurret(selectedTurret)) return;

        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        // стреляем по вражескому юниту
        if (Physics.Raycast(ray, out var hitU, 1000f, unitLayerMask))
        {
            var targetUnit = hitU.collider.GetComponentInParent<UnitV2>();
            if (targetUnit != null)
            {
                if (targetUnit.Team == UnitV2.Faction.Player) return;

                var cell = targetUnit.CurrentCell;
                if (cell == null) return;

                if (selectedTurret.TryFireAt(targetUnit, cell, out var reason))
                {
                    if (debugLogs) Debug.Log("[Turret] Fire OK");
                    RebuildHighlights();
                }
                else
                {
                    if (debugLogs) Debug.Log($"[Turret] Fire FAIL: {reason}");
                }
                return;
            }
        }
    }

    private void HandleRightClickAttack()
    {
        if (selectedUnit == null) return;
        if (!CanControl(selectedUnit)) return;
        if (selectedUnit.IsMoving) return;
        if (!selectedUnit.CanAct) return;

        Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);

        // 1) юнит
        if (Physics.Raycast(ray, out var hitU, 1000f, unitLayerMask))
        {
            var targetUnit = hitU.collider.GetComponentInParent<UnitV2>();
            if (targetUnit != null && targetUnit != selectedUnit)
            {
                if (targetUnit.Team == selectedUnit.Team) return;

                if (CombatSystemV2.TryPerformAttack(selectedUnit, targetUnit, targetUnit.CurrentCell, out var reason))
                {
                    if (debugLogs) Debug.Log($"[Attack] OK vs unit {targetUnit.name}");
                    RebuildHighlights();
                }
                else
                {
                    if (debugLogs) Debug.Log($"[Attack] FAIL: {reason}");
                }
                return;
            }
        }

        // 2) obstacle / damageable
        if (Physics.Raycast(ray, out var hitO, 1000f, obstacleLayerMask))
        {
            var obstacle = hitO.collider.GetComponentInParent<AsteroidObstacleV2>();
            var dmg = hitO.collider.GetComponentInParent<IDamageableV2>();

            HexCellV2 cell = obstacle != null ? obstacle.CurrentCell : null;
            if (dmg != null && cell != null)
            {
                if (CombatSystemV2.TryPerformAttack(selectedUnit, dmg, cell, out var reason))
                {
                    if (debugLogs) Debug.Log($"[Attack] OK vs obstacle {hitO.collider.name}");
                    RebuildHighlights();
                }
                else
                {
                    if (debugLogs) Debug.Log($"[Attack] FAIL vs obstacle: {reason}");
                }
            }
        }
    }

    private void SelectUnit(UnitV2 unit)
    {
        selectedUnit = unit;
        selectedTurret = null;

        abilityTargeting = false;
        activeAbility = null;

        if (debugLogs) Debug.Log($"[MoveCtrl] Selected {unit.name} ({unit.Team})");

        // при смене выбора пересчитаем path preview по текущему hover
        UpdatePathPreviewFromHover();
        RebuildHighlights();
    }

    private void SelectTurret(TurretV2 turret)
    {
        selectedTurret = turret;
        selectedUnit = null;

        abilityTargeting = false;
        activeAbility = null;

        pathPreview.Clear();

        if (debugLogs) Debug.Log($"[MoveCtrl] Selected turret {turret.name} ({turret.State})");
        RebuildHighlights();
    }

    private void ClearSelection()
    {
        selectedUnit = null;
        selectedTurret = null;

        abilityTargeting = false;
        activeAbility = null;

        pathPreview.Clear();

        RebuildHighlights();
    }

    private void RebuildHighlights()
    {
        foreach (var c in highlightedLastFrame)
            if (c != null) c.ResetHighlight();
        highlightedLastFrame.Clear();

        reachable.Clear();
        attackCells.Clear();
        turretAttackCells.Clear();
        abilityCells.Clear();

        // === Selected Unit highlights ===
        if (selectedUnit != null && selectedUnit.CurrentCell != null)
        {
            var reach = PathfinderV2.GetReachableCells(selectedUnit.CurrentCell, selectedUnit.MovementLeft, grid);
            foreach (var c in reach)
                if (c != null) reachable.Add(c);

            var atk = PathfinderV2.GetCellsInRange(selectedUnit.CurrentCell, selectedUnit.AttackRange, grid);
            foreach (var c in atk)
                if (c != null) attackCells.Add(c);

            if (abilityTargeting && activeAbility != null)
            {
                foreach (var c in grid.GetAllCells())
                {
                    if (c == null) continue;
                    int d = CombatSystemV2.HexDistance(selectedUnit.CurrentCell, c);
                    if (d >= activeAbility.minRange && d <= activeAbility.maxRange)
                        abilityCells.Add(c);
                }
            }

            foreach (var c in grid.GetAllCells())
            {
                if (c == null) continue;

                if (c == selectedUnit.CurrentCell)
                    c.SetHighlight(selectedColor);
                else if (pathPreview.Contains(c))
                    c.SetHighlight(pathColor);
                else if (abilityCells.Contains(c))
                    c.SetHighlight(abilityRangeColor);
                else if (attackCells.Contains(c))
                    c.SetHighlight(attackRangeColor);
                else if (reachable.Contains(c))
                    c.SetHighlight(reachableColor);
                else
                    continue;

                highlightedLastFrame.Add(c);
            }

            return;
        }

        // === Selected Turret highlights ===
        if (selectedTurret != null)
        {
            if (selectedTurret.CurrentCell != null)
            {
                var atk = PathfinderV2.GetCellsInRange(selectedTurret.CurrentCell, selectedTurret.AttackRange, grid);
                foreach (var c in atk)
                    if (c != null) turretAttackCells.Add(c);
            }

            foreach (var c in grid.GetAllCells())
            {
                if (c == null) continue;

                if (selectedTurret.CurrentCell != null && c == selectedTurret.CurrentCell)
                    c.SetHighlight(selectedColor);
                else if (turretAttackCells.Contains(c))
                    c.SetHighlight(attackRangeColor);
                else
                    continue;

                highlightedLastFrame.Add(c);
            }
        }
    }
}
