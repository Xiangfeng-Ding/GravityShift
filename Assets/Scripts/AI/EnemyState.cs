using UnityEngine;

/// <summary>
/// EnemyState defines all possible states for enemy AI
/// Used in Finite State Machine (FSM) implementation
/// </summary>
public enum EnemyState
{
    Idle,       // Standing still, scanning area
    Patrol,     // Following patrol route
    Chase,      // Pursuing detected player
    Attack,     // Within attack range, executing attack
    Return      // Lost player, returning to patrol route
}

/// <summary>
/// Base class for enemy state behaviors
/// Each state implements its own Enter, Update, and Exit logic
/// </summary>
public abstract class EnemyStateBase
{
    protected EnemyAI enemyAI;
    
    public EnemyStateBase(EnemyAI ai)
    {
        enemyAI = ai;
    }
    
    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
}

/// <summary>
/// Idle State: Enemy is stationary and scanning for player
/// </summary>
public class IdleState : EnemyStateBase
{
    private float idleTimer = 0f;
    private float idleDuration = 2f;
    
    public IdleState(EnemyAI ai) : base(ai) { }
    
    public override void Enter()
    {
        idleTimer = 0f;
        enemyAI.StopMovement();
        Debug.Log($"{enemyAI.name} entered IDLE state");
    }
    
    public override void Update()
    {
        idleTimer += Time.deltaTime;
        
        // Check for player detection
        if (enemyAI.CanSeePlayer())
        {
            enemyAI.ChangeState(EnemyState.Chase);
            return;
        }
        
        // After idle duration, start patrolling
        if (idleTimer >= idleDuration)
        {
            enemyAI.ChangeState(EnemyState.Patrol);
        }
    }
    
    public override void Exit()
    {
        // Nothing to clean up
    }
}

/// <summary>
/// Patrol State: Enemy follows waypoint path
/// </summary>
public class PatrolState : EnemyStateBase
{
    public PatrolState(EnemyAI ai) : base(ai) { }
    
    public override void Enter()
    {
        enemyAI.ResumePatrol();
        Debug.Log($"{enemyAI.name} entered PATROL state");
    }
    
    public override void Update()
    {
        // Check for player detection
        if (enemyAI.CanSeePlayer())
        {
            enemyAI.ChangeState(EnemyState.Chase);
            return;
        }
        
        // Continue patrol movement
        enemyAI.MoveAlongPatrolRoute();
    }
    
    public override void Exit()
    {
        // Nothing to clean up
    }
}

/// <summary>
/// Chase State: Enemy pursues detected player
/// </summary>
public class ChaseState : EnemyStateBase
{
    private float lostPlayerTimer = 0f;
    private float lostPlayerDuration = 3f;
    
    public ChaseState(EnemyAI ai) : base(ai) { }
    
    public override void Enter()
    {
        lostPlayerTimer = 0f;
        enemyAI.SetChaseSpeed();
        Debug.Log($"{enemyAI.name} entered CHASE state");
    }
    
    public override void Update()
    {
        // Check if player is still visible
        if (enemyAI.CanSeePlayer())
        {
            lostPlayerTimer = 0f;
            enemyAI.MoveTowardsPlayer();
            
            // Check if within attack range
            if (enemyAI.IsPlayerInAttackRange())
            {
                enemyAI.ChangeState(EnemyState.Attack);
            }
        }
        else
        {
            // Player lost sight
            lostPlayerTimer += Time.deltaTime;
            
            if (lostPlayerTimer >= lostPlayerDuration)
            {
                enemyAI.ChangeState(EnemyState.Return);
            }
        }
    }
    
    public override void Exit()
    {
        lostPlayerTimer = 0f;
    }
}

/// <summary>
/// Attack State: Enemy attacks player within range
/// </summary>
public class AttackState : EnemyStateBase
{
    private float attackCooldown = 2f;
    private float attackTimer = 0f;
    
    public AttackState(EnemyAI ai) : base(ai) { }
    
    public override void Enter()
    {
        attackTimer = 0f;
        enemyAI.StopMovement();
        Debug.Log($"{enemyAI.name} entered ATTACK state");
    }
    
    public override void Update()
    {
        attackTimer += Time.deltaTime;
        
        // Check if player is still in range
        if (!enemyAI.IsPlayerInAttackRange())
        {
            enemyAI.ChangeState(EnemyState.Chase);
            return;
        }
        
        // Face player
        enemyAI.FacePlayer();
        
        // Execute attack
        if (attackTimer >= attackCooldown)
        {
            enemyAI.ExecuteAttack();
            attackTimer = 0f;
        }
    }
    
    public override void Exit()
    {
        attackTimer = 0f;
    }
}

/// <summary>
/// Return State: Enemy returns to patrol route after losing player
/// </summary>
public class ReturnState : EnemyStateBase
{
    public ReturnState(EnemyAI ai) : base(ai) { }
    
    public override void Enter()
    {
        enemyAI.SetPatrolSpeed();
        Debug.Log($"{enemyAI.name} entered RETURN state");
    }
    
    public override void Update()
    {
        // Check for player detection
        if (enemyAI.CanSeePlayer())
        {
            enemyAI.ChangeState(EnemyState.Chase);
            return;
        }
        
        // Move back to patrol route
        if (enemyAI.ReturnToPatrolRoute())
        {
            // Reached patrol route, resume patrolling
            enemyAI.ChangeState(EnemyState.Patrol);
        }
    }
    
    public override void Exit()
    {
        // Nothing to clean up
    }
}
