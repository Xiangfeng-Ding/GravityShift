using UnityEngine;

/// <summary>
/// GravityController manages gravity direction changes
/// Handles 6-directional gravity switching with energy cost
/// </summary>
public class GravityController : MonoBehaviour
{
    [Header("Gravity Settings")]
    [SerializeField] private float gravityMagnitude = 20f;
    [SerializeField] private float gravityTransitionSpeed = 5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private bool rotatePlayer = true;
    [SerializeField] private bool rotateCamera = true;
    [SerializeField] private float rotationSpeed = 5f;
    
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform playerModel;
    
    // Current gravity state
    private Vector3 currentGravityDirection = Vector3.down;
    private Vector3 targetGravityDirection = Vector3.down;
    private Quaternion targetRotation;
    private bool isTransitioning = false;
    
    // Energy system reference
    private PlayerEnergy playerEnergy;
    private PlayerController playerController;
    
    // Gravity direction presets
    private readonly Vector3[] gravityDirections = new Vector3[]
    {
        Vector3.down,      // Normal gravity (0)
        Vector3.up,        // Inverted gravity (1)
        Vector3.left,      // Left wall (2)
        Vector3.right,     // Right wall (3)
        Vector3.forward,   // Forward wall (4)
        Vector3.back       // Back wall (5)
    };
    
    void Awake()
    {
        playerEnergy = GetComponent<PlayerEnergy>();
        playerController = GetComponent<PlayerController>();
        
        // Initialize gravity
        currentGravityDirection = Vector3.down;
        targetGravityDirection = Vector3.down;
        targetRotation = transform.rotation;
    }
    
    void Start()
    {
        // Set initial gravity
        Physics.gravity = currentGravityDirection * gravityMagnitude;
    }
    
    void Update()
    {
        HandleGravityInput();
        
        if (isTransitioning)
        {
            UpdateGravityTransition();
        }
    }
    
    /// <summary>
    /// Handle input for gravity direction changes
    /// G + Direction keys to change gravity
    /// </summary>
    private void HandleGravityInput()
    {
        if (isTransitioning)
            return;
        
        // Check if G key is held (gravity modifier)
        if (!Input.GetKey(KeyCode.G))
            return;
        
        Vector3 newGravityDirection = Vector3.zero;
        bool inputDetected = false;
        
        // Check direction inputs
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            newGravityDirection = Vector3.down;  // Normal gravity
            inputDetected = true;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            newGravityDirection = Vector3.up;  // Inverted gravity
            inputDetected = true;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            newGravityDirection = Vector3.left;  // Left wall
            inputDetected = true;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            newGravityDirection = Vector3.right;  // Right wall
            inputDetected = true;
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            newGravityDirection = Vector3.forward;  // Forward wall
            inputDetected = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            newGravityDirection = Vector3.back;  // Back wall
            inputDetected = true;
        }
        
        // If valid input detected, try to change gravity
        if (inputDetected && newGravityDirection != currentGravityDirection)
        {
            TryChangeGravity(newGravityDirection);
        }
    }
    
    /// <summary>
    /// Attempt to change gravity direction
    /// Checks energy availability before switching
    /// </summary>
    private void TryChangeGravity(Vector3 newDirection)
    {
        // Check if player has enough energy
        if (playerEnergy != null && !playerEnergy.TryConsumeEnergy(20f))
        {
            Debug.Log("Not enough energy to switch gravity!");
            return;
        }
        
        // Set target gravity
        targetGravityDirection = newDirection;
        
        // Calculate target rotation
        if (rotatePlayer)
        {
            Vector3 currentUp = -currentGravityDirection;
            Vector3 targetUp = -newDirection;
            targetRotation = Quaternion.FromToRotation(currentUp, targetUp) * transform.rotation;
        }
        
        // Start transition
        isTransitioning = true;
        
        // Notify player controller
        if (playerController != null)
        {
            playerController.OnGravityChanged();
        }
        
        // Visual and audio feedback
        if (VisualEffectsController.Instance != null)
        {
            VisualEffectsController.Instance.SpawnGravitySwitchEffect(transform.position, newDirection);
        }
        
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.ShakeGravitySwitch();
        }
        
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGravitySwitchSound();
        }
        
        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IncrementGravitySwitchCount();
        }
    }
    
    /// <summary>
    /// Smoothly transition to new gravity direction
    /// </summary>
    private void UpdateGravityTransition()
    {
        // Lerp gravity direction
        currentGravityDirection = Vector3.Lerp(
            currentGravityDirection,
            targetGravityDirection,
            Time.deltaTime * gravityTransitionSpeed
        );
        
        // Update Physics.gravity
        Physics.gravity = currentGravityDirection * gravityMagnitude;
        
        // Rotate player
        if (rotatePlayer)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
        
        // Check if transition is complete
        if (Vector3.Distance(currentGravityDirection, targetGravityDirection) < 0.01f)
        {
            currentGravityDirection = targetGravityDirection;
            Physics.gravity = currentGravityDirection * gravityMagnitude;
            
            if (rotatePlayer)
            {
                transform.rotation = targetRotation;
            }
            
            isTransitioning = false;
        }
    }
    
    /// <summary>
    /// Get the current gravity direction
    /// </summary>
    public Vector3 GetCurrentGravityDirection()
    {
        return currentGravityDirection;
    }
    
    /// <summary>
    /// Get the current gravity magnitude
    /// </summary>
    public float GetGravityMagnitude()
    {
        return gravityMagnitude;
    }
    
    /// <summary>
    /// Check if currently transitioning between gravity states
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
    
    /// <summary>
    /// Force set gravity direction (for special zones)
    /// </summary>
    public void ForceSetGravity(Vector3 direction)
    {
        currentGravityDirection = direction;
        targetGravityDirection = direction;
        Physics.gravity = currentGravityDirection * gravityMagnitude;
        isTransitioning = false;
    }
    
    /// <summary>
    /// Get gravity direction index (for UI display)
    /// </summary>
    public int GetGravityDirectionIndex()
    {
        for (int i = 0; i < gravityDirections.Length; i++)
        {
            if (Vector3.Distance(currentGravityDirection, gravityDirections[i]) < 0.1f)
            {
                return i;
            }
        }
        return 0;
    }
    
    /// <summary>
    /// Get gravity direction name for UI
    /// </summary>
    public string GetGravityDirectionName()
    {
        int index = GetGravityDirectionIndex();
        string[] names = { "Down", "Up", "Left", "Right", "Forward", "Back" };
        return names[index];
    }
}
