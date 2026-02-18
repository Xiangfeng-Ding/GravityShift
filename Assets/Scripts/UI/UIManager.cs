using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UIManager handles all UI updates and interactions
/// Manages HUD, menus, and UI elements
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("HUD Elements")]
    [SerializeField] private Slider energyBar;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private TextMeshProUGUI crystalText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI gravityDirectionText;
    
    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI startButtonText;
    [SerializeField] private TextMeshProUGUI settingsButtonText;
    [SerializeField] private TextMeshProUGUI quitButtonText;
    
    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private TextMeshProUGUI pausedText;
    [SerializeField] private TextMeshProUGUI resumeButtonText;
    [SerializeField] private TextMeshProUGUI restartButtonText;
    [SerializeField] private TextMeshProUGUI mainMenuButtonText;
    
    [Header("End Level Panel")]
    [SerializeField] private GameObject endLevelPanel;
    [SerializeField] private TextMeshProUGUI levelCompleteText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI ratingText;
    [SerializeField] private TextMeshProUGUI timeTakenText;
    [SerializeField] private TextMeshProUGUI crystalsCollectedText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI nextLevelButtonText;
    [SerializeField] private TextMeshProUGUI retryButtonText;
    
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider cameraSensitivitySlider;
    
    [Header("Message Display")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageDuration = 3f;
    
    private float messageTimer = 0f;
    private bool isShowingMessage = false;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Initialize UI
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        
        if (endLevelPanel != null)
            endLevelPanel.SetActive(false);
        
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        
        if (messagePanel != null)
            messagePanel.SetActive(false);
        
        // Initialize language dropdown
        InitializeLanguageDropdown();
        
        // Refresh all text
        RefreshAllText();
    }
    
    void Update()
    {
        // Handle message timer
        if (isShowingMessage)
        {
            messageTimer += Time.deltaTime;
            if (messageTimer >= messageDuration)
            {
                HideMessage();
            }
        }
    }
    
    /// <summary>
    /// Update energy bar display
    /// </summary>
    public void UpdateEnergyBar(float current, float max)
    {
        if (energyBar != null)
        {
            energyBar.value = current / max;
        }
        
        if (energyText != null)
        {
            energyText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
        }
    }
    
    /// <summary>
    /// Update crystal counter
    /// </summary>
    public void UpdateCrystalCount(int current, int total)
    {
        if (crystalText != null)
        {
            crystalText.text = $"{LanguageManager.Instance.GetText("crystals")}: {current}/{total}";
        }
    }
    
    /// <summary>
    /// Update timer display
    /// </summary>
    public void UpdateTimer(float timeRemaining)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{LanguageManager.Instance.GetText("time")}: {minutes:00}:{seconds:00}";
        }
    }
    
    /// <summary>
    /// Update gravity direction display
    /// </summary>
    public void UpdateGravityDirection(string direction)
    {
        if (gravityDirectionText != null)
        {
            string translatedDirection = LanguageManager.Instance.GetText($"gravity_{direction.ToLower()}");
            gravityDirectionText.text = $"{LanguageManager.Instance.GetText("gravity")}: {translatedDirection}";
        }
    }
    
    /// <summary>
    /// Show pause menu
    /// </summary>
    public void ShowPauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    /// <summary>
    /// Hide pause menu
    /// </summary>
    public void HidePauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    /// <summary>
    /// Show end level panel
    /// </summary>
    public void ShowEndLevelPanel(bool success, int score, string rating, float timeTaken, int crystals, int deaths)
    {
        if (endLevelPanel != null)
        {
            endLevelPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Update text
            if (levelCompleteText != null)
            {
                string key = success ? "level_complete" : "level_failed";
                levelCompleteText.text = LanguageManager.Instance.GetText(key);
            }
            
            if (scoreText != null)
                scoreText.text = $"{LanguageManager.Instance.GetText("score")}: {score}";
            
            if (ratingText != null)
                ratingText.text = $"{LanguageManager.Instance.GetText("rating")}: {rating}";
            
            if (timeTakenText != null)
            {
                int minutes = Mathf.FloorToInt(timeTaken / 60f);
                int seconds = Mathf.FloorToInt(timeTaken % 60f);
                timeTakenText.text = $"{LanguageManager.Instance.GetText("time_taken")}: {minutes:00}:{seconds:00}";
            }
            
            if (crystalsCollectedText != null)
                crystalsCollectedText.text = $"{LanguageManager.Instance.GetText("crystals_collected")}: {crystals}";
            
            if (deathsText != null)
                deathsText.text = $"{LanguageManager.Instance.GetText("deaths")}: {deaths}";
        }
    }
    
    /// <summary>
    /// Show settings panel
    /// </summary>
    public void ShowSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Hide settings panel
    /// </summary>
    public void HideSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Show temporary message
    /// </summary>
    public void ShowMessage(string messageKey)
    {
        if (messagePanel != null && messageText != null)
        {
            messagePanel.SetActive(true);
            messageText.text = LanguageManager.Instance.GetText(messageKey);
            messageTimer = 0f;
            isShowingMessage = true;
        }
    }
    
    /// <summary>
    /// Hide message
    /// </summary>
    private void HideMessage()
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
            isShowingMessage = false;
        }
    }
    
    /// <summary>
    /// Initialize language dropdown
    /// </summary>
    private void InitializeLanguageDropdown()
    {
        if (languageDropdown != null)
        {
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "English",
                "中文",
                "日本語",
                "한국어"
            });
            
            // Set current language
            if (LanguageManager.Instance != null)
            {
                languageDropdown.value = LanguageManager.Instance.GetCurrentLanguageIndex();
            }
            
            // Add listener
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }
    }
    
    /// <summary>
    /// Handle language change
    /// </summary>
    private void OnLanguageChanged(int index)
    {
        if (LanguageManager.Instance != null)
        {
            LanguageManager.Instance.SetLanguageByIndex(index);
        }
    }
    
    /// <summary>
    /// Refresh all text elements with current language
    /// </summary>
    public void RefreshAllText()
    {
        if (LanguageManager.Instance == null)
            return;
        
        // Main Menu
        if (titleText != null)
            titleText.text = LanguageManager.Instance.GetText("game_title");
        if (startButtonText != null)
            startButtonText.text = LanguageManager.Instance.GetText("start_game");
        if (settingsButtonText != null)
            settingsButtonText.text = LanguageManager.Instance.GetText("settings");
        if (quitButtonText != null)
            quitButtonText.text = LanguageManager.Instance.GetText("quit");
        
        // Pause Menu
        if (pausedText != null)
            pausedText.text = LanguageManager.Instance.GetText("paused");
        if (resumeButtonText != null)
            resumeButtonText.text = LanguageManager.Instance.GetText("resume");
        if (restartButtonText != null)
            restartButtonText.text = LanguageManager.Instance.GetText("restart");
        if (mainMenuButtonText != null)
            mainMenuButtonText.text = LanguageManager.Instance.GetText("main_menu");
        
        // End Level
        if (nextLevelButtonText != null)
            nextLevelButtonText.text = LanguageManager.Instance.GetText("next_level");
        if (retryButtonText != null)
            retryButtonText.text = LanguageManager.Instance.GetText("retry");
    }
}
