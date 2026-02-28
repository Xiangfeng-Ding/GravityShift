using System.Collections;
using UnityEngine;

public class TimedCollapsePlatform : MonoBehaviour
{
    [SerializeField] private float collapseDelay = 0.55f;
    [SerializeField] private float respawnDelay = 2.3f;
    [SerializeField] private float respawnSafeTime = 0.18f;
    [SerializeField] private float blinkFrequency = 13f;
    [SerializeField] private bool triggerOnlyFromTop = true;

    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool active = true;
    private bool armed;
    private Coroutine routine;
    private float respawnSafeUntil;

    public void Configure(float delay, float recover)
    {
        collapseDelay = Mathf.Clamp(delay, 0.1f, 3f);
        respawnDelay = Mathf.Clamp(recover, 0.4f, 8f);
        respawnSafeTime = Mathf.Clamp(respawnSafeTime, 0f, 1.5f);
    }

    private void Awake()
    {
        CacheParts();
    }

    private void OnEnable()
    {
        respawnSafeUntil = Time.time + Mathf.Max(0f, respawnSafeTime);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        armed = false;
        respawnSafeUntil = 0f;
        SetActiveState(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryArmCollapse(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        // Safety net: if enter callback is missed by collision state transition,
        // stay will still arm collapse while player remains on top.
        TryArmCollapse(collision);
    }

    private void TryArmCollapse(Collision collision)
    {
        if (!active || armed)
        {
            return;
        }

        if (Time.time < respawnSafeUntil)
        {
            return;
        }

        if (collision == null || collision.collider == null)
        {
            return;
        }

        if (collision.collider.GetComponentInParent<PlayerGravityMotor>() == null)
        {
            return;
        }

        if (triggerOnlyFromTop && !HasTopContact(collision))
        {
            return;
        }

        armed = true;
        if (routine != null)
        {
            StopCoroutine(routine);
        }
        routine = StartCoroutine(CollapseRoutine());
    }

    private IEnumerator CollapseRoutine()
    {
        float timer = 0f;
        while (timer < collapseDelay)
        {
            bool visible = Mathf.FloorToInt(timer * blinkFrequency) % 2 == 0;
            SetRenderers(visible);
            timer += Time.deltaTime;
            yield return null;
        }

        SetActiveState(false);
        RuntimeHUD.Instance?.ShowTransientMessage("Platform collapsed", 0.65f);

        yield return new WaitForSeconds(respawnDelay);
        SetActiveState(true);
        armed = false;
        routine = null;
    }

    private bool HasTopContact(Collision collision)
    {
        Vector3 upDir = GravitySystem.Instance != null ? GravitySystem.Instance.UpDirection : Vector3.up;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (Vector3.Dot(contact.normal, upDir) > 0.28f)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheParts()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetActiveState(bool isActive)
    {
        active = isActive;
        SetRenderers(isActive);
        respawnSafeUntil = isActive
            ? Time.time + Mathf.Max(0f, respawnSafeTime)
            : 0f;
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider c = cachedColliders[i];
                if (c != null)
                {
                    c.enabled = isActive;
                }
            }
        }
    }

    private void SetRenderers(bool visible)
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }
}
