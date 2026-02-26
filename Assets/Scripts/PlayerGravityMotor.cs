using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerGravityMotor : MonoBehaviour
{
    public static event Action<PlayerGravityMotor> OnAnyRespawned;

    public event Action OnRespawned;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float groundAcceleration = 42f;
    [SerializeField] private float airAcceleration = 17f;
    [SerializeField] private float jumpImpulse = 9.2f;
    [SerializeField] private float maxFallSpeed = 35f;
    [SerializeField] private bool instantGroundStartResponse = true;
    [SerializeField] private bool instantGroundStopOnRelease = true;
    [SerializeField] private bool instantAirStopOnRelease = true;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float jumpCutGravityMultiplier = 2.1f;
    [SerializeField] private int maxAirJumps = 1;

    [Header("Grounding")]
    [SerializeField] private float groundProbeDistance = 0.25f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Alignment")]
    [SerializeField] private float rotationSharpness = 12f;
    [SerializeField] private float postFlipInputLock = 0.1f;
    [SerializeField] private float respawnInputLock = 0.15f;
    [SerializeField, Range(8f, 90f)] private float hardUprightSnapAngle = 38f;
    [SerializeField] private float uprightRecoverySpeedDegPerSec = 540f;
    [SerializeField] private float postFlipUprightAssistDuration = 0.22f;
    [SerializeField, Range(4f, 60f)] private float postFlipGroundSnapAngle = 12f;
    [SerializeField, Range(8f, 80f)] private float postFlipAirSnapAngle = 24f;
    [SerializeField, Range(1f, 4f)] private float postFlipRecoveryBoost = 1.9f;

    [Header("Sticky Zone Rule")]
    [SerializeField] private float nonStickyDetachAcceleration = 18f;
    [SerializeField] private float nonStickyPlanarDamping = 0.58f;
    [SerializeField] private float nonStickyHintCooldown = 0.9f;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private GravitySystem subscribedGravity;
    private Transform cachedCameraTransform;
    private int cachedCameraFrame = -1;

    private Vector3 spawnPoint;
    private Vector2 moveInput;
    private bool grounded;
    private bool jumpHeld;
    private float inputLockLeft;
    private float coyoteLeft;
    private float jumpBufferLeft;
    private Vector3 lastMoveDirection = Vector3.forward;
    private float planarSpeed;
    private int airJumpsRemaining;
    private bool touchedNonStickyInvertedSurface;
    private float nextNonStickyHintTime;
    private float postFlipUprightAssistLeft;
    private readonly HashSet<int> stickyRuleZoneOwners = new HashSet<int>();

    public bool IsGrounded => grounded;
    public float PlanarSpeed => planarSpeed;
    public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

    public void SetSpawnPoint(Vector3 point)
    {
        spawnPoint = point;
    }

    public void SetStickyRuleZoneActive(UnityEngine.Object owner, bool active)
    {
        if (owner == null)
        {
            return;
        }

        int id = owner.GetInstanceID();
        if (active)
        {
            stickyRuleZoneOwners.Add(id);
        }
        else
        {
            stickyRuleZoneOwners.Remove(id);
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.mass = 1f;

        spawnPoint = transform.position;
        airJumpsRemaining = Mathf.Max(0, maxAirJumps);
    }

    private void OnEnable()
    {
        EnsureGravitySubscription();
    }

    private void OnDisable()
    {
        if (subscribedGravity != null)
        {
            subscribedGravity.OnGravityChanged -= HandleGravityChanged;
            subscribedGravity = null;
        }

        stickyRuleZoneOwners.Clear();
    }

    private void Update()
    {
        EnsureGravitySubscription();

        moveInput = RuntimeInput.ReadMove();
        if (RuntimeInput.JumpPressedThisFrame())
        {
            jumpBufferLeft = jumpBufferTime;
        }
        jumpHeld = RuntimeInput.JumpHeld();

        if (inputLockLeft > 0f)
        {
            inputLockLeft = Mathf.Max(0f, inputLockLeft - Time.deltaTime);
        }

        if (jumpBufferLeft > 0f)
        {
            jumpBufferLeft = Mathf.Max(0f, jumpBufferLeft - Time.deltaTime);
        }

        AlignToGravity(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity == null)
        {
            return;
        }

        Vector3 downDir = gravity.DownDirection;
        Vector3 upDir = gravity.UpDirection;
        if (postFlipUprightAssistLeft > 0f)
        {
            postFlipUprightAssistLeft = Mathf.Max(0f, postFlipUprightAssistLeft - Time.fixedDeltaTime);
        }

        touchedNonStickyInvertedSurface = false;
        grounded = CheckGrounded(downDir);
        coyoteLeft = grounded ? coyoteTime : Mathf.Max(0f, coyoteLeft - Time.fixedDeltaTime);
        if (grounded)
        {
            airJumpsRemaining = Mathf.Max(0, maxAirJumps);
        }

        // Sample movement again on physics tick to avoid one-step release lag.
        moveInput = RuntimeInput.ReadMove();
        Vector3 moveDirection = CalculateMoveDirection(downDir);
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = moveDirection;
        }

        Vector3 velocity = body.linearVelocity;
        float verticalSpeed = Vector3.Dot(velocity, upDir);
        verticalSpeed = Mathf.Max(verticalSpeed, -maxFallSpeed);
        Vector3 verticalVelocity = upDir * verticalSpeed;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, downDir);

        bool hasMoveInput = moveDirection.sqrMagnitude > 0.0001f;
        bool noMoveOrLocked = !hasMoveInput || inputLockLeft > 0f;
        bool treatAsGroundedForStop = grounded || coyoteLeft > 0f;
        bool shouldStopOnRelease = (instantGroundStopOnRelease && treatAsGroundedForStop)
            || (instantAirStopOnRelease && !treatAsGroundedForStop);
        if (noMoveOrLocked && shouldStopOnRelease)
        {
            planarVelocity = Vector3.zero;
        }
        else
        {
            float acceleration = grounded ? groundAcceleration : airAcceleration;
            Vector3 targetPlanarVelocity = ResolveTargetPlanarVelocity(moveDirection, planarVelocity);
            if (grounded && instantGroundStartResponse && hasMoveInput && inputLockLeft <= 0f)
            {
                planarVelocity = targetPlanarVelocity;
            }
            else
            {
                planarVelocity = Vector3.MoveTowards(planarVelocity, targetPlanarVelocity, acceleration * Time.fixedDeltaTime);
            }
        }
        planarSpeed = planarVelocity.magnitude;

        body.linearVelocity = planarVelocity + verticalVelocity;
        body.angularVelocity = Vector3.zero;
        ApplyNonStickySurfaceSlip(upDir);
        ApplyJumpCutIfNeeded(downDir, upDir);
        StabilizeUpright(upDir, downDir);

        if (jumpBufferLeft > 0f)
        {
            AttemptBufferedJump(upDir);
        }
    }

    private Vector3 ResolveTargetPlanarVelocity(Vector3 moveDirection, Vector3 currentPlanarVelocity)
    {
        if (inputLockLeft > 0f)
        {
            return Vector3.zero;
        }

        float baseSpeed = Mathf.Max(0.1f, moveSpeed);
        if (grounded)
        {
            return moveDirection * baseSpeed;
        }

        // Keep airborne momentum from bounce pads / moving platforms unless
        // the player is intentionally steering in another direction.
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            return currentPlanarVelocity;
        }

        Vector3 desiredDirection = moveDirection.normalized;
        float alongDesired = Vector3.Dot(currentPlanarVelocity, desiredDirection);
        if (alongDesired > baseSpeed)
        {
            return desiredDirection * alongDesired;
        }

        return desiredDirection * baseSpeed;
    }

    public void Respawn()
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity != null)
        {
            gravity.ResetToDefault();
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.position = spawnPoint;
        cachedCameraTransform = null;
        cachedCameraFrame = -1;

        Vector3 downDir = gravity != null ? gravity.DownDirection : Vector3.down;
        Vector3 upDir = -downDir;
        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(lastMoveDirection, downDir);
        if (forwardOnPlane.sqrMagnitude < 0.001f)
        {
            forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, downDir);
        }
        if (forwardOnPlane.sqrMagnitude < 0.001f)
        {
            forwardOnPlane = Vector3.ProjectOnPlane(Vector3.forward, downDir);
        }

        transform.rotation = Quaternion.LookRotation(forwardOnPlane.normalized, upDir);
        inputLockLeft = respawnInputLock;
        jumpBufferLeft = 0f;
        coyoteLeft = 0f;
        grounded = false;
        touchedNonStickyInvertedSurface = false;
        stickyRuleZoneOwners.Clear();
        airJumpsRemaining = Mathf.Max(0, maxAirJumps);
        postFlipUprightAssistLeft = 0f;

        OnRespawned?.Invoke();
        OnAnyRespawned?.Invoke(this);
    }

    private void ApplyJumpCutIfNeeded(Vector3 downDir, Vector3 upDir)
    {
        if (grounded || jumpHeld)
        {
            return;
        }

        float upwardSpeed = Vector3.Dot(body.linearVelocity, upDir);
        if (upwardSpeed <= 0f)
        {
            return;
        }

        float gravityStrength = Physics.gravity.magnitude;
        float extraGravity = gravityStrength * Mathf.Max(0f, jumpCutGravityMultiplier - 1f);
        if (extraGravity <= 0.01f)
        {
            return;
        }

        body.AddForce(downDir * extraGravity, ForceMode.Acceleration);
    }

    private void EnsureGravitySubscription()
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity == null || subscribedGravity == gravity)
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
        inputLockLeft = postFlipInputLock;
        postFlipUprightAssistLeft = Mathf.Max(postFlipUprightAssistLeft, Mathf.Max(0f, postFlipUprightAssistDuration));
        if (body != null)
        {
            body.angularVelocity = Vector3.zero;
        }
        SnapOrientationToCurrentGravity();
    }

    private void AttemptBufferedJump(Vector3 upDir)
    {
        if (inputLockLeft > 0f)
        {
            return;
        }

        bool useGroundJump = grounded || coyoteLeft > 0f;
        bool useAirJump = !useGroundJump && airJumpsRemaining > 0;
        bool canJump = useGroundJump || useAirJump;
        if (!canJump)
        {
            return;
        }

        float verticalSpeed = Vector3.Dot(body.linearVelocity, upDir);
        if (verticalSpeed < 0f)
        {
            body.linearVelocity -= upDir * verticalSpeed;
        }

        body.AddForce(upDir * jumpImpulse, ForceMode.VelocityChange);
        jumpBufferLeft = 0f;
        coyoteLeft = 0f;
        grounded = false;

        if (useAirJump)
        {
            airJumpsRemaining = Mathf.Max(0, airJumpsRemaining - 1);
            RuntimeHUD.Instance?.ShowTransientMessage("Double jump", 0.6f);
        }
    }

    private Vector3 CalculateMoveDirection(Vector3 planeNormal)
    {
        Transform cameraTransform = GetReferenceCamera();
        Vector3 referenceForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 referenceRight = cameraTransform != null ? cameraTransform.right : transform.right;

        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(referenceForward, planeNormal);
        if (forwardOnPlane.sqrMagnitude < 0.001f)
        {
            forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, planeNormal);
        }
        forwardOnPlane.Normalize();

        Vector3 rightOnPlane = Vector3.ProjectOnPlane(referenceRight, planeNormal);
        if (rightOnPlane.sqrMagnitude < 0.001f)
        {
            rightOnPlane = Vector3.Cross(planeNormal, forwardOnPlane);
        }
        rightOnPlane.Normalize();

        Vector3 movement = forwardOnPlane * moveInput.y + rightOnPlane * moveInput.x;
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        return movement;
    }

    private void SnapOrientationToCurrentGravity()
    {
        GravitySystem gravity = GravitySystem.Instance;
        Vector3 downDir = gravity != null ? gravity.DownDirection : Vector3.down;
        Vector3 upDir = gravity != null ? gravity.UpDirection : Vector3.up;
        Vector3 stableForward = ResolveStableForwardForGravity(downDir, upDir);

        if (stableForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(stableForward, upDir);
        lastMoveDirection = stableForward;
    }

    private Vector3 ResolveStableForwardForGravity(Vector3 downDir, Vector3 upDir)
    {
        Vector3 forward = Vector3.ProjectOnPlane(lastMoveDirection, downDir);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, downDir);
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            Transform cameraTransform = GetReferenceCamera();
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, downDir);
            }
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(upDir, Vector3.forward);
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(upDir, Vector3.right);
        }

        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.zero;
    }

    private void StabilizeUpright(Vector3 upDir, Vector3 downDir)
    {
        float tilt = Vector3.Angle(transform.up, upDir);
        if (tilt <= 0.5f)
        {
            return;
        }

        Vector3 stableForward = ResolveStableForwardForGravity(downDir, upDir);
        if (stableForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(stableForward, upDir);
        bool assistActive = postFlipUprightAssistLeft > 0f;
        float snapAngle = Mathf.Max(8f, hardUprightSnapAngle);
        if (assistActive)
        {
            float assistSnap = grounded
                ? Mathf.Clamp(postFlipGroundSnapAngle, 4f, 60f)
                : Mathf.Clamp(postFlipAirSnapAngle, 8f, 80f);
            snapAngle = Mathf.Min(snapAngle, assistSnap);
        }

        if (tilt >= snapAngle)
        {
            transform.rotation = targetRotation;
            lastMoveDirection = stableForward;
            return;
        }

        float recoverySpeed = Mathf.Max(60f, uprightRecoverySpeedDegPerSec);
        if (grounded)
        {
            recoverySpeed *= 1.15f;
        }
        if (assistActive)
        {
            recoverySpeed *= Mathf.Clamp(postFlipRecoveryBoost, 1f, 4f);
        }

        float maxStep = recoverySpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxStep);
    }

    private bool CheckGrounded(Vector3 downDir)
    {
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float scaleXZ = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float halfHeight = Mathf.Max((capsule.height * scaleY) * 0.5f, (capsule.radius * scaleXZ) + 0.02f);
        float radius = Mathf.Max(0.06f, capsule.radius * scaleXZ * 0.9f);
        float feetOffset = halfHeight - radius + 0.01f;
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 origin = center + downDir * feetOffset;
        float castDistance = groundProbeDistance + 0.04f;

        if (Physics.SphereCast(origin, radius, downDir, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody == body)
            {
                return false;
            }

            float standingAlignment = Vector3.Dot(hit.normal, -downDir);
            if (standingAlignment <= 0.25f)
            {
                return false;
            }

            if (!ShouldRequireStickySurface(downDir))
            {
                return true;
            }

            SurfaceType surfaceType = ResolveSurfaceType(hit.collider);
            if (surfaceType == SurfaceType.Sticky)
            {
                return true;
            }

            touchedNonStickyInvertedSurface = true;
            if (Time.time >= nextNonStickyHintTime)
            {
                nextNonStickyHintTime = Time.time + Mathf.Max(0.2f, nonStickyHintCooldown);
                RuntimeHUD.Instance?.ShowTransientMessage("Non-sticky surface. Find StickySurface.", 0.9f);
            }

            return false;
        }

        return false;
    }

    private bool ShouldRequireStickySurface(Vector3 downDir)
    {
        if (stickyRuleZoneOwners.Count <= 0)
        {
            return false;
        }

        // In inverted state, downDir points roughly to world up.
        return Vector3.Dot(downDir, Vector3.up) > 0.55f;
    }

    private void ApplyNonStickySurfaceSlip(Vector3 upDir)
    {
        if (!touchedNonStickyInvertedSurface || body == null)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        float verticalSpeed = Vector3.Dot(velocity, upDir);
        Vector3 vertical = upDir * verticalSpeed;
        Vector3 planar = Vector3.ProjectOnPlane(velocity, upDir);
        float damp = Mathf.Clamp01(nonStickyPlanarDamping);
        planar *= (1f - damp);
        body.linearVelocity = vertical + planar;
        body.AddForce(upDir * Mathf.Max(0f, nonStickyDetachAcceleration), ForceMode.Acceleration);
    }

    private static SurfaceType ResolveSurfaceType(Collider collider)
    {
        if (collider == null)
        {
            return SurfaceType.Unknown;
        }

        string tag = collider.tag;

        if (collider.GetComponentInParent<StickySurface>() != null || tag == "StickySurface")
        {
            return SurfaceType.Sticky;
        }

        if (collider.GetComponentInParent<NonStickySurface>() != null || tag == "NonStickySurface")
        {
            return SurfaceType.NonSticky;
        }

        return SurfaceType.Unknown;
    }

    private enum SurfaceType
    {
        Unknown = 0,
        Sticky = 1,
        NonSticky = 2
    }

    private void AlignToGravity(float deltaTime)
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity == null)
        {
            return;
        }

        Vector3 upDir = gravity.UpDirection;
        Vector3 downDir = gravity.DownDirection;

        Vector3 desiredForward;
        if (moveInput.sqrMagnitude > 0.01f)
        {
            desiredForward = lastMoveDirection.sqrMagnitude > 0.01f
                ? lastMoveDirection
                : Vector3.ProjectOnPlane(transform.forward, downDir);
        }
        else
        {
            // Keep facing direction stable when the player is not moving to avoid idle spin feedback.
            desiredForward = Vector3.ProjectOnPlane(transform.forward, downDir);
            if (desiredForward.sqrMagnitude < 0.001f && lastMoveDirection.sqrMagnitude > 0.01f)
            {
                desiredForward = Vector3.ProjectOnPlane(lastMoveDirection, downDir);
            }
        }

        if (desiredForward.sqrMagnitude < 0.001f)
        {
            desiredForward = Vector3.ProjectOnPlane(transform.forward, downDir);
        }

        if (desiredForward.sqrMagnitude < 0.001f)
        {
            desiredForward = Vector3.Cross(upDir, Vector3.forward);
        }

        if (desiredForward.sqrMagnitude < 0.001f)
        {
            desiredForward = Vector3.Cross(upDir, Vector3.right);
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredForward.normalized, upDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSharpness * deltaTime);
    }

    private Transform GetReferenceCamera()
    {
        if (cachedCameraFrame == Time.frameCount)
        {
            return cachedCameraTransform;
        }

        cachedCameraFrame = Time.frameCount;
        Camera main = Camera.main;
        cachedCameraTransform = main != null ? main.transform : null;
        return cachedCameraTransform;
    }
}

public class PlayerVisualAvatar : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform bodyPart;
    [SerializeField] private Transform headPart;
    [SerializeField] private Transform indicatorPart;
    [SerializeField] private Renderer pulseRenderer;

    [Header("Motion")]
    [SerializeField] private float bobAmplitude = 0.072f;
    [SerializeField] private float bobFrequency = 9.0f;
    [SerializeField] private float leanAngle = 14f;
    [SerializeField] private float visualSmooth = 12f;
    [SerializeField] private float bodySquashAmount = 0.11f;
    [SerializeField] private float indicatorSpinSpeed = 160f;
    [SerializeField, Range(0f, 1f)] private float airborneLeanMultiplier = 0.40f;
    [SerializeField] private float postFlipVisualStabilizeDuration = 0.24f;

    [Header("Arm Swing")]
    [SerializeField] private float armSwingAngle = 33f;
    [SerializeField] private float armSwingFrequency = 6.2f;
    [SerializeField] private float armSwingSmooth = 11f;
    [SerializeField] private float armTwistAngle = 8f;
    [SerializeField] private float armSideSwingAngle = 9f;
    [SerializeField, Range(0f, 0.9f)] private float armSecondarySwing = 0.34f;
    [SerializeField] private float armAirDamping = 0.20f;

    [Header("Leg Swing")]
    [SerializeField] private float legSwingAngle = 42f;
    [SerializeField] private float legLiftAngle = 18f;
    [SerializeField] private float legStrafeYawAngle = 10f;
    [SerializeField] private float legStanceRollAngle = 7f;
    [SerializeField] private float legLiftOffset = 0.078f;
    [SerializeField] private float legStrideOffset = 0.062f;
    [SerializeField] private float legSwingSmooth = 12f;
    [SerializeField] private float legAirDamping = 0.20f;

    [Header("Run Posture")]
    [SerializeField] private float runStartSpeed = 3.2f;
    [SerializeField] private float runTopSpeed = 7.0f;
    [SerializeField] private float runBlendSharpness = 10.5f;
    [SerializeField] private float runStrideBoost = 0.92f;
    [SerializeField] private float runFrequencyBoost = 3.1f;
    [SerializeField] private float runForwardLeanAngle = 14f;
    [SerializeField] private float runBobBoost = 1.05f;
    [SerializeField] private float runHeadNodBoost = 1.35f;

    [Header("Air And Landing")]
    [SerializeField] private float airPoseBlendSharpness = 11.8f;
    [SerializeField] private float airArmSuppression = 0.04f;
    [SerializeField] private float jumpArmBackAngle = 23f;
    [SerializeField] private float fallArmBraceAngle = 14f;
    [SerializeField] private float jumpLegTuckAngle = 28f;
    [SerializeField] private float fallLegBraceAngle = 18f;
    [SerializeField] private float landImpactMinSpeed = 4.6f;
    [SerializeField] private float landImpactMaxSpeed = 16f;
    [SerializeField] private float landRecoverSpeed = 6.2f;
    [SerializeField] private float landBodySquash = 0.38f;
    [SerializeField] private float landBodyDip = 0.16f;
    [SerializeField] private float landLegCompressAngle = 22f;

    [Header("Anticipation And Braking")]
    [SerializeField] private float preJumpCrouchDuration = 0.14f;
    [SerializeField] private float preJumpCrouchDepth = 0.20f;
    [SerializeField] private float preJumpArmBackAngle = 28f;
    [SerializeField] private float preJumpLegBendAngle = 16f;
    [SerializeField] private float anticipationRecoverSpeed = 12f;
    [SerializeField] private float stopTriggerSpeed = 2.2f;
    [SerializeField] private float stopTiltAngle = 18f;
    [SerializeField] private float stopArmCatchAngle = 24f;
    [SerializeField] private float stopLegBraceAngle = 19f;
    [SerializeField] private float stopImpactRecoverSpeed = 6.4f;
    [SerializeField] private float stopInertiaMax = 1.55f;
    [SerializeField] private float stopTriggerCooldown = 0.12f;

    [Header("Foot IK")]
    [SerializeField] private bool enableFootIk = true;
    [SerializeField] private LayerMask footIkMask = ~0;
    [SerializeField, Range(0f, 1f)] private float footIkGroundWeight = 1.00f;
    [SerializeField, Range(0f, 1f)] private float footIkAirWeight = 0.22f;
    [SerializeField] private float footIkRayStartHeight = 0.58f;
    [SerializeField] private float footIkRayDistance = 1.68f;
    [SerializeField] private float footIkFootClearance = 0.015f;
    [SerializeField] private float footIkMaxLift = 0.42f;
    [SerializeField] private float footIkMaxDrop = 0.44f;
    [SerializeField] private float footIkPitchAngle = 22f;
    [SerializeField] private float footIkRollAngle = 19f;
    [SerializeField] private float footIkSmooth = 16f;
    [SerializeField] private float footIkPredictionTime = 0.045f;

    [Header("Secondary Detail")]
    [SerializeField] private float idleBreathAmplitude = 0.010f;
    [SerializeField] private float idleBreathFrequency = 2.1f;
    [SerializeField] private float pelvisSwayOffset = 0.030f;
    [SerializeField] private float pelvisYawAngle = 5.2f;
    [SerializeField] private float pelvisRollAngle = 3.4f;
    [SerializeField] private float headStabilizeFactor = 0.28f;
    [SerializeField, Range(0f, 0.35f)] private float strideAsymmetry = 0.09f;
    [SerializeField] private float footstepPulseGain = 0.36f;
    [SerializeField] private float footstepPulseDecay = 6.8f;

    [Header("Scenario Tuning")]
    [SerializeField] private float walkMaxSpeed = 2.1f;
    [SerializeField] private float walkStrideShorten = 0.28f;
    [SerializeField] private float walkArmDampen = 0.38f;
    [SerializeField] private float sprintMinSpeed = 5.1f;
    [SerializeField] private float sprintStrideBoost = 0.36f;
    [SerializeField] private float sprintArmPumpBoost = 0.52f;
    [SerializeField] private float sprintTorsoLeanBoost = 4.8f;
    [SerializeField] private float landingReboundStrength = 0.46f;
    [SerializeField] private float landingReboundFrequency = 8.8f;
    [SerializeField] private float landingReboundDamping = 3.0f;
    [SerializeField] private float stopReboundStrength = 0.64f;
    [SerializeField] private float stopReboundFrequency = 8.4f;
    [SerializeField] private float stopReboundDamping = 2.8f;
    [SerializeField] private float slopeBodyPitchAngle = 9.5f;
    [SerializeField] private float slopeBodyRollAngle = 6.5f;
    [SerializeField] private float slopeStrideAdjust = 0.22f;
    [SerializeField] private float slopeLiftAdjust = 0.30f;
    [SerializeField] private float slopeArmAdjust = 0.20f;
    [SerializeField] private LayerMask slopeProbeMask = ~0;
    [SerializeField] private float slopeProbeStartHeight = 0.72f;
    [SerializeField] private float slopeProbeDistance = 1.85f;
    [SerializeField] private float slopeBlendSharpness = 7.2f;

    [Header("Sprint Dynamics")]
    [SerializeField] private float sprintBurstRiseSpeed = 8.0f;
    [SerializeField] private float sprintBurstDecaySpeed = 3.9f;
    [SerializeField] private float sprintBurstStrideBoost = 0.28f;
    [SerializeField] private float sprintBurstArmBoost = 0.24f;
    [SerializeField] private float sprintBurstLeanBoost = 5.6f;
    [SerializeField] private float sprintFatigueDelay = 1.35f;
    [SerializeField] private float sprintFatigueBuildSpeed = 0.56f;
    [SerializeField] private float sprintFatigueRecoverSpeed = 0.74f;
    [SerializeField] private float sprintFatigueStrideLoss = 0.18f;
    [SerializeField] private float sprintFatigueArmLoss = 0.22f;
    [SerializeField] private float sprintFatigueLeanLoss = 3.6f;
    [SerializeField] private float sprintFatigueAirArmSuppression = 0.16f;

    [Header("Inertia Detail")]
    [SerializeField] private float accelerationReference = 18f;
    [SerializeField] private float accelerationBlendSharpness = 11f;
    [SerializeField] private float accelerationPitchAngle = 5.8f;
    [SerializeField] private float accelerationRollAngle = 4.4f;
    [SerializeField] private float accelerationArmCatchAngle = 7.5f;
    [SerializeField] private float lateralAccelerationArmYaw = 4.2f;
    [SerializeField] private float accelerationStepPulseGain = 0.22f;

    [Header("Pulse")]
    [SerializeField] private Color basePulseColor = new Color(0.20f, 1f, 0.76f);
    [SerializeField] private float pulseFadeSpeed = 2.4f;

    private PlayerGravityMotor motor;
    private Rigidbody physicsBody;
    private GravitySystem subscribedGravity;

    private Vector3 visualBasePosition;
    private Quaternion visualBaseRotation;
    private Vector3 bodyBaseScale = Vector3.one;
    private Quaternion headBaseRotation = Quaternion.identity;
    private Vector3 indicatorBaseScale = Vector3.one;
    private Transform armLeftPart;
    private Transform armRightPart;
    private Transform legLeftPart;
    private Transform legRightPart;
    private Quaternion armLeftBaseRotation = Quaternion.identity;
    private Quaternion armRightBaseRotation = Quaternion.identity;
    private Quaternion legLeftBaseRotation = Quaternion.identity;
    private Quaternion legRightBaseRotation = Quaternion.identity;
    private Vector3 legLeftBasePosition;
    private Vector3 legRightBasePosition;
    private float legLeftHalfLength = 0.24f;
    private float legRightHalfLength = 0.24f;

    private MaterialPropertyBlock pulseBlock;
    private float flipPulse;
    private float gaitPhase;
    private float postFlipVisualStabilizeLeft;
    private bool wasGrounded;
    private float previousVerticalSpeed;
    private float airborneTime;
    private float runBlend;
    private float jumpPoseBlend;
    private float landingImpact;
    private float landingReboundPhase;
    private float landingReboundAmount;
    private float landingReboundValue;
    private float preJumpCrouch;
    private float preJumpHoldLeft;
    private float stopImpact;
    private float stopReboundPhase;
    private float stopReboundAmount;
    private float stopReboundValue;
    private float stopTriggerCooldownLeft;
    private float walkBlend;
    private float sprintBlend;
    private float sprintBurst;
    private float sprintFatigue;
    private float sprintSustainTime;
    private float previousSprintBlend;
    private float slopeForwardBlend;
    private float slopeSideBlend;
    private float previousPlanarSpeed;
    private Vector3 previousPlanarVelocity;
    private Vector2 stopLocalDir = Vector2.up;
    private float previousStepSignal;
    private float footstepPulse;
    private float leftIkOffsetSmoothed;
    private float rightIkOffsetSmoothed;
    private float leftIkPitchSmoothed;
    private float rightIkPitchSmoothed;
    private float leftIkRollSmoothed;
    private float rightIkRollSmoothed;
    private float gaitSeedPhase;
    private Vector3 smoothedGroundNormal = Vector3.up;
    private Vector2 smoothedLocalAcceleration;
    private Vector3 previousLocalPlanarVelocity;
    private readonly RaycastHit[] footIkHitBuffer = new RaycastHit[8];
    private bool motionProfileBaselineCaptured;
    private MotionIntensityPreset activeMotionPreset = MotionIntensityPreset.Normal;
    private MotionStyleProfile activeMotionStyle = MotionStyleProfile.Stable;
    private MotionProfileBaseline motionProfileBaseline;

    private struct MotionProfileBaseline
    {
        public float BobAmplitude;
        public float BobFrequency;
        public float LeanAngle;
        public float BodySquashAmount;
        public float ArmSwingAngle;
        public float ArmSwingFrequency;
        public float ArmTwistAngle;
        public float ArmSideSwingAngle;
        public float ArmSecondarySwing;
        public float ArmAirDamping;
        public float LegSwingAngle;
        public float LegLiftAngle;
        public float LegLiftOffset;
        public float LegStrideOffset;
        public float RunStartSpeed;
        public float RunTopSpeed;
        public float RunStrideBoost;
        public float RunFrequencyBoost;
        public float RunForwardLeanAngle;
        public float RunBobBoost;
        public float RunHeadNodBoost;
        public float AirArmSuppression;
        public float JumpArmBackAngle;
        public float FallArmBraceAngle;
        public float JumpLegTuckAngle;
        public float FallLegBraceAngle;
        public float LandRecoverSpeed;
        public float LandBodySquash;
        public float LandBodyDip;
        public float LandLegCompressAngle;
        public float PreJumpCrouchDepth;
        public float PreJumpArmBackAngle;
        public float PreJumpLegBendAngle;
        public float StopTiltAngle;
        public float StopArmCatchAngle;
        public float StopLegBraceAngle;
        public float StopImpactRecoverSpeed;
        public float StopInertiaMax;
        public float WalkMaxSpeed;
        public float WalkStrideShorten;
        public float WalkArmDampen;
        public float SprintMinSpeed;
        public float SprintStrideBoost;
        public float SprintArmPumpBoost;
        public float SprintTorsoLeanBoost;
        public float LandingReboundStrength;
        public float LandingReboundFrequency;
        public float LandingReboundDamping;
        public float StopReboundStrength;
        public float StopReboundFrequency;
        public float StopReboundDamping;
        public float SlopeBodyPitchAngle;
        public float SlopeBodyRollAngle;
        public float SlopeStrideAdjust;
        public float SlopeLiftAdjust;
        public float SlopeArmAdjust;
        public float FootIkGroundWeight;
        public float FootIkAirWeight;
        public float FootIkPredictionTime;
        public float FootIkPitchAngle;
        public float FootIkRollAngle;
        public float SprintBurstRiseSpeed;
        public float SprintBurstDecaySpeed;
        public float SprintBurstStrideBoost;
        public float SprintBurstArmBoost;
        public float SprintBurstLeanBoost;
        public float SprintFatigueDelay;
        public float SprintFatigueBuildSpeed;
        public float SprintFatigueRecoverSpeed;
        public float SprintFatigueStrideLoss;
        public float SprintFatigueArmLoss;
        public float SprintFatigueLeanLoss;
        public float SprintFatigueAirArmSuppression;
        public float AccelerationReference;
        public float AccelerationPitchAngle;
        public float AccelerationRollAngle;
        public float AccelerationArmCatchAngle;
        public float LateralAccelerationArmYaw;
        public float AccelerationStepPulseGain;
        public float FootstepPulseGain;
    }

    public void Configure(
        Transform newVisualRoot,
        Transform newBodyPart,
        Transform newHeadPart,
        Transform newIndicatorPart,
        Renderer newPulseRenderer)
    {
        visualRoot = newVisualRoot;
        bodyPart = newBodyPart;
        headPart = newHeadPart;
        indicatorPart = newIndicatorPart;
        pulseRenderer = newPulseRenderer;
        CaptureBasePose();
        SetupPulseBlock();
    }

    public void ApplyMotionProfile(MotionIntensityPreset preset, MotionStyleProfile style)
    {
        EnsureMotionProfileBaseline();

        activeMotionPreset = (MotionIntensityPreset)Mathf.Clamp((int)preset, 0, 2);
        activeMotionStyle = (MotionStyleProfile)Mathf.Clamp((int)style, 0, 2);
        RestoreMotionProfileBaseline();

        float amplitudeScale = 1f;
        float cadenceScale = 1f;
        float inertiaScale = 1f;
        float impactScale = 1f;
        float controlScale = 1f;
        float ikScale = 1f;
        float burstScale = 1f;
        float fatigueScale = 1f;

        switch (activeMotionPreset)
        {
            case MotionIntensityPreset.Realistic:
                amplitudeScale *= 0.92f;
                cadenceScale *= 0.95f;
                inertiaScale *= 0.90f;
                impactScale *= 0.88f;
                controlScale *= 1.05f;
                ikScale *= 1.08f;
                burstScale *= 0.92f;
                fatigueScale *= 0.94f;
                break;
            case MotionIntensityPreset.Cinematic:
                amplitudeScale *= 1.24f;
                cadenceScale *= 1.03f;
                inertiaScale *= 1.24f;
                impactScale *= 1.32f;
                controlScale *= 0.90f;
                ikScale *= 1.18f;
                burstScale *= 1.30f;
                fatigueScale *= 1.28f;
                break;
        }

        switch (activeMotionStyle)
        {
            case MotionStyleProfile.Stable:
                amplitudeScale *= 0.90f;
                cadenceScale *= 0.94f;
                inertiaScale *= 0.88f;
                impactScale *= 0.92f;
                controlScale *= 1.08f;
                ikScale *= 1.08f;
                burstScale *= 0.88f;
                fatigueScale *= 0.92f;
                break;
            case MotionStyleProfile.Agile:
                amplitudeScale *= 0.98f;
                cadenceScale *= 1.12f;
                inertiaScale *= 0.92f;
                impactScale *= 0.88f;
                controlScale *= 1.02f;
                ikScale *= 0.98f;
                burstScale *= 1.12f;
                fatigueScale *= 0.82f;
                break;
            case MotionStyleProfile.Exaggerated:
                amplitudeScale *= 1.20f;
                cadenceScale *= 1.05f;
                inertiaScale *= 1.24f;
                impactScale *= 1.18f;
                controlScale *= 0.84f;
                ikScale *= 1.10f;
                burstScale *= 1.25f;
                fatigueScale *= 1.24f;
                break;
        }

        bobAmplitude *= amplitudeScale;
        bobFrequency *= cadenceScale;
        leanAngle *= inertiaScale;
        bodySquashAmount *= Mathf.Lerp(1f, amplitudeScale, 0.78f) * Mathf.Lerp(1f, impactScale, 0.32f);

        armSwingAngle *= amplitudeScale;
        armSwingFrequency *= cadenceScale;
        armTwistAngle *= Mathf.Lerp(1f, amplitudeScale, 0.68f);
        armSideSwingAngle *= Mathf.Lerp(1f, amplitudeScale, 0.74f);
        armSecondarySwing = Mathf.Clamp(armSecondarySwing * Mathf.Lerp(1f, amplitudeScale, 0.35f), 0f, 0.9f);
        armAirDamping = Mathf.Clamp01(armAirDamping * Mathf.Lerp(1f, controlScale, 0.60f));

        legSwingAngle *= amplitudeScale;
        legLiftAngle *= Mathf.Lerp(1f, amplitudeScale, 0.84f);
        legLiftOffset *= Mathf.Lerp(1f, amplitudeScale, 0.70f);
        legStrideOffset *= Mathf.Lerp(1f, amplitudeScale, 0.72f);

        runStrideBoost *= Mathf.Lerp(1f, amplitudeScale, 0.86f);
        runFrequencyBoost *= cadenceScale;
        runForwardLeanAngle *= inertiaScale;
        runBobBoost *= Mathf.Lerp(1f, amplitudeScale, 0.70f);
        runHeadNodBoost *= Mathf.Lerp(1f, amplitudeScale, 0.76f);

        airArmSuppression = Mathf.Clamp01(airArmSuppression * Mathf.Lerp(1f, controlScale, 0.48f));
        if (activeMotionPreset == MotionIntensityPreset.Cinematic)
        {
            airArmSuppression = Mathf.Clamp01(airArmSuppression * 0.72f);
        }
        else if (activeMotionPreset == MotionIntensityPreset.Realistic)
        {
            airArmSuppression = Mathf.Clamp01(airArmSuppression * 1.16f);
        }

        jumpArmBackAngle *= amplitudeScale;
        fallArmBraceAngle *= Mathf.Lerp(1f, impactScale, 0.70f);
        jumpLegTuckAngle *= amplitudeScale;
        fallLegBraceAngle *= Mathf.Lerp(1f, impactScale, 0.66f);

        landRecoverSpeed /= Mathf.Lerp(1f, impactScale, 0.84f);
        landBodySquash *= impactScale;
        landBodyDip *= impactScale;
        landLegCompressAngle *= impactScale;

        preJumpCrouchDepth *= Mathf.Lerp(1f, impactScale, 0.50f) * Mathf.Lerp(1f, amplitudeScale, 0.36f);
        preJumpArmBackAngle *= amplitudeScale;
        preJumpLegBendAngle *= amplitudeScale;
        stopTiltAngle *= inertiaScale;
        stopArmCatchAngle *= inertiaScale;
        stopLegBraceAngle *= inertiaScale;
        stopImpactRecoverSpeed /= Mathf.Lerp(1f, impactScale, 0.65f);
        stopInertiaMax *= inertiaScale;

        walkStrideShorten *= Mathf.Lerp(1f, amplitudeScale, 0.42f);
        walkArmDampen = Mathf.Clamp(walkArmDampen * Mathf.Lerp(1f, controlScale, 0.58f), 0f, 0.95f);
        sprintStrideBoost *= Mathf.Lerp(1f, amplitudeScale, 0.76f);
        sprintArmPumpBoost *= Mathf.Lerp(1f, amplitudeScale, 0.72f) * Mathf.Lerp(1f, burstScale, 0.30f);
        sprintTorsoLeanBoost *= inertiaScale;

        landingReboundStrength = Mathf.Clamp01(landingReboundStrength * Mathf.Lerp(1f, impactScale, 0.86f));
        landingReboundFrequency *= cadenceScale;
        landingReboundDamping *= Mathf.Lerp(1f, impactScale, 0.30f);
        stopReboundStrength = Mathf.Clamp01(stopReboundStrength * Mathf.Lerp(1f, impactScale, 0.72f));
        stopReboundFrequency *= cadenceScale;
        stopReboundDamping *= Mathf.Lerp(1f, impactScale, 0.28f);

        slopeBodyPitchAngle *= inertiaScale;
        slopeBodyRollAngle *= inertiaScale;
        slopeStrideAdjust *= Mathf.Lerp(1f, amplitudeScale, 0.45f);
        slopeLiftAdjust = Mathf.Clamp01(slopeLiftAdjust * Mathf.Lerp(1f, amplitudeScale, 0.38f));
        slopeArmAdjust = Mathf.Clamp01(slopeArmAdjust * Mathf.Lerp(1f, amplitudeScale, 0.34f));

        footIkGroundWeight = Mathf.Clamp01(footIkGroundWeight * ikScale);
        footIkAirWeight = Mathf.Clamp01(footIkAirWeight * Mathf.Lerp(1f, ikScale, 0.35f));
        footIkPredictionTime = Mathf.Clamp(footIkPredictionTime * Mathf.Lerp(1f, ikScale, 0.62f), 0f, 0.15f);
        footIkPitchAngle *= Mathf.Lerp(1f, amplitudeScale, 0.34f);
        footIkRollAngle *= Mathf.Lerp(1f, amplitudeScale, 0.34f);

        sprintBurstRiseSpeed *= burstScale;
        sprintBurstDecaySpeed = Mathf.Max(0.1f, sprintBurstDecaySpeed / Mathf.Lerp(1f, burstScale, 0.42f));
        sprintBurstStrideBoost *= burstScale;
        sprintBurstArmBoost *= burstScale;
        sprintBurstLeanBoost *= Mathf.Lerp(1f, inertiaScale, 0.64f) * burstScale;
        sprintFatigueDelay = Mathf.Max(0f, sprintFatigueDelay / Mathf.Lerp(1f, fatigueScale, 0.28f));
        sprintFatigueBuildSpeed *= fatigueScale;
        sprintFatigueRecoverSpeed /= Mathf.Lerp(1f, fatigueScale, 0.44f);
        sprintFatigueStrideLoss *= fatigueScale;
        sprintFatigueArmLoss *= fatigueScale;
        sprintFatigueLeanLoss *= Mathf.Lerp(1f, inertiaScale, 0.48f) * fatigueScale;
        sprintFatigueAirArmSuppression *= fatigueScale;

        accelerationReference = Mathf.Max(2f, accelerationReference * Mathf.Lerp(1f, controlScale, 0.25f));
        accelerationPitchAngle *= inertiaScale;
        accelerationRollAngle *= inertiaScale;
        accelerationArmCatchAngle *= inertiaScale;
        lateralAccelerationArmYaw *= inertiaScale;
        accelerationStepPulseGain *= Mathf.Lerp(1f, impactScale, 0.56f);
        footstepPulseGain *= Mathf.Lerp(1f, impactScale, 0.56f);

        runTopSpeed = Mathf.Max(0.5f, runTopSpeed);
        runStartSpeed = Mathf.Clamp(runStartSpeed, 0f, runTopSpeed - 0.05f);
        sprintMinSpeed = Mathf.Clamp(sprintMinSpeed, runStartSpeed, runTopSpeed);
        walkMaxSpeed = Mathf.Clamp(walkMaxSpeed, 0.1f, runTopSpeed);
    }

    private void EnsureMotionProfileBaseline()
    {
        if (motionProfileBaselineCaptured)
        {
            return;
        }

        motionProfileBaseline = new MotionProfileBaseline
        {
            BobAmplitude = bobAmplitude,
            BobFrequency = bobFrequency,
            LeanAngle = leanAngle,
            BodySquashAmount = bodySquashAmount,
            ArmSwingAngle = armSwingAngle,
            ArmSwingFrequency = armSwingFrequency,
            ArmTwistAngle = armTwistAngle,
            ArmSideSwingAngle = armSideSwingAngle,
            ArmSecondarySwing = armSecondarySwing,
            ArmAirDamping = armAirDamping,
            LegSwingAngle = legSwingAngle,
            LegLiftAngle = legLiftAngle,
            LegLiftOffset = legLiftOffset,
            LegStrideOffset = legStrideOffset,
            RunStartSpeed = runStartSpeed,
            RunTopSpeed = runTopSpeed,
            RunStrideBoost = runStrideBoost,
            RunFrequencyBoost = runFrequencyBoost,
            RunForwardLeanAngle = runForwardLeanAngle,
            RunBobBoost = runBobBoost,
            RunHeadNodBoost = runHeadNodBoost,
            AirArmSuppression = airArmSuppression,
            JumpArmBackAngle = jumpArmBackAngle,
            FallArmBraceAngle = fallArmBraceAngle,
            JumpLegTuckAngle = jumpLegTuckAngle,
            FallLegBraceAngle = fallLegBraceAngle,
            LandRecoverSpeed = landRecoverSpeed,
            LandBodySquash = landBodySquash,
            LandBodyDip = landBodyDip,
            LandLegCompressAngle = landLegCompressAngle,
            PreJumpCrouchDepth = preJumpCrouchDepth,
            PreJumpArmBackAngle = preJumpArmBackAngle,
            PreJumpLegBendAngle = preJumpLegBendAngle,
            StopTiltAngle = stopTiltAngle,
            StopArmCatchAngle = stopArmCatchAngle,
            StopLegBraceAngle = stopLegBraceAngle,
            StopImpactRecoverSpeed = stopImpactRecoverSpeed,
            StopInertiaMax = stopInertiaMax,
            WalkMaxSpeed = walkMaxSpeed,
            WalkStrideShorten = walkStrideShorten,
            WalkArmDampen = walkArmDampen,
            SprintMinSpeed = sprintMinSpeed,
            SprintStrideBoost = sprintStrideBoost,
            SprintArmPumpBoost = sprintArmPumpBoost,
            SprintTorsoLeanBoost = sprintTorsoLeanBoost,
            LandingReboundStrength = landingReboundStrength,
            LandingReboundFrequency = landingReboundFrequency,
            LandingReboundDamping = landingReboundDamping,
            StopReboundStrength = stopReboundStrength,
            StopReboundFrequency = stopReboundFrequency,
            StopReboundDamping = stopReboundDamping,
            SlopeBodyPitchAngle = slopeBodyPitchAngle,
            SlopeBodyRollAngle = slopeBodyRollAngle,
            SlopeStrideAdjust = slopeStrideAdjust,
            SlopeLiftAdjust = slopeLiftAdjust,
            SlopeArmAdjust = slopeArmAdjust,
            FootIkGroundWeight = footIkGroundWeight,
            FootIkAirWeight = footIkAirWeight,
            FootIkPredictionTime = footIkPredictionTime,
            FootIkPitchAngle = footIkPitchAngle,
            FootIkRollAngle = footIkRollAngle,
            SprintBurstRiseSpeed = sprintBurstRiseSpeed,
            SprintBurstDecaySpeed = sprintBurstDecaySpeed,
            SprintBurstStrideBoost = sprintBurstStrideBoost,
            SprintBurstArmBoost = sprintBurstArmBoost,
            SprintBurstLeanBoost = sprintBurstLeanBoost,
            SprintFatigueDelay = sprintFatigueDelay,
            SprintFatigueBuildSpeed = sprintFatigueBuildSpeed,
            SprintFatigueRecoverSpeed = sprintFatigueRecoverSpeed,
            SprintFatigueStrideLoss = sprintFatigueStrideLoss,
            SprintFatigueArmLoss = sprintFatigueArmLoss,
            SprintFatigueLeanLoss = sprintFatigueLeanLoss,
            SprintFatigueAirArmSuppression = sprintFatigueAirArmSuppression,
            AccelerationReference = accelerationReference,
            AccelerationPitchAngle = accelerationPitchAngle,
            AccelerationRollAngle = accelerationRollAngle,
            AccelerationArmCatchAngle = accelerationArmCatchAngle,
            LateralAccelerationArmYaw = lateralAccelerationArmYaw,
            AccelerationStepPulseGain = accelerationStepPulseGain,
            FootstepPulseGain = footstepPulseGain
        };

        motionProfileBaselineCaptured = true;
    }

    private void RestoreMotionProfileBaseline()
    {
        bobAmplitude = motionProfileBaseline.BobAmplitude;
        bobFrequency = motionProfileBaseline.BobFrequency;
        leanAngle = motionProfileBaseline.LeanAngle;
        bodySquashAmount = motionProfileBaseline.BodySquashAmount;
        armSwingAngle = motionProfileBaseline.ArmSwingAngle;
        armSwingFrequency = motionProfileBaseline.ArmSwingFrequency;
        armTwistAngle = motionProfileBaseline.ArmTwistAngle;
        armSideSwingAngle = motionProfileBaseline.ArmSideSwingAngle;
        armSecondarySwing = motionProfileBaseline.ArmSecondarySwing;
        armAirDamping = motionProfileBaseline.ArmAirDamping;
        legSwingAngle = motionProfileBaseline.LegSwingAngle;
        legLiftAngle = motionProfileBaseline.LegLiftAngle;
        legLiftOffset = motionProfileBaseline.LegLiftOffset;
        legStrideOffset = motionProfileBaseline.LegStrideOffset;
        runStartSpeed = motionProfileBaseline.RunStartSpeed;
        runTopSpeed = motionProfileBaseline.RunTopSpeed;
        runStrideBoost = motionProfileBaseline.RunStrideBoost;
        runFrequencyBoost = motionProfileBaseline.RunFrequencyBoost;
        runForwardLeanAngle = motionProfileBaseline.RunForwardLeanAngle;
        runBobBoost = motionProfileBaseline.RunBobBoost;
        runHeadNodBoost = motionProfileBaseline.RunHeadNodBoost;
        airArmSuppression = motionProfileBaseline.AirArmSuppression;
        jumpArmBackAngle = motionProfileBaseline.JumpArmBackAngle;
        fallArmBraceAngle = motionProfileBaseline.FallArmBraceAngle;
        jumpLegTuckAngle = motionProfileBaseline.JumpLegTuckAngle;
        fallLegBraceAngle = motionProfileBaseline.FallLegBraceAngle;
        landRecoverSpeed = motionProfileBaseline.LandRecoverSpeed;
        landBodySquash = motionProfileBaseline.LandBodySquash;
        landBodyDip = motionProfileBaseline.LandBodyDip;
        landLegCompressAngle = motionProfileBaseline.LandLegCompressAngle;
        preJumpCrouchDepth = motionProfileBaseline.PreJumpCrouchDepth;
        preJumpArmBackAngle = motionProfileBaseline.PreJumpArmBackAngle;
        preJumpLegBendAngle = motionProfileBaseline.PreJumpLegBendAngle;
        stopTiltAngle = motionProfileBaseline.StopTiltAngle;
        stopArmCatchAngle = motionProfileBaseline.StopArmCatchAngle;
        stopLegBraceAngle = motionProfileBaseline.StopLegBraceAngle;
        stopImpactRecoverSpeed = motionProfileBaseline.StopImpactRecoverSpeed;
        stopInertiaMax = motionProfileBaseline.StopInertiaMax;
        walkMaxSpeed = motionProfileBaseline.WalkMaxSpeed;
        walkStrideShorten = motionProfileBaseline.WalkStrideShorten;
        walkArmDampen = motionProfileBaseline.WalkArmDampen;
        sprintMinSpeed = motionProfileBaseline.SprintMinSpeed;
        sprintStrideBoost = motionProfileBaseline.SprintStrideBoost;
        sprintArmPumpBoost = motionProfileBaseline.SprintArmPumpBoost;
        sprintTorsoLeanBoost = motionProfileBaseline.SprintTorsoLeanBoost;
        landingReboundStrength = motionProfileBaseline.LandingReboundStrength;
        landingReboundFrequency = motionProfileBaseline.LandingReboundFrequency;
        landingReboundDamping = motionProfileBaseline.LandingReboundDamping;
        stopReboundStrength = motionProfileBaseline.StopReboundStrength;
        stopReboundFrequency = motionProfileBaseline.StopReboundFrequency;
        stopReboundDamping = motionProfileBaseline.StopReboundDamping;
        slopeBodyPitchAngle = motionProfileBaseline.SlopeBodyPitchAngle;
        slopeBodyRollAngle = motionProfileBaseline.SlopeBodyRollAngle;
        slopeStrideAdjust = motionProfileBaseline.SlopeStrideAdjust;
        slopeLiftAdjust = motionProfileBaseline.SlopeLiftAdjust;
        slopeArmAdjust = motionProfileBaseline.SlopeArmAdjust;
        footIkGroundWeight = motionProfileBaseline.FootIkGroundWeight;
        footIkAirWeight = motionProfileBaseline.FootIkAirWeight;
        footIkPredictionTime = motionProfileBaseline.FootIkPredictionTime;
        footIkPitchAngle = motionProfileBaseline.FootIkPitchAngle;
        footIkRollAngle = motionProfileBaseline.FootIkRollAngle;
        sprintBurstRiseSpeed = motionProfileBaseline.SprintBurstRiseSpeed;
        sprintBurstDecaySpeed = motionProfileBaseline.SprintBurstDecaySpeed;
        sprintBurstStrideBoost = motionProfileBaseline.SprintBurstStrideBoost;
        sprintBurstArmBoost = motionProfileBaseline.SprintBurstArmBoost;
        sprintBurstLeanBoost = motionProfileBaseline.SprintBurstLeanBoost;
        sprintFatigueDelay = motionProfileBaseline.SprintFatigueDelay;
        sprintFatigueBuildSpeed = motionProfileBaseline.SprintFatigueBuildSpeed;
        sprintFatigueRecoverSpeed = motionProfileBaseline.SprintFatigueRecoverSpeed;
        sprintFatigueStrideLoss = motionProfileBaseline.SprintFatigueStrideLoss;
        sprintFatigueArmLoss = motionProfileBaseline.SprintFatigueArmLoss;
        sprintFatigueLeanLoss = motionProfileBaseline.SprintFatigueLeanLoss;
        sprintFatigueAirArmSuppression = motionProfileBaseline.SprintFatigueAirArmSuppression;
        accelerationReference = motionProfileBaseline.AccelerationReference;
        accelerationPitchAngle = motionProfileBaseline.AccelerationPitchAngle;
        accelerationRollAngle = motionProfileBaseline.AccelerationRollAngle;
        accelerationArmCatchAngle = motionProfileBaseline.AccelerationArmCatchAngle;
        lateralAccelerationArmYaw = motionProfileBaseline.LateralAccelerationArmYaw;
        accelerationStepPulseGain = motionProfileBaseline.AccelerationStepPulseGain;
        footstepPulseGain = motionProfileBaseline.FootstepPulseGain;
    }

    private void Awake()
    {
        motor = GetComponent<PlayerGravityMotor>();
        physicsBody = GetComponent<Rigidbody>();

        EnsureMotionProfileBaseline();
        ApplyMotionProfile(activeMotionPreset, activeMotionStyle);
        CaptureBasePose();
        SetupPulseBlock();
        InitializeDynamicState();
    }

    private void OnEnable()
    {
        TrySubscribeGravity();
        if (motor != null)
        {
            motor.OnRespawned += HandleMotorRespawned;
        }
        ApplyMotionProfile(activeMotionPreset, activeMotionStyle);
        InitializeDynamicState();
    }

    private void OnDisable()
    {
        if (subscribedGravity != null)
        {
            subscribedGravity.OnGravityChanged -= HandleGravityChanged;
            subscribedGravity = null;
        }

        if (motor != null)
        {
            motor.OnRespawned -= HandleMotorRespawned;
        }
    }

    private void OnValidate()
    {
        runTopSpeed = Mathf.Max(0.5f, runTopSpeed);
        runStartSpeed = Mathf.Clamp(runStartSpeed, 0f, runTopSpeed - 0.05f);
        sprintMinSpeed = Mathf.Clamp(sprintMinSpeed, runStartSpeed, runTopSpeed);
        walkMaxSpeed = Mathf.Clamp(walkMaxSpeed, 0.1f, runTopSpeed);

        visualSmooth = Mathf.Max(1f, visualSmooth);
        armSwingSmooth = Mathf.Max(1f, armSwingSmooth);
        legSwingSmooth = Mathf.Max(1f, legSwingSmooth);
        footIkSmooth = Mathf.Max(1f, footIkSmooth);
        runBlendSharpness = Mathf.Max(1f, runBlendSharpness);
        airPoseBlendSharpness = Mathf.Max(1f, airPoseBlendSharpness);
        slopeBlendSharpness = Mathf.Max(1f, slopeBlendSharpness);

        landImpactMinSpeed = Mathf.Max(0f, landImpactMinSpeed);
        landImpactMaxSpeed = Mathf.Max(landImpactMinSpeed + 0.1f, landImpactMaxSpeed);
        landRecoverSpeed = Mathf.Max(0.1f, landRecoverSpeed);
        stopImpactRecoverSpeed = Mathf.Max(0.1f, stopImpactRecoverSpeed);
        stopInertiaMax = Mathf.Clamp(stopInertiaMax, 0.1f, 2.5f);
        stopTriggerCooldown = Mathf.Clamp(stopTriggerCooldown, 0f, 0.4f);

        preJumpCrouchDuration = Mathf.Max(0.01f, preJumpCrouchDuration);
        preJumpCrouchDepth = Mathf.Max(0f, preJumpCrouchDepth);
        anticipationRecoverSpeed = Mathf.Max(0.1f, anticipationRecoverSpeed);

        footIkGroundWeight = Mathf.Clamp01(footIkGroundWeight);
        footIkAirWeight = Mathf.Clamp01(footIkAirWeight);
        footIkRayStartHeight = Mathf.Max(0.02f, footIkRayStartHeight);
        footIkRayDistance = Mathf.Max(0.08f, footIkRayDistance);
        footIkMaxLift = Mathf.Max(0.02f, footIkMaxLift);
        footIkMaxDrop = Mathf.Max(0.02f, footIkMaxDrop);
        footIkPitchAngle = Mathf.Max(0f, footIkPitchAngle);
        footIkRollAngle = Mathf.Max(0f, footIkRollAngle);
        footIkPredictionTime = Mathf.Clamp(footIkPredictionTime, 0f, 0.15f);

        strideAsymmetry = Mathf.Clamp(strideAsymmetry, 0f, 0.35f);

        landingReboundStrength = Mathf.Clamp01(landingReboundStrength);
        stopReboundStrength = Mathf.Clamp01(stopReboundStrength);
        landingReboundFrequency = Mathf.Max(0.1f, landingReboundFrequency);
        stopReboundFrequency = Mathf.Max(0.1f, stopReboundFrequency);
        landingReboundDamping = Mathf.Max(0.1f, landingReboundDamping);
        stopReboundDamping = Mathf.Max(0.1f, stopReboundDamping);

        slopeProbeStartHeight = Mathf.Max(0.1f, slopeProbeStartHeight);
        slopeProbeDistance = Mathf.Max(0.15f, slopeProbeDistance);
        slopeStrideAdjust = Mathf.Clamp(slopeStrideAdjust, -0.6f, 0.6f);
        slopeLiftAdjust = Mathf.Clamp01(slopeLiftAdjust);
        slopeArmAdjust = Mathf.Clamp01(slopeArmAdjust);

        sprintBurstRiseSpeed = Mathf.Max(0.1f, sprintBurstRiseSpeed);
        sprintBurstDecaySpeed = Mathf.Max(0.1f, sprintBurstDecaySpeed);
        sprintFatigueDelay = Mathf.Max(0f, sprintFatigueDelay);
        sprintFatigueBuildSpeed = Mathf.Max(0.01f, sprintFatigueBuildSpeed);
        sprintFatigueRecoverSpeed = Mathf.Max(0.01f, sprintFatigueRecoverSpeed);
        sprintFatigueStrideLoss = Mathf.Clamp(sprintFatigueStrideLoss, 0f, 0.9f);
        sprintFatigueArmLoss = Mathf.Clamp(sprintFatigueArmLoss, 0f, 0.9f);
        sprintFatigueLeanLoss = Mathf.Max(0f, sprintFatigueLeanLoss);
        sprintFatigueAirArmSuppression = Mathf.Clamp(sprintFatigueAirArmSuppression, 0f, 1f);

        accelerationReference = Mathf.Max(2f, accelerationReference);
        accelerationBlendSharpness = Mathf.Max(1f, accelerationBlendSharpness);
        accelerationPitchAngle = Mathf.Max(0f, accelerationPitchAngle);
        accelerationRollAngle = Mathf.Max(0f, accelerationRollAngle);
        accelerationArmCatchAngle = Mathf.Max(0f, accelerationArmCatchAngle);
        lateralAccelerationArmYaw = Mathf.Max(0f, lateralAccelerationArmYaw);
        accelerationStepPulseGain = Mathf.Max(0f, accelerationStepPulseGain);

        if (!Application.isPlaying)
        {
            motionProfileBaselineCaptured = false;
        }
    }

    private void Update()
    {
        TrySubscribeGravity();
        if (postFlipVisualStabilizeLeft > 0f)
        {
            postFlipVisualStabilizeLeft = Mathf.Max(0f, postFlipVisualStabilizeLeft - Time.deltaTime);
        }
        AnimateVisual(Time.deltaTime);
        UpdatePulse(Time.deltaTime);
    }

    private void TrySubscribeGravity()
    {
        GravitySystem gravity = GravitySystem.Instance;
        if (gravity == null || subscribedGravity == gravity)
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
        flipPulse = 1f;
        postFlipVisualStabilizeLeft = Mathf.Max(
            postFlipVisualStabilizeLeft,
            Mathf.Max(0f, postFlipVisualStabilizeDuration)
        );
    }

    private void HandleMotorRespawned()
    {
        InitializeDynamicState();
    }

    private void InitializeDynamicState()
    {
        Vector3 downDir = GravitySystem.Instance != null ? GravitySystem.Instance.DownDirection : Vector3.down;
        Vector3 upDir = -downDir;
        wasGrounded = motor != null && motor.IsGrounded;
        previousVerticalSpeed = physicsBody != null ? Vector3.Dot(physicsBody.linearVelocity, upDir) : 0f;
        previousPlanarVelocity = physicsBody != null ? Vector3.ProjectOnPlane(physicsBody.linearVelocity, downDir) : Vector3.zero;
        previousPlanarSpeed = previousPlanarVelocity.magnitude;
        previousLocalPlanarVelocity = transform.InverseTransformDirection(previousPlanarVelocity);
        airborneTime = 0f;
        runBlend = 0f;
        walkBlend = 0f;
        sprintBlend = 0f;
        sprintBurst = 0f;
        sprintFatigue = 0f;
        sprintSustainTime = 0f;
        previousSprintBlend = 0f;
        slopeForwardBlend = 0f;
        slopeSideBlend = 0f;
        jumpPoseBlend = 0f;
        landingImpact = 0f;
        landingReboundPhase = 0f;
        landingReboundAmount = 0f;
        landingReboundValue = 0f;
        preJumpCrouch = 0f;
        preJumpHoldLeft = 0f;
        stopImpact = 0f;
        stopReboundPhase = 0f;
        stopReboundAmount = 0f;
        stopReboundValue = 0f;
        stopTriggerCooldownLeft = 0f;
        stopLocalDir = Vector2.up;
        previousStepSignal = 0f;
        footstepPulse = 0f;
        leftIkOffsetSmoothed = 0f;
        rightIkOffsetSmoothed = 0f;
        leftIkPitchSmoothed = 0f;
        rightIkPitchSmoothed = 0f;
        leftIkRollSmoothed = 0f;
        rightIkRollSmoothed = 0f;
        smoothedLocalAcceleration = Vector2.zero;
        gaitSeedPhase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * 0.01137f, Mathf.PI * 2f);
        smoothedGroundNormal = upDir;
    }

    private void UpdateMotionState(float deltaTime, bool isGrounded, float speed, float verticalSpeed)
    {
        float runMin = Mathf.Min(runStartSpeed, runTopSpeed);
        float runMax = Mathf.Max(runStartSpeed + 0.01f, runTopSpeed);
        float runTarget = Mathf.InverseLerp(runMin, runMax, speed);
        if (!isGrounded)
        {
            runTarget *= 0.45f;
        }
        runBlend = Mathf.Lerp(
            runBlend,
            runTarget,
            1f - Mathf.Exp(-Mathf.Max(1f, runBlendSharpness) * deltaTime)
        );

        float previousAirborneTime = airborneTime;
        if (!isGrounded)
        {
            airborneTime += deltaTime;
        }

        float rise01 = Mathf.Clamp01(verticalSpeed / 8.5f);
        float fall01 = Mathf.Clamp01(-verticalSpeed / 12f);
        float airTarget = isGrounded
            ? 0f
            : Mathf.Clamp01(0.24f + rise01 * 0.46f + fall01 * 0.66f + airborneTime * 1.05f);

        jumpPoseBlend = Mathf.Lerp(
            jumpPoseBlend,
            airTarget,
            1f - Mathf.Exp(-Mathf.Max(1f, airPoseBlendSharpness) * deltaTime)
        );

        if (!wasGrounded && isGrounded)
        {
            // Filter tiny ground-contact jitter so slopes do not constantly fake "mini landings".
            float minLandingAirTime = 0.045f;
            float minLandingSpeed = Mathf.Max(1.2f, landImpactMinSpeed * 0.45f);
            bool significantLanding = previousAirborneTime >= minLandingAirTime || -previousVerticalSpeed >= minLandingSpeed;
            if (significantLanding)
            {
                // Landing impact combines fall speed and actual airtime so short hops stay light.
                float impactSpeed01 = Mathf.InverseLerp(landImpactMinSpeed, landImpactMaxSpeed, -previousVerticalSpeed);
                float impactAir01 = Mathf.Clamp01(previousAirborneTime * 2.4f);
                float impact = Mathf.Clamp01(impactSpeed01 * 0.82f + impactAir01 * 0.40f);
                landingImpact = Mathf.Max(landingImpact, impact);
                float landingPulse = (0.08f + impact * 0.36f) * footstepPulseGain * (0.75f + runBlend * 0.35f);
                footstepPulse = Mathf.Max(footstepPulse, landingPulse);
                float reboundSeed = impact * Mathf.Clamp01(landingReboundStrength);
                if (reboundSeed > landingReboundAmount)
                {
                    landingReboundAmount = reboundSeed;
                    landingReboundPhase = 0f;
                }
            }
            airborneTime = 0f;
        }
        else if (isGrounded)
        {
            airborneTime = 0f;
        }

        landingImpact = Mathf.MoveTowards(landingImpact, 0f, Mathf.Max(0.01f, landRecoverSpeed) * deltaTime);
        if (landingReboundAmount > 0.001f)
        {
            landingReboundPhase = Mathf.Min(1f, landingReboundPhase + deltaTime * Mathf.Max(0.1f, landingReboundFrequency));
            float envelope = Mathf.Exp(-landingReboundPhase * Mathf.Max(0.1f, landingReboundDamping));
            landingReboundValue = Mathf.Sin(landingReboundPhase * Mathf.PI) * landingReboundAmount * envelope;
            if (landingReboundPhase >= 1f)
            {
                landingReboundAmount = 0f;
                landingReboundValue = 0f;
            }
        }
        else
        {
            landingReboundValue = 0f;
        }

        previousVerticalSpeed = verticalSpeed;
        wasGrounded = isGrounded;
    }

    private void UpdateAnticipationAndStopState(
        float deltaTime,
        bool isGrounded,
        float speed,
        Vector3 planarVelocity,
        float verticalSpeed)
    {
        if (stopTriggerCooldownLeft > 0f)
        {
            stopTriggerCooldownLeft = Mathf.Max(0f, stopTriggerCooldownLeft - deltaTime);
        }

        bool jumpPressed = RuntimeInput.JumpPressedThisFrame();
        if (jumpPressed && isGrounded && jumpPoseBlend < 0.18f)
        {
            preJumpHoldLeft = Mathf.Max(preJumpHoldLeft, Mathf.Max(0.03f, preJumpCrouchDuration));
        }

        if (preJumpHoldLeft > 0f)
        {
            preJumpHoldLeft = Mathf.Max(0f, preJumpHoldLeft - deltaTime);
            preJumpCrouch = Mathf.Lerp(
                preJumpCrouch,
                1f,
                1f - Mathf.Exp(-Mathf.Max(1f, anticipationRecoverSpeed * 1.6f) * deltaTime)
            );
        }
        else
        {
            float recover = isGrounded ? anticipationRecoverSpeed : anticipationRecoverSpeed * 2.2f;
            if (verticalSpeed > 0.65f)
            {
                recover *= 1.35f;
            }

            preJumpCrouch = Mathf.Lerp(
                preJumpCrouch,
                0f,
                1f - Mathf.Exp(-Mathf.Max(1f, recover) * deltaTime)
            );
        }

        float decel = (previousPlanarSpeed - speed) / Mathf.Max(0.005f, deltaTime);
        Vector2 moveInputNow = RuntimeInput.ReadMove();
        bool noMoveInput = moveInputNow.sqrMagnitude <= 0.01f;
        float directionDot = 1f;
        if (previousPlanarSpeed > 0.2f && speed > 0.2f)
        {
            directionDot = Vector3.Dot(previousPlanarVelocity.normalized, planarVelocity.normalized);
        }

        bool releasedStop = isGrounded
            && noMoveInput
            && previousPlanarSpeed > stopTriggerSpeed
            && decel > 0.6f;
        bool reverseBrakeStop = isGrounded
            && previousPlanarSpeed > stopTriggerSpeed
            && directionDot < 0.30f
            && decel > 0.3f;
        bool triggeredStop = (releasedStop || reverseBrakeStop) && stopTriggerCooldownLeft <= 0f;

        if (triggeredStop)
        {
            float speedDrop01 = Mathf.Clamp01((previousPlanarSpeed - speed) / Mathf.Max(0.01f, previousPlanarSpeed));
            float stopAmount = speedDrop01 * Mathf.Clamp(stopInertiaMax, 0.1f, 2f);
            stopImpact = Mathf.Max(stopImpact, stopAmount);
            float reboundSeed = stopAmount * Mathf.Clamp01(stopReboundStrength);
            if (reboundSeed > stopReboundAmount)
            {
                stopReboundAmount = reboundSeed;
                stopReboundPhase = 0f;
            }

            Vector3 previousLocal = transform.InverseTransformDirection(previousPlanarVelocity);
            Vector2 direction = new Vector2(previousLocal.x, previousLocal.z);
            if (direction.sqrMagnitude > 0.0001f)
            {
                stopLocalDir = direction.normalized;
            }

            stopTriggerCooldownLeft = Mathf.Max(0f, stopTriggerCooldown);
        }

        float stopRecover = Mathf.Max(0.1f, stopImpactRecoverSpeed) * (isGrounded ? 1f : 1.8f);
        stopImpact = Mathf.MoveTowards(stopImpact, 0f, stopRecover * deltaTime);
        if (stopReboundAmount > 0.001f)
        {
            stopReboundPhase = Mathf.Min(1f, stopReboundPhase + deltaTime * Mathf.Max(0.1f, stopReboundFrequency));
            float envelope = Mathf.Exp(-stopReboundPhase * Mathf.Max(0.1f, stopReboundDamping));
            stopReboundValue = Mathf.Sin(stopReboundPhase * Mathf.PI) * stopReboundAmount * envelope;
            if (stopReboundPhase >= 1f)
            {
                stopReboundAmount = 0f;
                stopReboundValue = 0f;
            }
        }
        else
        {
            stopReboundValue = 0f;
        }

        previousPlanarVelocity = planarVelocity;
        previousPlanarSpeed = speed;
    }

    private void UpdateScenarioProfiles(
        float deltaTime,
        bool isGrounded,
        float speed,
        Vector3 upDir,
        Vector3 downDir)
    {
        float walkTarget = isGrounded ? 1f - Mathf.InverseLerp(0.1f, Mathf.Max(0.2f, walkMaxSpeed), speed) : 0f;
        float sprintMin = Mathf.Min(runTopSpeed - 0.1f, Mathf.Max(0.1f, sprintMinSpeed));
        float sprintTarget = isGrounded ? Mathf.InverseLerp(sprintMin, Mathf.Max(sprintMin + 0.1f, runTopSpeed), speed) : 0f;

        float blendRate = 1f - Mathf.Exp(-Mathf.Max(1f, runBlendSharpness) * deltaTime);
        walkBlend = Mathf.Lerp(walkBlend, Mathf.Clamp01(walkTarget), blendRate);
        sprintBlend = Mathf.Lerp(sprintBlend, Mathf.Clamp01(sprintTarget), blendRate);

        Vector3 targetGroundNormal = upDir;
        if (isGrounded && TrySampleGroundNormal(upDir, downDir, out RaycastHit slopeHit))
        {
            targetGroundNormal = slopeHit.normal;
        }

        smoothedGroundNormal = Vector3.Slerp(
            smoothedGroundNormal,
            targetGroundNormal,
            1f - Mathf.Exp(-Mathf.Max(1f, slopeBlendSharpness) * deltaTime)
        );
        smoothedGroundNormal.Normalize();

        Vector3 localGroundNormal = transform.InverseTransformDirection(smoothedGroundNormal);
        float targetSlopeForward = Mathf.Clamp(-localGroundNormal.z, -1f, 1f);
        float targetSlopeSide = Mathf.Clamp(localGroundNormal.x, -1f, 1f);

        float slopeBlend = 1f - Mathf.Exp(-Mathf.Max(1f, slopeBlendSharpness) * deltaTime);
        slopeForwardBlend = Mathf.Lerp(slopeForwardBlend, targetSlopeForward, slopeBlend);
        slopeSideBlend = Mathf.Lerp(slopeSideBlend, targetSlopeSide, slopeBlend);

        UpdateSprintDynamics(deltaTime, isGrounded, speed);
    }

    private void UpdateSprintDynamics(float deltaTime, bool isGrounded, float speed)
    {
        bool sprinting = isGrounded
            && sprintBlend > 0.32f
            && speed >= Mathf.Max(0.1f, sprintMinSpeed * 0.85f);
        float entryThreshold = 0.52f;
        bool enteredSprint = sprinting
            && previousSprintBlend <= entryThreshold
            && sprintBlend > entryThreshold;
        float accelerationNorm = Mathf.Max(2f, accelerationReference);
        float forwardAcceleration01 = Mathf.Clamp01(smoothedLocalAcceleration.y / accelerationNorm);

        if (enteredSprint)
        {
            sprintBurst = Mathf.Max(sprintBurst, 1f);
            sprintSustainTime = 0f;
        }

        if (sprinting)
        {
            sprintSustainTime += deltaTime;
            sprintBurst = Mathf.Clamp01(
                sprintBurst + forwardAcceleration01 * Mathf.Max(0.1f, sprintBurstRiseSpeed) * deltaTime
            );
            if (sprintSustainTime >= sprintFatigueDelay)
            {
                sprintFatigue = Mathf.MoveTowards(
                    sprintFatigue,
                    1f,
                    Mathf.Max(0.01f, sprintFatigueBuildSpeed) * deltaTime
                );
            }
            else
            {
                sprintFatigue = Mathf.MoveTowards(
                    sprintFatigue,
                    0f,
                    Mathf.Max(0.01f, sprintFatigueRecoverSpeed) * deltaTime * 0.55f
                );
            }
        }
        else
        {
            sprintSustainTime = Mathf.Max(0f, sprintSustainTime - deltaTime * 1.8f);
            sprintFatigue = Mathf.MoveTowards(
                sprintFatigue,
                0f,
                Mathf.Max(0.01f, sprintFatigueRecoverSpeed) * deltaTime
            );
        }

        sprintBurst = Mathf.MoveTowards(
            sprintBurst,
            0f,
            Mathf.Max(0.1f, sprintBurstDecaySpeed) * deltaTime
        );
        previousSprintBlend = sprintBlend;
    }

    private bool TrySampleGroundNormal(Vector3 upDir, Vector3 downDir, out RaycastHit hit)
    {
        Vector3 rayOrigin = transform.position + upDir * Mathf.Max(0.1f, slopeProbeStartHeight);
        float rayDistance = Mathf.Max(0.15f, slopeProbeDistance);
        int count = Physics.RaycastNonAlloc(
            rayOrigin,
            downDir,
            footIkHitBuffer,
            rayDistance,
            slopeProbeMask,
            QueryTriggerInteraction.Ignore
        );

        if (count <= 0)
        {
            hit = default;
            return false;
        }

        int bestIndex = -1;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = footIkHitBuffer[i];
            if (candidate.collider == null)
            {
                continue;
            }

            if (candidate.collider.attachedRigidbody == physicsBody)
            {
                continue;
            }

            float align = Vector3.Dot(candidate.normal, upDir);
            if (align < 0.28f)
            {
                continue;
            }

            if (candidate.distance < bestDistance)
            {
                bestDistance = candidate.distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            hit = default;
            return false;
        }

        hit = footIkHitBuffer[bestIndex];
        return true;
    }

    private void AnimateVisual(float deltaTime)
    {
        if (visualRoot == null || physicsBody == null)
        {
            return;
        }

        GravitySystem gravity = GravitySystem.Instance;
        Vector3 downDir = gravity != null ? gravity.DownDirection : Vector3.down;
        Vector3 upDir = -downDir;
        Vector3 velocity = physicsBody.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, downDir);
        float speed = motor != null ? motor.PlanarSpeed : planarVelocity.magnitude;
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, runTopSpeed));
        bool isGrounded = motor != null && motor.IsGrounded;
        float verticalSpeed = Vector3.Dot(velocity, upDir);
        float rise01 = Mathf.Clamp01(verticalSpeed / 8.5f);
        float fall01 = Mathf.Clamp01(-verticalSpeed / 12f);
        Vector3 localPlanarVelocity = transform.InverseTransformDirection(planarVelocity);

        float localAccelerationX = 0f;
        float localAccelerationZ = 0f;
        if (deltaTime > 0.0001f)
        {
            localAccelerationX = (localPlanarVelocity.x - previousLocalPlanarVelocity.x) / deltaTime;
            localAccelerationZ = (localPlanarVelocity.z - previousLocalPlanarVelocity.z) / deltaTime;
        }
        previousLocalPlanarVelocity = localPlanarVelocity;

        float accelerationBlend = 1f - Mathf.Exp(-Mathf.Max(1f, accelerationBlendSharpness) * deltaTime);
        smoothedLocalAcceleration = Vector2.Lerp(
            smoothedLocalAcceleration,
            new Vector2(localAccelerationX, localAccelerationZ),
            accelerationBlend
        );

        UpdateMotionState(deltaTime, isGrounded, speed, verticalSpeed);
        UpdateAnticipationAndStopState(deltaTime, isGrounded, speed, planarVelocity, verticalSpeed);
        UpdateScenarioProfiles(deltaTime, isGrounded, speed, upDir, downDir);
        float sprintBurstAmount = sprintBurst * sprintBlend;
        float sprintFatigueAmount = sprintFatigue * sprintBlend;

        float bob = 0f;
        if (isGrounded)
        {
            float runBobScale = 1f + runBlend * runBobBoost + sprintBlend * 0.25f;
            runBobScale += sprintBurstAmount * 0.22f;
            runBobScale -= sprintFatigueAmount * 0.18f;
            float walkRateScale = Mathf.Lerp(0.72f, 1f, 1f - walkBlend);
            float sprintRateScale = 1f + sprintBlend * 0.26f + sprintBurstAmount * 0.18f - sprintFatigueAmount * 0.12f;
            sprintRateScale = Mathf.Clamp(sprintRateScale, 0.55f, 2.2f);
            float phase = Time.time * (bobFrequency * walkRateScale * sprintRateScale + speed * 0.22f + runBlend * runFrequencyBoost * 0.45f);
            bob = Mathf.Sin(phase) * bobAmplitude * speed01 * runBobScale;
        }
        float idleBreath = Mathf.Sin(Time.time * idleBreathFrequency) * idleBreathAmplitude * (1f - speed01) * (1f - jumpPoseBlend * 0.55f);

        float airborneOffset = jumpPoseBlend * (rise01 * 0.018f - fall01 * 0.028f);
        float landingDip = landingImpact * landBodyDip;
        float landingReboundLift = landingReboundValue * landBodyDip * 0.7f;
        float crouchDip = preJumpCrouch * preJumpCrouchDepth;
        float stopDip = stopImpact * preJumpCrouchDepth * 0.48f;
        float stopReboundLift = stopReboundValue * preJumpCrouchDepth * 0.28f;
        float swayPhase = gaitPhase * 2f + gaitSeedPhase;
        float swayWeight = speed01 * (0.40f + runBlend * 0.75f);
        float pelvisX = Mathf.Sin(swayPhase) * pelvisSwayOffset * swayWeight;
        float pelvisZ = Mathf.Sin(gaitPhase + gaitSeedPhase * 0.7f) * pelvisSwayOffset * 0.24f * swayWeight;
        Vector3 targetVisualPos = visualBasePosition + new Vector3(
            pelvisX,
            bob + idleBreath + airborneOffset - landingDip + landingReboundLift - crouchDip - stopDip + stopReboundLift,
            pelvisZ
        );
        visualRoot.localPosition = Vector3.Lerp(
            visualRoot.localPosition,
            targetVisualPos,
            1f - Mathf.Exp(-visualSmooth * deltaTime)
        );

        float accelerationNorm = Mathf.Max(2f, accelerationReference);
        float accelerationForward = Mathf.Clamp(smoothedLocalAcceleration.y / accelerationNorm, -1f, 1f);
        float accelerationSide = Mathf.Clamp(smoothedLocalAcceleration.x / accelerationNorm, -1f, 1f);
        float accelerationBodyWeight = isGrounded ? 1f : 0.42f;
        float roll = Mathf.Clamp(localPlanarVelocity.x / 6f, -1f, 1f) * -leanAngle;
        float pitch = Mathf.Clamp(localPlanarVelocity.z / 6f, -1f, 1f) * (leanAngle * 0.55f);
        pitch += Mathf.Sign(localPlanarVelocity.z) * (runForwardLeanAngle + sprintTorsoLeanBoost * sprintBlend) * runBlend * Mathf.Clamp01(Mathf.Abs(localPlanarVelocity.z) / 6f);
        pitch += sprintBurstAmount * sprintBurstLeanBoost;
        pitch -= sprintFatigueAmount * sprintFatigueLeanLoss;
        pitch += jumpPoseBlend * (fall01 * 4.2f - rise01 * 2f);
        pitch += landingImpact * 6.5f;
        pitch -= landingReboundValue * 5.2f;
        pitch += preJumpCrouch * 5.5f;
        pitch += -stopLocalDir.y * stopTiltAngle * stopImpact;
        pitch += stopLocalDir.y * stopTiltAngle * 0.52f * stopReboundValue;
        pitch += slopeForwardBlend * slopeBodyPitchAngle * (0.45f + speed01 * 0.55f);
        pitch += accelerationForward * accelerationPitchAngle * accelerationBodyWeight * (0.35f + speed01 * 0.65f);
        roll += stopLocalDir.x * stopTiltAngle * 0.62f * stopImpact;
        roll += -stopLocalDir.x * stopTiltAngle * 0.36f * stopReboundValue;
        roll += Mathf.Sin(swayPhase) * pelvisRollAngle * swayWeight;
        roll += -slopeSideBlend * slopeBodyRollAngle * (0.35f + speed01 * 0.65f);
        roll += -accelerationSide * accelerationRollAngle * accelerationBodyWeight * (0.30f + speed01 * 0.70f);
        float yaw = Mathf.Sin(swayPhase + Mathf.PI * 0.5f) * pelvisYawAngle * swayWeight;

        float leanScale = 1f;
        if (!isGrounded)
        {
            leanScale *= Mathf.Clamp01(airborneLeanMultiplier);
        }
        if (postFlipVisualStabilizeDuration > 0.001f && postFlipVisualStabilizeLeft > 0f)
        {
            float stabilize = Mathf.Clamp01(postFlipVisualStabilizeLeft / postFlipVisualStabilizeDuration);
            leanScale *= (1f - stabilize);
        }
        roll *= leanScale;
        pitch *= leanScale;
        yaw *= leanScale;
        Quaternion targetVisualRot = visualBaseRotation * Quaternion.Euler(pitch, yaw, roll);
        visualRoot.localRotation = Quaternion.Slerp(
            visualRoot.localRotation,
            targetVisualRot,
            1f - Mathf.Exp(-visualSmooth * deltaTime)
        );

        if (bodyPart != null)
        {
            float compression = (isGrounded ? 1f : 0.45f) * bodySquashAmount * speed01;
            compression *= 1f + runBlend * 0.30f;
            compression += landingImpact * landBodySquash;
            compression -= landingReboundValue * landBodySquash * 0.55f;
            compression -= sprintBurstAmount * (bodySquashAmount * 0.24f);
            compression += sprintFatigueAmount * (bodySquashAmount * 0.46f);
            compression += preJumpCrouch * (bodySquashAmount * 1.9f + 0.04f);
            compression += stopImpact * (bodySquashAmount * 1.1f + 0.02f);
            compression -= stopReboundValue * (bodySquashAmount * 0.75f);
            float airStretch = jumpPoseBlend * rise01 * 0.10f;
            Vector3 targetScale = new Vector3(
                Mathf.Max(0.08f, bodyBaseScale.x + compression * 0.45f - airStretch * 0.22f),
                Mathf.Max(0.1f, bodyBaseScale.y - compression + airStretch),
                Mathf.Max(0.08f, bodyBaseScale.z + compression * 0.45f - airStretch * 0.22f)
            );
            bodyPart.localScale = Vector3.Lerp(
                bodyPart.localScale,
                targetScale,
                1f - Mathf.Exp(-visualSmooth * deltaTime)
            );
        }

        if (headPart != null)
        {
            float nodFrequency = 3.0f + speed * 0.4f + runBlend * runHeadNodBoost * 2.1f + sprintBlend * 1.3f;
            nodFrequency += sprintBurstAmount * 1.8f;
            nodFrequency *= 1f - sprintFatigueAmount * 0.10f;
            nodFrequency *= Mathf.Lerp(0.82f, 1f, 1f - walkBlend);
            float nodAmplitude = 2.2f * speed01 * (1f + runBlend * runHeadNodBoost);
            nodAmplitude *= 1f + sprintBurstAmount * 0.30f;
            nodAmplitude *= 1f - sprintFatigueAmount * 0.18f;
            float nod = Mathf.Sin(Time.time * nodFrequency) * nodAmplitude;
            nod += landingImpact * -5.5f;
            nod += landingReboundValue * 2.8f;
            nod += jumpPoseBlend * (fall01 * 2.8f - rise01 * 1.6f);
            nod += preJumpCrouch * 3.2f;
            nod += stopImpact * (-stopLocalDir.y * 2.6f);
            nod += stopReboundValue * (stopLocalDir.y * 2f);
            nod += sprintFatigueAmount * -1.4f;
            float stabilizePitch = -pitch * Mathf.Clamp01(headStabilizeFactor);
            float stabilizeRoll = -roll * Mathf.Clamp01(headStabilizeFactor * 0.75f);
            Quaternion targetHead = headBaseRotation * Quaternion.Euler(nod + stabilizePitch, 0f, stabilizeRoll);
            headPart.localRotation = Quaternion.Slerp(
                headPart.localRotation,
                targetHead,
                1f - Mathf.Exp(-8f * deltaTime)
            );
        }

        if (armLeftPart != null || armRightPart != null || legLeftPart != null || legRightPart != null)
        {
            AnimateLocomotion(
                deltaTime,
                speed,
                speed01,
                localPlanarVelocity,
                verticalSpeed,
                isGrounded,
                rise01,
                fall01,
                upDir,
                downDir
            );
        }

        if (indicatorPart != null)
        {
            indicatorPart.Rotate(Vector3.up, indicatorSpinSpeed * deltaTime, Space.Self);
            float readiness = gravity != null && gravity.CanFlip ? 1f : 0f;
            Vector3 targetIndicatorScale = indicatorBaseScale * Mathf.Lerp(0.78f, 1f, readiness);
            indicatorPart.localScale = Vector3.Lerp(
                indicatorPart.localScale,
                targetIndicatorScale,
                1f - Mathf.Exp(-7f * deltaTime)
            );
        }
    }

    private void UpdatePulse(float deltaTime)
    {
        if (pulseRenderer == null || pulseBlock == null)
        {
            return;
        }

        flipPulse = Mathf.Max(0f, flipPulse - deltaTime * pulseFadeSpeed);
        footstepPulse = Mathf.Max(0f, footstepPulse - deltaTime * Mathf.Max(0.2f, footstepPulseDecay));
        float idlePulse = 0.85f + Mathf.Sin(Time.time * 4f) * 0.15f;
        float locomotionPulse = sprintBlend * 0.08f + walkBlend * 0.03f + sprintBurst * 0.10f + sprintFatigue * 0.05f;
        float impactPulse = landingImpact * 0.24f + stopImpact * 0.12f + landingReboundValue * 0.06f;
        float intensity = idlePulse + locomotionPulse + footstepPulse + impactPulse + flipPulse * 2.2f;
        Color emissionColor = basePulseColor * intensity;

        pulseRenderer.GetPropertyBlock(pulseBlock);
        pulseBlock.SetColor("_EmissionColor", emissionColor);
        pulseRenderer.SetPropertyBlock(pulseBlock);
    }

    private void CaptureBasePose()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        ResolveLimbReferences();

        visualBasePosition = visualRoot.localPosition;
        visualBaseRotation = visualRoot.localRotation;

        if (bodyPart != null)
        {
            bodyBaseScale = bodyPart.localScale;
        }

        if (headPart != null)
        {
            headBaseRotation = headPart.localRotation;
        }

        if (indicatorPart != null)
        {
            indicatorBaseScale = indicatorPart.localScale;
        }

        if (armLeftPart != null)
        {
            armLeftBaseRotation = armLeftPart.localRotation;
        }

        if (armRightPart != null)
        {
            armRightBaseRotation = armRightPart.localRotation;
        }

        if (legLeftPart != null)
        {
            legLeftBaseRotation = legLeftPart.localRotation;
            legLeftBasePosition = legLeftPart.localPosition;
            legLeftHalfLength = EstimateLegHalfLength(legLeftPart);
        }

        if (legRightPart != null)
        {
            legRightBaseRotation = legRightPart.localRotation;
            legRightBasePosition = legRightPart.localPosition;
            legRightHalfLength = EstimateLegHalfLength(legRightPart);
        }
    }

    private static float EstimateLegHalfLength(Transform leg)
    {
        if (leg == null)
        {
            return 0.24f;
        }

        Renderer renderer = leg.GetComponent<Renderer>();
        if (renderer != null)
        {
            return Mathf.Max(0.08f, renderer.localBounds.extents.y * Mathf.Abs(leg.lossyScale.y));
        }

        return Mathf.Max(0.08f, Mathf.Abs(leg.lossyScale.y) * 0.5f);
    }

    private void SetupPulseBlock()
    {
        if (pulseRenderer == null)
        {
            return;
        }

        pulseBlock = new MaterialPropertyBlock();
        Material shared = pulseRenderer.sharedMaterial;
        if (shared != null && shared.HasProperty("_EmissionColor"))
        {
            basePulseColor = shared.GetColor("_EmissionColor");
            if (basePulseColor.maxColorComponent <= 0.001f)
            {
                basePulseColor = new Color(0.20f, 1f, 0.76f);
            }
        }
    }

    private void AnimateLocomotion(
        float deltaTime,
        float speed,
        float speed01,
        Vector3 localPlanarVelocity,
        float verticalSpeed,
        bool isGrounded,
        float rise01,
        float fall01,
        Vector3 upDir,
        Vector3 downDir)
    {
        if (armSwingFrequency <= 0.01f)
        {
            return;
        }

        float armGroundedFactor = isGrounded ? 1f : Mathf.Clamp01(armAirDamping);
        float legGroundedFactor = isGrounded ? 1f : Mathf.Clamp01(legAirDamping);
        float sprintBurstAmount = sprintBurst * sprintBlend;
        float sprintFatigueAmount = sprintFatigue * sprintBlend;

        float forwardFactor = Mathf.Abs(Mathf.Clamp(localPlanarVelocity.z / 5.5f, -1f, 1f));
        float strafeFactor = Mathf.Abs(Mathf.Clamp(localPlanarVelocity.x / 5.5f, -1f, 1f));
        float directionalFactor = Mathf.Clamp01(Mathf.Lerp(0.60f, 1f, forwardFactor) + strafeFactor * 0.22f);
        float walkStrideScale = Mathf.Lerp(1f, 1f - Mathf.Clamp01(walkStrideShorten), walkBlend);
        float fatigueStrideScale = Mathf.Lerp(
            1f,
            Mathf.Max(0.2f, 1f - Mathf.Clamp(sprintFatigueStrideLoss, 0f, 0.9f)),
            sprintFatigueAmount
        );
        float sprintStrideScale = (1f + sprintBlend * Mathf.Max(0f, sprintStrideBoost))
            * (1f + sprintBurstAmount * Mathf.Max(0f, sprintBurstStrideBoost))
            * fatigueStrideScale;
        float slopeStrideScale = 1f - slopeForwardBlend * Mathf.Clamp(slopeStrideAdjust, -0.6f, 0.6f);
        float accelerationNorm = Mathf.Max(2f, accelerationReference);
        float accelerationForward = Mathf.Clamp(smoothedLocalAcceleration.y / accelerationNorm, -1f, 1f);
        float accelerationSide = Mathf.Clamp(smoothedLocalAcceleration.x / accelerationNorm, -1f, 1f);
        float accelerationStrideBoost = 1f + Mathf.Abs(accelerationForward) * 0.14f + Mathf.Abs(accelerationSide) * 0.08f + sprintBurstAmount * 0.10f;
        float runStrideScale = (1f + runBlend * runStrideBoost) * walkStrideScale * sprintStrideScale * slopeStrideScale * accelerationStrideBoost;
        runStrideScale = Mathf.Clamp(runStrideScale, 0.62f, 2.2f);
        float armIntensity = Mathf.Clamp01(speed01 * directionalFactor) * armGroundedFactor;
        float legIntensity = Mathf.Clamp01(speed01 * directionalFactor) * legGroundedFactor;
        armIntensity *= 1f + runBlend * 0.34f;
        armIntensity *= 1f + sprintBlend * sprintArmPumpBoost;
        armIntensity *= 1f + sprintBurstAmount * sprintBurstArmBoost;
        armIntensity *= Mathf.Lerp(
            1f,
            Mathf.Max(0.2f, 1f - Mathf.Clamp(sprintFatigueArmLoss, 0f, 0.9f)),
            sprintFatigueAmount
        );
        armIntensity *= Mathf.Lerp(1f, 1f - Mathf.Clamp01(walkArmDampen), walkBlend);
        armIntensity *= 1f + Mathf.Abs(slopeForwardBlend) * Mathf.Clamp01(slopeArmAdjust);
        legIntensity *= runStrideScale;
        legIntensity *= Mathf.Lerp(1f, Mathf.Max(0.2f, 1f - sprintFatigueStrideLoss * 0.82f), sprintFatigueAmount);
        float dynamicAirArmSuppression = Mathf.Clamp01(
            airArmSuppression - sprintFatigueAmount * Mathf.Clamp(sprintFatigueAirArmSuppression, 0f, 1f)
        );
        armIntensity *= Mathf.Lerp(1f, dynamicAirArmSuppression, jumpPoseBlend);
        legIntensity *= Mathf.Lerp(1f, 0.62f, jumpPoseBlend);

        float walkRateScale = Mathf.Lerp(0.72f, 1f, 1f - walkBlend);
        float sprintRateScale = 1f + sprintBlend * 0.34f + sprintBurstAmount * 0.22f - sprintFatigueAmount * 0.14f;
        sprintRateScale = Mathf.Clamp(sprintRateScale, 0.55f, 2.4f);
        float slopeRateScale = 1f + Mathf.Abs(slopeForwardBlend) * 0.12f;
        float phaseRate = (armSwingFrequency + speed * 0.62f + runBlend * runFrequencyBoost) * walkRateScale * sprintRateScale * slopeRateScale;
        gaitPhase += deltaTime * phaseRate;
        if (gaitPhase > Mathf.PI * 2f)
        {
            gaitPhase -= Mathf.PI * 2f;
        }

        float forwardSigned = Mathf.Clamp(localPlanarVelocity.z / 5.5f, -1f, 1f);
        float gaitDirection = Mathf.Abs(forwardSigned) > 0.08f ? Mathf.Sign(forwardSigned) : 1f;
        float step = Mathf.Sin(gaitPhase) * gaitDirection;
        float stepHarmonic = Mathf.Sin(gaitPhase * 2f) * gaitDirection;
        float strafeSigned = Mathf.Clamp(localPlanarVelocity.x / 5.5f, -1f, 1f);
        UpdateFootstepPulse(step, isGrounded, speed01, legIntensity);
        float armBlend = 1f - Mathf.Exp(-Mathf.Max(1f, armSwingSmooth) * deltaTime);
        float ikBlend = 1f - Mathf.Exp(-Mathf.Max(1f, footIkSmooth) * deltaTime);
        float legBlend = Mathf.Max(
            1f - Mathf.Exp(-Mathf.Max(1f, legSwingSmooth) * deltaTime),
            ikBlend * Mathf.Lerp(footIkAirWeight, footIkGroundWeight, isGrounded ? 1f : 0f)
        );
        float armAirPitch = jumpPoseBlend * (-rise01 * jumpArmBackAngle + fall01 * fallArmBraceAngle);
        float armAirRoll = jumpPoseBlend * Mathf.Clamp(verticalSpeed / 12f, -1f, 1f) * 2.2f;
        float armAnticipationPitch = -preJumpCrouch * preJumpArmBackAngle;
        float stopArmPitch = stopImpact * stopArmCatchAngle * stopLocalDir.y;
        float fatigueArmPitch = sprintFatigueAmount * stopArmCatchAngle * 0.26f;
        float slopeArmPitch = slopeForwardBlend * jumpArmBackAngle * 0.22f * (0.35f + speed01 * 0.65f);
        float accelArmPitch = -accelerationForward * accelerationArmCatchAngle * (isGrounded ? 1f : 0.42f) * (0.35f + speed01 * 0.65f);
        float accelArmYaw = accelerationSide * lateralAccelerationArmYaw * (isGrounded ? 1f : 0.52f) * (0.30f + speed01 * 0.70f);
        float shoulderCounterYaw = Mathf.Sin(gaitPhase + gaitSeedPhase * 0.5f) * pelvisYawAngle * (0.25f + runBlend * 0.50f);

        if (armLeftPart != null)
        {
            float pitch = (step + stepHarmonic * armSecondarySwing) * armSwingAngle * armIntensity
                + armAirPitch
                + slopeArmPitch
                + armAnticipationPitch
                + accelArmPitch
                + stopArmPitch
                - fatigueArmPitch;
            float yaw = -strafeSigned * armSideSwingAngle * armIntensity - shoulderCounterYaw - accelArmYaw;
            float roll = (-strafeSigned - step * 0.35f) * armTwistAngle * armIntensity + armAirRoll;
            Quaternion leftTarget = armLeftBaseRotation * Quaternion.Euler(pitch, yaw, roll);
            armLeftPart.localRotation = Quaternion.Slerp(armLeftPart.localRotation, leftTarget, armBlend);
        }

        if (armRightPart != null)
        {
            float pitch = (-step - stepHarmonic * armSecondarySwing) * armSwingAngle * armIntensity
                + armAirPitch
                + slopeArmPitch
                + armAnticipationPitch
                + accelArmPitch
                + stopArmPitch
                - fatigueArmPitch;
            float yaw = strafeSigned * armSideSwingAngle * armIntensity + shoulderCounterYaw - accelArmYaw;
            float roll = (strafeSigned + step * 0.35f) * armTwistAngle * armIntensity - armAirRoll;
            Quaternion rightTarget = armRightBaseRotation * Quaternion.Euler(pitch, yaw, roll);
            armRightPart.localRotation = Quaternion.Slerp(armRightPart.localRotation, rightTarget, armBlend);
        }

        if (legLeftPart != null)
        {
            AnimateSingleLeg(
                legLeftPart,
                legLeftBaseRotation,
                legLeftBasePosition,
                -step,
                strafeSigned,
                legIntensity,
                legBlend,
                runStrideScale,
                rise01,
                fall01,
                upDir,
                downDir,
                isGrounded,
                true
            );
        }

        if (legRightPart != null)
        {
            AnimateSingleLeg(
                legRightPart,
                legRightBaseRotation,
                legRightBasePosition,
                step,
                strafeSigned,
                legIntensity,
                legBlend,
                runStrideScale,
                rise01,
                fall01,
                upDir,
                downDir,
                isGrounded,
                false
            );
        }
    }

    private void AnimateSingleLeg(
        Transform leg,
        Quaternion baseRotation,
        Vector3 basePosition,
        float step,
        float strafeSigned,
        float legIntensity,
        float blend,
        float runStrideScale,
        float rise01,
        float fall01,
        Vector3 upDir,
        Vector3 downDir,
        bool isGrounded,
        bool isLeftLeg)
    {
        if (leg == null)
        {
            return;
        }

        float asymmetryScale = 1f + (isLeftLeg ? -1f : 1f) * strideAsymmetry * (0.35f + runBlend * 0.65f);
        float sideSign = basePosition.x < 0f ? -1f : 1f;
        float lift = Mathf.Max(0f, step);
        float strideHarmonic = Mathf.Sin((gaitPhase + (sideSign < 0f ? Mathf.PI : 0f)) * 2f) * 0.18f;
        float airLegPitch = jumpPoseBlend * (rise01 * jumpLegTuckAngle - fall01 * fallLegBraceAngle);
        float landingLegPitch = -landingImpact * landLegCompressAngle;
        float anticipationLegPitch = -preJumpCrouch * preJumpLegBendAngle;
        float stopLegPitch = stopImpact * stopLegBraceAngle * stopLocalDir.y;
        float slopeLegPitch = slopeForwardBlend * legSwingAngle * 0.22f * (0.35f + legIntensity * 0.65f);
        float sprintBurstAmount = sprintBurst * sprintBlend;
        float sprintFatigueAmount = sprintFatigue * sprintBlend;
        float sprintKneeDrive = sprintBlend * legLiftAngle * 0.22f;
        sprintKneeDrive += sprintBurstAmount * legLiftAngle * 0.35f;
        sprintKneeDrive -= sprintFatigueAmount * legLiftAngle * 0.30f;

        float pitch = (step + strideHarmonic) * legSwingAngle * legIntensity * runStrideScale
            - lift * legLiftAngle * legIntensity
            + airLegPitch
            + landingLegPitch
            + slopeLegPitch
            + sprintKneeDrive
            + anticipationLegPitch
            + stopLegPitch;
        pitch *= asymmetryScale;
        float yaw = strafeSigned * legStrafeYawAngle * sideSign * legIntensity + slopeSideBlend * legStrafeYawAngle * 0.24f * sideSign;
        float roll = sideSign * legStanceRollAngle * legIntensity * (0.65f + Mathf.Abs(step) * 0.35f) * asymmetryScale;
        roll += -slopeSideBlend * legStanceRollAngle * 0.55f;
        Quaternion targetRotation = baseRotation * Quaternion.Euler(pitch, yaw, roll);

        Vector3 targetPosition = basePosition;
        float lowSpeedPlanting = Mathf.Lerp(0.42f, 1f, Mathf.Clamp01(legIntensity * 1.8f));
        float accelerationNorm = Mathf.Max(2f, accelerationReference);
        float accelForward = Mathf.Clamp(smoothedLocalAcceleration.y / accelerationNorm, -1f, 1f);
        float accelStrideBias = 1f + Mathf.Abs(accelForward) * 0.08f;
        targetPosition.z += step * legStrideOffset * legIntensity * runStrideScale * lowSpeedPlanting * accelStrideBias * asymmetryScale;
        float slopeLift = Mathf.Max(0f, slopeForwardBlend) * legLiftOffset * Mathf.Clamp01(slopeLiftAdjust);
        targetPosition.y += lift * legLiftOffset * legIntensity * lowSpeedPlanting + slopeLift;
        targetPosition.y += jumpPoseBlend * (rise01 * legLiftOffset * 0.72f - fall01 * legLiftOffset * 0.36f);
        targetPosition.y -= landingImpact * legLiftOffset * 0.9f;

        if (enableFootIk)
        {
            float groundWeight = isGrounded ? Mathf.Clamp01(footIkGroundWeight) : Mathf.Clamp01(footIkAirWeight);
            float plantWeight = Mathf.Lerp(1f, 0.34f, lift);
            float ikWeight = groundWeight * plantWeight * (1f - jumpPoseBlend * 0.55f);
            ApplyFootIk(
                leg,
                ref targetRotation,
                ref targetPosition,
                upDir,
                downDir,
                sideSign,
                isLeftLeg ? legLeftHalfLength : legRightHalfLength,
                ikWeight,
                isLeftLeg
            );
        }

        leg.localRotation = Quaternion.Slerp(leg.localRotation, targetRotation, blend);
        leg.localPosition = Vector3.Lerp(leg.localPosition, targetPosition, blend);
    }

    private void UpdateFootstepPulse(float stepSignal, bool isGrounded, float speed01, float legIntensity)
    {
        bool crossedPhase = (previousStepSignal <= 0f && stepSignal > 0f)
            || (previousStepSignal >= 0f && stepSignal < 0f);

        if (isGrounded && crossedPhase && speed01 > 0.06f && legIntensity > 0.08f)
        {
            float accelerationNorm = Mathf.Max(2f, accelerationReference);
            float accelerationPulse = (
                Mathf.Abs(smoothedLocalAcceleration.x)
                + Mathf.Abs(smoothedLocalAcceleration.y)
            ) / (accelerationNorm * 2f);
            float pulse = (
                0.12f
                + speed01 * 0.42f
                + runBlend * 0.26f
                + sprintBlend * 0.20f
                + sprintBurst * 0.22f
                + sprintFatigue * 0.12f
                + walkBlend * 0.08f
                + Mathf.Abs(slopeForwardBlend) * 0.09f
                + Mathf.Clamp01(accelerationPulse) * accelerationStepPulseGain
                + stopImpact * 0.24f
            ) * footstepPulseGain;
            footstepPulse = Mathf.Max(footstepPulse, pulse);
        }

        previousStepSignal = stepSignal;
    }

    private void ApplyFootIk(
        Transform leg,
        ref Quaternion targetRotation,
        ref Vector3 targetPosition,
        Vector3 upDir,
        Vector3 downDir,
        float sideSign,
        float legHalfLength,
        float ikWeight,
        bool isLeftLeg)
    {
        if (leg == null || visualRoot == null)
        {
            return;
        }

        if (ikWeight <= 0.001f)
        {
            SmoothFootIkState(isLeftLeg, 0f, 0f, 0f, out float fadeOffset, out float fadePitch, out float fadeRoll);
            targetPosition += visualRoot.InverseTransformVector(upDir * fadeOffset);
            targetRotation *= Quaternion.Euler(fadePitch, 0f, fadeRoll);
            return;
        }

        float rayStart = Mathf.Max(0.02f, footIkRayStartHeight);
        float rayDistance = Mathf.Max(0.08f, footIkRayDistance);
        float halfLen = Mathf.Max(0.08f, legHalfLength);

        Vector3 legCenterWorld = visualRoot.TransformPoint(targetPosition);
        Vector3 footWorld = legCenterWorld + downDir * halfLen;
        Vector3 rayOrigin = footWorld + upDir * rayStart;
        if (physicsBody != null && footIkPredictionTime > 0.0001f)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(physicsBody.linearVelocity, downDir);
            float predictionTime = Mathf.Clamp(footIkPredictionTime, 0f, 0.15f);
            Vector3 predictedOffset = Vector3.ClampMagnitude(planarVelocity * predictionTime, 0.45f);
            rayOrigin += predictedOffset * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(ikWeight));
        }
        float castDistance = rayStart + rayDistance;

        float desiredOffset = 0f;
        float desiredPitch = 0f;
        float desiredRoll = 0f;

        if (!TryFootIkGroundHit(rayOrigin, downDir, castDistance, out RaycastHit hit))
        {
            SmoothFootIkState(isLeftLeg, desiredOffset, desiredPitch, desiredRoll, out float missingOffset, out float missingPitch, out float missingRoll);
            targetPosition += visualRoot.InverseTransformVector(upDir * missingOffset);
            targetRotation *= Quaternion.Euler(missingPitch, 0f, missingRoll);
            return;
        }

        float standingAlignment = Vector3.Dot(hit.normal, upDir);
        if (standingAlignment < 0.22f)
        {
            SmoothFootIkState(isLeftLeg, desiredOffset, desiredPitch, desiredRoll, out float badOffset, out float badPitch, out float badRoll);
            targetPosition += visualRoot.InverseTransformVector(upDir * badOffset);
            targetRotation *= Quaternion.Euler(badPitch, 0f, badRoll);
            return;
        }

        Vector3 targetFootWorld = hit.point + upDir * footIkFootClearance;
        desiredOffset = Vector3.Dot(targetFootWorld - footWorld, upDir);
        desiredOffset = Mathf.Clamp(
            desiredOffset,
            -Mathf.Max(0.02f, footIkMaxDrop),
            Mathf.Max(0.02f, footIkMaxLift)
        ) * ikWeight;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, downDir);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(visualRoot.forward, downDir);
        }
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(upDir, Vector3.right);
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(upDir, forward).normalized;
        float slopeForward = Mathf.Clamp(Vector3.Dot(hit.normal, forward), -1f, 1f);
        float slopeRight = Mathf.Clamp(Vector3.Dot(hit.normal, right), -1f, 1f);

        desiredPitch = -slopeForward * footIkPitchAngle * ikWeight;
        desiredRoll = slopeRight * footIkRollAngle * ikWeight;
        // Slightly bias outside leg on aggressive bank to keep silhouette stable.
        desiredRoll += sideSign * slopeRight * footIkRollAngle * 0.12f * ikWeight;

        SmoothFootIkState(isLeftLeg, desiredOffset, desiredPitch, desiredRoll, out float footDelta, out float ikPitch, out float ikRoll);
        targetPosition += visualRoot.InverseTransformVector(upDir * footDelta);
        targetRotation *= Quaternion.Euler(ikPitch, 0f, ikRoll);
    }

    private void SmoothFootIkState(
        bool isLeftLeg,
        float targetOffset,
        float targetPitch,
        float targetRoll,
        out float smoothedOffset,
        out float smoothedPitch,
        out float smoothedRoll)
    {
        float blend = 1f - Mathf.Exp(-Mathf.Max(1f, footIkSmooth) * Time.deltaTime);
        if (isLeftLeg)
        {
            leftIkOffsetSmoothed = Mathf.Lerp(leftIkOffsetSmoothed, targetOffset, blend);
            leftIkPitchSmoothed = Mathf.Lerp(leftIkPitchSmoothed, targetPitch, blend);
            leftIkRollSmoothed = Mathf.Lerp(leftIkRollSmoothed, targetRoll, blend);
            smoothedOffset = leftIkOffsetSmoothed;
            smoothedPitch = leftIkPitchSmoothed;
            smoothedRoll = leftIkRollSmoothed;
            return;
        }

        rightIkOffsetSmoothed = Mathf.Lerp(rightIkOffsetSmoothed, targetOffset, blend);
        rightIkPitchSmoothed = Mathf.Lerp(rightIkPitchSmoothed, targetPitch, blend);
        rightIkRollSmoothed = Mathf.Lerp(rightIkRollSmoothed, targetRoll, blend);
        smoothedOffset = rightIkOffsetSmoothed;
        smoothedPitch = rightIkPitchSmoothed;
        smoothedRoll = rightIkRollSmoothed;
    }

    private bool TryFootIkGroundHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            footIkHitBuffer,
            distance,
            footIkMask,
            QueryTriggerInteraction.Ignore
        );

        if (count <= 0)
        {
            hit = default;
            return false;
        }

        int bestIndex = -1;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = footIkHitBuffer[i];
            if (candidate.collider == null)
            {
                continue;
            }

            if (candidate.collider.attachedRigidbody == physicsBody)
            {
                continue;
            }

            if (candidate.distance < bestDistance)
            {
                bestDistance = candidate.distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            hit = default;
            return false;
        }

        hit = footIkHitBuffer[bestIndex];
        return true;
    }

    private void ResolveLimbReferences()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (armLeftPart == null)
        {
            armLeftPart = FindNamedChildRecursive(visualRoot, "ArmL");
        }

        if (armRightPart == null)
        {
            armRightPart = FindNamedChildRecursive(visualRoot, "ArmR");
        }

        if (legLeftPart == null)
        {
            legLeftPart = FindNamedChildRecursive(visualRoot, "LegL");
        }

        if (legRightPart == null)
        {
            legRightPart = FindNamedChildRecursive(visualRoot, "LegR");
        }
    }

    private static Transform FindNamedChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindNamedChildRecursive(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
