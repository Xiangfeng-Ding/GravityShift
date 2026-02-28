using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnergyGate : MonoBehaviour
{
    [SerializeField] private float unlockFlashDuration = 0.35f;
    [SerializeField] private float flashFrequency = 22f;

    private Renderer[] gateRenderers;
    private Collider[] gateColliders;
    private LevelProgress subscribedProgress;
    private Coroutine bindRoutine;
    private Coroutine openingRoutine;
    private bool opening;
    private bool isOpen;

    private void Awake()
    {
        gateRenderers = GetComponentsInChildren<Renderer>(true);
        gateColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private void Start()
    {
        LevelProgress progress = LevelProgress.Instance;
        if (progress != null && progress.GateUnlocked)
        {
            SetGateState(false);
        }
        else
        {
            SetGateState(true);
        }
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (openingRoutine != null)
        {
            StopCoroutine(openingRoutine);
            openingRoutine = null;
            opening = false;
        }

        if (subscribedProgress != null)
        {
            subscribedProgress.OnGateUnlocked -= HandleGateUnlocked;
            subscribedProgress = null;
        }
    }

    private IEnumerator BindWhenReady()
    {
        while (enabled && LevelProgress.Instance == null)
        {
            yield return null;
        }

        if (!enabled)
        {
            yield break;
        }

        LevelProgress progress = LevelProgress.Instance;
        if (progress == null || subscribedProgress == progress)
        {
            yield break;
        }

        if (subscribedProgress != null)
        {
            subscribedProgress.OnGateUnlocked -= HandleGateUnlocked;
        }

        subscribedProgress = progress;
        subscribedProgress.OnGateUnlocked += HandleGateUnlocked;
        bindRoutine = null;

        if (subscribedProgress.GateUnlocked && !isOpen)
        {
            HandleGateUnlocked();
        }
    }

    private void HandleGateUnlocked()
    {
        if (!opening && !isOpen)
        {
            openingRoutine = StartCoroutine(OpenGateRoutine());
        }
    }

    private IEnumerator OpenGateRoutine()
    {
        if (gateRenderers == null || gateColliders == null)
        {
            yield break;
        }

        opening = true;
        float elapsed = 0f;

        while (elapsed < unlockFlashDuration)
        {
            bool visible = Mathf.FloorToInt(elapsed * flashFrequency) % 2 == 0;
            SetRenderersVisible(visible);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetGateState(false);
        isOpen = true;
        opening = false;
        openingRoutine = null;
        RuntimeHUD.Instance?.ShowTransientMessage("Exit gate unlocked", 1.5f);
        GameFeedback.Instance?.PlayBarrierUnlocked(MechanismFeedbackType.ExitGate);
    }

    private void SetGateState(bool closed)
    {
        isOpen = !closed;
        SetRenderersVisible(closed);
        for (int i = 0; i < gateColliders.Length; i++)
        {
            if (gateColliders[i] != null)
            {
                gateColliders[i].enabled = closed;
            }
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < gateRenderers.Length; i++)
        {
            if (gateRenderers[i] != null)
            {
                gateRenderers[i].enabled = visible;
            }
        }
    }
}
