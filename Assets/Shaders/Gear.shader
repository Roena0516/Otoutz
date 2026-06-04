// Procedural playfield ("gear") pattern so the lane floor isn't a flat single colour.
// Adds: a soft glow toward the judgement line, subtle per-lane shading, lane divider lines,
// and gently scrolling "speed" lines for a sense of motion. All tunable via material props.
// UV: u 0..1 across the 4 lanes (boundaries at .25/.5/.75), v 0 = near (judgement) .. 1 = far.
Shader "PROJECT-O/Gear"
{
    Properties
    {
        _BaseColor   ("Base Color", Color) = (0.05, 0.05, 0.065, 1)
        _NearColor   ("Near Glow Color", Color) = (0.10, 0.16, 0.34, 1)
        _NearStrength("Near Glow Strength", Range(0,3)) = 1.0
        _LaneTint    ("Lane Alt Tint", Color) = (0.06, 0.09, 0.16, 1)
        _LaneStrength("Lane Alt Strength", Range(0,1)) = 0.5
        _LineColor   ("Divider Line Color", Color) = (0.28, 0.36, 0.62, 1)
        _LineStrength("Divider Line Strength", Range(0,2)) = 0.7
        _BeatColor   ("Speed Line Color", Color) = (0.18, 0.26, 0.5, 1)
        _BeatStrength("Speed Line Strength", Range(0,2)) = 0.6
        _BeatCount   ("Speed Line Count", Float) = 4
        _BeatWidth   ("Speed Line Width (px)", Range(0.5, 8)) = 3.5
        _ScrollOffset("Scroll Offset (beats)", Float) = 0
        _BeatPhase   ("Beat Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _NearColor, _LaneTint, _LineColor, _BeatColor;
                float _NearStrength, _LaneStrength, _LineStrength, _BeatStrength, _BeatCount, _BeatWidth, _ScrollOffset, _BeatPhase;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float u = IN.uv.x;
                float v = IN.uv.y;
                half3 col = _BaseColor.rgb;

                // depth: 1 near the judgement line, 0 far away
                float depth = saturate(1.0 - v);

                // soft glow toward the judgement line
                col += _NearColor.rgb * pow(depth, 3.0) * _NearStrength;

                // subtle alternating shade per lane (4 lanes)
                float lane = floor(u * 4.0);
                float parity = fmod(lane, 2.0);
                col += _LaneTint.rgb * (parity * _LaneStrength * 0.06);

                // lane divider lines at u = .25/.5/.75
                float lines = 0.0;
                [unroll] for (int i = 1; i < 4; i++)
                {
                    float d = abs(u - i * 0.25);
                    lines += smoothstep(0.008, 0.0, d);
                }
                col += _LineColor.rgb * lines * _LineStrength;

                // beat lines driven by the song position (set from script), fading into the distance.
                // Crisp, constant screen-space width via fwidth so they aren't thick/blurry up close.
                float scroll = v * _BeatCount + _ScrollOffset + _BeatPhase;
                float f = frac(scroll);
                float d = min(f, 1.0 - f);                          // distance to nearest line (beats)
                float aa = max(fwidth(scroll), 1e-5) * _BeatWidth;  // ~_BeatWidth pixels wide
                float beat = 1.0 - smoothstep(0.0, aa, d);
                col += _BeatColor.rgb * beat * _BeatStrength * (depth * 0.9 + 0.1);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
