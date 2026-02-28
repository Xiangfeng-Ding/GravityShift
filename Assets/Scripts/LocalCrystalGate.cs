using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LocalCrystalGate : MonoBehaviour
{
    [SerializeField] private string gateLabel = "Crystal Gate";
    [SerializeField] private int requiredCrystals = 3;
    [SerializeField] private TextMesh counterText;
    [SerializeField] private MechanismFeedbackType feedbackType = MechanismFeedbackType.GenericBarrier;
    [SerializeField] private float unlockFlashDuration = 0.28f;
    [SerializeField] private float flashFrequency = 24f;

    private Renderer[] gateRenderers;
    private Collider[] gateColliders;
    private int collectedCrystals;
    private bool unlocked;
    private float flashTimer;

    public string GateLabel => gateLabel;
    public int RequiredCrystals => requiredCrystals;
    public int CollectedCrystals => collectedCrystals;
    public bool IsUnlocked => unlocked;

    private void Awake()
    {
        gateRenderers = GetComponentsInChildren<Renderer>(true);
        gateColliders = GetComponentsInChildren<Collider>(true);
        SetGateClosed(true);
        RefreshCounterText();
    }

    private void Update()
    {
        if (!unlocked || flashTimer <= 0f)
        {
            return;
        }

        flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
        bool visible = flashTimer > 0f && (Mathf.FloorToInt((unlockFlashDuration - flashTimer) * flashFrequency) % 2 == 0);
        SetRenderersVisible(visible);

        if (flashTimer <= 0f)
        {
            SetRenderersVisible(false);
        }
    }

    public void Configure(string label, int required, TextMesh textMesh = null, MechanismFeedbackType type = MechanismFeedbackType.GenericBarrier)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            gateLabel = label.Trim();
        }

        requiredCrystals = Mathf.Clamp(required, 1, 12);
        counterText = textMesh != null ? textMesh : counterText;
        feedbackType = type;
        collectedCrystals = 0;
        unlocked = false;
        flashTimer = 0f;
        SetGateClosed(true);
        RefreshCounterText();
    }

    public bool RegisterCrystalCollected()
    {
        if (unlocked)
        {
            return false;
        }

        collectedCrystals = Mathf.Clamp(collectedCrystals + 1, 0, requiredCrystals);
        RefreshCounterText();

        if (collectedCrystals >= requiredCrystals)
        {
            UnlockGate();
        }

        return true;
    }

    public void ClampRequirementToAvailable(int availableCrystals)
    {
        int clampedAvailable = Mathf.Max(1, availableCrystals);
        if (requiredCrystals <= clampedAvailable)
        {
            return;
        }

        requiredCrystals = clampedAvailable;
        collectedCrystals = Mathf.Clamp(collectedCrystals, 0, requiredCrystals);
        RefreshCounterText();

        if (!unlocked && collectedCrystals >= requiredCrystals)
        {
            UnlockGate();
        }
    }

    private void UnlockGate()
    {
        if (unlocked)
        {
            return;
        }

        unlocked = true;
        flashTimer = Mathf.Max(0.05f, unlockFlashDuration);
        SetGateClosed(false);
        RuntimeHUD.Instance?.ShowTransientMessage(gateLabel + " opened", 1.2f);
        GameFeedback.Instance?.PlayBarrierUnlocked(feedbackType);
        RefreshCounterText();
    }

    private void RefreshCounterText()
    {
        if (counterText == null)
        {
            return;
        }

        if (unlocked)
        {
            counterText.text = gateLabel + "\nOPEN";
            counterText.color = new Color(0.45f, 1f, 0.72f);
            return;
        }

        counterText.text = gateLabel + "\n" + collectedCrystals + "/" + requiredCrystals;
        counterText.color = new Color(0.95f, 0.95f, 1f);
    }

    private void SetGateClosed(bool closed)
    {
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

        SetRenderersVisible(closed);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (gateRenderers == null)
        {
            return;
        }

        for (int i = 0; i < gateRenderers.Length; i++)
        {
            Renderer renderer = gateRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
