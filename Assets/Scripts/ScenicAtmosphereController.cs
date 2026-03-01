using UnityEngine;
using System.Reflection;

public class ScenicAtmosphereController : MonoBehaviour
{
    private const float MinCycleDuration = 30f;

    private Light sun;
    private Material terrainGrassMaterial;
    private Material terrainSoilMaterial;
    private Material terrainRockMaterial;
    private Material mountainNearCoolMaterial;
    private Material mountainNearWarmMaterial;
    private Material mountainFarCoolMaterial;
    private Material mountainFarWarmMaterial;
    private Material cloudMaterial;
    private Material skyboxMaterial;
    private MonoBehaviour renderEnhancer;
    private MethodInfo setAtmosphereMethod;
    private readonly object[] atmosphereInvokeArgs = new object[2];

    private Transform[] clouds;
    private Vector3[] cloudBasePositions;
    private Vector3[] cloudDirections;
    private Vector3[] cloudPerpDirections;
    private float[] cloudOrbitSpeeds;
    private float[] cloudOrbitRanges;
    private float[] cloudBobAmplitudes;
    private float[] cloudBobSpeeds;
    private float[] cloudPhases;

    private float cycleDuration = 84f;
    private float cycleOffset;
    private float nextGiUpdateAt;
    private bool configured;

    public void Configure(
        Light sunLight,
        Material grass,
        Material soil,
        Material rock,
        Material nearCool,
        Material nearWarm,
        Material farCool,
        Material farWarm,
        Material cloud,
        Material skybox,
        MonoBehaviour enhancer,
        Transform[] cloudTransforms)
    {
        sun = sunLight;
        terrainGrassMaterial = grass;
        terrainSoilMaterial = soil;
        terrainRockMaterial = rock;
        mountainNearCoolMaterial = nearCool;
        mountainNearWarmMaterial = nearWarm;
        mountainFarCoolMaterial = farCool;
        mountainFarWarmMaterial = farWarm;
        cloudMaterial = cloud;
        skyboxMaterial = skybox;
        renderEnhancer = enhancer;
        setAtmosphereMethod = renderEnhancer != null
            ? renderEnhancer.GetType().GetMethod("SetAtmosphere", new[] { typeof(float), typeof(float) })
            : null;

        int count = cloudTransforms != null ? cloudTransforms.Length : 0;
        clouds = new Transform[count];
        cloudBasePositions = new Vector3[count];
        cloudDirections = new Vector3[count];
        cloudPerpDirections = new Vector3[count];
        cloudOrbitSpeeds = new float[count];
        cloudOrbitRanges = new float[count];
        cloudBobAmplitudes = new float[count];
        cloudBobSpeeds = new float[count];
        cloudPhases = new float[count];

        for (int i = 0; i < count; i++)
        {
            Transform cloudTransform = cloudTransforms[i];
            clouds[i] = cloudTransform;
            if (cloudTransform == null)
            {
                continue;
            }

            cloudBasePositions[i] = cloudTransform.position;
            float seed = i * 1.37f + 0.41f;
            float angle = Mathf.Lerp(0f, Mathf.PI * 2f, Wave01(seed));
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            cloudDirections[i] = dir;
            cloudPerpDirections[i] = Vector3.Cross(Vector3.up, dir);
            cloudOrbitSpeeds[i] = Mathf.Lerp(0.15f, 0.42f, Wave01(seed + 1.2f));
            cloudOrbitRanges[i] = Mathf.Lerp(10f, 34f, Wave01(seed + 2.1f));
            cloudBobAmplitudes[i] = Mathf.Lerp(0.35f, 1.80f, Wave01(seed + 3.4f));
            cloudBobSpeeds[i] = Mathf.Lerp(0.18f, 0.58f, Wave01(seed + 4.1f));
            cloudPhases[i] = seed * 2.31f;
        }

        cycleDuration = Mathf.Max(MinCycleDuration, 108f);
        cycleOffset = Wave01(Time.realtimeSinceStartup * 0.31f) * cycleDuration;
        nextGiUpdateAt = 0f;
        configured = true;
        ApplyAtmosphere(0f);
        AnimateClouds(0f);
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        float t = Time.timeSinceLevelLoad + cycleOffset;
        ApplyAtmosphere(t);
        AnimateClouds(t);
    }

    private void ApplyAtmosphere(float timeSeconds)
    {
        float cycle01 = Mathf.Repeat(timeSeconds / Mathf.Max(MinCycleDuration, cycleDuration), 1f);
        float warm01 = 0.5f + 0.5f * Mathf.Sin(cycle01 * Mathf.PI * 2f - Mathf.PI * 0.5f);
        float daylight01 = Mathf.Clamp01(Mathf.Sin(cycle01 * Mathf.PI) * 0.95f + 0.05f);

        Color skyCool = new Color(0.62f, 0.75f, 0.87f);
        Color skyWarm = new Color(0.90f, 0.72f, 0.56f);
        Color equatorCool = new Color(0.46f, 0.54f, 0.56f);
        Color equatorWarm = new Color(0.62f, 0.48f, 0.40f);
        Color groundCool = new Color(0.27f, 0.32f, 0.24f);
        Color groundWarm = new Color(0.38f, 0.30f, 0.22f);
        Color fogCool = new Color(0.70f, 0.79f, 0.84f);
        Color fogWarm = new Color(0.92f, 0.70f, 0.54f);

        RenderSettings.ambientSkyColor = Color.Lerp(skyCool, skyWarm, warm01 * 0.86f);
        RenderSettings.ambientEquatorColor = Color.Lerp(equatorCool, equatorWarm, warm01 * 0.78f);
        RenderSettings.ambientGroundColor = Color.Lerp(groundCool, groundWarm, warm01 * 0.72f);
        RenderSettings.ambientIntensity = Mathf.Lerp(1.26f, 1.72f, daylight01);
        RenderSettings.reflectionIntensity = Mathf.Lerp(1.16f, 1.56f, daylight01);
        RenderSettings.fogColor = Color.Lerp(fogCool, fogWarm, warm01 * 0.86f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(0.0013f, 0.0038f, 1f - daylight01);

        if (sun != null)
        {
            float sunPitch = Mathf.Lerp(62f, 34f, warm01);
            float sunYaw = Mathf.Lerp(-36f, -18f, warm01);
            sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
            sun.color = Color.Lerp(new Color(1f, 0.97f, 0.90f), new Color(1f, 0.76f, 0.55f), warm01);
            sun.useColorTemperature = true;
            sun.colorTemperature = Mathf.Lerp(5900f, 4350f, warm01);
            sun.intensity = Mathf.Lerp(1.68f, 2.42f, daylight01);
            sun.bounceIntensity = Mathf.Lerp(0.92f, 1.20f, daylight01);
            sun.shadowStrength = Mathf.Lerp(0.72f, 0.58f, warm01 * 0.55f);
            sun.shadowBias = Mathf.Lerp(0.020f, 0.028f, 1f - daylight01);
        }

        SetMaterialColor(terrainGrassMaterial, Color.Lerp(
            new Color(0.30f, 0.46f, 0.30f),
            new Color(0.43f, 0.55f, 0.34f),
            warm01 * 0.75f), 0f);

        SetMaterialColor(terrainSoilMaterial, Color.Lerp(
            new Color(0.41f, 0.31f, 0.21f),
            new Color(0.53f, 0.38f, 0.24f),
            warm01), 0f);

        SetMaterialColor(terrainRockMaterial, Color.Lerp(
            new Color(0.30f, 0.30f, 0.31f),
            new Color(0.40f, 0.34f, 0.31f),
            warm01 * 0.66f), 0f);

        SetMaterialColor(mountainNearCoolMaterial, Color.Lerp(
            new Color(0.41f, 0.49f, 0.54f),
            new Color(0.50f, 0.47f, 0.42f),
            warm01 * 0.42f), 0f);

        SetMaterialColor(mountainNearWarmMaterial, Color.Lerp(
            new Color(0.53f, 0.42f, 0.34f),
            new Color(0.64f, 0.47f, 0.36f),
            warm01), 0f);

        SetMaterialColor(mountainFarCoolMaterial, Color.Lerp(
            new Color(0.33f, 0.40f, 0.47f),
            new Color(0.43f, 0.40f, 0.40f),
            warm01 * 0.36f), 0f);

        SetMaterialColor(mountainFarWarmMaterial, Color.Lerp(
            new Color(0.44f, 0.37f, 0.33f),
            new Color(0.54f, 0.43f, 0.36f),
            warm01 * 0.88f), 0f);

        SetMaterialColor(cloudMaterial, Color.Lerp(
            new Color(0.82f, 0.90f, 0.96f),
            new Color(0.98f, 0.84f, 0.72f),
            warm01 * 0.62f), Mathf.Lerp(0.02f, 0.09f, daylight01));

        if (skyboxMaterial != null)
        {
            SetSkyboxParams(skyboxMaterial, warm01, daylight01);
            if (Time.time >= nextGiUpdateAt)
            {
                DynamicGI.UpdateEnvironment();
                nextGiUpdateAt = Time.time + 1.6f;
            }
        }

        if (renderEnhancer != null)
        {
            atmosphereInvokeArgs[0] = daylight01;
            atmosphereInvokeArgs[1] = warm01;
            setAtmosphereMethod?.Invoke(renderEnhancer, atmosphereInvokeArgs);
        }
    }

    private void AnimateClouds(float timeSeconds)
    {
        if (clouds == null || cloudBasePositions == null)
        {
            return;
        }

        for (int i = 0; i < clouds.Length; i++)
        {
            Transform cloud = clouds[i];
            if (cloud == null)
            {
                continue;
            }

            float phase = cloudPhases[i];
            float speed = cloudOrbitSpeeds[i] * Mathf.Lerp(0.82f, 1.28f, Wave01(timeSeconds * 0.11f + phase));
            float orbitA = Mathf.Sin(timeSeconds * speed + phase);
            float orbitB = Mathf.Cos(timeSeconds * (speed * 0.66f) + phase * 1.27f);
            float range = cloudOrbitRanges[i];

            Vector3 offset = cloudDirections[i] * (orbitA * range) +
                             cloudPerpDirections[i] * (orbitB * range * 0.34f) +
                             Vector3.up * (Mathf.Sin(timeSeconds * cloudBobSpeeds[i] + phase * 1.13f) * cloudBobAmplitudes[i]);

            cloud.position = cloudBasePositions[i] + offset;
            float yaw = Mathf.Sin(timeSeconds * (0.12f + speed * 0.16f) + phase) * 14f;
            cloud.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private static void SetMaterialColor(Material material, Color color, float emissionStrength)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            if (emissionStrength > 0.0001f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private static void SetSkyboxParams(Material skybox, float warm01, float daylight01)
    {
        if (skybox == null)
        {
            return;
        }

        if (skybox.HasProperty("_Exposure"))
        {
            skybox.SetFloat("_Exposure", Mathf.Lerp(1.08f, 1.52f, daylight01));
        }
        if (skybox.HasProperty("_AtmosphereThickness"))
        {
            skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.64f, 1.22f, 1f - daylight01));
        }
        if (skybox.HasProperty("_SkyTint"))
        {
            skybox.SetColor("_SkyTint", Color.Lerp(
                new Color(0.56f, 0.71f, 0.90f, 1f),
                new Color(0.92f, 0.68f, 0.52f, 1f),
                warm01 * 0.8f));
        }
        if (skybox.HasProperty("_GroundColor"))
        {
            skybox.SetColor("_GroundColor", Color.Lerp(
                new Color(0.36f, 0.39f, 0.35f, 1f),
                new Color(0.46f, 0.36f, 0.28f, 1f),
                warm01 * 0.68f));
        }
        if (skybox.HasProperty("_SunSize"))
        {
            skybox.SetFloat("_SunSize", Mathf.Lerp(0.025f, 0.055f, warm01));
        }
        if (skybox.HasProperty("_SunSizeConvergence"))
        {
            skybox.SetFloat("_SunSizeConvergence", Mathf.Lerp(4f, 8f, daylight01));
        }
    }

    private static float Wave01(float value)
    {
        return 0.5f + Mathf.Sin(value) * 0.5f;
    }
}
