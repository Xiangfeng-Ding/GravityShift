using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// GameManager controls overall game flow and state
/// Manages level progression, scoring, checkpoints, and win/lose conditions
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int totalCrystalsInLevel = 5;
    [SerializeField] private float levelTimeLimit = 420f; // 7 minutes default
    
    [Header("Difficulty Settings")]
    [SerializeField] private int difficulty = 1; // 0=Easy, 1=Normal, 2=Hard
    [SerializeField] private int checkpointsEasy = 4;
    [SerializeField] private int checkpointsNormal = 2;
    [SerializeField] private int checkpointsHard = 1;
    
    [Header("Crystal Requirements")]
    [SerializeField] private float crystalRequirementPercent = 0.7f; // 70% for normal levels
    
    [Header("Player References")]
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 spawnPosition;
    
    // Game state
    private int collectedCrystals = 0;
    private int requiredCrystals = 0;
    private float timeRemaining = 0f;
    private int deathCount = 0;
    private int gravitySwitchCount = 0;
    private float levelStartTime = 0f;
    
    // Checkpoint
    private Vector3 checkpointPosition;
    private int currentCheckpointID = -1;
    
    // Score
    private int currentScore = 0;
    private string currentRating = "D";
    
    // State flags
    private bool isLevelActive = false;
    private bool isLevelComplete = false;
    
    void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Load difficulty from PlayerPrefs
        difficulty = PlayerPrefs.GetInt("Difficulty", 1);
        
        // Initialize level
        InitializeLevel();
    }
    
    void Update()
    {
        if (!isLevelActive || isLevelComplete)
            return;
        
        // Update timer
        timeRemaining -= Time.deltaTime;
        
        // Check time limit
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            LevelFailed("Time's up!");
        }
    }
    
    /// <summary>
    /// Initialize level settings
    /// </summary>
    private void InitializeLevel()
    {
        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                spawnPosition = player.position;
            }
        }
        
        // Set checkpoint to spawn
        checkpointPosition = spawnPosition;
        
        // Count total crystals in level
        CrystalPickup[] crystals = FindObjectsOfType<CrystalPickup>();
        totalCrystalsInLevel = crystals.Length;
        
        // Calculate required crystals based on level
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.Contains("Level5") || sceneName.Contains("Final"))
        {
            requiredCrystals = totalCrystalsInLevel; // 100% for final level
        }
        else if (sceneName.Contains("Level1") || sceneName.Contains("Tutorial"))
        {
            requiredCrystals = Mathf.Min(3, totalCrystalsInLevel); // 3 crystals for tutorial
        }
        else
        {
            requiredCrystals = Mathf.CeilToInt(totalCrystalsInLevel * crystalRequirementPercent);
        }
        
        // Adjust difficulty settings
        ApplyDifficultySettings();
        
        // Reset counters
        collectedCrystals = 0;
        deathCount = 0;
        gravitySwitchCount = 0;
        
        // Start timer
        timeRemaining = levelTimeLimit;
        levelStartTime = Time.time;
        
        // Activate level
        isLevelActive = true;
        isLevelComplete = false;
        
        Debug.Log($"Level initialized: {totalCrystalsInLevel} crystals, {requiredCrystals} required, {levelTimeLimit}s time limit");
    }
    
    /// <summary>
    /// Apply difficulty-specific settings
    /// </summary>
    private void ApplyDifficultySettings()
    {
        switch (difficulty)
        {
            case 0: // Easy
                levelTimeLimit = 600f; // 10 minutes
                crystalRequirementPercent = 0.5f; // 50%
                break;
            case 1: // Normal
                levelTimeLimit = 420f; // 7 minutes
                crystalRequirementPercent = 0.7f; // 70%
                break;
            case 2: // Hard
                levelTimeLimit = 300f; // 5 minutes
                crystalRequirementPercent = 0.9f; // 90%
                break;
        }
        
        // Recalculate required crystals
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.Contains("Level5") && !sceneName.Contains("Final") && 
            !sceneName.Contains("Level1") && !sceneName.Contains("Tutorial"))
        {
            requiredCrystals = Mathf.CeilToInt(totalCrystalsInLevel * crystalRequirementPercent);
        }
    }
    
    /// <summary>
    /// Collect a crystal
    /// </summary>
    public void CollectCrystal(int value = 1)
    {
        collectedCrystals += value;
        
        Debug.Log($"Crystal collected! {collectedCrystals}/{totalCrystalsInLevel} (Required: {requiredCrystals})");
    }
    
    /// <summary>
    /// Set checkpoint position
    /// </summary>
    public void SetCheckpoint(Vector3 position, int checkpointID)
    {
        checkpointPosition = position;
        currentCheckpointID = checkpointID;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage("checkpoint_activated");
        }
        
        Debug.Log($"Checkpoint {checkpointID} set at {position}");
    }
    
    /// <summary>
    /// Player death - respawn at checkpoint
    /// </summary>
    public void PlayerDied()
    {
        deathCount++;
        
        // Respawn player
        if (player != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.position = checkpointPosition;
                controller.enabled = true;
            }
            else
            {
                player.position = checkpointPosition;
            }
            
            // Reset player velocity
            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.SetVelocity(Vector3.zero);
            }
            
            // Restore energy
            PlayerEnergy playerEnergy = player.GetComponent<PlayerEnergy>();
            if (playerEnergy != null)
            {
                playerEnergy.RestoreFullEnergy();
            }
        }
        
        Debug.Log($"Player died! Deaths: {deathCount}. Respawning at checkpoint.");
    }
    
    /// <summary>
    /// Player hit by enemy
    /// </summary>
    public void PlayerHitByEnemy()
    {
        PlayerDied();
    }
    
    /// <summary>
    /// Player reached exit portal
    /// </summary>
    public void ReachedExit()
    {
        // Check if player has enough crystals
        if (collectedCrystals >= requiredCrystals)
        {
            LevelComplete();
        }
        else
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowMessage("barrier_unlocked");
            }
            Debug.Log($"Need {requiredCrystals - collectedCrystals} more crystals to exit!");
        }
    }
    
    /// <summary>
    /// Level completed successfully
    /// </summary>
    private void LevelComplete()
    {
        if (isLevelComplete)
            return;
        
        isLevelComplete = true;
        isLevelActive = false;
        
        // Calculate score
        CalculateScore();
        
        // Show end level UI
        float timeTaken = Time.time - levelStartTime;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowEndLevelPanel(
                true,
                currentScore,
                currentRating,
                timeTaken,
                collectedCrystals,
                deathCount
            );
        }
        
        Debug.Log($"Level Complete! Score: {currentScore}, Rating: {currentRating}");
    }
    
    /// <summary>
    /// Level failed
    /// </summary>
    private void LevelFailed(string reason)
    {
        if (isLevelComplete)
            return;
        
        isLevelComplete = true;
        isLevelActive = false;
        
        // Show end level UI
        float timeTaken = Time.time - levelStartTime;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowEndLevelPanel(
                false,
                0,
                "F",
                timeTaken,
                collectedCrystals,
                deathCount
            );
        }
        
        Debug.Log($"Level Failed: {reason}");
    }
    
    /// <summary>
    /// Calculate final score and rating
    /// </summary>
    private void CalculateScore()
    {
        currentScore = 0;
        
        // Base score for completion
        currentScore += 1000;
        
        // Crystal bonus (100 points per crystal)
        currentScore += collectedCrystals * 100;
        
        // Time bonus (remaining time in seconds)
        currentScore += Mathf.RoundToInt(timeRemaining * 10f);
        
        // Death penalty (-100 per death)
        currentScore -= deathCount * 100;
        
        // Gravity switch efficiency bonus
        int optimalSwitches = 20; // Estimated optimal switches
        if (gravitySwitchCount <= optimalSwitches)
        {
            currentScore += (optimalSwitches - gravitySwitchCount) * 10;
        }
        
        // Ensure score is not negative
        currentScore = Mathf.Max(0, currentScore);
        
        // Calculate rating
        float maxPossibleScore = 1000 + totalCrystalsInLevel * 100 + levelTimeLimit * 10 + optimalSwitches * 10;
        float scorePercent = currentScore / maxPossibleScore;
        
        if (scorePercent >= 0.9f)
            currentRating = "S";
        else if (scorePercent >= 0.8f)
            currentRating = "A";
        else if (scorePercent >= 0.7f)
            currentRating = "B";
        else if (scorePercent >= 0.6f)
            currentRating = "C";
        else
            currentRating = "D";
    }
    
    /// <summary>
    /// Load next level
    /// </summary>
    public void LoadNextLevel()
    {
        int nextLevel = currentLevel + 1;
        
        if (nextLevel <= 5)
        {
            string nextSceneName = $"Level{nextLevel}";
            if (nextLevel == 1) nextSceneName = "Level1_Tutorial";
            else if (nextLevel == 2) nextSceneName = "Level2_Platforms";
            else if (nextLevel == 3) nextSceneName = "Level3_Hazards";
            else if (nextLevel == 4) nextSceneName = "Level4_Mechanisms";
            else if (nextLevel == 5) nextSceneName = "Level5_Final";
            
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            // All levels complete, return to main menu
            SceneManager.LoadScene("MainMenu");
        }
    }
    
    /// <summary>
    /// Restart current level
    /// </summary>
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Get collected crystals count
    /// </summary>
    public int GetCollectedCrystals()
    {
        return collectedCrystals;
    }
    
    /// <summary>
    /// Get total crystals in level
    /// </summary>
    public int GetTotalCrystals()
    {
        return totalCrystalsInLevel;
    }
    
    /// <summary>
    /// Get required crystals count
    /// </summary>
    public int GetRequiredCrystals()
    {
        return requiredCrystals;
    }
    
    /// <summary>
    /// Get time remaining
    /// </summary>
    public float GetTimeRemaining()
    {
        return timeRemaining;
    }
    
    /// <summary>
    /// Get death count
    /// </summary>
    public int GetDeathCount()
    {
        return deathCount;
    }
    
    /// <summary>
    /// Increment gravity switch counter
    /// </summary>
    public void IncrementGravitySwitchCount()
    {
        gravitySwitchCount++;
    }
    
    /// <summary>
    /// Get current difficulty
    /// </summary>
    public int GetDifficulty()
    {
        return difficulty;
    }
}
