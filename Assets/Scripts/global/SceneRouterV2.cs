using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Minimal scene router for demo flow.
/// - Map -> LocalZone
/// - LocalZone -> (optional Combat) -> Map
/// - Can start Combat from other scenes too.
/// </summary>
public sealed class SceneRouterV2 : MonoBehaviour
{
    public static SceneRouterV2 Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Scene with the node-map UI.")]
    public string mapSceneName = "SystemMapScene";

    [Tooltip("Scene with local exploration for a selected node.")]
    public string localZoneSceneName = "LocalZoneScene";

    [Tooltip("Scene with combat (your existing battle scene).")]
    public string combatSceneName = "CombatScene";

    [Header("Runtime")]
    [SerializeField] private string returnSceneName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Back-compat for older HUD buttons.
    /// </summary>
    public void LoadCombatTest()
    {
        LoadCombat();
    }

    /// <summary>
    /// Loads the LocalZone scene (exploration inside a node).
    /// </summary>
    public void LoadLocalZone()
    {
        if (string.IsNullOrWhiteSpace(localZoneSceneName))
        {
            Debug.LogError("SceneRouterV2: localZoneSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(localZoneSceneName);
    }

    /// <summary>
    /// Loads combat and remembers where to return after combat.
    /// </summary>
    public void LoadCombat(string returnToSceneName = null)
    {
        if (string.IsNullOrWhiteSpace(combatSceneName))
        {
            Debug.LogError("SceneRouterV2: combatSceneName is empty.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(returnToSceneName))
            returnSceneName = returnToSceneName;
        else
            RememberReturnScene();

        SceneManager.LoadScene(combatSceneName);
    }

    public void ReturnFromCombat()
    {
        string target = string.IsNullOrWhiteSpace(returnSceneName) ? mapSceneName : returnSceneName;
        if (string.IsNullOrWhiteSpace(target))
        {
            Debug.LogError("SceneRouterV2: cannot return, target scene name is empty.");
            return;
        }
        SceneManager.LoadScene(target);
    }

    public void LoadMap()
    {
        if (string.IsNullOrWhiteSpace(mapSceneName))
        {
            Debug.LogError("SceneRouterV2: mapSceneName is empty.");
            return;
        }
        SceneManager.LoadScene(mapSceneName);
    }

    private void RememberReturnScene()
    {
        var active = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(active) && active != combatSceneName)
            returnSceneName = active;
    }
}
