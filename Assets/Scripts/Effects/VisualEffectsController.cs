using UnityEngine;

/// <summary>
/// VisualEffectsController manages particle effects and visual feedback
/// Spawns effects for gravity switch, crystal pickup, death, etc.
/// </summary>
public class VisualEffectsController : MonoBehaviour
{
    public static VisualEffectsController Instance { get; private set; }
    
    [Header("Particle Effects")]
    [SerializeField] private GameObject gravitySwitchEffect;
    [SerializeField] private GameObject crystalPickupEffect;
    [SerializeField] private GameObject checkpointActivateEffect;
    [SerializeField] private GameObject playerDeathEffect;
    [SerializeField] private GameObject enemyAlertEffect;
    [SerializeField] private GameObject barrierUnlockEffect;
    
    [Header("Effect Settings")]
    [SerializeField] private float effectLifetime = 2f;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Spawn gravity switch effect at position
    /// </summary>
    public void SpawnGravitySwitchEffect(Vector3 position, Vector3 gravityDirection)
    {
        if (gravitySwitchEffect != null)
        {
            GameObject effect = Instantiate(gravitySwitchEffect, position, Quaternion.LookRotation(gravityDirection));
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Spawn crystal pickup effect at position
    /// </summary>
    public void SpawnCrystalPickupEffect(Vector3 position)
    {
        if (crystalPickupEffect != null)
        {
            GameObject effect = Instantiate(crystalPickupEffect, position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Spawn checkpoint activate effect at position
    /// </summary>
    public void SpawnCheckpointEffect(Vector3 position)
    {
        if (checkpointActivateEffect != null)
        {
            GameObject effect = Instantiate(checkpointActivateEffect, position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Spawn player death effect at position
    /// </summary>
    public void SpawnPlayerDeathEffect(Vector3 position)
    {
        if (playerDeathEffect != null)
        {
            GameObject effect = Instantiate(playerDeathEffect, position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Spawn enemy alert effect at position
    /// </summary>
    public void SpawnEnemyAlertEffect(Vector3 position)
    {
        if (enemyAlertEffect != null)
        {
            GameObject effect = Instantiate(enemyAlertEffect, position + Vector3.up * 2f, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Spawn barrier unlock effect at position
    /// </summary>
    public void SpawnBarrierUnlockEffect(Vector3 position)
    {
        if (barrierUnlockEffect != null)
        {
            GameObject effect = Instantiate(barrierUnlockEffect, position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }
    }
    
    /// <summary>
    /// Create simple particle effect (fallback when prefab not assigned)
    /// </summary>
    private GameObject CreateSimpleParticleEffect(Vector3 position, Color color)
    {
        GameObject effectObj = new GameObject("SimpleEffect");
        effectObj.transform.position = position;
        
        ParticleSystem ps = effectObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = color;
        main.startSize = 0.5f;
        main.startLifetime = 1f;
        main.maxParticles = 20;
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });
        
        return effectObj;
    }
}
