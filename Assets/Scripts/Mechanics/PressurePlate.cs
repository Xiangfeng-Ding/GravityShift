using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PressurePlate activates mechanisms when player stands on it
/// Can control doors, platforms, barriers, etc.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Plate Settings")]
    [SerializeField] private bool requiresContinuousPress = true;
    [SerializeField] private bool oneTimeUse = false;
    [SerializeField] private float activationDelay = 0f;
    
    [Header("Visual Feedback")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressedHeight = -0.1f;
    [SerializeField] private float normalHeight = 0f;
    [SerializeField] private float pressSpeed = 5f;
    
    [Header("Connected Mechanisms")]
    [SerializeField] private EnergyBarrier[] connectedBarriers;
    [SerializeField] private MovingPlatform[] connectedPlatforms;
    [SerializeField] private GameObject[] objectsToActivate;
    [SerializeField] private GameObject[] objectsToDeactivate;
    
    [Header("Audio")]
    [SerializeField] private AudioClip pressSound;
    [SerializeField] private AudioClip releaseSound;
    
    [Header("Events")]
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;
    
    // Plate state
    private bool isPressed = false;
    private bool hasBeenUsed = false;
    private float activationTimer = 0f;
    private int objectsOnPlate = 0;
    
    void Update()
    {
        UpdatePlateVisual();
        
        // Handle activation delay
        if (isPressed && activationDelay > 0f && activationTimer < activationDelay)
        {
            activationTimer += Time.deltaTime;
            if (activationTimer >= activationDelay)
            {
                TriggerActivation();
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Movable"))
        {
            objectsOnPlate++;
            
            if (!isPressed && !hasBeenUsed)
            {
                PressPressurePlate();
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Movable"))
        {
            objectsOnPlate--;
            
            if (objectsOnPlate <= 0 && isPressed && requiresContinuousPress)
            {
                ReleasePressurePlate();
            }
        }
    }
    
    /// <summary>
    /// Press the pressure plate
    /// </summary>
    private void PressPressurePlate()
    {
        isPressed = true;
        activationTimer = 0f;
        
        // Play press sound
        if (pressSound != null)
        {
            AudioSource.PlayClipAtPoint(pressSound, transform.position);
        }
        
        // If no delay, activate immediately
        if (activationDelay <= 0f)
        {
            TriggerActivation();
        }
        
        Debug.Log("Pressure plate pressed!");
    }
    
    /// <summary>
    /// Release the pressure plate
    /// </summary>
    private void ReleasePressurePlate()
    {
        isPressed = false;
        activationTimer = 0f;
        
        // Play release sound
        if (releaseSound != null)
        {
            AudioSource.PlayClipAtPoint(releaseSound, transform.position);
        }
        
        TriggerDeactivation();
        
        Debug.Log("Pressure plate released!");
    }
    
    /// <summary>
    /// Trigger activation of connected mechanisms
    /// </summary>
    private void TriggerActivation()
    {
        // Deactivate connected barriers
        foreach (EnergyBarrier barrier in connectedBarriers)
        {
            if (barrier != null)
            {
                barrier.DeactivateBarrier();
            }
        }
        
        // Activate connected platforms
        foreach (MovingPlatform platform in connectedPlatforms)
        {
            if (platform != null)
            {
                platform.Activate();
            }
        }
        
        // Activate objects
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
        
        // Deactivate objects
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        
        // Invoke event
        onActivated?.Invoke();
        
        // Mark as used if one-time
        if (oneTimeUse)
        {
            hasBeenUsed = true;
        }
    }
    
    /// <summary>
    /// Trigger deactivation of connected mechanisms
    /// </summary>
    private void TriggerDeactivation()
    {
        // Reactivate connected barriers
        foreach (EnergyBarrier barrier in connectedBarriers)
        {
            if (barrier != null)
            {
                barrier.ActivateBarrier();
            }
        }
        
        // Deactivate connected platforms
        foreach (MovingPlatform platform in connectedPlatforms)
        {
            if (platform != null)
            {
                platform.Deactivate();
            }
        }
        
        // Deactivate objects
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        
        // Reactivate objects
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
        
        // Invoke event
        onDeactivated?.Invoke();
    }
    
    /// <summary>
    /// Update plate visual position based on pressed state
    /// </summary>
    private void UpdatePlateVisual()
    {
        if (plateVisual == null)
            return;
        
        float targetHeight = isPressed ? pressedHeight : normalHeight;
        Vector3 currentPos = plateVisual.localPosition;
        currentPos.y = Mathf.Lerp(currentPos.y, targetHeight, Time.deltaTime * pressSpeed);
        plateVisual.localPosition = currentPos;
    }
    
    /// <summary>
    /// Check if plate is currently pressed
    /// </summary>
    public bool IsPressed()
    {
        return isPressed;
    }
    
    /// <summary>
    /// Manually activate plate (for testing or scripted events)
    /// </summary>
    public void ForceActivate()
    {
        if (!hasBeenUsed)
        {
            isPressed = true;
            TriggerActivation();
        }
    }
    
    /// <summary>
    /// Reset plate to initial state
    /// </summary>
    public void ResetPlate()
    {
        isPressed = false;
        hasBeenUsed = false;
        activationTimer = 0f;
        objectsOnPlate = 0;
    }
}
