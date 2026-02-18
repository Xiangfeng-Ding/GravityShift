using UnityEngine;

/// <summary>
/// MovingPlatform creates moving platforms that follow waypoints
/// Supports linear and circular movement patterns
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Type")]
    [SerializeField] private MovementType movementType = MovementType.Linear;
    
    [Header("Linear Movement")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool loopMovement = true;
    [SerializeField] private float waitTimeAtWaypoint = 1f;
    
    [Header("Circular Movement")]
    [SerializeField] private Vector3 circleCenter = Vector3.zero;
    [SerializeField] private float circleRadius = 5f;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Header("Platform Settings")]
    [SerializeField] private bool startActive = true;
    [SerializeField] private bool affectedByGravity = false;
    
    // Movement state
    private int currentWaypointIndex = 0;
    private bool movingForward = true;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private bool isActive = true;
    
    // Circular movement
    private float currentAngle = 0f;
    private Vector3 startPosition;
    
    // Player parenting
    private Transform playerOnPlatform = null;
    
    public enum MovementType
    {
        Linear,
        Circular,
        Triggered
    }
    
    void Start()
    {
        isActive = startActive;
        startPosition = transform.position;
        
        // Initialize circular movement
        if (movementType == MovementType.Circular)
        {
            circleCenter = startPosition;
        }
        
        // Validate waypoints for linear movement
        if (movementType == MovementType.Linear && (waypoints == null || waypoints.Length < 2))
        {
            Debug.LogWarning("MovingPlatform requires at least 2 waypoints for Linear movement!");
            isActive = false;
        }
    }
    
    void Update()
    {
        if (!isActive)
            return;
        
        switch (movementType)
        {
            case MovementType.Linear:
                UpdateLinearMovement();
                break;
            case MovementType.Circular:
                UpdateCircularMovement();
                break;
            case MovementType.Triggered:
                // Triggered platforms are controlled externally
                break;
        }
    }
    
    /// <summary>
    /// Update linear waypoint-based movement
    /// </summary>
    private void UpdateLinearMovement()
    {
        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtWaypoint)
            {
                isWaiting = false;
                waitTimer = 0f;
                AdvanceToNextWaypoint();
            }
            return;
        }
        
        // Get target waypoint
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        
        // Move towards waypoint
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetWaypoint.position);
        
        if (distance > 0.1f)
        {
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;
            
            // Move player with platform
            if (playerOnPlatform != null)
            {
                playerOnPlatform.position += movement;
            }
        }
        else
        {
            // Reached waypoint
            transform.position = targetWaypoint.position;
            isWaiting = true;
        }
    }
    
    /// <summary>
    /// Advance to next waypoint in sequence
    /// </summary>
    private void AdvanceToNextWaypoint()
    {
        if (loopMovement)
        {
            // Loop through waypoints
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            // Ping-pong between waypoints
            if (movingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = waypoints.Length - 2;
                    movingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 1;
                    movingForward = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Update circular/orbital movement
    /// </summary>
    private void UpdateCircularMovement()
    {
        currentAngle += rotationSpeed * Time.deltaTime;
        
        // Calculate position on circle
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector3 offset = Vector3.zero;
        
        if (rotationAxis == Vector3.up)
        {
            offset = new Vector3(
                Mathf.Cos(radians) * circleRadius,
                0f,
                Mathf.Sin(radians) * circleRadius
            );
        }
        else if (rotationAxis == Vector3.right)
        {
            offset = new Vector3(
                0f,
                Mathf.Sin(radians) * circleRadius,
                Mathf.Cos(radians) * circleRadius
            );
        }
        else if (rotationAxis == Vector3.forward)
        {
            offset = new Vector3(
                Mathf.Cos(radians) * circleRadius,
                Mathf.Sin(radians) * circleRadius,
                0f
            );
        }
        
        Vector3 newPosition = circleCenter + offset;
        Vector3 movement = newPosition - transform.position;
        transform.position = newPosition;
        
        // Move player with platform
        if (playerOnPlatform != null)
        {
            playerOnPlatform.position += movement;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerOnPlatform = other.transform;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerOnPlatform = null;
        }
    }
    
    /// <summary>
    /// Activate platform movement
    /// </summary>
    public void Activate()
    {
        isActive = true;
    }
    
    /// <summary>
    /// Deactivate platform movement
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
    }
    
    /// <summary>
    /// Toggle platform active state
    /// </summary>
    public void Toggle()
    {
        isActive = !isActive;
    }
    
    /// <summary>
    /// Move platform to specific waypoint (for triggered platforms)
    /// </summary>
    public void MoveToWaypoint(int waypointIndex)
    {
        if (waypoints != null && waypointIndex >= 0 && waypointIndex < waypoints.Length)
        {
            currentWaypointIndex = waypointIndex;
        }
    }
}
