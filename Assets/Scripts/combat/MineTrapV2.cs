using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class MineTrapV2 : MonoBehaviour, IDamageableV2
{
    [Header("Setup")]
    [SerializeField] private HexGridV2 grid;
    [SerializeField] private TurnManagerV2 turnManager;

    [Header("Mine")]
    [SerializeField] private int turnsToExplode = 2; // ПОЛНЫЕ РАУНДЫ (RoundEnded)
    [SerializeField] private int damage = 4;
    [SerializeField] private int explosionRange = 1;

    [Header("Reveal")]
    [SerializeField] private GameObject visualsRoot;   // модель/визуал, выключаем пока не активирована
    [SerializeField] private Collider triggerCollider; // должен быть isTrigger = true

    [Header("UI - Timer")]
    [SerializeField] private GameObject timerRoot; // иконка/контейнер таймера (опционально)
    [SerializeField] private TMP_Text countdownText; // текст оставшихся раундов (опционально)

    public HexCellV2 CurrentCell { get; private set; }

    public int ExplosionRange => explosionRange;

    public bool IsAlive => true;

    private bool _armed;
    private int _roundsLeft;
    private bool _isExplodingOrExploded;

    // раскрыта ли мина для игрока (визуал + таймер)
    private bool _revealedToPlayer;

    public void PlaceOnCell(HexCellV2 cell)
    {
        if (cell == null) return;

        CurrentCell = cell;

        // мина не блокирует движение, но занимает "Occupant" как препятствие для спавна/логики
        cell.TrySetOccupant(this, false);

        transform.position = cell.transform.position;

        _armed = false;
        _roundsLeft = 0;
        _isExplodingOrExploded = false;
        _revealedToPlayer = false;

        if (visualsRoot != null) visualsRoot.SetActive(false);
        UpdateCountdownUI();
    }

    private void Start()
    {
        if (grid == null) grid = FindObjectOfType<HexGridV2>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();

        if (turnManager != null)
            turnManager.OnRoundEnded += OnRoundEnded;

        if (triggerCollider == null)
            triggerCollider = GetComponentInChildren<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        UpdateCountdownUI();
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.OnRoundEnded -= OnRoundEnded;

        if (CurrentCell != null)
            CurrentCell.ClearOccupant(this);
    }

    private void UpdateCountdownUI()
    {
        // таймер показываем только если мина раскрыта игроку
        if (timerRoot != null)
            timerRoot.SetActive(_armed && !_isExplodingOrExploded && _revealedToPlayer);

        if (countdownText != null)
            countdownText.text = Mathf.Max(0, _roundsLeft).ToString();
    }

    private void RevealMineToPlayer()
    {
        if (_revealedToPlayer) return;

        _revealedToPlayer = true;

        if (visualsRoot != null)
            visualsRoot.SetActive(true);

        UpdateCountdownUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isExplodingOrExploded) return;

        var unit = other.GetComponentInParent<UnitV2>();
        if (unit == null) return;

        // FIX: если мина уже взведена врагом, но игрок зашёл позже — надо раскрыть
        if (_armed)
        {
            if (!_revealedToPlayer && unit.Team == UnitV2.Faction.Player)
                RevealMineToPlayer();

            return;
        }

        // первое взведение
        _armed = true;
        _roundsLeft = turnsToExplode;

        // раскрываем ТОЛЬКО если активировал игрок
        if (unit.Team == UnitV2.Faction.Player)
            RevealMineToPlayer();
        else
            UpdateCountdownUI();

        Debug.Log($"[Mine] Armed by {unit.name} (team={unit.Team}) at ({CurrentCell.Col},{CurrentCell.Row}) => {_roundsLeft} rounds (revealed={_revealedToPlayer})");
    }

    private void OnRoundEnded(int roundIndex)
    {
        if (!_armed) return;
        if (_isExplodingOrExploded) return;

        _roundsLeft--;
        UpdateCountdownUI();

        if (_roundsLeft <= 0)
            Explode();
    }

    private void Explode()
    {
        if (_isExplodingOrExploded) return;
        _isExplodingOrExploded = true;
        UpdateCountdownUI();

        if (grid == null || CurrentCell == null)
        {
            Destroy(gameObject);
            return;
        }

        List<HexCellV2> cells = PathfinderV2.GetCellsInRange(CurrentCell, explosionRange, grid);

        foreach (var c in cells)
        {
            if (c == null) continue;

            if (c.Occupant is IDamageableV2 dmgObj)
            {
                if (ReferenceEquals(dmgObj, this)) continue;
                dmgObj.TakeDamage(damage, null);
            }
        }

        Destroy(gameObject);
    }

    public void TakeDamage(int damageAmount, UnitV2 attacker = null)
    {
        if (_isExplodingOrExploded) return;
        Explode();
    }
}
