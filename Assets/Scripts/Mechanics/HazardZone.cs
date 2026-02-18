using UnityEngine;

/// <summary>
/// HazardZone kills player on contact
/// Used for death zones, spikes, energy fields, etc.
/// </summary>
public class HazardZone : MonoBehaviour
{
    [Header("Hazard Settings")]
    [SerializeField] private HazardType hazardType = HazardType.DeathZone;
    [SerializeField] private bool instantKill = true;
    [SerializeField] private float damageAmount = 100f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private Color hazardColor = Color.red;
    
    public enum HazardType
    {
        DeathZone,      // Void/abyss
        Spikes,         // Spike trap
        EnergyField,    // Energy hazard
        Laser           // Laser beam
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            TriggerHazard(other.gameObject);
        }
    }
    
    /// <summary>
    /// Trigger hazard effect on player
    /// </summary>
    private void TriggerHazard(GameObject player)
    {
        if (instantKill)
        {
            // Instant death
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.PlayerDied();
            }
            
            // Spawn death effect
            if (deathEffect != null)
            {
                Instantiate(deathEffect, player.transform.position, Quaternion.identity);
            }
            
            // Play death sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPlayerDeathSound();
            }
            
            Debug.Log($"Player killed by {hazardType}");
        }
    }
    
    /// <summary>
    /// Get hazard type
    /// </summary>
    public HazardType GetHazardType()
    {
        return hazardType;
    }
}
