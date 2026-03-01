Shader "Hidden/GravityShift/RenderEnhancer"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Contrast ("Contrast", Float) = 1.1
        _Saturation ("Saturation", Float) = 1.0
        _Brightness ("Brightness", Float) = 1.0
        _Vibrance ("Vibrance", Float) = 0.2
        _MidtoneContrast ("MidtoneContrast", Float) = 0.2
        _HighlightCompression ("HighlightCompression", Float) = 0.2
        _ShadowLift ("ShadowLift", Float) = 0.04
        _Vignette ("Vignette", Float) = 0.15
        _VignetteSmoothness ("VignetteSmoothness", Float) = 1.25
        _GrainIntensity ("GrainIntensity", Float) = 0.03
        _SharpenStrength ("SharpenStrength", Float) = 0.4
        _BloomIntensity ("BloomIntensity", Float) = 0.26
        _BloomThreshold ("BloomThreshold", Float) = 0.74
        _BloomTint ("BloomTint", Color) = (0.72,0.86,1,1)
        _ChromaticAberration ("ChromaticAberration", Float) = 0.06
        _FilmicStrength ("FilmicStrength", Float) = 0.88
        _Tint ("Tint", Color) = (1,1,1,1)
        _DepthFogIntensity ("DepthFogIntensity", Float) = 0.3
        _DepthFogStart ("DepthFogStart", Float) = 0.08
        _DepthFogEnd ("DepthFogEnd", Float) = 0.85
        _DepthFogColor ("DepthFogColor", Color) = (0.74,0.8,0.84,1)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;

            float _Contrast;
            float _Saturation;
            float _Brightness;
            float _Vibrance;
            float _MidtoneContrast;
            float _HighlightCompression;
            float _ShadowLift;
            float _Vignette;
            float _VignetteSmoothness;
            float _GrainIntensity;
            float _SharpenStrength;
            float _BloomIntensity;
            float _BloomThreshold;
            float4 _BloomTint;
            float _ChromaticAberration;
            float _FilmicStrength;
            float4 _Tint;
            float _DepthFogIntensity;
            float _DepthFogStart;
            float _DepthFogEnd;
            float4 _DepthFogColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float3 ACESFilm(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            float3 ApplyVibrance(float3 color, float vibrance)
            {
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float maxC = max(color.r, max(color.g, color.b));
                float minC = min(color.r, min(color.g, color.b));
                float sat = saturate(maxC - minC);
                float amount = 1.0 + saturate(vibrance) * (1.0 - sat);
                return lerp(luminance.xxx, color, amount);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float dist = length(centered);

                float2 texel = _MainTex_TexelSize.xy;
                float2 caOffset = centered * (_ChromaticAberration * (0.0009 + dist * 0.0028));
                float r = tex2D(_MainTex, uv + caOffset).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - caOffset).b;
                float3 c = float3(r, g, b);

                float3 n = tex2D(_MainTex, uv + float2(0.0, texel.y)).rgb;
                float3 s = tex2D(_MainTex, uv - float2(0.0, texel.y)).rgb;
                float3 e = tex2D(_MainTex, uv + float2(texel.x, 0.0)).rgb;
                float3 w = tex2D(_MainTex, uv - float2(texel.x, 0.0)).rgb;
                float3 blur = (n + s + e + w) * 0.25;
                c = lerp(c, c + (c - blur), saturate(_SharpenStrength));

                float2 bloomStepA = texel * 2.0;
                float2 bloomStepB = texel * 4.2;
                float3 b0 = max(c - _BloomThreshold, 0.0);
                float3 b1 = max(tex2D(_MainTex, uv + float2(bloomStepA.x, 0.0)).rgb - _BloomThreshold, 0.0);
                float3 b2 = max(tex2D(_MainTex, uv - float2(bloomStepA.x, 0.0)).rgb - _BloomThreshold, 0.0);
                float3 b3 = max(tex2D(_MainTex, uv + float2(0.0, bloomStepA.y)).rgb - _BloomThreshold, 0.0);
                float3 b4 = max(tex2D(_MainTex, uv - float2(0.0, bloomStepA.y)).rgb - _BloomThreshold, 0.0);
                float3 b5 = max(tex2D(_MainTex, uv + bloomStepB).rgb - _BloomThreshold, 0.0);
                float3 b6 = max(tex2D(_MainTex, uv - bloomStepB).rgb - _BloomThreshold, 0.0);
                float3 b7 = max(tex2D(_MainTex, uv + float2(bloomStepB.x, -bloomStepB.y)).rgb - _BloomThreshold, 0.0);
                float3 b8 = max(tex2D(_MainTex, uv + float2(-bloomStepB.x, bloomStepB.y)).rgb - _BloomThreshold, 0.0);
                float3 bloom = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8) / 9.0;
                c += bloom * _BloomIntensity * _BloomTint.rgb;

                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(luma.xxx, c, _Saturation);
                c = ApplyVibrance(c, _Vibrance);
                c = (c - 0.5) * _Contrast + 0.5;

                float midtoneMask = smoothstep(0.12, 0.78, luma) * (1.0 - smoothstep(0.72, 1.0, luma));
                c = lerp(c, (c - 0.5) * (1.0 + _MidtoneContrast * midtoneMask) + 0.5, 0.9);

                c *= _Brightness;
                c *= _Tint.rgb;
                c = lerp(c, ACESFilm(max(c, 0.0)), saturate(_FilmicStrength));

                float3 highlightCompressed = c / (1.0 + c * (0.85 + _HighlightCompression * 1.45));
                c = lerp(c, highlightCompressed, saturate(_HighlightCompression));
                c = max(c, _ShadowLift * (1.0 - c));

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float linear01Depth = Linear01Depth(rawDepth);
                float fog01 = saturate((linear01Depth - _DepthFogStart) / max(0.0001, _DepthFogEnd - _DepthFogStart));
                fog01 = smoothstep(0.0, 1.0, fog01);
                c = lerp(c, lerp(c, _DepthFogColor.rgb, 0.84), fog01 * saturate(_DepthFogIntensity));

                float vigDist = dist * 1.4142;
                float vig = pow(saturate(1.0 - vigDist), max(0.05, _VignetteSmoothness));
                c *= lerp(1.0 - _Vignette, 1.0, vig);

                float grainSeed = Hash21(uv * _ScreenParams.xy + _Time.yy * 37.7);
                float grain = (grainSeed - 0.5) * _GrainIntensity;
                c += grain;

                return float4(saturate(c), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
