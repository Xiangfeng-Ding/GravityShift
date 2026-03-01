using System.Collections;
using UnityEngine;

public class LaserBarrier : MonoBehaviour
{
    [SerializeField] private float unlockFlashDuration = 0.4f;
    [SerializeField] private float flashFrequency = 26f;
    [SerializeField] private MechanismFeedbackType feedbackType = MechanismFeedbackType.GenericBarrier;

    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private GravitySwitchButton linkedButton;
    private bool unlocked;
    private bool buttonBound;
    private Coroutine unlockingRoutine;

    public GravitySwitchButton LinkedButton => linkedButton;

    private void Awake()
    {
        CacheBarrierParts();
        SetBarrierActive(true);
    }

    private void OnEnable()
    {
        BindLinkedButton();
    }

    private void OnDisable()
    {
        if (unlockingRoutine != null)
        {
            StopCoroutine(unlockingRoutine);
            unlockingRoutine = null;
        }

        UnbindLinkedButton();
    }

    public void LinkButton(GravitySwitchButton button)
    {
        if (linkedButton == button && buttonBound)
        {
            if (linkedButton != null && linkedButton.IsActivated && !unlocked)
            {
                HandleButtonActivated();
            }
            return;
        }

        UnbindLinkedButton();
        linkedButton = button;
        BindLinkedButton();
    }

    public void ResetBarrier()
    {
        unlocked = false;
        if (unlockingRoutine != null)
        {
            StopCoroutine(unlockingRoutine);
            unlockingRoutine = null;
        }
        SetBarrierActive(true);
    }

    public void ConfigureFeedbackType(MechanismFeedbackType type)
    {
        feedbackType = type;
    }

    private void HandleButtonActivated()
    {
        if (unlocked)
        {
            return;
        }

        if (unlockingRoutine != null)
        {
            StopCoroutine(unlockingRoutine);
        }
        unlockingRoutine = StartCoroutine(UnlockRoutine());
    }

    private IEnumerator UnlockRoutine()
    {
        float elapsed = 0f;
        while (elapsed < unlockFlashDuration)
        {
            bool visible = Mathf.FloorToInt(elapsed * flashFrequency) % 2 == 0;
            SetRenderers(visible);
            elapsed += Time.deltaTime;
            yield return null;
        }

        unlocked = true;
        SetBarrierActive(false);
        unlockingRoutine = null;
        RuntimeHUD.Instance?.ShowTransientMessage("Laser barrier disabled", 1.5f);
        GameFeedback.Instance?.PlayBarrierUnlocked(feedbackType);
    }

    private void CacheBarrierParts()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void BindLinkedButton()
    {
        if (buttonBound || linkedButton == null)
        {
            return;
        }

        linkedButton.OnActivated += HandleButtonActivated;
        buttonBound = true;

        if (linkedButton.IsActivated && !unlocked)
        {
            HandleButtonActivated();
        }
    }

    private void UnbindLinkedButton()
    {
        if (!buttonBound || linkedButton == null)
        {
            buttonBound = false;
            return;
        }

        linkedButton.OnActivated -= HandleButtonActivated;
        buttonBound = false;
    }

    private void SetBarrierActive(bool active)
    {
        SetRenderers(active);

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider collider = cachedColliders[i];
            if (collider != null && collider.gameObject != gameObject)
            {
                collider.enabled = active;
            }
        }
    }

    private void SetRenderers(bool visible)
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
    }
}
