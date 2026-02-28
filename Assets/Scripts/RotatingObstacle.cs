using UnityEngine;

public class RotatingObstacle : MonoBehaviour
{
    [SerializeField] private Vector3 localAxis = Vector3.up;
    [SerializeField] private float speedDegPerSec = 120f;
    [SerializeField] private bool pingPong = false;
    [SerializeField] private float swingAngle = 95f;

    private Quaternion initialRotation;
    private float timeOffset;

    public void Configure(float speed, bool oscillate = false, float angle = 95f)
    {
        speedDegPerSec = Mathf.Clamp(speed, 10f, 420f);
        pingPong = oscillate;
        swingAngle = Mathf.Clamp(angle, 25f, 170f);
    }

    private void Awake()
    {
        initialRotation = transform.localRotation;
        timeOffset = Random.value * 7f;
    }

    private void Update()
    {
        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        if (!pingPong)
        {
            transform.Rotate(axis, speedDegPerSec * Time.deltaTime, Space.Self);
            return;
        }

        float angle = Mathf.Sin((Time.time + timeOffset) * speedDegPerSec * Mathf.Deg2Rad * 0.24f) * swingAngle;
        transform.localRotation = initialRotation * Quaternion.AngleAxis(angle, axis);
    }
}
