using UnityEngine;

public class ScenicDistanceCuller : MonoBehaviour
{
    [SerializeField] private float maxDistance = 110f;
    [SerializeField] private float updateInterval = 0.25f;

    private Renderer[] renderers;
    private float nextCheckAt;
    private bool visibleState = true;

    public void Configure(float distance, float interval)
    {
        maxDistance = Mathf.Max(10f, distance);
        updateInterval = Mathf.Clamp(interval, 0.06f, 1f);
        nextCheckAt = 0f;
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        nextCheckAt = 0f;
    }

    private void Update()
    {
        if (Time.time < nextCheckAt)
        {
            return;
        }

        nextCheckAt = Time.time + updateInterval;

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        float distance = Vector3.Distance(camera.transform.position, transform.position);
        bool shouldShow = distance <= maxDistance;
        if (shouldShow == visibleState)
        {
            return;
        }

        visibleState = shouldShow;
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                renderer.enabled = visibleState;
            }
        }
    }
}
