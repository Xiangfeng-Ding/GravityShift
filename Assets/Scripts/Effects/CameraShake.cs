using UnityEngine;
using System.Collections;

/// <summary>
/// CameraShake provides camera shake effects for impact feedback
/// Used for gravity switch, enemy attacks, explosions, etc.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }
    
    [Header("Shake Settings")]
    [SerializeField] private float defaultShakeDuration = 0.2f;
    [SerializeField] private float defaultShakeMagnitude = 0.1f;
    [SerializeField] private float dampingSpeed = 1.0f;
    
    private Vector3 originalPosition;
    private bool isShaking = false;
    
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
    
    void Start()
    {
        originalPosition = transform.localPosition;
    }
    
    /// <summary>
    /// Trigger camera shake with default settings
    /// </summary>
    public void Shake()
    {
        Shake(defaultShakeDuration, defaultShakeMagnitude);
    }
    
    /// <summary>
    /// Trigger camera shake with custom duration and magnitude
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }
    }
    
    /// <summary>
    /// Shake coroutine
    /// </summary>
    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            
            transform.localPosition = originalPosition + new Vector3(x, y, 0f);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Smoothly return to original position
        float returnElapsed = 0f;
        float returnDuration = 0.1f;
        Vector3 currentPos = transform.localPosition;
        
        while (returnElapsed < returnDuration)
        {
            transform.localPosition = Vector3.Lerp(currentPos, originalPosition, returnElapsed / returnDuration);
            returnElapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.localPosition = originalPosition;
        isShaking = false;
    }
    
    /// <summary>
    /// Shake for gravity switch (light shake)
    /// </summary>
    public void ShakeGravitySwitch()
    {
        Shake(0.15f, 0.05f);
    }
    
    /// <summary>
    /// Shake for enemy attack (medium shake)
    /// </summary>
    public void ShakeEnemyAttack()
    {
        Shake(0.25f, 0.15f);
    }
    
    /// <summary>
    /// Shake for player death (heavy shake)
    /// </summary>
    public void ShakePlayerDeath()
    {
        Shake(0.4f, 0.25f);
    }
    
    /// <summary>
    /// Shake for explosion or impact (very heavy shake)
    /// </summary>
    public void ShakeExplosion()
    {
        Shake(0.5f, 0.35f);
    }
}
