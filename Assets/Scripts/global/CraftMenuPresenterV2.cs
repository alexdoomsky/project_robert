using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns craft cards from UnitDefV2 list.
/// Updated: supports Open/Close/Toggle + optional close button + optional ESC close.
/// </summary>
public sealed class CraftMenuPresenterV2 : MonoBehaviour
{
    [Header("Data")]
    public List<UnitDefV2> defs = new List<UnitDefV2>();

    [Header("UI")]
    public Transform combatContentRoot;
    public Transform dronesContentRoot;
    public UnitCardViewV2 cardPrefab;

    [Header("Open/Close")]
    [Tooltip("Optional: button inside the craft menu to close it.")]
    public Button closeButton;

    [Tooltip("If true, ESC closes the craft menu.")]
    public bool closeOnEsc = true;

    private RunStateV2 _state;
    private readonly List<UnitCardViewV2> _cards = new List<UnitCardViewV2>();
    private bool _builtOnce;

    private void Awake()
    {
        _state = RunStateV2.Instance;
        if (_state == null)
            _state = FindObjectOfType<RunStateV2>();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Update()
    {
        if (!closeOnEsc) return;
        if (!gameObject.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void OnEnable()
    {
        // Build only once, then just refresh on reopen.
        if (!_builtOnce)
        {
            Build();
            _builtOnce = true;
        }

        if (_state != null)
            _state.OnChanged += RefreshAll;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (_state != null)
            _state.OnChanged -= RefreshAll;
    }

    public void Open() => gameObject.SetActive(true);

    public void Close() => gameObject.SetActive(false);

    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    public void Build()
    {
        Clear();
        if (cardPrefab == null) return;
        if (combatContentRoot == null && dronesContentRoot == null) return;

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def == null) continue;

            Transform root = def.category == UnitCategoryV2.Drone ? dronesContentRoot : combatContentRoot;
            if (root == null) continue;

            var card = Instantiate(cardPrefab, root);
            card.name = $"Card_{def.id}";
            card.Bind(def, _state);
            _cards.Add(card);
        }
    }

    public void RefreshAll()
    {
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i] != null)
                _cards[i].Refresh();
    }

    public void Clear()
    {
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i] != null)
                Destroy(_cards[i].gameObject);
        _cards.Clear();
    }
}
