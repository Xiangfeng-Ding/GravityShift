using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GravitySwitchButton : MonoBehaviour
{
    [SerializeField] private Color idleColor = new Color(1f, 0.45f, 0.2f);
    [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.6f);
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.5f, 0.58f);
    [SerializeField] private float lockedHintCooldown = 1f;
    [SerializeField] private string lockedHintMessage = "Switch is locked";

    public event Action OnActivated;

    public bool IsActivated { get; private set; }
    public bool IsInteractable => isInteractable;

    private Renderer markerRenderer;
    private MaterialPropertyBlock markerBlock;
    private bool isInteractable = true;
    private float nextLockedHintTime;
    private readonly HashSet<int> consumedPlayerIds = new HashSet<int>();

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

    public void ResetState()
    {
        IsActivated = false;
        consumedPlayerIds.Clear();
        ApplyVisualState();
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

    private void OnTriggerEnter(Collider other)
    {
        TryHandlePresence(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Safety net: if enter is missed due trigger state transitions, stay can still activate once.
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
        if (IsActivated || !GameDirector.AllowGameplayInput)
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
                nextLockedHintTime = Time.time + lockedHintCooldown;
                RuntimeHUD.Instance?.ShowTransientMessage(lockedHintMessage, 1.3f);
                GameFeedback.Instance?.PlaySwitchLocked();
            }
            return false;
        }

        IsActivated = true;
        ApplyColor(activeColor, 1.9f);
        RuntimeHUD.Instance?.ShowTransientMessage("Switch activated", 1.2f);
        GameFeedback.Instance?.PlaySwitchActivated();
        OnActivated?.Invoke();
        return true;
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
        if (IsActivated)
        {
            ApplyColor(activeColor, 1.9f);
            return;
        }

        if (!isInteractable)
        {
            ApplyColor(lockedColor, 0.65f);
            return;
        }

        ApplyColor(idleColor, 1.2f);
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
