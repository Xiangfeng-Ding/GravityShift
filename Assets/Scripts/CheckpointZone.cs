using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CheckpointZone : MonoBehaviour
{
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.76f, 0.24f);
    [SerializeField] private Color activeColor = new Color(0.25f, 1f, 0.65f);

    private bool activated;
    private Renderer markerRenderer;
    private MaterialPropertyBlock markerBlock;
    private int lastHandledFrame = -1;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        markerRenderer = GetComponentInChildren<Renderer>();
        if (markerRenderer != null)
        {
            markerBlock = new MaterialPropertyBlock();
            ApplyMarkerColor(inactiveColor, 1.2f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        lastHandledFrame = Time.frameCount;
        TryActivate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryActivate(other);
    }

    private void TryActivate(Collider other)
    {
        if (activated)
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

        activated = true;
        player.SetSpawnPoint(transform.position + spawnOffset);
        ApplyMarkerColor(activeColor, 1.8f);
        RuntimeHUD.Instance?.ShowTransientMessage("Checkpoint updated", 2f);
    }

    private void ApplyMarkerColor(Color color, float emissionMultiplier)
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
