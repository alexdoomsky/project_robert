using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;
    public GameObject compendiumPanel;
    // Добавьте другие панели по необходимости

    [Header("Scene Names")]
    public string gameSceneName;
    public string escapeSceneName;

    void Start()
    {
        ReturnToMainMenu(); // Активируем главное меню при старте
    }

    void Update()
    {
        // Обработка нажатия ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LoadEscapeScene();
        }
    }

    // Методы для кнопок
    public void OpenPanel(GameObject panelToOpen)
    {
        // Сначала деактивируем все панели
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        compendiumPanel.SetActive(false);
        // Добавьте другие панели...

        // Затем активируем нужную
        panelToOpen.SetActive(true);
    }

    public void ReturnToMainMenu()
    {
        OpenPanel(mainMenuPanel);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void LoadEscapeScene()
    {
        SceneManager.LoadScene(escapeSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}