// Simple URP-compatible unlit additive shader for the note-hit effects.
// Blend is fixed (SrcAlpha One) so it works reliably when materials are built at runtime —
// unlike URP/Unlit, whose transparent/additive state is baked by its ShaderGUI.
Shader "PROJECT-O/HitAdditive"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                o.color = IN.color;
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor * IN.color;
            }
            ENDHLSL
        }
    }
}
