using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single outcome panel (no separate victory/defeat popups).
/// Shows battle report and provides navigation buttons.
/// 
/// Wiring:
/// - outcomeRoot: parent panel that contains report UI + buttons.
/// - reportUI: optional (if you want to auto-fill report text).
/// - victorySceneName/defeatSceneName: where Continue goes depending on result.
/// </summary>
public sealed class BattleOutcomeUIV2 : MonoBehaviour
{
    [Header("UI Root")]
    [SerializeField] private GameObject outcomeRoot;

    [Header("Optional report presenter")]
    [SerializeField] private BattleReportUIV2 reportUI;

    [Header("Scene buttons (configure in inspector)")]
    [SerializeField] private string victorySceneName = "HubScene";
    [SerializeField] private string defeatSceneName = "HubScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _lastVictory;

    private void Awake()
    {
        if (reportUI == null)
            reportUI = GetComponentInChildren<BattleReportUIV2>(true);

        Hide();
    }

    public void Show(bool victory, BattleEndControllerV2.BattleReport report)
    {
        _lastVictory = victory;

        if (outcomeRoot != null)
            outcomeRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (reportUI != null)
            reportUI.Show(report);
    }

    public void Hide()
    {
        if (outcomeRoot != null)
            outcomeRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    // === Buttons ===

    /// <summary>
    /// Continue after battle. Loads victory/defeat scene depending on result.
    /// </summary>
    public void Continue()
    {
        Time.timeScale = 1f;
        LoadSceneSafe(_lastVictory ? victorySceneName : defeatSceneName);
    }

    public void RestartCurrent()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        LoadSceneSafe(mainMenuSceneName);
    }

    private static void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("BattleOutcomeUIV2: sceneName is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
