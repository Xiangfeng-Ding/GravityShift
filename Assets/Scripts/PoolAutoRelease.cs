using UnityEngine;

public class PoolAutoRelease : MonoBehaviour
{
    private string poolKey;
    private int maxPoolSize = 64;
    private float releaseAt = -1f;

    public void Arm(string key, float lifetime, int poolSize = 64)
    {
        poolKey = key;
        maxPoolSize = Mathf.Clamp(poolSize, 1, 2048);
        releaseAt = Time.time + Mathf.Max(0.02f, lifetime);
        enabled = true;
    }

    private void OnDisable()
    {
        releaseAt = -1f;
    }

    private void Update()
    {
        if (releaseAt < 0f || Time.time < releaseAt)
        {
            return;
        }

        releaseAt = -1f;
        RuntimeObjectPool pool = RuntimeObjectPool.Instance;
        if (pool != null)
        {
            pool.Release(poolKey, gameObject, maxPoolSize);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
