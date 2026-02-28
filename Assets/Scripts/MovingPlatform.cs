using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MovingPlatform : MonoBehaviour
{
    private sealed class SupportedBodyRecord
    {
        public Rigidbody Body;
        public float LastSeenTime;
    }

    [SerializeField] private Vector3 pointALocal = Vector3.zero;
    [SerializeField] private Vector3 pointBLocal = new Vector3(6f, 0f, 0f);
    [SerializeField] private float travelDuration = 2.8f;
    [SerializeField] private bool pingPongMotion = true;
    [SerializeField] private bool smoothStepMotion = true;
    [SerializeField] private float initialProgress = 0f;
    [SerializeField] private float carryNormalThreshold = 0.35f;
    [SerializeField] private float supportContactGraceSeconds = 0.12f;

    private readonly Dictionary<int, SupportedBodyRecord> supportedBodies = new Dictionary<int, SupportedBodyRecord>();
    private readonly List<int> cleanupBuffer = new List<int>();

    private Rigidbody platformBody;
    private Vector3 worldPointA;
    private Vector3 worldPointB;
    private float progressTimer;
    private Transform cachedParent;
    private Vector3 cachedParentPosition;
    private Quaternion cachedParentRotation;
    private Vector3 cachedParentScale;

    public void Configure(
        Vector3 localPointA,
        Vector3 localPointB,
        float duration,
        bool pingPong = true,
        float startProgress = 0f)
    {
        pointALocal = localPointA;
        pointBLocal = localPointB;
        travelDuration = Mathf.Max(0.25f, duration);
        pingPongMotion = pingPong;
        initialProgress = Mathf.Clamp01(startProgress);

        CacheParentTransformState();
        RebuildPath();
        progressTimer = initialProgress;
        SetPlatformPositionImmediate(EvaluatePosition(initialProgress));
    }

    private void Awake()
    {
        platformBody = GetComponent<Rigidbody>();
        platformBody.isKinematic = true;
        platformBody.interpolation = RigidbodyInterpolation.Interpolate;
        platformBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        platformBody.useGravity = false;

        CacheParentTransformState();
        RebuildPath();
        progressTimer = Mathf.Clamp01(initialProgress);
        SetPlatformPositionImmediate(EvaluatePosition(progressTimer));
    }

    private void OnEnable()
    {
        CacheParentTransformState();
        RebuildPath();
        supportedBodies.Clear();
        cleanupBuffer.Clear();
    }

    private void OnDisable()
    {
        supportedBodies.Clear();
        cleanupBuffer.Clear();
    }

    private void FixedUpdate()
    {
        UpdatePathIfParentChanged();

        float duration = Mathf.Max(0.25f, travelDuration);
        progressTimer += Time.fixedDeltaTime / duration;

        float normalized = pingPongMotion
            ? Mathf.PingPong(progressTimer, 1f)
            : Mathf.Repeat(progressTimer, 1f);

        Vector3 targetPosition = EvaluatePosition(normalized);
        Vector3 delta = targetPosition - transform.position;
        platformBody.MovePosition(targetPosition);
        CarrySupportedBodies(delta);
        CleanupSupportedBodies();
    }

    private void OnTransformParentChanged()
    {
        CacheParentTransformState();
        RebuildPath();
    }

    private void OnCollisionStay(Collision collision)
    {
        Rigidbody body = collision.rigidbody;
        if (body == null || body == platformBody || body.isKinematic)
        {
            return;
        }

        Vector3 upDir = GravitySystem.Instance != null ? GravitySystem.Instance.UpDirection : Vector3.up;
        int contactCount = collision.contactCount;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (Vector3.Dot(contact.normal, upDir) > carryNormalThreshold)
            {
                int id = body.GetInstanceID();
                if (supportedBodies.TryGetValue(id, out SupportedBodyRecord record))
                {
                    record.LastSeenTime = Time.time;
                    if (record.Body == null)
                    {
                        record.Body = body;
                    }
                }
                else
                {
                    supportedBodies[id] = new SupportedBodyRecord
                    {
                        Body = body,
                        LastSeenTime = Time.time
                    };
                }
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        Rigidbody body = collision.rigidbody;
        if (body != null)
        {
            supportedBodies.Remove(body.GetInstanceID());
        }
    }

    private void CarrySupportedBodies(Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f || supportedBodies.Count == 0)
        {
            return;
        }

        float staleAfter = Mathf.Max(0.02f, supportContactGraceSeconds);
        foreach (KeyValuePair<int, SupportedBodyRecord> kvp in supportedBodies)
        {
            SupportedBodyRecord record = kvp.Value;
            Rigidbody body = record != null ? record.Body : null;
            if (body == null || body.isKinematic)
            {
                cleanupBuffer.Add(kvp.Key);
                continue;
            }

            bool stale = record == null || (Time.time - record.LastSeenTime) > staleAfter;
            if (stale)
            {
                cleanupBuffer.Add(kvp.Key);
                continue;
            }

            body.MovePosition(body.position + delta);
        }
    }

    private void CleanupSupportedBodies()
    {
        if (cleanupBuffer.Count == 0)
        {
            return;
        }

        for (int i = 0; i < cleanupBuffer.Count; i++)
        {
            supportedBodies.Remove(cleanupBuffer[i]);
        }
        cleanupBuffer.Clear();
    }

    private void RebuildPath()
    {
        Transform parent = transform.parent;
        if (parent != null)
        {
            worldPointA = parent.TransformPoint(pointALocal);
            worldPointB = parent.TransformPoint(pointBLocal);
            return;
        }

        worldPointA = pointALocal;
        worldPointB = pointBLocal;
    }

    private void CacheParentTransformState()
    {
        cachedParent = transform.parent;
        if (cachedParent == null)
        {
            cachedParentPosition = Vector3.zero;
            cachedParentRotation = Quaternion.identity;
            cachedParentScale = Vector3.one;
            return;
        }

        cachedParentPosition = cachedParent.position;
        cachedParentRotation = cachedParent.rotation;
        cachedParentScale = cachedParent.lossyScale;
    }

    private void UpdatePathIfParentChanged()
    {
        Transform parent = transform.parent;
        if (parent != cachedParent)
        {
            CacheParentTransformState();
            RebuildPath();
            return;
        }

        if (parent == null)
        {
            return;
        }

        bool moved = (parent.position - cachedParentPosition).sqrMagnitude > 0.000001f
            || Quaternion.Angle(parent.rotation, cachedParentRotation) > 0.01f
            || (parent.lossyScale - cachedParentScale).sqrMagnitude > 0.000001f;

        if (!moved)
        {
            return;
        }

        CacheParentTransformState();
        RebuildPath();
    }

    private Vector3 EvaluatePosition(float t)
    {
        t = Mathf.Clamp01(t);
        if (smoothStepMotion)
        {
            t = t * t * (3f - 2f * t);
        }

        return Vector3.Lerp(worldPointA, worldPointB, t);
    }

    private void SetPlatformPositionImmediate(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        if (platformBody != null)
        {
            platformBody.position = worldPosition;
        }
    }
}
