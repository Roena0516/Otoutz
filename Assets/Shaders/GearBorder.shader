// Flowing neon border for the playfield side rails (the "Outline" quad behind the gear).
// A gradient base with bright pulses streaming toward the player, pushed slightly past 1 so it
// blooms. Only the strips peeking out beside the gear are visible, so this reads as glowing rails.
// UV: v 0 = near (judgement) .. 1 = far.
Shader "PROJECT-O/GearBorder"
{
    Properties
    {
        _ColorA      ("Color Near", Color) = (0.30, 0.38, 0.85, 1)
        _ColorB      ("Color Far", Color)  = (0.55, 0.35, 0.85, 1)
        _Brightness  ("Brightness", Range(0,3)) = 0.8
        _FlowColor   ("Flow Color", Color) = (0.70, 0.75, 1.00, 1)
        _FlowStrength("Flow Strength", Range(0,3)) = 0.55
        _FlowCount   ("Flow Count", Float) = 5
        _FlowSpeed   ("Flow Speed", Float) = 0.8
        _EdgeFade    ("Edge Fade", Range(0.005, 0.3)) = 0.06
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
                half4 _ColorA, _ColorB, _FlowColor;
                float _Brightness, _FlowStrength, _FlowCount, _FlowSpeed, _EdgeFade;
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

                // base gradient along the rail
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, v) * _Brightness;

                // bright pulses flowing toward the player
                float f = frac(v * _FlowCount + _Time.y * _FlowSpeed);
                float pulse = smoothstep(0.0, 0.5, f) * smoothstep(1.0, 0.5, f);
                col += _FlowColor.rgb * pulse * _FlowStrength;

                // fade out softly toward the outermost left/right edges (u→0 and u→1)
                float edge = smoothstep(0.0, _EdgeFade, u) * smoothstep(1.0, 1.0 - _EdgeFade, u);

                return half4(col, edge);
            }
            ENDHLSL
        }
    }
}
