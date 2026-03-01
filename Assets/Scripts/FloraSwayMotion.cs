using UnityEngine;

public class FloraSwayMotion : MonoBehaviour
{
    [SerializeField] private float amplitude = 8f;
    [SerializeField] private float frequency = 1.1f;
    [SerializeField] private float twistAmplitude = 4f;
    [SerializeField] private float phaseOffset;

    private Quaternion baseRotation;

    public void Configure(float amplitude, float frequency, float twistAmplitude, float phaseOffset)
    {
        this.amplitude = Mathf.Clamp(amplitude, 0f, 24f);
        this.frequency = Mathf.Clamp(frequency, 0.1f, 4f);
        this.twistAmplitude = Mathf.Clamp(twistAmplitude, 0f, 18f);
        this.phaseOffset = phaseOffset;
        baseRotation = transform.localRotation;
    }

    private void Awake()
    {
        baseRotation = transform.localRotation;
    }

    private void Update()
    {
        float t = Time.time * frequency + phaseOffset;
        float pitch = Mathf.Sin(t) * amplitude;
        float roll = Mathf.Cos(t * 0.84f + 0.91f) * (amplitude * 0.56f);
        float yaw = Mathf.Sin(t * 0.67f + 1.37f) * twistAmplitude;
        transform.localRotation = baseRotation * Quaternion.Euler(pitch, yaw, roll);
    }
}
