using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlateGate : MonoBehaviour
{
    [SerializeField] private PressurePlateTrigger linkedPlate;
    [SerializeField] private bool stayOpenOnceActivated = true;
    [SerializeField] private MechanismFeedbackType feedbackType = MechanismFeedbackType.GenericBarrier;

    private Renderer[] gateRenderers;
    private Collider[] gateColliders;
    private bool isOpen;
    private bool bound;
    private int enabledFrame = -1;
    private bool pendingMissingPlateValidation;
    private bool missingPlateWarningLogged;

    public PressurePlateTrigger LinkedPlate => linkedPlate;

    private void Awake()
    {
        gateRenderers = GetComponentsInChildren<Renderer>(true);
        gateColliders = GetComponentsInChildren<Collider>(true);
        SetGateClosed(true);
    }

    private void OnEnable()
    {
        enabledFrame = Time.frameCount;
        pendingMissingPlateValidation = false;
        BindPlate();
    }

    private void OnDisable()
    {
        pendingMissingPlateValidation = false;
        UnbindPlate();
    }

    private void Update()
    {
        if (!pendingMissingPlateValidation || bound)
        {
            return;
        }

        if (ShouldDeferMissingPlateValidation())
        {
            return;
        }

        pendingMissingPlateValidation = false;
        if (linkedPlate != null)
        {
            BindPlate();
            return;
        }

        OpenMissingPlateFailSafe();
    }

    public void Configure(PressurePlateTrigger plate, bool persistentOpen, MechanismFeedbackType type = MechanismFeedbackType.GenericBarrier)
    {
        UnbindPlate();
        linkedPlate = plate;
        stayOpenOnceActivated = persistentOpen;
        feedbackType = type;
        isOpen = false;
        pendingMissingPlateValidation = false;
        missingPlateWarningLogged = false;
        SetGateClosed(true);
        BindPlate();
    }

    private void BindPlate()
    {
        if (bound)
        {
            return;
        }

        if (linkedPlate == null)
        {
            if (ShouldDeferMissingPlateValidation())
            {
                pendingMissingPlateValidation = true;
                return;
            }

            OpenMissingPlateFailSafe();
            return;
        }

        pendingMissingPlateValidation = false;
        linkedPlate.OnPressedStateChanged += HandlePlateStateChanged;
        bound = true;

        HandlePlateStateChanged(linkedPlate.IsPressed);
    }

    private bool ShouldDeferMissingPlateValidation()
    {
        return isActiveAndEnabled && enabledFrame >= 0 && Time.frameCount <= (enabledFrame + 1);
    }

    private void OpenMissingPlateFailSafe()
    {
        if (isOpen)
        {
            return;
        }

        // Fail-safe: avoid permanent progression lock if plate reference is missing.
        isOpen = true;
        SetGateClosed(false);
        if (!missingPlateWarningLogged)
        {
            Debug.LogWarning("[PressurePlateGate] Missing linked plate on '" + name + "'. Opening gate fail-safe.");
            missingPlateWarningLogged = true;
        }
    }

    private void UnbindPlate()
    {
        if (!bound || linkedPlate == null)
        {
            bound = false;
            return;
        }

        linkedPlate.OnPressedStateChanged -= HandlePlateStateChanged;
        bound = false;
    }

    private void HandlePlateStateChanged(bool pressed)
    {
        if (pressed)
        {
            if (!isOpen)
            {
                isOpen = true;
                SetGateClosed(false);
                RuntimeHUD.Instance?.ShowTransientMessage("Pressure gate opened", 1.2f);
                GameFeedback.Instance?.PlayBarrierUnlocked(feedbackType);
            }

            return;
        }

        if (!stayOpenOnceActivated && isOpen)
        {
            isOpen = false;
            SetGateClosed(true);
            RuntimeHUD.Instance?.ShowTransientMessage("Pressure gate closed", 0.9f);
        }
    }

    private void SetGateClosed(bool closed)
    {
        if (gateRenderers != null)
        {
            for (int i = 0; i < gateRenderers.Length; i++)
            {
                Renderer renderer = gateRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = closed;
                }
            }
        }

        if (gateColliders != null)
        {
            for (int i = 0; i < gateColliders.Length; i++)
            {
                Collider collider = gateColliders[i];
                if (collider != null)
                {
                    collider.enabled = closed;
                }
            }
        }
    }
}
