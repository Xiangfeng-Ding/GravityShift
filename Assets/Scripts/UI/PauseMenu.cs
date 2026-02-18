using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PauseMenu handles pause menu interactions
/// Resume, restart, return to main menu
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    
    private bool isPaused = false;
    
    void Update()
    {
        // Handle pause input
        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }
    
    /// <summary>
    /// Pause the game
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPauseMenu();
        }
    }
    
    /// <summary>
    /// Resume the game
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HidePauseMenu();
        }
    }
    
    /// <summary>
    /// Restart current level
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Return to main menu
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
    
    /// <summary>
    /// Check if game is paused
    /// </summary>
    public bool IsPaused()
    {
        return isPaused;
    }
}
