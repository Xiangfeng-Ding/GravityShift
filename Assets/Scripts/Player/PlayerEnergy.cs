using UnityEngine;
using System;

/// <summary>
/// PlayerEnergy manages the energy system for gravity switching
/// Energy regenerates over time and is consumed when switching gravity
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 100f;
    [SerializeField] private float energyRegenRate = 10f;  // Energy per second
    [SerializeField] private float regenDelay = 0.5f;  // Delay after consumption before regen starts
    
    [Header("Energy Costs")]
    [SerializeField] private float gravitySwitchCost = 20f;
    
    // Energy state
    private float timeSinceLastConsumption = 0f;
    private bool isRegenerating = true;
    
    // Events
    public event Action<float, float> OnEnergyChanged;  // current, max
    public event Action OnEnergyDepleted;
    public event Action OnEnergyLow;  // Triggered at 30%
    
    private bool hasTriggeredLowWarning = false;
    
    void Start()
    {
        currentEnergy = maxEnergy;
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
    
    void Update()
    {
        HandleEnergyRegeneration();
        CheckEnergyWarnings();
    }
    
    /// <summary>
    /// Handle energy regeneration over time
    /// </summary>
    private void HandleEnergyRegeneration()
    {
        // Update timer
        timeSinceLastConsumption += Time.deltaTime;
        
        // Check if we can start regenerating
        if (timeSinceLastConsumption >= regenDelay)
        {
            isRegenerating = true;
        }
        
        // Regenerate energy
        if (isRegenerating && currentEnergy < maxEnergy)
        {
            currentEnergy += energyRegenRate * Time.deltaTime;
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
            
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }
    
    /// <summary>
    /// Check for energy warnings (low energy, depleted)
    /// </summary>
    private void CheckEnergyWarnings()
    {
        float energyPercent = currentEnergy / maxEnergy;
        
        // Low energy warning at 30%
        if (energyPercent <= 0.3f && !hasTriggeredLowWarning)
        {
            OnEnergyLow?.Invoke();
            hasTriggeredLowWarning = true;
        }
        else if (energyPercent > 0.3f)
        {
            hasTriggeredLowWarning = false;
        }
        
        // Depleted warning
        if (currentEnergy <= 0f)
        {
            OnEnergyDepleted?.Invoke();
        }
    }
    
    /// <summary>
    /// Try to consume energy for an action
    /// Returns true if successful, false if not enough energy
    /// </summary>
    public bool TryConsumeEnergy(float amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            timeSinceLastConsumption = 0f;
            isRegenerating = false;
            
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Add energy (from pickups)
    /// </summary>
    public void AddEnergy(float amount)
    {
        currentEnergy += amount;
        currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
    
    /// <summary>
    /// Restore energy to full
    /// </summary>
    public void RestoreFullEnergy()
    {
        currentEnergy = maxEnergy;
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
    
    /// <summary>
    /// Get current energy value
    /// </summary>
    public float GetCurrentEnergy()
    {
        return currentEnergy;
    }
    
    /// <summary>
    /// Get max energy value
    /// </summary>
    public float GetMaxEnergy()
    {
        return maxEnergy;
    }
    
    /// <summary>
    /// Get energy percentage (0-1)
    /// </summary>
    public float GetEnergyPercent()
    {
        return currentEnergy / maxEnergy;
    }
    
    /// <summary>
    /// Check if player has enough energy for an action
    /// </summary>
    public bool HasEnoughEnergy(float amount)
    {
        return currentEnergy >= amount;
    }
    
    /// <summary>
    /// Set max energy (for upgrades or difficulty)
    /// </summary>
    public void SetMaxEnergy(float newMax)
    {
        float ratio = currentEnergy / maxEnergy;
        maxEnergy = newMax;
        currentEnergy = maxEnergy * ratio;
        
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
}
