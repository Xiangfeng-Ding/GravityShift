using UnityEngine;

/// <summary>
/// CrystalPickup handles gravity crystal collection
/// Crystals are required to unlock energy barriers and complete levels
/// </summary>
public class CrystalPickup : MonoBehaviour
{
    [Header("Crystal Settings")]
    [SerializeField] private int crystalValue = 1;
    [SerializeField] private bool rotateOverTime = true;
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private bool bobUpDown = true;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;
    
    [Header("Effects")]
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private AudioClip collectSound;
    
    private Vector3 startPosition;
    private float bobTimer = 0f;
    
    void Start()
    {
        startPosition = transform.position;
        // Randomize bob timer for variety
        bobTimer = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void Update()
    {
        // Rotate crystal
        if (rotateOverTime)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
        
        // Bob up and down
        if (bobUpDown)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float yOffset = Mathf.Sin(bobTimer) * bobHeight;
            transform.position = startPosition + Vector3.up * yOffset;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if player collected the crystal
        if (other.CompareTag("Player"))
        {
            CollectCrystal(other.gameObject);
        }
    }
    
    /// <summary>
    /// Handle crystal collection
    /// </summary>
    private void CollectCrystal(GameObject player)
    {
        // Notify GameManager
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.CollectCrystal(crystalValue);
        }
        
        // Play collect effect
        if (collectEffect != null)
        {
            Instantiate(collectEffect, transform.position, Quaternion.identity);
        }
        
        // Play sound
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }
        
        // Visual effects
        if (VisualEffectsController.Instance != null)
        {
            VisualEffectsController.Instance.SpawnCrystalPickupEffect(transform.position);
        }
        
        // Audio manager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCrystalPickupSound();
        }
        
        // Destroy crystal
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Get crystal value
    /// </summary>
    public int GetCrystalValue()
    {
        return crystalValue;
    }
}
