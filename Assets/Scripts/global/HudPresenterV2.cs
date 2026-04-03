using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD binding: HP, heals, materials, drones.
/// Unity 6: uses TextMeshPro (TMP_Text).
/// Also provides buttons to open/close craft menu and to start combat scene for tests.
/// </summary>
public sealed class HudPresenterV2 : MonoBehaviour
{
    [Header("Bindings")]
    public TMP_Text hpText;
    public TMP_Text materialsText;
    public TMP_Text dronesText;
    public TMP_Text healsText;

    [Header("Buttons")]
    public Button healButton;

    [Header("Craft Menu")]
    [Tooltip("HUD button that toggles Craft menu.")]
    public Button craftMenuButton;
    [Tooltip("Craft menu panel presenter. If null, will try FindObjectOfType (including inactive).")]
    public CraftMenuPresenterV2 craftMenu;

    [Header("Combat Test")]
    [Tooltip("Optional button to load combat scene for tests.")]
    public Button combatTestButton;
    [Tooltip("If SceneRouterV2 exists, it will be used. Otherwise this scene name is used.")]
    public string combatSceneName = "CombatScene";

    private RunStateV2 _state;
    private SceneRouterV2 _router;

    private void Awake()
    {
        _state = RunStateV2.Instance;
        if (_state == null)
            _state = FindObjectOfType<RunStateV2>();

        if (craftMenu == null)
            craftMenu = FindObjectOfType<CraftMenuPresenterV2>(true);

        _router = SceneRouterV2.Instance;
        if (_router == null)
            _router = FindObjectOfType<SceneRouterV2>(true);

        if (healButton != null)
        {
            healButton.onClick.RemoveAllListeners();
            healButton.onClick.AddListener(OnHealClicked);
        }

        if (craftMenuButton != null)
        {
            craftMenuButton.onClick.RemoveAllListeners();
            craftMenuButton.onClick.AddListener(OnCraftMenuClicked);
        }

        if (combatTestButton != null)
        {
            combatTestButton.onClick.RemoveAllListeners();
            combatTestButton.onClick.AddListener(OnCombatTestClicked);
        }
    }

    private void OnEnable()
    {
        if (_state != null)
            _state.OnChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (_state != null)
            _state.OnChanged -= Refresh;
    }

    private void OnHealClicked()
    {
        if (_state == null) return;
        _state.TryHeal();
    }

    private void OnCraftMenuClicked()
    {
        if (craftMenu == null) return;
        craftMenu.Toggle();
    }

    private void OnCombatTestClicked()
    {
        // Prefer SceneRouter, because later it will remember return scene.
        if (_router != null)
        {
            _router.combatSceneName = string.IsNullOrWhiteSpace(_router.combatSceneName) ? combatSceneName : _router.combatSceneName;
            _router.LoadCombatTest();
            return;
        }

        if (string.IsNullOrWhiteSpace(combatSceneName)) return;
        UnityEngine.SceneManagement.SceneManager.LoadScene(combatSceneName);
    }

    public void Refresh()
    {
        if (_state == null) return;

        if (hpText != null)
            hpText.text = $"{_state.currentHp}/{_state.maxHp}";

        if (materialsText != null)
            materialsText.text = $"{_state.materials}";

        if (dronesText != null)
            dronesText.text = $"{_state.drones}";

        if (healsText != null)
            healsText.text = $"{_state.healCharges}";

        if (healButton != null)
            healButton.interactable = _state.healCharges > 0 && _state.currentHp < _state.maxHp;
    }
}
