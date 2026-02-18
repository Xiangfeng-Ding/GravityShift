using UnityEngine;

/// <summary>
/// HUDController updates HUD elements during gameplay
/// Connects game state to UI display
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEnergy playerEnergy;
    [SerializeField] private GravityController gravityController;
    
    private GameManager gameManager;
    
    void Start()
    {
        // Find game manager
        gameManager = FindObjectOfType<GameManager>();
        
        // Subscribe to energy changes
        if (playerEnergy != null)
        {
            playerEnergy.OnEnergyChanged += OnEnergyChanged;
            playerEnergy.OnEnergyLow += OnEnergyLow;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (playerEnergy != null)
        {
            playerEnergy.OnEnergyChanged -= OnEnergyChanged;
            playerEnergy.OnEnergyLow -= OnEnergyLow;
        }
    }
    
    void Update()
    {
        UpdateHUD();
    }
    
    /// <summary>
    /// Update all HUD elements
    /// </summary>
    private void UpdateHUD()
    {
        if (UIManager.Instance == null)
            return;
        
        // Update crystal count
        if (gameManager != null)
        {
            UIManager.Instance.UpdateCrystalCount(
                gameManager.GetCollectedCrystals(),
                gameManager.GetTotalCrystals()
            );
            
            // Update timer
            UIManager.Instance.UpdateTimer(gameManager.GetTimeRemaining());
        }
        
        // Update gravity direction
        if (gravityController != null)
        {
            UIManager.Instance.UpdateGravityDirection(gravityController.GetGravityDirectionName());
        }
    }
    
    /// <summary>
    /// Handle energy changed event
    /// </summary>
    private void OnEnergyChanged(float current, float max)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateEnergyBar(current, max);
        }
    }
    
    /// <summary>
    /// Handle low energy warning
    /// </summary>
    private void OnEnergyLow()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage("low_energy");
        }
    }
}
