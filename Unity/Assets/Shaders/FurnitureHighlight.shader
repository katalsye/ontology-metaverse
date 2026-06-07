Shader "Custom/FurnitureHighlight"
{
    Properties
    {
        _Color     ("Highlight Color", Color)  = (1, 0.85, 0, 1)
        _Intensity ("Intensity",       Range(0,1)) = 0.35
        // BlendMode: 0=Additive  1=Screen  2=Multiply
        [KeywordEnum(Additive, Screen, Multiply)] _Blend ("Blend Mode", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }

        // ── Additive ──────────────────────────────────────────
        Pass
        {
            Name "Additive"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _BLEND_ADDITIVE _BLEND_SCREEN _BLEND_MULTIPLY
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; };
            struct Vary { float4 pos : SV_POSITION; };

            Vary vert(Attr IN) { Vary O; O.pos = TransformObjectToHClip(IN.pos.xyz); return O; }

            half4 frag(Vary IN) : SV_Target
            {
                return half4(_Color.rgb * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
