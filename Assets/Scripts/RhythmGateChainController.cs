using UnityEngine;

public class RhythmGateChainController : MonoBehaviour
{
    [SerializeField] private RhythmSwitch switchA;
    [SerializeField] private RhythmSwitch switchB;
    [SerializeField] private RhythmGate gateA;
    [SerializeField] private RhythmGate gateB;
    [SerializeField] private string switchBLockedHint = "Trigger A gate first";
    [SerializeField] private string switchBReadyHint = "A gate open - trigger switch B now";
    [SerializeField] private string chainRetryHint = "A window closed - restart from switch A";
    [SerializeField] private string chainCompleteHint = "A->B rhythm chain complete";

    private bool gateAWindowActive;
    private bool gateBTriggeredInCurrentWindow;
    private bool eventsBound;

    public RhythmSwitch SwitchA => switchA;
    public RhythmSwitch SwitchB => switchB;
    public RhythmGate GateA => gateA;
    public RhythmGate GateB => gateB;

    public void Configure(
        RhythmSwitch newSwitchA,
        RhythmSwitch newSwitchB,
        RhythmGate newGateA,
        RhythmGate newGateB,
        string lockedHint = null)
    {
        UnbindEvents();

        switchA = newSwitchA;
        switchB = newSwitchB;
        gateA = newGateA;
        gateB = newGateB;

        if (!string.IsNullOrEmpty(lockedHint))
        {
            switchBLockedHint = lockedHint;
        }

        ResetChainState();
        BindEvents();
    }

    private void OnEnable()
    {
        ResetChainState();
        BindEvents();
        PlayerGravityMotor.OnAnyRespawned += HandleAnyRespawned;
    }

    private void OnDisable()
    {
        PlayerGravityMotor.OnAnyRespawned -= HandleAnyRespawned;
        UnbindEvents();
    }

    private void BindEvents()
    {
        if (eventsBound)
        {
            return;
        }

        if (switchA != null)
        {
            switchA.OnTriggered += HandleSwitchATriggered;
        }

        if (switchB != null)
        {
            switchB.OnTriggered += HandleSwitchBTriggered;
        }

        if (gateA != null)
        {
            gateA.OnOpened += HandleGateAOpened;
            gateA.OnClosed += HandleGateAClosed;
        }

        if (gateB != null)
        {
            gateB.OnClosed += HandleGateBClosed;
        }

        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (switchA != null)
        {
            switchA.OnTriggered -= HandleSwitchATriggered;
        }

        if (switchB != null)
        {
            switchB.OnTriggered -= HandleSwitchBTriggered;
        }

        if (gateA != null)
        {
            gateA.OnOpened -= HandleGateAOpened;
            gateA.OnClosed -= HandleGateAClosed;
        }

        if (gateB != null)
        {
            gateB.OnClosed -= HandleGateBClosed;
        }

        eventsBound = false;
    }

    private void ResetChainState()
    {
        gateAWindowActive = false;
        gateBTriggeredInCurrentWindow = false;

        if (switchA != null)
        {
            switchA.ResetState(true);
        }

        if (switchB != null)
        {
            switchB.ResetState(false, switchBLockedHint);
        }

        if (gateA != null)
        {
            gateA.ResetGate(true);
        }

        if (gateB != null)
        {
            gateB.ResetGate(true);
        }
    }

    private void HandleSwitchATriggered()
    {
        // Fallback for the case where gate A was opened before this callback executes.
        if (!gateAWindowActive && gateA != null && gateA.IsOpen)
        {
            HandleGateAOpened(gateA.RemainingWindow);
        }
    }

    private void HandleSwitchBTriggered()
    {
        bool gateBAlreadyOpen = gateB != null && gateB.IsOpen;
        bool canPass = gateBAlreadyOpen || (gateAWindowActive && gateA != null && gateA.IsOpen);
        if (!canPass)
        {
            if (switchB != null)
            {
                switchB.SetInteractable(false, switchBLockedHint);
            }
            return;
        }

        gateBTriggeredInCurrentWindow = true;
        if (gateB != null)
        {
            gateB.OpenWindow();
        }
        RuntimeHUD.Instance?.ShowTransientMessage(chainCompleteHint, 1.0f);
    }

    private void HandleGateAOpened(float _)
    {
        if (gateAWindowActive)
        {
            return;
        }

        gateAWindowActive = true;
        gateBTriggeredInCurrentWindow = false;

        if (switchB != null)
        {
            switchB.SetInteractable(true, switchBLockedHint);
        }

        RuntimeHUD.Instance?.ShowTransientMessage(switchBReadyHint, 1.15f);
    }

    private void HandleGateAClosed()
    {
        if (gateBTriggeredInCurrentWindow)
        {
            gateAWindowActive = false;
            return;
        }

        // Full reset avoids retry edge-cases where A/B switch trigger state can stay stale.
        ResetChainState();
        RuntimeHUD.Instance?.ShowTransientMessage(chainRetryHint, 1.0f);
    }

    private void HandleGateBClosed()
    {
        gateAWindowActive = false;
        gateBTriggeredInCurrentWindow = false;

        if (switchB != null)
        {
            switchB.SetInteractable(false, switchBLockedHint);
        }
    }

    private void HandleAnyRespawned(PlayerGravityMotor _)
    {
        ResetChainState();
        RuntimeHUD.Instance?.ShowTransientMessage("Rhythm chain reset", 0.8f);
    }
}
