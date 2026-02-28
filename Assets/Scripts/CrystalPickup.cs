using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CrystalPickup : MonoBehaviour
{
    private const string DefaultCrystalPoolKey = "Crystal";
    private const string BurstPoolKey = "CrystalBurstFx";
    private const int CrystalPoolSize = 360;
    private const int BurstPoolSize = 96;
    private static Material burstSharedMaterial;

    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseIntensity = 0.65f;

    private Vector3 baseLocalPosition;
    private Renderer crystalRenderer;
    private MaterialPropertyBlock crystalBlock;
    private Color baseColor = Color.white;
    private Color baseEmissionColor;
    private bool hasEmissionColor;
    private Collider triggerCollider;
    private bool collected;
    private RuntimeObjectPool runtimePool;
    private string poolKey = DefaultCrystalPoolKey;
    private int lastHandledFrame = -1;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        collected = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    public void ConfigurePool(RuntimeObjectPool pool, string key = DefaultCrystalPoolKey)
    {
        runtimePool = pool;
        poolKey = string.IsNullOrWhiteSpace(key) ? DefaultCrystalPoolKey : key.Trim();
    }

    public void PrepareForSpawn()
    {
        EnsureInitialized();
        collected = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }

        baseLocalPosition = transform.localPosition;
        RefreshBaseColors();
    }

    public void RecycleImmediate()
    {
        collected = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        ReleaseToPool();
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = baseLocalPosition + Vector3.up * bobOffset;

        if (crystalRenderer != null && crystalBlock != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            crystalRenderer.GetPropertyBlock(crystalBlock);
            crystalBlock.SetColor("_Color", baseColor);
            if (hasEmissionColor)
            {
                crystalBlock.SetColor("_EmissionColor", baseEmissionColor * pulse);
            }
            crystalRenderer.SetPropertyBlock(crystalBlock);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        lastHandledFrame = Time.frameCount;
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (lastHandledFrame == Time.frameCount)
        {
            return;
        }

        lastHandledFrame = Time.frameCount;
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (collected)
        {
            return;
        }

        GameDirector director = GameDirector.Instance;
        if (director != null && director.State != GameState.Playing)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerGravityMotor>() == null)
        {
            return;
        }

        collected = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        LevelProgress progress = LevelProgress.Instance;
        if (progress != null)
        {
            progress.AddCrystal();
            RuntimeHUD.Instance?.ShowTransientMessage(
                "Crystal collected  " + progress.CollectedCrystals + "/" + progress.TotalCrystals,
                1.4f
            );
        }

        SpawnCollectBurst();
        ReleaseToPool();
    }

    private void EnsureInitialized()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        if (crystalRenderer == null)
        {
            crystalRenderer = GetComponent<Renderer>();
            if (crystalRenderer == null)
            {
                crystalRenderer = GetComponentInChildren<Renderer>();
            }
        }

        if (crystalBlock == null)
        {
            crystalBlock = new MaterialPropertyBlock();
        }

        if (runtimePool == null)
        {
            runtimePool = RuntimeObjectPool.Instance;
        }

        RefreshBaseColors();
        baseLocalPosition = transform.localPosition;
    }

    private void RefreshBaseColors()
    {
        if (crystalRenderer == null)
        {
            return;
        }

        Material shared = crystalRenderer.sharedMaterial;
        if (shared != null && shared.HasProperty("_Color"))
        {
            baseColor = shared.color;
        }

        if (crystalBlock == null)
        {
            crystalBlock = new MaterialPropertyBlock();
        }

        crystalRenderer.GetPropertyBlock(crystalBlock);
        Color blockColor = crystalBlock.GetColor("_Color");
        if (blockColor.maxColorComponent > 0.0001f)
        {
            baseColor = blockColor;
        }

        hasEmissionColor = shared != null && shared.HasProperty("_EmissionColor");
        if (hasEmissionColor)
        {
            baseEmissionColor = shared.GetColor("_EmissionColor");
        }

        Color blockEmission = crystalBlock.GetColor("_EmissionColor");
        if (blockEmission.maxColorComponent > 0.0001f)
        {
            baseEmissionColor = blockEmission;
            hasEmissionColor = true;
        }

        if (!hasEmissionColor || baseEmissionColor.maxColorComponent <= 0.001f)
        {
            baseEmissionColor = baseColor * 1.2f;
            hasEmissionColor = true;
        }
    }

    private void SpawnCollectBurst()
    {
        RuntimeObjectPool pool = runtimePool != null ? runtimePool : RuntimeObjectPool.Instance;
        GameObject burst = pool != null
            ? pool.Get(BurstPoolKey, CreateBurstPrefab, BurstPoolSize)
            : CreateBurstPrefab();
        if (burst == null)
        {
            return;
        }

        burst.transform.position = transform.position;
        ParticleSystem particles = burst.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            ParticleSystem.MainModule main = particles.main;
            Color burstColor = baseColor.maxColorComponent > 0.001f ? baseColor : new Color(0.35f, 0.9f, 1f);
            main.startColor = burstColor;
            particles.Clear(true);
            particles.Play(true);
        }

        PoolAutoRelease autoRelease = burst.GetComponent<PoolAutoRelease>();
        if (autoRelease != null && pool != null)
        {
            autoRelease.Arm(BurstPoolKey, 1.2f, BurstPoolSize);
        }
        else
        {
            Destroy(burst, 1.2f);
        }
    }

    private static GameObject CreateBurstPrefab()
    {
        GameObject burst = new GameObject("CrystalBurst");

        ParticleSystem particles = burst.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.5f;
        main.startLifetime = 0.35f;
        main.startSpeed = 4f;
        main.startSize = 0.18f;
        main.maxParticles = 48;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.35f, 0.9f, 1f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (particleShader == null)
        {
            particleShader = Shader.Find("Particles/Standard Unlit");
        }
        if (particleShader != null)
        {
            if (burstSharedMaterial == null || burstSharedMaterial.shader != particleShader)
            {
                burstSharedMaterial = new Material(particleShader)
                {
                    name = "CrystalBurstShared"
                };
            }
            renderer.sharedMaterial = burstSharedMaterial;
        }

        burst.AddComponent<PoolAutoRelease>();
        return burst;
    }

    private void ReleaseToPool()
    {
        RuntimeObjectPool pool = runtimePool != null ? runtimePool : RuntimeObjectPool.Instance;
        if (pool == null)
        {
            Destroy(gameObject);
            return;
        }

        pool.Release(poolKey, gameObject, CrystalPoolSize);
    }
}
