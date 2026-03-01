using System;
using System.Collections.Generic;
using UnityEngine;

public class RuntimeObjectPool : MonoBehaviour
{
    private sealed class PoolBucket
    {
        public readonly Queue<GameObject> Items = new Queue<GameObject>();
        public int MaxSize = 64;
    }

    private static RuntimeObjectPool instance;
    private readonly Dictionary<string, PoolBucket> buckets = new Dictionary<string, PoolBucket>();

    public static RuntimeObjectPool Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<RuntimeObjectPool>();
            if (instance != null)
            {
                return instance;
            }

            GameObject root = new GameObject("RuntimeObjectPool");
            instance = root.AddComponent<RuntimeObjectPool>();
            DontDestroyOnLoad(root);
            return instance;
        }
    }

    public GameObject Get(string key, Func<GameObject> factory, int maxPoolSize = 64)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return factory != null ? factory() : null;
        }

        PoolBucket bucket = GetOrCreateBucket(key, maxPoolSize);
        while (bucket.Items.Count > 0)
        {
            GameObject pooled = bucket.Items.Dequeue();
            if (pooled == null)
            {
                continue;
            }

            pooled.SetActive(true);
            return pooled;
        }

        GameObject created = factory != null ? factory() : null;
        if (created != null)
        {
            created.SetActive(true);
        }
        return created;
    }

    public void Release(string key, GameObject instanceToRelease, int maxPoolSize = 64)
    {
        if (instanceToRelease == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            Destroy(instanceToRelease);
            return;
        }

        PoolBucket bucket = GetOrCreateBucket(key, maxPoolSize);
        if (bucket.Items.Count >= bucket.MaxSize)
        {
            Destroy(instanceToRelease);
            return;
        }

        instanceToRelease.transform.SetParent(transform, false);
        instanceToRelease.SetActive(false);
        bucket.Items.Enqueue(instanceToRelease);
    }

    private PoolBucket GetOrCreateBucket(string key, int maxPoolSize)
    {
        if (!buckets.TryGetValue(key, out PoolBucket bucket) || bucket == null)
        {
            bucket = new PoolBucket();
            buckets[key] = bucket;
        }

        bucket.MaxSize = Mathf.Clamp(maxPoolSize, 1, 2048);
        return bucket;
    }
}
