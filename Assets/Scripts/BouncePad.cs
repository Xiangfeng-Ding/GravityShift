using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BouncePad : MonoBehaviour
{
    [SerializeField] private float bounceVelocity = 18f;
    [SerializeField] private float forwardBoost = 9.5f;
    [SerializeField] private float cooldown = 0.22f;
    [SerializeField] private float minUpwardVelocity = 4f;
    [SerializeField] private float lateralBoost = 1.12f;
    [SerializeField] private bool showHint = true;

    private float cooldownLeft;
    private readonly HashSet<int> consumedPlayerIds = new HashSet<int>();

    public void Configure(float velocity, float triggerCooldown = 0.22f, float planarBoost = 9.5f)
    {
        bounceVelocity = Mathf.Max(6f, velocity);
        cooldown = Mathf.Clamp(triggerCooldown, 0.05f, 1.2f);
        forwardBoost = Mathf.Clamp(planarBoost, 0f, 24f);
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void Update()
    {
        if (cooldownLeft > 0f)
        {
            cooldownLeft = Mathf.Max(0f, cooldownLeft - Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleBounce(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Safety net: if enter event is missed by trigger transition, stay can still bounce once.
        TryHandleBounce(other);
    }

    private void OnTriggerExit(Collider other)
    {
        int id = ResolvePlayerId(other);
        if (id != 0)
        {
            consumedPlayerIds.Remove(id);
        }
    }

    private void OnDisable()
    {
        consumedPlayerIds.Clear();
    }

    private void TryHandleBounce(Collider other)
    {
        int playerId = ResolvePlayerId(other);
        if (playerId == 0)
        {
            return;
        }

        if (consumedPlayerIds.Contains(playerId))
        {
            return;
        }

        if (cooldownLeft > 0f)
        {
            return;
        }

        PlayerGravityMotor motor = other != null ? other.GetComponentInParent<PlayerGravityMotor>() : null;
        if (motor == null)
        {
            return;
        }

        Rigidbody body = motor.GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        Vector3 upDir = GravitySystem.Instance != null ? GravitySystem.Instance.UpDirection : Vector3.up;
        Vector3 downDir = -upDir;
        Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, downDir).normalized;
        if (forwardDir.sqrMagnitude < 0.001f)
        {
            forwardDir = Vector3.ProjectOnPlane(body.transform.forward, downDir).normalized;
        }

        Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, downDir) * Mathf.Clamp(lateralBoost, 1f, 1.4f);
        if (forwardDir.sqrMagnitude > 0.001f && forwardBoost > 0f)
        {
            planar += forwardDir * forwardBoost;
            planar = Vector3.ClampMagnitude(planar, Mathf.Max(10f, forwardBoost * 1.9f));
        }
        float currentUp = Vector3.Dot(body.linearVelocity, upDir);
        float targetUp = Mathf.Max(Mathf.Max(minUpwardVelocity, bounceVelocity), currentUp + 2f);
        body.linearVelocity = planar + upDir * targetUp;

        cooldownLeft = cooldown;

        if (showHint)
        {
            RuntimeHUD.Instance?.ShowTransientMessage("Bounce!", 0.7f);
        }

        GameFeedback.Instance?.PlayBouncePad();
        consumedPlayerIds.Add(playerId);
    }

    private static int ResolvePlayerId(Collider other)
    {
        if (other == null)
        {
            return 0;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        return player != null ? player.GetInstanceID() : 0;
    }
}
