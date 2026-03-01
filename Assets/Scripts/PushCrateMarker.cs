using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushCrateMarker : MonoBehaviour
{
    [SerializeField] private float crateMass = 2.4f;
    [SerializeField] private float linearDamping = 1.8f;
    [SerializeField] private float angularDamping = 3f;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.mass = Mathf.Max(0.2f, crateMass);
        body.linearDamping = Mathf.Max(0f, linearDamping);
        body.angularDamping = Mathf.Max(0f, angularDamping);
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
}
