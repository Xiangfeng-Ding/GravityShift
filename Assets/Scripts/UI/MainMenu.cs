using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// MainMenu handles main menu interactions
/// Start game, settings, difficulty selection, quit
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private GameObject settingsPanel;
    
    [Header("Difficulty Buttons")]
    [SerializeField] private TextMeshProUGUI easyButtonText;
    [SerializeField] private TextMeshProUGUI normalButtonText;
    [SerializeField] private TextMeshProUGUI hardButtonText;
    
    void Start()
    {
        // Show main panel
        ShowMainPanel();
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Ensure time scale is normal
        Time.timeScale = 1f;
        
        // Update text
        UpdateDifficultyText();
    }
    
    /// <summary>
    /// Show main panel
    /// </summary>
    public void ShowMainPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);
        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
    
    /// <summary>
    /// Show difficulty selection panel
    /// </summary>
    public void ShowDifficultyPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (difficultyPanel != null)
            difficultyPanel.SetActive(true);
    }
    
    /// <summary>
    /// Show settings panel
    /// </summary>
    public void ShowSettings()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }
    
    /// <summary>
    /// Start game with selected difficulty
    /// </summary>
    public void StartGame(int difficulty)
    {
        // Save difficulty setting
        PlayerPrefs.SetInt("Difficulty", difficulty);
        PlayerPrefs.Save();
        
        // Load first level
        SceneManager.LoadScene("Level1_Tutorial");
    }
    
    /// <summary>
    /// Start game with Easy difficulty
    /// </summary>
    public void StartGameEasy()
    {
        StartGame(0);
    }
    
    /// <summary>
    /// Start game with Normal difficulty
    /// </summary>
    public void StartGameNormal()
    {
        StartGame(1);
    }
    
    /// <summary>
    /// Start game with Hard difficulty
    /// </summary>
    public void StartGameHard()
    {
        StartGame(2);
    }
    
    /// <summary>
    /// Quit game
    /// </summary>
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    /// <summary>
    /// Update difficulty button text
    /// </summary>
    private void UpdateDifficultyText()
    {
        if (LanguageManager.Instance == null)
            return;
        
        if (easyButtonText != null)
            easyButtonText.text = LanguageManager.Instance.GetText("easy");
        if (normalButtonText != null)
            normalButtonText.text = LanguageManager.Instance.GetText("normal");
        if (hardButtonText != null)
            hardButtonText.text = LanguageManager.Instance.GetText("hard");
    }
}
