using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlateTrigger : MonoBehaviour
{
    private sealed class ActivatorRecord
    {
        public UnityEngine.Object Owner;
        public int RefCount;
        public float LastSeenTime;
    }

    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressedOffset = 0.08f;
    [SerializeField] private float visualSmooth = 12f;
    [SerializeField] private bool allowPlayer = false;
    [SerializeField] private bool allowCrate = true;
    [SerializeField] private float staleActivatorPruneInterval = 0.25f;
    [SerializeField] private float staleNoContactTimeout = 1.00f;

    public event Action<bool> OnPressedStateChanged;

    public bool IsPressed => isPressed;

    private readonly Dictionary<int, ActivatorRecord> occupantRefCounts = new Dictionary<int, ActivatorRecord>();
    private readonly List<int> staleActivatorIds = new List<int>();
    private bool isPressed;
    private Vector3 plateVisualBaseLocalPosition;
    private float nextPruneAt;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        if (plateVisual == null && transform.childCount > 0)
        {
            plateVisual = transform.GetChild(0);
        }

        if (plateVisual != null)
        {
            plateVisualBaseLocalPosition = plateVisual.localPosition;
        }
    }

    private void Update()
    {
        if (plateVisual == null)
        {
            PruneStaleActivators();
            return;
        }

        Vector3 target = plateVisualBaseLocalPosition + (isPressed ? Vector3.down * Mathf.Abs(pressedOffset) : Vector3.zero);
        float blend = 1f - Mathf.Exp(-Mathf.Max(1f, visualSmooth) * Time.deltaTime);
        plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, blend);
        PruneStaleActivators();
    }

    private void OnDisable()
    {
        if (occupantRefCounts.Count > 0)
        {
            occupantRefCounts.Clear();
            RefreshPressedState();
        }
    }

    public void Configure(bool canPlayerActivate, bool canCrateActivate)
    {
        allowPlayer = canPlayerActivate;
        allowCrate = canCrateActivate;
        RefreshPressedState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveActivator(other, out int id, out UnityEngine.Object owner))
        {
            return;
        }

        if (occupantRefCounts.TryGetValue(id, out ActivatorRecord record))
        {
            record.RefCount += 1;
            if (record.Owner == null)
            {
                record.Owner = owner;
            }
            record.LastSeenTime = Time.time;
        }
        else
        {
            occupantRefCounts[id] = new ActivatorRecord
            {
                Owner = owner,
                RefCount = 1,
                LastSeenTime = Time.time
            };
        }

        RefreshPressedState();
    }

    private void OnTriggerExit(Collider other)
    {
        int id = GetActivatorId(other);
        if (id == 0)
        {
            return;
        }

        if (occupantRefCounts.TryGetValue(id, out ActivatorRecord record))
        {
            record.RefCount -= 1;
            if (record.RefCount <= 0)
            {
                occupantRefCounts.Remove(id);
            }
        }

        RefreshPressedState();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryResolveActivator(other, out int id, out UnityEngine.Object owner))
        {
            return;
        }

        if (occupantRefCounts.TryGetValue(id, out ActivatorRecord record))
        {
            if (record.RefCount <= 0)
            {
                record.RefCount = 1;
            }
            if (record.Owner == null)
            {
                record.Owner = owner;
            }
            record.LastSeenTime = Time.time;
        }
        else
        {
            occupantRefCounts[id] = new ActivatorRecord
            {
                Owner = owner,
                RefCount = 1,
                LastSeenTime = Time.time
            };
        }

        RefreshPressedState();
    }

    private bool TryResolveActivator(Collider other, out int id, out UnityEngine.Object owner)
    {
        id = 0;
        owner = null;
        if (other == null)
        {
            return false;
        }

        if (allowCrate && other.GetComponentInParent<PushCrateMarker>() != null)
        {
            id = GetActivatorId(other);
            owner = ResolveActivatorOwner(other);
            return id != 0;
        }

        if (allowPlayer && other.GetComponentInParent<PlayerGravityMotor>() != null)
        {
            id = GetActivatorId(other);
            owner = ResolveActivatorOwner(other);
            return id != 0;
        }

        return false;
    }

    private static int GetActivatorId(Collider other)
    {
        Rigidbody body = other != null ? other.attachedRigidbody : null;
        if (body != null)
        {
            return body.GetInstanceID();
        }

        Transform root = other != null ? other.transform.root : null;
        return root != null ? root.GetInstanceID() : (other != null ? other.GetInstanceID() : 0);
    }

    private static UnityEngine.Object ResolveActivatorOwner(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Rigidbody body = other.attachedRigidbody;
        if (body != null)
        {
            return body;
        }

        Transform root = other.transform.root;
        if (root != null)
        {
            return root;
        }

        return other;
    }

    private void PruneStaleActivators()
    {
        if (occupantRefCounts.Count == 0)
        {
            return;
        }

        if (Time.time < nextPruneAt)
        {
            return;
        }

        nextPruneAt = Time.time + Mathf.Max(0.05f, staleActivatorPruneInterval);
        staleActivatorIds.Clear();

        foreach (KeyValuePair<int, ActivatorRecord> kvp in occupantRefCounts)
        {
            ActivatorRecord record = kvp.Value;
            bool staleByContactTimeout = record != null &&
                                         record.LastSeenTime > 0f &&
                                         (Time.time - record.LastSeenTime) > Mathf.Max(0.2f, staleNoContactTimeout);
            if (record == null || record.RefCount <= 0 || record.Owner == null || staleByContactTimeout)
            {
                staleActivatorIds.Add(kvp.Key);
            }
        }

        if (staleActivatorIds.Count == 0)
        {
            return;
        }

        for (int i = 0; i < staleActivatorIds.Count; i++)
        {
            occupantRefCounts.Remove(staleActivatorIds[i]);
        }

        staleActivatorIds.Clear();
        RefreshPressedState();
    }

    private void RefreshPressedState()
    {
        bool nextPressed = occupantRefCounts.Count > 0;
        if (nextPressed == isPressed)
        {
            return;
        }

        isPressed = nextPressed;
        OnPressedStateChanged?.Invoke(isPressed);
    }
}
