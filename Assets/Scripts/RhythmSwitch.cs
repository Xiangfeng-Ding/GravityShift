using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RhythmSwitch : MonoBehaviour
{
    [SerializeField] private float triggerCooldown = 0.45f;
    [SerializeField] private float activePulseDuration = 0.20f;
    [SerializeField] private string activationMessage = "Rhythm switch triggered";
    [SerializeField] private string lockedHintMessage = "Switch locked";
    [SerializeField] private float lockedHintCooldown = 0.9f;
    [SerializeField] private Color idleColor = new Color(1f, 0.58f, 0.20f);
    [SerializeField] private Color activeColor = new Color(0.28f, 1f, 0.78f);
    [SerializeField] private Color cooldownColor = new Color(0.42f, 0.70f, 0.90f);
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.51f, 0.60f);

    public event Action OnTriggered;

    private Renderer markerRenderer;
    private MaterialPropertyBlock markerBlock;
    private float cooldownLeft;
    private float activePulseLeft;
    private bool isInteractable = true;
    private float nextLockedHintTime;
    private readonly HashSet<int> consumedPlayerIds = new HashSet<int>();

    public float CooldownLeft => Mathf.Max(0f, cooldownLeft);
    public bool IsInteractable => isInteractable;

    public void Configure(float cooldown, string message = null)
    {
        triggerCooldown = Mathf.Clamp(cooldown, 0.08f, 2f);
        if (!string.IsNullOrEmpty(message))
        {
            activationMessage = message;
        }
    }

    public void SetInteractable(bool value, string lockedHint = null)
    {
        isInteractable = value;
        if (!string.IsNullOrEmpty(lockedHint))
        {
            lockedHintMessage = lockedHint;
        }
        ApplyVisualState();
    }

    public void ResetState(bool interactable = true, string lockedHint = null)
    {
        cooldownLeft = 0f;
        activePulseLeft = 0f;
        nextLockedHintTime = 0f;
        consumedPlayerIds.Clear();
        SetInteractable(interactable, lockedHint);
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        markerRenderer = GetComponentInChildren<Renderer>();
        if (markerRenderer != null)
        {
            markerBlock = new MaterialPropertyBlock();
        }

        ApplyVisualState();
    }

    private void Update()
    {
        if (cooldownLeft > 0f)
        {
            cooldownLeft = Mathf.Max(0f, cooldownLeft - Time.deltaTime);
        }

        if (activePulseLeft > 0f)
        {
            activePulseLeft = Mathf.Max(0f, activePulseLeft - Time.deltaTime);
        }

        ApplyVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandlePresence(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Safety net: if enter is missed due trigger state transitions, stay can still trigger once.
        TryHandlePresence(other);
    }

    private void OnTriggerExit(Collider other)
    {
        int playerId = ResolvePlayerId(other);
        if (playerId != 0)
        {
            consumedPlayerIds.Remove(playerId);
        }
    }

    private void OnDisable()
    {
        consumedPlayerIds.Clear();
    }

    private void TryHandlePresence(Collider other)
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

        if (TryHandleTrigger(other))
        {
            consumedPlayerIds.Add(playerId);
        }
    }

    private bool TryHandleTrigger(Collider other)
    {
        if (!GameDirector.AllowGameplayInput)
        {
            return false;
        }

        if (cooldownLeft > 0f)
        {
            return false;
        }

        if (other.GetComponentInParent<PlayerGravityMotor>() == null)
        {
            return false;
        }

        if (!isInteractable)
        {
            if (Time.time >= nextLockedHintTime)
            {
                nextLockedHintTime = Time.time + Mathf.Max(0.2f, lockedHintCooldown);
                if (!string.IsNullOrEmpty(lockedHintMessage))
                {
                    RuntimeHUD.Instance?.ShowTransientMessage(lockedHintMessage, 0.95f);
                }
                GameFeedback.Instance?.PlaySwitchLocked();
            }
            return false;
        }

        TriggerSwitch();
        return true;
    }

    private void TriggerSwitch()
    {
        cooldownLeft = Mathf.Max(0.08f, triggerCooldown);
        activePulseLeft = Mathf.Max(0.08f, activePulseDuration);

        if (!string.IsNullOrEmpty(activationMessage))
        {
            RuntimeHUD.Instance?.ShowTransientMessage(activationMessage, 0.95f);
        }

        GameFeedback.Instance?.PlayRhythmSwitchTriggered();
        OnTriggered?.Invoke();
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

    private void ApplyVisualState()
    {
        if (!isInteractable)
        {
            ApplyColor(lockedColor, 0.7f);
            return;
        }

        if (activePulseLeft > 0f)
        {
            ApplyColor(activeColor, 2.0f);
            return;
        }

        if (cooldownLeft > 0f)
        {
            ApplyColor(cooldownColor, 1.2f);
            return;
        }

        ApplyColor(idleColor, 1.25f);
    }

    private void ApplyColor(Color color, float emissionMultiplier)
    {
        if (markerRenderer == null || markerBlock == null)
        {
            return;
        }

        markerRenderer.GetPropertyBlock(markerBlock);
        markerBlock.SetColor("_Color", color);
        markerBlock.SetColor("_EmissionColor", color * emissionMultiplier);
        markerRenderer.SetPropertyBlock(markerBlock);
    }
}
