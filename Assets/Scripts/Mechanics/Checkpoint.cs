using UnityEngine;

/// <summary>
/// Checkpoint saves player progress and respawn position
/// Activates when player enters trigger zone
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointID = 0;
    [SerializeField] private bool isActivated = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private MeshRenderer checkpointRenderer;
    [SerializeField] private GameObject activationEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    
    void Start()
    {
        UpdateVisuals();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            ActivateCheckpoint(other.gameObject);
        }
    }
    
    /// <summary>
    /// Activate this checkpoint
    /// </summary>
    private void ActivateCheckpoint(GameObject player)
    {
        isActivated = true;
        
        // Notify GameManager
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.SetCheckpoint(transform.position, checkpointID);
        }
        
        // Visual feedback
        UpdateVisuals();
        
        // Spawn activation effect
        if (activationEffect != null)
        {
            Instantiate(activationEffect, transform.position, Quaternion.identity);
        }
        
        // Play sound
        if (activationSound != null)
        {
            AudioSource.PlayClipAtPoint(activationSound, transform.position);
        }
        
        Debug.Log($"Checkpoint {checkpointID} activated!");
    }
    
    /// <summary>
    /// Update checkpoint visuals based on activation state
    /// </summary>
    private void UpdateVisuals()
    {
        if (checkpointRenderer != null)
        {
            if (isActivated && activeMaterial != null)
            {
                checkpointRenderer.material = activeMaterial;
            }
            else if (!isActivated && inactiveMaterial != null)
            {
                checkpointRenderer.material = inactiveMaterial;
            }
        }
    }
    
    /// <summary>
    /// Get checkpoint position
    /// </summary>
    public Vector3 GetCheckpointPosition()
    {
        return transform.position;
    }
    
    /// <summary>
    /// Check if checkpoint is activated
    /// </summary>
    public bool IsActivated()
    {
        return isActivated;
    }
    
    /// <summary>
    /// Get checkpoint ID
    /// </summary>
    public int GetCheckpointID()
    {
        return checkpointID;
    }
    
    /// <summary>
    /// Manually activate checkpoint (for testing)
    /// </summary>
    public void ForceActivate()
    {
        isActivated = true;
        UpdateVisuals();
    }
    
    /// <summary>
    /// Reset checkpoint to inactive state
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        UpdateVisuals();
    }
}
