using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GravityCameraFollow : MonoBehaviour
{
    public static GravityCameraFollow ActiveInstance { get; private set; }

    [SerializeField] private Transform target;
    [SerializeField] private float followDistance = 9.2f;
    [SerializeField] private float focusHeight = 1.35f;
    [SerializeField] private float followSmoothTime = 0.12f;
    [SerializeField] private float rotationSharpness = 11f;
    [SerializeField] private float yawSpeed = 1.35f;
    [SerializeField] private float pitchSpeed = 1.25f;
    [SerializeField] private float maxLookDeltaPerFrame = 10f;
    [SerializeField] private float minPitch = -55f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float collisionRadius = 0.28f;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private float postFlipLookLock = 0.10f;
    [SerializeField] private float postFlipRotationBoostDuration = 0.35f;
    [SerializeField] private float postFlipRotationSharpness = 20f;
    [SerializeField] private float autoRecenterSpeed = 0.22f;
    [SerializeField] private float autoRecenterDelay = 0.28f;
    [SerializeField] private float velocityLeadTime = 0.05f;
    [SerializeField] private float verticalVelocityLead = 0.04f;
    [SerializeField] private float accelerationLagStrength = 0.012f;
    [SerializeField] private float inertiaBlendSharpness = 9f;
    [SerializeField] private float maxInertiaOffset = 0.42f;

    private Vector3 followVelocity;
    private float yaw;
    private float pitch = 10f;
    private float mouseSensitivity = 1f;
    private float noLookTime;
    private float postFlipLookLockLeft;
    private float postFlipRotationBoostLeft;
    private GravitySystem subscribedGravity;
    private Vector3 smoothedTargetVelocity;
    private Vector3 previousTargetVelocity;
    private Vector3 smoothedTargetAcceleration;
    private Vector3 inertiaOffset;

    private float shakeTimeLeft;
    private float shakeDuration;
    private float shakeAmplitude;
    private float shakeFrequency = 26f;
    private float motionResponseNormalized = 0.56f;
    private MotionIntensityPreset motionPreset = MotionIntensityPreset.Normal;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        smoothedTargetVelocity = Vector3.zero;
        previousTargetVelocity = Vector3.zero;
        smoothedTargetAcceleration = Vector3.zero;
        inertiaOffset = Vector3.zero;
        AlignYawWithTarget();
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = Mathf.Clamp(sensitivity, 0.4f, 3.2f);
    }

    public void SetFieldOfView(float value)
    {
        float clamped = Mathf.Clamp(value, 55f, 95f);
        Camera camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.fieldOfView = clamped;
        }
    }

    public void SetMotionResponse(float normalized)
    {
        motionResponseNormalized = Mathf.Clamp01(normalized);
        RebuildMotionTuning();
    }

    public void SetMotionPreset(MotionIntensityPreset preset)
    {
        motionPreset = (MotionIntensityPreset)Mathf.Clamp((int)preset, 0, 2);
        RebuildMotionTuning();
    }

    public void SetAutoRecenterSpeed(float value)
    {
        autoRecenterSpeed = Mathf.Clamp(value, 0f, 2f);
    }

    public void AddShake(float amplitude, float duration, float frequency = 26f)
    {
        if (amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeTimeLeft = Mathf.Max(shakeTimeLeft, duration);
        shakeFrequency = Mathf.Max(4f, frequency);
    }

    private void OnValidate()
    {
        followDistance = Mathf.Max(0.5f, followDistance);
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        rotationSharpness = Mathf.Max(0.1f, rotationSharpness);
        yawSpeed = Mathf.Max(0.01f, yawSpeed);
        pitchSpeed = Mathf.Max(0.01f, pitchSpeed);
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        autoRecenterSpeed = Mathf.Max(0f, autoRecenterSpeed);
        autoRecenterDelay = Mathf.Max(0f, autoRecenterDelay);
        velocityLeadTime = Mathf.Clamp(velocityLeadTime, 0f, 0.2f);
        verticalVelocityLead = Mathf.Clamp(verticalVelocityLead, 0f, 0.2f);
        accelerationLagStrength = Mathf.Clamp(accelerationLagStrength, 0f, 0.05f);
        inertiaBlendSharpness = Mathf.Max(0.1f, inertiaBlendSharpness);
        maxInertiaOffset = Mathf.Clamp(maxInertiaOffset, 0f, 1.5f);
        motionResponseNormalized = Mathf.Clamp01(motionResponseNormalized);
        motionPreset = (MotionIntensityPreset)Mathf.Clamp((int)motionPreset, 0, 2);
        RebuildMotionTuning();
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        RebuildMotionTuning();
        RebindGravitySubscription();
        previousTargetVelocity = GetTargetVelocity();
        smoothedTargetVelocity = previousTargetVelocity;
        smoothedTargetAcceleration = Vector3.zero;
        inertiaOffset = Vector3.zero;
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        if (subscribedGravity != null)
        {
            subscribedGravity.OnGravityChanged -= HandleGravityChanged;
            subscribedGravity = null;
        }
    }

    private void Start()
    {
        RebuildMotionTuning();
        AlignYawWithTarget();
        RebindGravitySubscription();
        previousTargetVelocity = GetTargetVelocity();
        smoothedTargetVelocity = previousTargetVelocity;
        smoothedTargetAcceleration = Vector3.zero;
        inertiaOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        RebindGravitySubscription();
        if (postFlipLookLockLeft > 0f)
        {
            postFlipLookLockLeft = Mathf.Max(0f, postFlipLookLockLeft - Time.deltaTime);
        }

        if (postFlipRotationBoostLeft > 0f)
        {
            postFlipRotationBoostLeft = Mathf.Max(0f, postFlipRotationBoostLeft - Time.deltaTime);
        }

        GravitySystem gravity = GravitySystem.Instance;
        Vector3 upDir = gravity != null ? gravity.UpDirection : Vector3.up;
        Vector3 downDir = gravity != null ? gravity.DownDirection : Vector3.down;

        bool lookedThisFrame = false;
        if (GameDirector.AllowGameplayInput && postFlipLookLockLeft <= 0f)
        {
            Vector2 lookDelta = RuntimeInput.ReadLookDelta();
            if (maxLookDeltaPerFrame > 0f)
            {
                lookDelta.x = Mathf.Clamp(lookDelta.x, -maxLookDeltaPerFrame, maxLookDeltaPerFrame);
                lookDelta.y = Mathf.Clamp(lookDelta.y, -maxLookDeltaPerFrame, maxLookDeltaPerFrame);
            }
            if (lookDelta.sqrMagnitude > 0.000001f)
            {
                lookedThisFrame = true;
                yaw += lookDelta.x * yawSpeed * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - lookDelta.y * pitchSpeed * mouseSensitivity, minPitch, maxPitch);
            }
        }

        if (lookedThisFrame)
        {
            noLookTime = 0f;
        }
        else
        {
            noLookTime += Time.deltaTime;
            TryApplyAutoRecenter(downDir);
        }

        Vector3 referenceForward = GetPlanarReferenceForward(downDir);

        Quaternion yawRotation = Quaternion.AngleAxis(yaw, upDir);
        Vector3 horizontalLook = Vector3.ProjectOnPlane(yawRotation * referenceForward, downDir).normalized;
        if (horizontalLook.sqrMagnitude < 0.001f)
        {
            horizontalLook = Vector3.ProjectOnPlane(target.forward, downDir).normalized;
        }
        if (horizontalLook.sqrMagnitude < 0.001f)
        {
            horizontalLook = referenceForward;
        }

        Vector3 rightAxis = Vector3.Cross(upDir, horizontalLook).normalized;
        if (rightAxis.sqrMagnitude < 0.001f)
        {
            rightAxis = Vector3.Cross(upDir, Vector3.forward).normalized;
        }

        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, rightAxis);
        Vector3 lookDirection = (pitchRotation * horizontalLook).normalized;

        UpdateInertiaOffsets(downDir, upDir, Time.deltaTime, out Vector3 focusLeadOffset, out Vector3 lagOffset);
        Vector3 focusPoint = target.position + upDir * focusHeight + focusLeadOffset;
        Vector3 desiredPosition = focusPoint - lookDirection * followDistance + lagOffset;
        Vector3 resolvedPosition = ResolveObstruction(focusPoint, desiredPosition);
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, resolvedPosition, ref followVelocity, followSmoothTime);
        Vector3 finalPosition = ApplyCameraShake(smoothedPosition);
        transform.position = finalPosition;

        Quaternion desiredRotation = Quaternion.LookRotation((focusPoint - finalPosition).normalized, upDir);
        float sharpness = postFlipRotationBoostLeft > 0f
            ? postFlipRotationSharpness
            : rotationSharpness;
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, sharpness * Time.deltaTime);
    }

    private void AlignYawWithTarget()
    {
        if (target == null)
        {
            return;
        }

        GravitySystem gravity = GravitySystem.Instance;
        Vector3 downDir = gravity != null ? gravity.DownDirection : Vector3.down;
        Vector3 planarForward = Vector3.ProjectOnPlane(target.forward, downDir);
        if (planarForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 reference = GetPlanarReferenceForward(downDir);

        float signed = Vector3.SignedAngle(reference, planarForward.normalized, -downDir);
        yaw = signed;
    }

    private static Vector3 GetPlanarReferenceForward(Vector3 downDir)
    {
        Vector3 reference = Vector3.ProjectOnPlane(Vector3.forward, downDir);
        if (reference.sqrMagnitude < 0.001f)
        {
            reference = Vector3.ProjectOnPlane(Vector3.right, downDir);
        }
        if (reference.sqrMagnitude < 0.001f)
        {
            reference = Vector3.Cross(Vector3.up, downDir);
        }
        if (reference.sqrMagnitude < 0.001f)
        {
            reference = Vector3.forward;
        }

        return reference.normalized;
    }

    private Vector3 ResolveObstruction(Vector3 focusPoint, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - focusPoint;
        float distance = direction.magnitude;
        if (distance < 0.001f)
        {
            return desiredPosition;
        }

        direction /= distance;
        if (Physics.SphereCast(focusPoint, collisionRadius, direction, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            if (target != null && hit.collider != null && hit.collider.transform.IsChildOf(target))
            {
                return desiredPosition;
            }

            float safeDistance = Mathf.Max(0.45f, hit.distance - 0.1f);
            return focusPoint + direction * safeDistance;
        }

        return desiredPosition;
    }

    private Vector3 ApplyCameraShake(Vector3 basePosition)
    {
        if (shakeTimeLeft <= 0f || shakeAmplitude <= 0f)
        {
            return basePosition;
        }

        shakeTimeLeft = Mathf.Max(0f, shakeTimeLeft - Time.deltaTime);
        float normalized = shakeDuration > 0.0001f ? shakeTimeLeft / shakeDuration : 0f;
        float amplitude = shakeAmplitude * normalized;

        float t = Time.time * shakeFrequency;
        float offsetX = (Mathf.PerlinNoise(t, 0.11f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0.27f, t) - 0.5f) * 2f;
        Vector3 offset = (transform.right * offsetX + transform.up * offsetY) * amplitude;

        if (shakeTimeLeft <= 0f)
        {
            shakeAmplitude = 0f;
            shakeDuration = 0f;
        }

        return basePosition + offset;
    }

    private void RebindGravitySubscription()
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity == null || gravity == subscribedGravity)
        {
            return;
        }

        if (subscribedGravity != null)
        {
            subscribedGravity.OnGravityChanged -= HandleGravityChanged;
        }

        subscribedGravity = gravity;
        subscribedGravity.OnGravityChanged += HandleGravityChanged;
    }

    private void HandleGravityChanged(Vector3 _)
    {
        postFlipLookLockLeft = postFlipLookLock;
        postFlipRotationBoostLeft = postFlipRotationBoostDuration;
        noLookTime = 0f;
    }

    private void RebuildMotionTuning()
    {
        float t = Mathf.Clamp01(motionResponseNormalized);
        float baseFollowSmooth = Mathf.Lerp(0.22f, 0.06f, t);
        float baseRotationSharpness = Mathf.Lerp(8f, 19f, t);
        float basePostFlipLookLock = Mathf.Lerp(0.14f, 0.05f, t);
        float basePostFlipRotationSharpness = Mathf.Lerp(14f, 26f, t);
        float baseInertiaBlendSharpness = Mathf.Lerp(6f, 14f, t);
        float baseVelocityLeadTime = Mathf.Lerp(0.035f, 0.075f, t);
        float baseVerticalLeadTime = Mathf.Lerp(0.022f, 0.055f, t);
        float baseAccelerationLagStrength = Mathf.Lerp(0.008f, 0.017f, t);
        float baseMaxOffset = Mathf.Lerp(0.32f, 0.48f, t);

        float smoothMul = 1f;
        float rotationMul = 1f;
        float lookLockMul = 1f;
        float postFlipRotationMul = 1f;
        float inertiaBlendMul = 1f;
        float leadMul = 1f;
        float verticalLeadMul = 1f;
        float lagMul = 1f;
        float offsetMul = 1f;

        switch (motionPreset)
        {
            case MotionIntensityPreset.Realistic:
                smoothMul = 1.05f;
                rotationMul = 0.94f;
                lookLockMul = 1.08f;
                postFlipRotationMul = 0.96f;
                inertiaBlendMul = 1.10f;
                leadMul = 0.88f;
                verticalLeadMul = 0.86f;
                lagMul = 0.86f;
                offsetMul = 0.82f;
                break;
            case MotionIntensityPreset.Cinematic:
                smoothMul = 1.22f;
                rotationMul = 0.88f;
                lookLockMul = 1.12f;
                postFlipRotationMul = 1.02f;
                inertiaBlendMul = 0.84f;
                leadMul = 1.25f;
                verticalLeadMul = 1.20f;
                lagMul = 1.35f;
                offsetMul = 1.45f;
                break;
        }

        followSmoothTime = Mathf.Clamp(baseFollowSmooth * smoothMul, 0.04f, 0.45f);
        rotationSharpness = Mathf.Clamp(baseRotationSharpness * rotationMul, 4f, 28f);
        postFlipLookLock = Mathf.Clamp(basePostFlipLookLock * lookLockMul, 0.02f, 0.25f);
        postFlipRotationSharpness = Mathf.Clamp(basePostFlipRotationSharpness * postFlipRotationMul, 8f, 32f);
        inertiaBlendSharpness = Mathf.Clamp(baseInertiaBlendSharpness * inertiaBlendMul, 2f, 20f);
        velocityLeadTime = Mathf.Clamp(baseVelocityLeadTime * leadMul, 0f, 0.2f);
        verticalVelocityLead = Mathf.Clamp(baseVerticalLeadTime * verticalLeadMul, 0f, 0.2f);
        accelerationLagStrength = Mathf.Clamp(baseAccelerationLagStrength * lagMul, 0f, 0.05f);
        maxInertiaOffset = Mathf.Clamp(baseMaxOffset * offsetMul, 0f, 1.5f);
    }

    private void UpdateInertiaOffsets(
        Vector3 downDir,
        Vector3 upDir,
        float deltaTime,
        out Vector3 focusLeadOffset,
        out Vector3 lagOffset)
    {
        Vector3 targetVelocity = GetTargetVelocity();
        if (deltaTime <= 0.0001f)
        {
            smoothedTargetVelocity = targetVelocity;
            smoothedTargetAcceleration = Vector3.zero;
            previousTargetVelocity = targetVelocity;
            focusLeadOffset = Vector3.zero;
            lagOffset = inertiaOffset;
            return;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(1f, inertiaBlendSharpness) * deltaTime);
        smoothedTargetVelocity = Vector3.Lerp(smoothedTargetVelocity, targetVelocity, blend);

        Vector3 rawAcceleration = (targetVelocity - previousTargetVelocity) / deltaTime;
        previousTargetVelocity = targetVelocity;
        smoothedTargetAcceleration = Vector3.Lerp(smoothedTargetAcceleration, rawAcceleration, blend);

        Vector3 planarVelocity = Vector3.ProjectOnPlane(smoothedTargetVelocity, downDir);
        float verticalVelocity = Vector3.Dot(smoothedTargetVelocity, upDir);
        focusLeadOffset = planarVelocity * Mathf.Clamp(velocityLeadTime, 0f, 0.2f);
        focusLeadOffset += upDir * Mathf.Clamp(
            verticalVelocity * Mathf.Clamp(verticalVelocityLead, 0f, 0.2f),
            -0.35f,
            0.35f
        );

        float lagStrength = Mathf.Clamp(accelerationLagStrength, 0f, 0.05f);
        float lagLimit = Mathf.Max(0f, maxInertiaOffset);
        Vector3 planarAccel = Vector3.ProjectOnPlane(smoothedTargetAcceleration, downDir);
        Vector3 targetLag = Vector3.ClampMagnitude(-planarAccel * lagStrength, lagLimit);
        float verticalLagLimit = lagLimit * 0.35f;
        float verticalLag = Mathf.Clamp(
            -Vector3.Dot(smoothedTargetAcceleration, upDir) * lagStrength * 0.55f,
            -verticalLagLimit,
            verticalLagLimit
        );
        targetLag += upDir * verticalLag;

        inertiaOffset = Vector3.Lerp(inertiaOffset, targetLag, blend);
        lagOffset = inertiaOffset;
    }

    private void TryApplyAutoRecenter(Vector3 downDir)
    {
        if (autoRecenterSpeed <= 0.0001f || noLookTime < autoRecenterDelay || target == null)
        {
            return;
        }

        if (GetTargetPlanarSpeed(downDir) < 0.45f)
        {
            return;
        }

        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, downDir);
        if (targetForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 referenceForward = GetPlanarReferenceForward(downDir);
        float targetYaw = Vector3.SignedAngle(referenceForward, targetForward.normalized, -downDir);
        float maxStep = Mathf.Lerp(24f, 150f, Mathf.Clamp01(autoRecenterSpeed * 0.5f)) * Time.deltaTime;
        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, maxStep);
    }

    private float GetTargetPlanarSpeed(Vector3 downDir)
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(GetTargetVelocity(), downDir);
        return planarVelocity.magnitude;
    }

    private Vector3 GetTargetVelocity()
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }
}
