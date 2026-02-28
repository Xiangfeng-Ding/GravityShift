using UnityEngine;

public class SequentialMechanismChain : MonoBehaviour
{
    [SerializeField] private GravitySwitchButton firstSwitch;
    [SerializeField] private GravitySwitchButton secondSwitch;
    [SerializeField] private string lockedHint = "Activate switch A first";
    [SerializeField] private string secondUnlockedHint = "Switch B unlocked";
    [SerializeField] private string completedHint = "Chain completed";

    private bool firstActivated;
    private bool completed;
    private bool eventsBound;

    public GravitySwitchButton FirstSwitch => firstSwitch;
    public GravitySwitchButton SecondSwitch => secondSwitch;

    public void Configure(
        GravitySwitchButton newFirstSwitch,
        GravitySwitchButton newSecondSwitch,
        string lockedMessage)
    {
        UnbindEvents();

        firstSwitch = newFirstSwitch;
        secondSwitch = newSecondSwitch;
        if (!string.IsNullOrEmpty(lockedMessage))
        {
            lockedHint = lockedMessage;
        }

        firstActivated = firstSwitch != null && firstSwitch.IsActivated;
        completed = secondSwitch != null && secondSwitch.IsActivated;
        ApplySwitchState();
        BindEvents();
    }

    private void OnEnable()
    {
        ApplySwitchState();
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void BindEvents()
    {
        if (eventsBound)
        {
            return;
        }

        if (firstSwitch != null)
        {
            firstSwitch.OnActivated += HandleFirstActivated;
        }

        if (secondSwitch != null)
        {
            secondSwitch.OnActivated += HandleSecondActivated;
        }

        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (firstSwitch != null)
        {
            firstSwitch.OnActivated -= HandleFirstActivated;
        }

        if (secondSwitch != null)
        {
            secondSwitch.OnActivated -= HandleSecondActivated;
        }

        eventsBound = false;
    }

    private void HandleFirstActivated()
    {
        if (firstActivated)
        {
            return;
        }

        firstActivated = true;
        ApplySwitchState();
        RuntimeHUD.Instance?.ShowTransientMessage(secondUnlockedHint, 1.4f);
        GameFeedback.Instance?.PlayChainStepUnlocked();
    }

    private void HandleSecondActivated()
    {
        if (!firstActivated)
        {
            if (secondSwitch != null)
            {
                secondSwitch.ResetState();
                secondSwitch.SetInteractable(false, lockedHint);
            }
            return;
        }

        if (completed)
        {
            return;
        }

        completed = true;
        RuntimeHUD.Instance?.ShowTransientMessage(completedHint, 1.5f);
        GameFeedback.Instance?.PlayChainCompleted();
    }

    private void ApplySwitchState()
    {
        if (secondSwitch == null)
        {
            return;
        }

        bool secondShouldUnlock = firstActivated || completed;
        secondSwitch.SetInteractable(secondShouldUnlock, lockedHint);
    }
}
