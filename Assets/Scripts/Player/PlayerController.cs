using UnityEngine;

/// <summary>
/// PlayerController handles basic player movement and input
/// Uses CharacterController for stable physics-based movement
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = 20f;
    
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    // Components
    private CharacterController characterController;
    private GravityController gravityController;
    
    // Movement
    private Vector3 moveDirection = Vector3.zero;
    private Vector3 velocity = Vector3.zero;
    private bool isGrounded;
    
    // Camera rotation
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        gravityController = GetComponent<GravityController>();
        
        // Lock cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        HandleInput();
        HandleCameraRotation();
    }
    
    void FixedUpdate()
    {
        CheckGroundStatus();
        ApplyMovement();
    }
    
    /// <summary>
    /// Handle player input for movement and jumping
    /// </summary>
    private void HandleInput()
    {
        // Get input axes
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Calculate movement direction relative to camera
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        
        // Project onto horizontal plane relative to current gravity
        Vector3 gravityUp = -gravityController.GetCurrentGravityDirection();
        forward = Vector3.ProjectOnPlane(forward, gravityUp).normalized;
        right = Vector3.ProjectOnPlane(right, gravityUp).normalized;
        
        // Calculate desired move direction
        moveDirection = (forward * vertical + right * horizontal).normalized;
        
        // Jump input
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // Jump in the opposite direction of gravity
            velocity += gravityUp * jumpForce;
        }
        
        // Unlock cursor with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    /// <summary>
    /// Handle camera rotation based on mouse input
    /// </summary>
    private void HandleCameraRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;
        
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate camera vertically
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);
        
        // Rotate player horizontally
        rotationY += mouseX;
        
        // Apply rotations
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
    }
    
    /// <summary>
    /// Check if player is on the ground using SphereCast
    /// </summary>
    private void CheckGroundStatus()
    {
        Vector3 gravityDir = gravityController.GetCurrentGravityDirection();
        Vector3 spherePosition = transform.position - gravityDir * (characterController.height / 2f - characterController.radius);
        
        isGrounded = Physics.SphereCast(
            spherePosition,
            characterController.radius * 0.9f,
            gravityDir,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer
        );
        
        // Reset vertical velocity when grounded
        if (isGrounded && Vector3.Dot(velocity, gravityDir) > 0)
        {
            velocity = Vector3.ProjectOnPlane(velocity, gravityDir);
        }
    }
    
    /// <summary>
    /// Apply movement and gravity to the character
    /// </summary>
    private void ApplyMovement()
    {
        // Apply gravity
        Vector3 gravityDir = gravityController.GetCurrentGravityDirection();
        velocity += gravityDir * gravity * Time.fixedDeltaTime;
        
        // Calculate final movement
        Vector3 movement = moveDirection * moveSpeed + velocity;
        
        // Move the character
        characterController.Move(movement * Time.fixedDeltaTime);
    }
    
    /// <summary>
    /// Called by GravityController when gravity changes
    /// Reduces velocity to prevent exploitation
    /// </summary>
    public void OnGravityChanged()
    {
        // Reduce velocity by 30% to prevent gravity switching abuse
        velocity *= 0.7f;
    }
    
    /// <summary>
    /// Get current grounded status
    /// </summary>
    public bool IsGrounded()
    {
        return isGrounded;
    }
    
    /// <summary>
    /// Get current velocity
    /// </summary>
    public Vector3 GetVelocity()
    {
        return velocity;
    }
    
    /// <summary>
    /// Set velocity (used for external forces like knockback)
    /// </summary>
    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }
    
    /// <summary>
    /// Add force to player (for knockback, wind, etc.)
    /// </summary>
    public void AddForce(Vector3 force)
    {
        velocity += force;
    }
}
