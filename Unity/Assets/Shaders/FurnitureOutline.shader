Shader "Custom/FurnitureOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 4.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 posCS    = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float2 normalCS = normalize(TransformWorldToHClip(float4(normalWS, 0)).xy);
                // 종횡비 보정
                normalCS.x *= _ScreenParams.y / _ScreenParams.x;
                posCS.xy += normalCS * (_OutlineWidth * 0.001) * posCS.w;
                OUT.positionHCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
