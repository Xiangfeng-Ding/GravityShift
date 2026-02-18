using UnityEngine;

/// <summary>
/// ExitPortal marks the level exit
/// Player must have collected required crystals to exit
/// </summary>
public class ExitPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [SerializeField] private bool requiresCrystals = true;
    [SerializeField] private GameObject portalEffect;
    [SerializeField] private float rotationSpeed = 50f;
    
    [Header("Visual")]
    [SerializeField] private Transform portalVisual;
    
    void Update()
    {
        // Rotate portal visual
        if (portalVisual != null)
        {
            portalVisual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            AttemptExit();
        }
    }
    
    /// <summary>
    /// Attempt to exit level
    /// </summary>
    private void AttemptExit()
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            if (requiresCrystals)
            {
                gameManager.ReachedExit();
            }
            else
            {
                // No crystal requirement, exit immediately
                gameManager.ReachedExit();
            }
        }
    }
}
