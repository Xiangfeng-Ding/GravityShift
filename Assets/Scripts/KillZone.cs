using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    [SerializeField] private float respawnCooldown = 0.35f;

    private static float globalLastRespawnTime = -100f;
    private float lastRespawnTime = -100f;
    private int lastHandledFrame = -1;

    public static void ResetGlobalCooldown()
    {
        globalLastRespawnTime = -100f;
    }

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
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryHandlePlayer(other);
    }

    private void TryHandlePlayer(Collider other)
    {
        GameDirector director = GameDirector.Instance;
        if (director == null || director.State != GameState.Playing)
        {
            return;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        if (player == null)
        {
            return;
        }

        float now = Time.time;
        float cooldown = Mathf.Max(0.02f, respawnCooldown);
        if (now - lastRespawnTime < cooldown || now - globalLastRespawnTime < cooldown)
        {
            return;
        }

        lastRespawnTime = now;
        globalLastRespawnTime = now;
        LevelProgress.Instance?.RegisterDeath();
        player.Respawn();
        RuntimeHUD.Instance?.ShowTransientMessage("Fell into abyss - respawn", 1.8f);
    }
}
