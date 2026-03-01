using UnityEngine;

public enum MechanismFeedbackType
{
    GenericBarrier = 0,
    GateA = 1,
    GateB = 2,
    ExitGate = 3,
    RhythmGate = 4,
    RhythmGateA = 5,
    RhythmGateB = 6
}

[RequireComponent(typeof(AudioSource))]
public class GameFeedback : MonoBehaviour
{
    public static GameFeedback Instance { get; private set; }

    private AudioSource oneShotSource;
    private AudioClip switchActivatedClip;
    private AudioClip switchLockedClip;
    private AudioClip barrierUnlockedClip;
    private AudioClip gateAUnlockedClip;
    private AudioClip gateBUnlockedClip;
    private AudioClip exitGateUnlockedClip;
    private AudioClip rhythmGateUnlockedClip;
    private AudioClip rhythmGateAUnlockedClip;
    private AudioClip rhythmGateBUnlockedClip;
    private AudioClip rhythmGateClosedClip;
    private AudioClip chainStepClip;
    private AudioClip chainCompleteClip;
    private AudioClip bouncePadClip;
    private AudioClip rhythmSwitchClip;
    private AudioClip previewClip;

    private float feedbackIntensity = 1f;
    private bool cameraShakeEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        oneShotSource = GetComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.volume = 1f;

        BuildClips();
    }

    private void OnDestroy()
    {
        ReleaseClip(ref switchActivatedClip);
        ReleaseClip(ref switchLockedClip);
        ReleaseClip(ref barrierUnlockedClip);
        ReleaseClip(ref gateAUnlockedClip);
        ReleaseClip(ref gateBUnlockedClip);
        ReleaseClip(ref exitGateUnlockedClip);
        ReleaseClip(ref rhythmGateUnlockedClip);
        ReleaseClip(ref rhythmGateAUnlockedClip);
        ReleaseClip(ref rhythmGateBUnlockedClip);
        ReleaseClip(ref rhythmGateClosedClip);
        ReleaseClip(ref chainStepClip);
        ReleaseClip(ref chainCompleteClip);
        ReleaseClip(ref bouncePadClip);
        ReleaseClip(ref rhythmSwitchClip);
        ReleaseClip(ref previewClip);

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Configure(float intensity, bool enableCameraShake)
    {
        feedbackIntensity = Mathf.Clamp(intensity, 0f, 2f);
        cameraShakeEnabled = enableCameraShake;
    }

    public void PlaySwitchActivated()
    {
        Play(oneShotSource, switchActivatedClip, ScaleAudio(0.36f), Random.Range(0.98f, 1.04f));
        RuntimeHUD.Instance?.ShowAlertBanner("Switch activated", new Color(0.28f, 0.92f, 0.62f), 1.0f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.28f, 0.92f, 0.62f), 0.14f, ScaleVisual(0.12f));
    }

    public void PlaySwitchLocked()
    {
        Play(oneShotSource, switchLockedClip, ScaleAudio(0.32f), 1f);
        RuntimeHUD.Instance?.ShowAlertBanner("Switch locked", new Color(0.94f, 0.44f, 0.40f), 1.0f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.94f, 0.44f, 0.40f), 0.10f, ScaleVisual(0.08f));
    }

    public void PlayBarrierUnlocked(MechanismFeedbackType type)
    {
        AudioClip clip = barrierUnlockedClip;
        string text = "Barrier disabled";
        Color color = new Color(0.42f, 0.88f, 1f);
        float shakeAmplitude = 0.055f;
        float shakeDuration = 0.14f;
        float shakeFrequency = 23f;

        switch (type)
        {
            case MechanismFeedbackType.GateA:
                clip = gateAUnlockedClip;
                text = "Gate A unlocked";
                color = new Color(1f, 0.80f, 0.34f);
                shakeAmplitude = 0.07f;
                shakeDuration = 0.16f;
                shakeFrequency = 25f;
                break;
            case MechanismFeedbackType.GateB:
                clip = gateBUnlockedClip;
                text = "Gate B unlocked";
                color = new Color(0.52f, 0.95f, 1f);
                shakeAmplitude = 0.08f;
                shakeDuration = 0.18f;
                shakeFrequency = 27f;
                break;
            case MechanismFeedbackType.ExitGate:
                clip = exitGateUnlockedClip;
                text = "Exit gate unlocked";
                color = new Color(0.45f, 1f, 0.78f);
                shakeAmplitude = 0.11f;
                shakeDuration = 0.24f;
                shakeFrequency = 30f;
                break;
            case MechanismFeedbackType.RhythmGate:
                clip = rhythmGateUnlockedClip;
                text = "Rhythm gate open";
                color = new Color(0.96f, 0.76f, 0.30f);
                shakeAmplitude = 0.09f;
                shakeDuration = 0.18f;
                shakeFrequency = 28f;
                break;
            case MechanismFeedbackType.RhythmGateA:
                clip = rhythmGateAUnlockedClip;
                text = "Rhythm gate A open";
                color = new Color(0.98f, 0.72f, 0.28f);
                shakeAmplitude = 0.08f;
                shakeDuration = 0.16f;
                shakeFrequency = 26f;
                break;
            case MechanismFeedbackType.RhythmGateB:
                clip = rhythmGateBUnlockedClip;
                text = "Rhythm gate B open";
                color = new Color(0.30f, 0.90f, 1f);
                shakeAmplitude = 0.10f;
                shakeDuration = 0.18f;
                shakeFrequency = 29f;
                break;
        }

        Play(oneShotSource, clip, ScaleAudio(0.44f), 1f);
        RuntimeHUD.Instance?.ShowAlertBanner(text, color, 1.3f);
        RuntimeHUD.Instance?.FlashScreen(color, 0.15f, ScaleVisual(0.12f));
        ApplyShake(shakeAmplitude, shakeDuration, shakeFrequency);
    }

    public void PlayChainStepUnlocked()
    {
        Play(oneShotSource, chainStepClip, ScaleAudio(0.46f), 1f);
        RuntimeHUD.Instance?.ShowAlertBanner("Chain step A complete - B unlocked", new Color(1f, 0.82f, 0.34f), 1.45f);
        RuntimeHUD.Instance?.FlashScreen(new Color(1f, 0.82f, 0.34f), 0.18f, ScaleVisual(0.14f));
        ApplyShake(0.08f, 0.20f, 27f);
    }

    public void PlayChainCompleted()
    {
        Play(oneShotSource, chainCompleteClip, ScaleAudio(0.54f), 1f);
        RuntimeHUD.Instance?.ShowAlertBanner("Chain completed - route opened", new Color(0.35f, 1f, 0.76f), 1.6f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.35f, 1f, 0.76f), 0.24f, ScaleVisual(0.18f));
        ApplyShake(0.12f, 0.28f, 30f);
    }

    public void PlayPreview(Color accent)
    {
        Play(oneShotSource, previewClip, ScaleAudio(0.5f), 1f);
        RuntimeHUD.Instance?.ShowAlertBanner("Feedback preview", accent, 1.15f);
        RuntimeHUD.Instance?.FlashScreen(accent, 0.16f, ScaleVisual(0.14f));
        ApplyShake(0.06f, 0.18f, 25f);
    }

    public void PlayBouncePad()
    {
        Play(oneShotSource, bouncePadClip, ScaleAudio(0.46f), Random.Range(1.04f, 1.10f));
        RuntimeHUD.Instance?.ShowAlertBanner("Mega bounce", new Color(0.95f, 0.34f, 1f), 0.9f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.95f, 0.34f, 1f), 0.12f, ScaleVisual(0.11f));
        ApplyShake(0.07f, 0.16f, 26f);
    }

    public void PlayRhythmSwitchTriggered()
    {
        Play(oneShotSource, rhythmSwitchClip, ScaleAudio(0.40f), Random.Range(1.02f, 1.08f));
        RuntimeHUD.Instance?.ShowAlertBanner("Rhythm switch", new Color(0.82f, 0.90f, 1f), 0.9f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.80f, 0.88f, 1f), 0.11f, ScaleVisual(0.10f));
        ApplyShake(0.05f, 0.14f, 22f);
    }

    public void PlayRhythmGateClosed()
    {
        Play(oneShotSource, rhythmGateClosedClip, ScaleAudio(0.36f), 0.94f);
        RuntimeHUD.Instance?.ShowAlertBanner("Rhythm gate closed", new Color(0.92f, 0.45f, 0.35f), 0.9f);
        RuntimeHUD.Instance?.FlashScreen(new Color(0.92f, 0.45f, 0.35f), 0.10f, ScaleVisual(0.08f));
        ApplyShake(0.045f, 0.12f, 20f);
    }

    private void BuildClips()
    {
        switchActivatedClip = CreateToneClip("Sfx_SwitchActivated", 630f, 820f, 0.11f, 0.24f);
        switchLockedClip = CreateToneClip("Sfx_SwitchLocked", 230f, 170f, 0.12f, 0.22f);
        barrierUnlockedClip = CreateToneClip("Sfx_BarrierUnlocked", 420f, 920f, 0.16f, 0.24f);
        gateAUnlockedClip = CreateToneClip("Sfx_GateAUnlocked", 300f, 680f, 0.18f, 0.24f);
        gateBUnlockedClip = CreateToneClip("Sfx_GateBUnlocked", 520f, 1060f, 0.18f, 0.24f);
        exitGateUnlockedClip = CreateToneClip("Sfx_ExitGateUnlocked", 260f, 1400f, 0.26f, 0.24f);
        rhythmGateUnlockedClip = CreateToneClip("Sfx_RhythmGateOpen", 450f, 1180f, 0.16f, 0.24f);
        rhythmGateAUnlockedClip = CreateToneClip("Sfx_RhythmGateAOpen", 290f, 880f, 0.15f, 0.24f);
        rhythmGateBUnlockedClip = CreateToneClip("Sfx_RhythmGateBOpen", 620f, 1260f, 0.15f, 0.24f);
        rhythmGateClosedClip = CreateToneClip("Sfx_RhythmGateClosed", 440f, 210f, 0.14f, 0.23f);
        chainStepClip = CreateToneClip("Sfx_ChainStep", 380f, 1120f, 0.21f, 0.24f);
        chainCompleteClip = CreateToneClip("Sfx_ChainComplete", 340f, 1280f, 0.29f, 0.24f);
        bouncePadClip = CreateToneClip("Sfx_BouncePad", 340f, 1550f, 0.18f, 0.24f);
        rhythmSwitchClip = CreateToneClip("Sfx_RhythmSwitch", 720f, 980f, 0.09f, 0.22f);
        previewClip = CreateToneClip("Sfx_Preview", 460f, 1020f, 0.2f, 0.24f);
    }

    private float ScaleAudio(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume * feedbackIntensity);
    }

    private float ScaleVisual(float baseAlpha)
    {
        if (feedbackIntensity <= 0.01f)
        {
            return 0f;
        }

        float visualScale = Mathf.Lerp(0.1f, 1.5f, Mathf.Clamp01(feedbackIntensity / 2f));
        return Mathf.Clamp(baseAlpha * visualScale, 0f, 0.7f);
    }

    private void ApplyShake(float amplitude, float duration, float frequency)
    {
        if (!cameraShakeEnabled || feedbackIntensity <= 0.01f)
        {
            return;
        }

        float strength = Mathf.Lerp(0.3f, 1.45f, Mathf.Clamp01(feedbackIntensity / 2f));
        GravityCameraFollow.ActiveInstance?.AddShake(amplitude * strength, duration, frequency);
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        if (source == null || clip == null || volume <= 0f)
        {
            return;
        }

        source.pitch = Mathf.Clamp(pitch, 0.6f, 1.5f);
        source.PlayOneShot(clip, volume);
    }

    private static AudioClip CreateToneClip(string name, float frequencyStart, float frequencyEnd, float duration, float gain)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(64, Mathf.CeilToInt(sampleRate * Mathf.Max(0.04f, duration)));
        float[] samples = new float[sampleCount];

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);
            float frequency = Mathf.Lerp(frequencyStart, frequencyEnd, t);
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.2f));
            float envelope = attack * release;

            phase += (2f * Mathf.PI * frequency) / sampleRate;
            float harmonic = Mathf.Sin(phase * 2f) * 0.18f;
            samples[i] = (Mathf.Sin(phase) + harmonic) * envelope * gain;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static void ReleaseClip(ref AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        Destroy(clip);
        clip = null;
    }
}
