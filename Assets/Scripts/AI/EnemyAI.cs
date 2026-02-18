using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EnemyAI implements Finite State Machine for patrol drone enemies
/// Handles detection, patrol, chase, and attack behaviors
/// Affected by gravity changes like the player
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float detectionAngle = 90f;
    [SerializeField] private LayerMask detectionLayers;
    [SerializeField] private LayerMask obstacleLayers;
    
    [Header("Movement Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float rotationSpeed = 5f;
    
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private float waypointReachDistance = 0.5f;
    [SerializeField] private float waitTimeAtWaypoint = 1f;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private GameObject attackEffect;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject detectionCone;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material alertMaterial;
    [SerializeField] private MeshRenderer enemyRenderer;
    
    // State Machine
    private EnemyState currentState = EnemyState.Idle;
    private Dictionary<EnemyState, EnemyStateBase> states;
    
    // References
    private Transform player;
    private CharacterController characterController;
    private GravityController gravityController;
    
    // Patrol
    private int currentWaypointIndex = 0;
    private float waypointWaitTimer = 0f;
    private bool isWaitingAtWaypoint = false;
    private Vector3 patrolStartPosition;
    
    // Movement
    private Vector3 velocity = Vector3.zero;
    private float currentSpeed = 0f;
    
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        
        // Get gravity controller from player
        if (player != null)
        {
            gravityController = player.GetComponent<GravityController>();
        }
        
        patrolStartPosition = transform.position;
    }
    
    void Start()
    {
        // Initialize state machine
        states = new Dictionary<EnemyState, EnemyStateBase>
        {
            { EnemyState.Idle, new IdleState(this) },
            { EnemyState.Patrol, new PatrolState(this) },
            { EnemyState.Chase, new ChaseState(this) },
            { EnemyState.Attack, new AttackState(this) },
            { EnemyState.Return, new ReturnState(this) }
        };
        
        // Start in Idle state
        ChangeState(EnemyState.Idle);
        
        currentSpeed = patrolSpeed;
    }
    
    void Update()
    {
        // Update current state
        if (states.ContainsKey(currentState))
        {
            states[currentState].Update();
        }
        
        // Apply gravity
        ApplyGravity();
    }
    
    /// <summary>
    /// Change to a new state
    /// </summary>
    public void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
            return;
        
        // Exit current state
        if (states.ContainsKey(currentState))
        {
            states[currentState].Exit();
        }
        
        // Change state
        currentState = newState;
        
        // Enter new state
        if (states.ContainsKey(newState))
        {
            states[newState].Enter();
        }
        
        // Update visual feedback
        UpdateVisualFeedback();
    }
    
    /// <summary>
    /// Check if enemy can see the player
    /// Uses raycast for line of sight detection
    /// </summary>
    public bool CanSeePlayer()
    {
        if (player == null)
            return false;
        
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Check distance
        if (distanceToPlayer > detectionRange)
            return false;
        
        // Check angle
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > detectionAngle / 2f)
            return false;
        
        // Check line of sight
        if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, detectionRange, obstacleLayers))
        {
            if (!hit.collider.CompareTag("Player"))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if player is within attack range
    /// </summary>
    public bool IsPlayerInAttackRange()
    {
        if (player == null)
            return false;
        
        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= attackRange;
    }
    
    /// <summary>
    /// Move along patrol route
    /// </summary>
    public void MoveAlongPatrolRoute()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            return;
        
        if (isWaitingAtWaypoint)
        {
            waypointWaitTimer += Time.deltaTime;
            if (waypointWaitTimer >= waitTimeAtWaypoint)
            {
                isWaitingAtWaypoint = false;
                waypointWaitTimer = 0f;
                AdvanceToNextWaypoint();
            }
            return;
        }
        
        // Get current waypoint
        Transform targetWaypoint = patrolWaypoints[currentWaypointIndex];
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetWaypoint.position);
        
        // Move towards waypoint
        if (distance > waypointReachDistance)
        {
            MoveInDirection(direction);
            FaceDirection(direction);
        }
        else
        {
            // Reached waypoint
            isWaitingAtWaypoint = true;
        }
    }
    
    /// <summary>
    /// Advance to next waypoint in patrol route
    /// </summary>
    private void AdvanceToNextWaypoint()
    {
        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
    }
    
    /// <summary>
    /// Move towards player
    /// </summary>
    public void MoveTowardsPlayer()
    {
        if (player == null)
            return;
        
        Vector3 direction = (player.position - transform.position).normalized;
        MoveInDirection(direction);
        FaceDirection(direction);
    }
    
    /// <summary>
    /// Return to patrol route
    /// </summary>
    public bool ReturnToPatrolRoute()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
        {
            // No patrol route, return to start position
            Vector3 direction = (patrolStartPosition - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, patrolStartPosition);
            
            if (distance > waypointReachDistance)
            {
                MoveInDirection(direction);
                FaceDirection(direction);
                return false;
            }
            return true;
        }
        
        // Return to nearest waypoint
        Transform nearestWaypoint = GetNearestWaypoint();
        Vector3 dirToWaypoint = (nearestWaypoint.position - transform.position).normalized;
        float distToWaypoint = Vector3.Distance(transform.position, nearestWaypoint.position);
        
        if (distToWaypoint > waypointReachDistance)
        {
            MoveInDirection(dirToWaypoint);
            FaceDirection(dirToWaypoint);
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get nearest patrol waypoint
    /// </summary>
    private Transform GetNearestWaypoint()
    {
        Transform nearest = patrolWaypoints[0];
        float minDistance = Vector3.Distance(transform.position, nearest.position);
        
        for (int i = 1; i < patrolWaypoints.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, patrolWaypoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = patrolWaypoints[i];
                currentWaypointIndex = i;
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Execute attack on player
    /// </summary>
    public void ExecuteAttack()
    {
        if (player == null)
            return;
        
        // Spawn attack effect
        if (attackEffect != null)
        {
            Instantiate(attackEffect, transform.position + transform.forward * 1f, Quaternion.identity);
        }
        
        // Apply knockback to player
        Vector3 knockbackDirection = (player.position - transform.position).normalized;
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.AddForce(knockbackDirection * knockbackForce);
        }
        
        // Check for player death
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.PlayerHitByEnemy();
        }
        
        Debug.Log("Enemy attacked player!");
    }
    
    /// <summary>
    /// Move in specified direction
    /// </summary>
    private void MoveInDirection(Vector3 direction)
    {
        if (characterController != null)
        {
            Vector3 movement = direction * currentSpeed * Time.deltaTime;
            characterController.Move(movement);
        }
        else
        {
            transform.position += direction * currentSpeed * Time.deltaTime;
        }
    }
    
    /// <summary>
    /// Face specified direction
    /// </summary>
    private void FaceDirection(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
    /// <summary>
    /// Face player
    /// </summary>
    public void FacePlayer()
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            FaceDirection(direction);
        }
    }
    
    /// <summary>
    /// Apply gravity to enemy (same as player)
    /// </summary>
    private void ApplyGravity()
    {
        if (gravityController != null && characterController != null)
        {
            Vector3 gravityDir = gravityController.GetCurrentGravityDirection();
            velocity += gravityDir * gravityController.GetGravityMagnitude() * Time.deltaTime;
            
            characterController.Move(velocity * Time.deltaTime);
            
            // Reset velocity if grounded
            if (characterController.isGrounded)
            {
                velocity = Vector3.zero;
            }
        }
    }
    
    /// <summary>
    /// Stop movement
    /// </summary>
    public void StopMovement()
    {
        currentSpeed = 0f;
    }
    
    /// <summary>
    /// Resume patrol
    /// </summary>
    public void ResumePatrol()
    {
        currentSpeed = patrolSpeed;
    }
    
    /// <summary>
    /// Set chase speed
    /// </summary>
    public void SetChaseSpeed()
    {
        currentSpeed = chaseSpeed;
    }
    
    /// <summary>
    /// Set patrol speed
    /// </summary>
    public void SetPatrolSpeed()
    {
        currentSpeed = patrolSpeed;
    }
    
    /// <summary>
    /// Update visual feedback based on state
    /// </summary>
    private void UpdateVisualFeedback()
    {
        if (enemyRenderer == null)
            return;
        
        switch (currentState)
        {
            case EnemyState.Chase:
            case EnemyState.Attack:
                if (alertMaterial != null)
                    enemyRenderer.material = alertMaterial;
                break;
            default:
                if (normalMaterial != null)
                    enemyRenderer.material = normalMaterial;
                break;
        }
    }
    
    /// <summary>
    /// Draw detection range in editor
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw patrol route
        if (patrolWaypoints != null && patrolWaypoints.Length > 1)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(patrolWaypoints[i].position, 0.3f);
                    
                    int nextIndex = (i + 1) % patrolWaypoints.Length;
                    if (patrolWaypoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[nextIndex].position);
                    }
                }
            }
        }
    }
}
