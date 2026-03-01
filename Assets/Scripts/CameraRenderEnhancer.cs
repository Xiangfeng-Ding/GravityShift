using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraRenderEnhancer : MonoBehaviour
{
    [SerializeField, Range(0.85f, 1.45f)] private float baseContrast = 1.12f;
    [SerializeField, Range(0.75f, 1.35f)] private float baseSaturation = 1.12f;
    [SerializeField, Range(0.85f, 1.30f)] private float baseBrightness = 1.18f;
    [SerializeField, Range(0f, 0.9f)] private float baseVibrance = 0.24f;
    [SerializeField, Range(0f, 0.75f)] private float baseMidtoneContrast = 0.20f;
    [SerializeField, Range(0f, 0.6f)] private float baseHighlightCompression = 0.20f;
    [SerializeField, Range(0f, 0.25f)] private float baseShadowLift = 0.070f;
    [SerializeField, Range(0f, 0.55f)] private float baseVignette = 0.075f;
    [SerializeField, Range(0.2f, 2.5f)] private float vignetteSmoothness = 1.35f;
    [SerializeField, Range(0f, 0.25f)] private float baseGrain = 0.018f;
    [SerializeField, Range(0f, 1.1f)] private float baseSharpen = 0.56f;
    [SerializeField, Range(0f, 1.2f)] private float baseBloomIntensity = 0.34f;
    [SerializeField, Range(0.2f, 1.3f)] private float baseBloomThreshold = 0.69f;
    [SerializeField, Range(0f, 0.35f)] private float baseChromaticAberration = 0.028f;
    [SerializeField, Range(0f, 1f)] private float baseFilmicStrength = 0.84f;
    [SerializeField, Range(0f, 0.85f)] private float baseDepthFogIntensity = 0.20f;
    [SerializeField, Range(0.01f, 0.45f)] private float depthFogStart = 0.08f;
    [SerializeField, Range(0.20f, 1f)] private float depthFogEnd = 0.84f;
    [SerializeField, Range(0f, 0.65f)] private float highResSharpenDamping = 0.14f;
    [SerializeField, Range(0f, 0.35f)] private float highResGrainDamping = 0.22f;

    private Material postMaterial;
    private float profileIntensity = 1f;
    private float daylight01 = 1f;
    private float warm01;

    public void ConfigureProfile(float intensity)
    {
        profileIntensity = Mathf.Clamp(intensity, 0.55f, 1.6f);
    }

    public void SetAtmosphere(float daylight, float warm)
    {
        daylight01 = Mathf.Clamp01(daylight);
        warm01 = Mathf.Clamp01(warm);
    }

    private void OnEnable()
    {
        EnsureMaterial();
    }

    private void OnDisable()
    {
        if (postMaterial != null)
        {
            Destroy(postMaterial);
            postMaterial = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!EnsureMaterial())
        {
            Graphics.Blit(source, destination);
            return;
        }

        ApplyDynamicParams();
        Graphics.Blit(source, destination, postMaterial, 0);
    }

    private bool EnsureMaterial()
    {
        if (postMaterial != null)
        {
            return true;
        }

        Shader shader = Shader.Find("Hidden/GravityShift/RenderEnhancer");
        if (shader == null || !shader.isSupported)
        {
            return false;
        }

        postMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return postMaterial != null;
    }

    private void ApplyDynamicParams()
    {
        if (postMaterial == null)
        {
            return;
        }

        float dayExposure = Mathf.Lerp(1.08f, 1.24f, daylight01);
        float warmContrast = Mathf.Lerp(1.02f, 0.98f, warm01 * 0.7f);
        float contrast = baseContrast * profileIntensity * warmContrast;
        float saturation = baseSaturation * Mathf.Lerp(0.98f, 1.15f, daylight01);
        float brightness = baseBrightness * dayExposure;
        float vignette = baseVignette * Mathf.Lerp(1.02f, 0.74f, daylight01);
        float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        float highRes01 = Mathf.InverseLerp(900f, 2160f, shortSide);

        float grain = baseGrain * Mathf.Lerp(0.86f, 1.10f, 1f - daylight01) * Mathf.Lerp(0.76f, 1.00f, profileIntensity - 0.55f);
        grain *= Mathf.Lerp(1f, 1f - highResGrainDamping, highRes01);

        float sharpen = baseSharpen * Mathf.Lerp(0.92f, 1.16f, daylight01) * Mathf.Lerp(0.86f, 1.24f, profileIntensity - 0.55f);
        sharpen *= Mathf.Lerp(1f, 1f - highResSharpenDamping, highRes01);

        float vibrance = baseVibrance * Mathf.Lerp(0.90f, 1.22f, daylight01) * Mathf.Lerp(0.88f, 1.12f, profileIntensity - 0.55f);
        float midtoneContrast = baseMidtoneContrast * Mathf.Lerp(0.86f, 1.10f, daylight01);
        float highlightCompression = baseHighlightCompression * Mathf.Lerp(0.82f, 1.16f, 1f - daylight01);
        float shadowLift = baseShadowLift * Mathf.Lerp(0.92f, 1.25f, 1f - daylight01);

        float bloomIntensity = baseBloomIntensity * Mathf.Lerp(0.90f, 1.26f, daylight01) * Mathf.Lerp(0.86f, 1.26f, profileIntensity - 0.55f);
        float bloomThreshold = Mathf.Lerp(baseBloomThreshold + 0.06f, baseBloomThreshold - 0.10f, daylight01);
        float chromatic = baseChromaticAberration * Mathf.Lerp(0.40f, 0.88f, profileIntensity - 0.55f) * Mathf.Lerp(0.74f, 1.00f, 1f - daylight01);
        float filmic = Mathf.Lerp(baseFilmicStrength * 0.72f, baseFilmicStrength, profileIntensity);
        float depthFogIntensity = baseDepthFogIntensity * Mathf.Lerp(0.80f, 1.12f, 1f - daylight01);
        float depthFogStartValue = Mathf.Clamp01(depthFogStart * Mathf.Lerp(0.88f, 1.08f, daylight01));
        float depthFogEndValue = Mathf.Clamp(depthFogEnd * Mathf.Lerp(0.96f, 1.08f, daylight01), depthFogStartValue + 0.08f, 1f);

        Color tintCool = new Color(0.96f, 1.00f, 1.04f, 1f);
        Color tintWarm = new Color(1.07f, 0.95f, 0.88f, 1f);
        Color tint = Color.Lerp(tintCool, tintWarm, warm01 * 0.72f);
        Color bloomTint = Color.Lerp(new Color(0.62f, 0.86f, 1f), new Color(1f, 0.80f, 0.58f), warm01 * 0.86f);
        Color depthFogColor = RenderSettings.fogColor;
        if (depthFogColor.maxColorComponent <= 0.001f)
        {
            depthFogColor = Color.Lerp(new Color(0.76f, 0.84f, 0.90f), new Color(0.92f, 0.76f, 0.62f), warm01 * 0.78f);
        }

        postMaterial.SetFloat("_Contrast", contrast);
        postMaterial.SetFloat("_Saturation", saturation);
        postMaterial.SetFloat("_Brightness", brightness);
        postMaterial.SetFloat("_Vibrance", vibrance);
        postMaterial.SetFloat("_MidtoneContrast", midtoneContrast);
        postMaterial.SetFloat("_HighlightCompression", highlightCompression);
        postMaterial.SetFloat("_ShadowLift", shadowLift);
        postMaterial.SetFloat("_Vignette", vignette);
        postMaterial.SetFloat("_VignetteSmoothness", vignetteSmoothness);
        postMaterial.SetFloat("_GrainIntensity", grain);
        postMaterial.SetFloat("_SharpenStrength", sharpen);
        postMaterial.SetFloat("_BloomIntensity", bloomIntensity);
        postMaterial.SetFloat("_BloomThreshold", bloomThreshold);
        postMaterial.SetColor("_BloomTint", bloomTint);
        postMaterial.SetFloat("_ChromaticAberration", chromatic);
        postMaterial.SetFloat("_FilmicStrength", filmic);
        postMaterial.SetColor("_Tint", tint);
        postMaterial.SetFloat("_DepthFogIntensity", depthFogIntensity);
        postMaterial.SetFloat("_DepthFogStart", depthFogStartValue);
        postMaterial.SetFloat("_DepthFogEnd", depthFogEndValue);
        postMaterial.SetColor("_DepthFogColor", depthFogColor);
    }
}
