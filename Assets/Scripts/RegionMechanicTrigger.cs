using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RegionMechanicTrigger : MonoBehaviour
{
    [SerializeField] private int regionIndex = 1;
    [SerializeField] private string regionTitle = "Region";
    [SerializeField] private string introMessage = "New mechanic.";
    [SerializeField] private float messageDuration = 2.6f;

    private bool shown;
    private int lastHandledFrame = -1;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    public void Configure(int index, string title, string message, float duration = 2.6f)
    {
        regionIndex = Mathf.Max(1, index);
        if (!string.IsNullOrWhiteSpace(title))
        {
            regionTitle = title.Trim();
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            introMessage = message.Trim();
        }
        messageDuration = Mathf.Clamp(duration, 1f, 8f);
    }

    private void OnTriggerEnter(Collider other)
    {
        lastHandledFrame = Time.frameCount;
        TryShow(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryShow(other);
    }

    private void TryShow(Collider other)
    {
        if (shown)
        {
            return;
        }

        GameDirector director = GameDirector.Instance;
        if (director == null || director.State != GameState.Playing)
        {
            return;
        }

        if (other == null || other.GetComponentInParent<PlayerGravityMotor>() == null)
        {
            return;
        }

        shown = true;

        string header = "Zone " + regionIndex + " - " + regionTitle;
        string body = string.IsNullOrWhiteSpace(introMessage) ? string.Empty : ("  " + introMessage);
        RuntimeHUD.Instance?.ShowAlertBanner(header, new Color(0.42f, 0.88f, 1f), messageDuration);
        RuntimeHUD.Instance?.ShowTransientMessage(header + body, messageDuration + 0.35f);
    }
}
