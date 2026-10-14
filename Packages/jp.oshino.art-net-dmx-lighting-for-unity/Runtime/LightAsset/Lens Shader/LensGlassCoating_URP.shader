Shader "ArtNet/URP/Lens Glass Coating"
{
    Properties
    {
        _GlassTint ("Glass Tint", Color) = (0.22, 0.36, 0.52, 1)
        _CenterOpacity ("Center Opacity", Range(0, 1)) = 0.055
        _EdgeOpacity ("Edge Opacity", Range(0, 1)) = 0.2
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.8
        _SurfaceMetallic ("Surface Metallic", Range(0, 1)) = 0
        _SurfaceSmoothness ("Surface Smoothness", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+20" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            // Additive optical glints cannot attenuate the DMX/gobo layer.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/LightAsset/Lens Shader/LensGlassCoating.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassTint;
                float _CenterOpacity;
                float _EdgeOpacity;
                float _HighlightStrength;
                float _SurfaceMetallic;
                float _SurfaceSmoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 color;
                float opacity;
                EvaluateLensGlassCoating(input.uv, input.normalWS,
                    GetCameraPositionWS() - input.positionWS, _GlassTint, _CenterOpacity, _EdgeOpacity,
                    _HighlightStrength, _SurfaceMetallic, _SurfaceSmoothness, color, opacity);
                return half4(color * opacity, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
