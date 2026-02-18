using UnityEngine;

/// <summary>
/// EnergyBarrier blocks player progress until required crystals are collected
/// Can be unlocked by collecting enough crystals or activating mechanisms
/// </summary>
public class EnergyBarrier : MonoBehaviour
{
    [Header("Barrier Settings")]
    [SerializeField] private int requiredCrystals = 3;
    [SerializeField] private bool isActive = true;
    
    [Header("Visual Settings")]
    [SerializeField] private Material barrierMaterial;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color inactiveColor = Color.green;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    
    [Header("References")]
    [SerializeField] private Collider barrierCollider;
    [SerializeField] private MeshRenderer barrierRenderer;
    [SerializeField] private GameObject deactivationEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip blockedSound;
    
    private float pulseTimer = 0f;
    private bool hasPlayedBlockedSound = false;
    
    void Start()
    {
        UpdateBarrierState();
    }
    
    void Update()
    {
        // Pulse effect when active
        if (isActive && barrierRenderer != null)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = Mathf.Sin(pulseTimer) * pulseIntensity + 1f;
            
            Color currentColor = activeColor * pulse;
            currentColor.a = activeColor.a;
            
            if (barrierMaterial != null)
            {
                barrierMaterial.SetColor("_Color", currentColor);
                barrierMaterial.SetColor("_EmissionColor", currentColor * 0.5f);
            }
        }
        
        // Check if should unlock based on crystal count
        CheckUnlockCondition();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isActive)
        {
            // Play blocked sound
            if (blockedSound != null && !hasPlayedBlockedSound)
            {
                AudioSource.PlayClipAtPoint(blockedSound, transform.position);
                hasPlayedBlockedSound = true;
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            hasPlayedBlockedSound = false;
        }
    }
    
    /// <summary>
    /// Check if barrier should unlock based on crystal count
    /// </summary>
    private void CheckUnlockCondition()
    {
        if (!isActive)
            return;
        
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            int currentCrystals = gameManager.GetCollectedCrystals();
            
            if (currentCrystals >= requiredCrystals)
            {
                DeactivateBarrier();
            }
        }
    }
    
    /// <summary>
    /// Deactivate the barrier
    /// </summary>
    public void DeactivateBarrier()
    {
        if (!isActive)
            return;
        
        isActive = false;
        UpdateBarrierState();
        
        // Spawn deactivation effect
        if (deactivationEffect != null)
        {
            Instantiate(deactivationEffect, transform.position, Quaternion.identity);
        }
        
        // Play unlock sound
        if (unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(unlockSound, transform.position);
        }
        
        Debug.Log($"Energy Barrier unlocked! Required crystals: {requiredCrystals}");
    }
    
    /// <summary>
    /// Activate the barrier
    /// </summary>
    public void ActivateBarrier()
    {
        isActive = true;
        UpdateBarrierState();
    }
    
    /// <summary>
    /// Update barrier collider and visuals based on state
    /// </summary>
    private void UpdateBarrierState()
    {
        // Update collider
        if (barrierCollider != null)
        {
            barrierCollider.enabled = isActive;
        }
        
        // Update visuals
        if (barrierRenderer != null)
        {
            barrierRenderer.enabled = isActive;
            
            if (!isActive && barrierMaterial != null)
            {
                barrierMaterial.SetColor("_Color", inactiveColor);
                barrierMaterial.SetColor("_EmissionColor", inactiveColor * 0.5f);
            }
        }
    }
    
    /// <summary>
    /// Get required crystal count
    /// </summary>
    public int GetRequiredCrystals()
    {
        return requiredCrystals;
    }
    
    /// <summary>
    /// Check if barrier is active
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
    
    /// <summary>
    /// Set required crystals (for dynamic barriers)
    /// </summary>
    public void SetRequiredCrystals(int count)
    {
        requiredCrystals = count;
    }
}
