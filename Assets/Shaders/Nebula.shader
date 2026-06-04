// Animated nebula backdrop for the in-game playfield: drifting fbm clouds, slowly falling
// twinkling particles, a vignette, and a beat-reactive brightness pulse (fed from script via
// _BeatPulse). Rendered on a full-screen RawImage on the far-plane background canvas, so all
// gameplay draws in front of it. Kept dark/low-contrast so notes stay readable.
Shader "PROJECT-O/Nebula"
{
    Properties
    {
        _ColorDeep    ("Deep Color", Color)  = (0.04, 0.04, 0.10, 1)
        _ColorMid     ("Mid Color", Color)   = (0.12, 0.08, 0.24, 1)
        _ColorHot     ("Hot Color", Color)   = (0.32, 0.16, 0.48, 1)
        _ParticleColor("Particle Color", Color) = (0.65, 0.75, 1.0, 1)
        _Brightness   ("Brightness", Range(0,2)) = 0.7
        _Scale        ("Cloud Scale", Float) = 3.0
        _Speed        ("Drift Speed", Float) = 1.0
        _Vignette     ("Vignette", Range(0,3)) = 1.2
        _SpecTex      ("Spectrum", 2D) = "black" {}
        _WaveColor    ("Wave Color", Color) = (0.45, 0.7, 1.0, 1)
        _WaveStrength ("Wave Strength", Range(0,3)) = 0.6
        _WaveScale    ("Wave Height", Range(0,1)) = 0.32
        _WaveCenter   ("Wave Center Y", Range(0,1)) = 0.5
        _WaveThickness("Wave Thickness", Range(0.002,0.05)) = 0.012
        [HideInInspector] _MainTex ("Tex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDeep, _ColorMid, _ColorHot, _ParticleColor, _WaveColor;
                float _Brightness, _Scale, _Speed, _Vignette;
                float _WaveStrength, _WaveScale, _WaveCenter, _WaveThickness;
                float4 _MainTex_ST, _SpecTex_ST;
            CBUFFER_END

            TEXTURE2D(_SpecTex);
            SAMPLER(sampler_SpecTex);

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }
            float2 hash22(float2 p)
            {
                float n = sin(dot(p, float2(41.0, 289.0)));
                return frac(float2(262144.0, 32768.0) * n);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1)), d = hash21(i + float2(1,1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                [unroll] for (int i = 0; i < 5; i++) { v += amp * vnoise(p); p *= 2.02; amp *= 0.5; }
                return v;
            }

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float asp = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 uv = IN.uv;
                float2 p = float2(uv.x * asp, uv.y);
                float t = _Time.y * _Speed;

                // drifting nebula clouds (two fbm layers)
                float n1 = fbm(p * _Scale + float2(t * 0.06, t * 0.03));
                float n2 = fbm(p * _Scale * 1.7 - float2(t * 0.045, t * 0.05) + 7.3);
                float neb = saturate(n1 * 0.7 + n2 * 0.5);
                neb = pow(neb, 1.8);

                half3 col = lerp(_ColorDeep.rgb, _ColorMid.rgb, neb);
                col = lerp(col, _ColorHot.rgb, smoothstep(0.55, 1.0, neb) * 0.7);

                // slowly falling, twinkling particles (two layers)
                float spark = 0.0;
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float scale = lerp(11.0, 22.0, k);
                    float2 gp = p * scale + float2(0.0, t * (0.30 + 0.18 * k));
                    float2 cell = floor(gp), f = frac(gp);
                    float2 rnd = hash22(cell);
                    float2 center = 0.5 + 0.34 * sin(t * 0.5 + rnd * 6.2831);
                    float d = length(f - center);
                    float tw = 0.5 + 0.5 * sin(t * 2.0 + rnd.x * 6.2831);
                    spark += smoothstep(0.06, 0.0, d) * tw * (0.6 - 0.2 * k);
                }
                col += _ParticleColor.rgb * spark;

                col *= _Brightness;

                // subtle audio waveform from the FFT spectrum (bass-centred, mirrored)
                float sx = abs(uv.x - 0.5) * 2.0;
                float amp = SAMPLE_TEXTURE2D(_SpecTex, sampler_SpecTex, float2(sx, 0.5)).r * _WaveScale;
                float wd = abs(uv.y - _WaveCenter);
                float fill = smoothstep(amp, amp - 0.02, wd) * 0.18;
                float edge = exp(-pow((wd - amp) / _WaveThickness, 2.0));
                col += _WaveColor.rgb * (fill + edge) * _WaveStrength;

                // vignette
                float2 cuv = uv - 0.5;
                col *= saturate(1.0 - dot(cuv, cuv) * _Vignette);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
