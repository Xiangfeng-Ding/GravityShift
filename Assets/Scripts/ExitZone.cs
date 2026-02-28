using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [SerializeField] private float lockedHintCooldown = 1.2f;

    private bool triggered;
    private float lastLockedHintTime = -100f;
    private int lastHandledFrame = -1;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        lastHandledFrame = Time.frameCount;
        TryHandlePlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Safety net: if enter event is missed due fast motion/collider state transition,
        // stay will still process completion once per frame.
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryHandlePlayer(other);
    }

    private void TryHandlePlayer(Collider other)
    {
        if (triggered)
        {
            return;
        }

        GameDirector director = GameDirector.Instance;
        if (director != null && director.State != GameState.Playing)
        {
            return;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        if (player == null)
        {
            return;
        }

        LevelProgress progress = LevelProgress.Instance;
        if (progress == null)
        {
            return;
        }

        if (!progress.GateUnlocked)
        {
            if (Time.time - lastLockedHintTime > lockedHintCooldown)
            {
                lastLockedHintTime = Time.time;
                int missing = Mathf.Max(0, progress.MinimumRequired - progress.CollectedCrystals);
                RuntimeHUD.Instance?.ShowTransientMessage(
                    "Need " + missing + " more crystals to exit (restart run if rules were changed)",
                    1.8f
                );
            }
            return;
        }

        if (progress.TryCompleteLevel())
        {
            triggered = true;
        }
    }
}
