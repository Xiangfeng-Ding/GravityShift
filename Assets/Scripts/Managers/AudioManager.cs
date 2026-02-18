using UnityEngine;

/// <summary>
/// AudioManager handles all audio playback
/// Manages music, sound effects, and volume settings
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    
    [Header("Music Tracks")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip level1Music;
    [SerializeField] private AudioClip level2Music;
    [SerializeField] private AudioClip level3Music;
    [SerializeField] private AudioClip level4Music;
    [SerializeField] private AudioClip level5Music;
    
    [Header("Sound Effects")]
    [SerializeField] private AudioClip gravitySwitchSound;
    [SerializeField] private AudioClip crystalPickupSound;
    [SerializeField] private AudioClip checkpointSound;
    [SerializeField] private AudioClip barrierUnlockSound;
    [SerializeField] private AudioClip enemyAlertSound;
    [SerializeField] private AudioClip playerDeathSound;
    [SerializeField] private AudioClip levelCompleteSound;
    [SerializeField] private AudioClip buttonClickSound;
    
    [Header("Volume Settings")]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float musicVolume = 0.7f;
    [SerializeField] private float sfxVolume = 1f;
    
    void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Create audio sources if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }
    
    void Start()
    {
        // Load volume settings from PlayerPrefs
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Play music track
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null)
            return;
        
        if (musicSource.clip == clip && musicSource.isPlaying)
            return;
        
        musicSource.clip = clip;
        musicSource.Play();
    }
    
    /// <summary>
    /// Stop music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }
    
    /// <summary>
    /// Play sound effect
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;
        
        sfxSource.PlayOneShot(clip);
    }
    
    /// <summary>
    /// Play sound effect at position
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null)
            return;
        
        AudioSource.PlayClipAtPoint(clip, position, sfxVolume * masterVolume);
    }
    
    /// <summary>
    /// Set master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Set music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Set SFX volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        ApplyVolumeSettings();
    }
    
    /// <summary>
    /// Apply volume settings to audio sources
    /// </summary>
    private void ApplyVolumeSettings()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume * masterVolume;
        }
        
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume * masterVolume;
        }
    }
    
    /// <summary>
    /// Play gravity switch sound
    /// </summary>
    public void PlayGravitySwitchSound()
    {
        PlaySFX(gravitySwitchSound);
    }
    
    /// <summary>
    /// Play crystal pickup sound
    /// </summary>
    public void PlayCrystalPickupSound()
    {
        PlaySFX(crystalPickupSound);
    }
    
    /// <summary>
    /// Play checkpoint sound
    /// </summary>
    public void PlayCheckpointSound()
    {
        PlaySFX(checkpointSound);
    }
    
    /// <summary>
    /// Play barrier unlock sound
    /// </summary>
    public void PlayBarrierUnlockSound()
    {
        PlaySFX(barrierUnlockSound);
    }
    
    /// <summary>
    /// Play enemy alert sound
    /// </summary>
    public void PlayEnemyAlertSound()
    {
        PlaySFX(enemyAlertSound);
    }
    
    /// <summary>
    /// Play player death sound
    /// </summary>
    public void PlayPlayerDeathSound()
    {
        PlaySFX(playerDeathSound);
    }
    
    /// <summary>
    /// Play level complete sound
    /// </summary>
    public void PlayLevelCompleteSound()
    {
        PlaySFX(levelCompleteSound);
    }
    
    /// <summary>
    /// Play button click sound
    /// </summary>
    public void PlayButtonClickSound()
    {
        PlaySFX(buttonClickSound);
    }
    
    /// <summary>
    /// Get master volume
    /// </summary>
    public float GetMasterVolume()
    {
        return masterVolume;
    }
    
    /// <summary>
    /// Get music volume
    /// </summary>
    public float GetMusicVolume()
    {
        return musicVolume;
    }
    
    /// <summary>
    /// Get SFX volume
    /// </summary>
    public float GetSFXVolume()
    {
        return sfxVolume;
    }
}
