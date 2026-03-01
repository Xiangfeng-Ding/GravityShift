using System;
using UnityEngine;

public class RhythmGate : MonoBehaviour
{
    [SerializeField] private float openWindowDuration = 3.4f;
    [SerializeField] private float warningLeadTime = 0.85f;
    [SerializeField] private float warningFlashFrequency = 18f;
    [SerializeField] private string openedMessage = "Rhythm gate open";
    [SerializeField] private string closedMessage = "Rhythm gate closed";
    [SerializeField] private MechanismFeedbackType feedbackType = MechanismFeedbackType.RhythmGate;
    [SerializeField] private string countdownKey = "rhythm_gate";
    [SerializeField] private string countdownLabel = "Rhythm Gate";
    [SerializeField] private Color countdownColor = new Color(0.95f, 0.72f, 0.30f);

    public event Action<float> OnOpened;
    public event Action OnClosed;

    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private RhythmSwitch linkedSwitch;
    private bool switchBound;
    private bool gateOpen;
    private float openWindowLeft;
    private bool visualVisible = true;

    public bool IsOpen => gateOpen;
    public float RemainingWindow => Mathf.Max(0f, openWindowLeft);
    public RhythmSwitch LinkedSwitch => linkedSwitch;

    public void Configure(
        float windowDuration,
        float warningTime = 0.85f,
        float flashFrequency = 18f,
        string hudCountdownKey = null,
        string hudCountdownLabel = null,
        Color? hudCountdownColor = null)
    {
        openWindowDuration = Mathf.Clamp(windowDuration, 0.8f, 12f);
        warningLeadTime = Mathf.Clamp(warningTime, 0.2f, openWindowDuration);
        warningFlashFrequency = Mathf.Clamp(flashFrequency, 4f, 50f);

        if (!string.IsNullOrEmpty(hudCountdownKey))
        {
            countdownKey = hudCountdownKey;
        }

        if (!string.IsNullOrEmpty(hudCountdownLabel))
        {
            countdownLabel = hudCountdownLabel;
        }

        if (hudCountdownColor.HasValue)
        {
            countdownColor = hudCountdownColor.Value;
        }
    }

    public void ConfigureFeedbackType(MechanismFeedbackType type)
    {
        feedbackType = type;
    }

    public void OpenWindow(float durationOverride = -1f)
    {
        float duration = durationOverride > 0f
            ? Mathf.Clamp(durationOverride, 0.2f, 20f)
            : Mathf.Clamp(openWindowDuration, 0.8f, 12f);
        bool wasClosed = !gateOpen;

        openWindowLeft = duration;
        RuntimeHUD.Instance?.ShowCountdownBar(countdownKey, countdownLabel, countdownColor, duration);

        if (wasClosed)
        {
            OpenGate(duration);
            OnOpened?.Invoke(duration);
        }
        else
        {
            RuntimeHUD.Instance?.ShowTransientMessage("Gate window reset: " + duration.ToString("0.0") + "s", 0.9f);
        }
    }

    public void ResetGate(bool forceClosed = true)
    {
        openWindowLeft = 0f;
        gateOpen = !forceClosed;
        if (forceClosed)
        {
            SetClosedState(true);
        }
        else
        {
            SetClosedState(false);
        }
        RuntimeHUD.Instance?.HideCountdownBar(countdownKey);
    }

    public void LinkSwitch(RhythmSwitch rhythmSwitch)
    {
        if (linkedSwitch == rhythmSwitch && switchBound)
        {
            return;
        }

        UnbindSwitch();
        linkedSwitch = rhythmSwitch;
        BindSwitch();
    }

    private void Awake()
    {
        CacheParts();
        SetClosedState(true);
    }

    private void OnEnable()
    {
        BindSwitch();
    }

    private void OnDisable()
    {
        UnbindSwitch();
    }

    private void Update()
    {
        if (!gateOpen)
        {
            return;
        }

        openWindowLeft = Mathf.Max(0f, openWindowLeft - Time.deltaTime);
        if (openWindowLeft <= 0f)
        {
            CloseGate();
            return;
        }

        UpdateOpenVisual();
    }

    private void HandleSwitchTriggered()
    {
        OpenWindow();
    }

    private void OpenGate(float duration)
    {
        gateOpen = true;
        SetCollidersEnabled(false);
        SetRenderersVisible(false);
        RuntimeHUD.Instance?.ShowTransientMessage(openedMessage + " (" + duration.ToString("0.0") + "s)", 1.1f);
        GameFeedback.Instance?.PlayBarrierUnlocked(feedbackType);
    }

    private void CloseGate()
    {
        gateOpen = false;
        openWindowLeft = 0f;
        SetClosedState(true);
        RuntimeHUD.Instance?.HideCountdownBar(countdownKey);
        RuntimeHUD.Instance?.ShowTransientMessage(closedMessage, 1f);
        GameFeedback.Instance?.PlayRhythmGateClosed();
        OnClosed?.Invoke();
    }

    private void UpdateOpenVisual()
    {
        if (openWindowLeft > warningLeadTime)
        {
            SetRenderersVisible(false);
            return;
        }

        bool visible = Mathf.FloorToInt(Time.time * warningFlashFrequency) % 2 == 0;
        SetRenderersVisible(visible);
    }

    private void SetClosedState(bool closed)
    {
        gateOpen = !closed;
        SetCollidersEnabled(closed);
        SetRenderersVisible(closed);
    }

    private void SetCollidersEnabled(bool enabledState)
    {
        if (cachedColliders == null)
        {
            return;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider c = cachedColliders[i];
            if (c != null)
            {
                c.enabled = enabledState;
            }
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (cachedRenderers == null || visualVisible == visible)
        {
            return;
        }

        visualVisible = visible;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
    }

    private void CacheParts()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void BindSwitch()
    {
        if (switchBound || linkedSwitch == null)
        {
            return;
        }

        linkedSwitch.OnTriggered += HandleSwitchTriggered;
        switchBound = true;
    }

    private void UnbindSwitch()
    {
        if (!switchBound || linkedSwitch == null)
        {
            switchBound = false;
            return;
        }

        linkedSwitch.OnTriggered -= HandleSwitchTriggered;
        switchBound = false;
    }
}
