Shader "ArtNet/URP/Lens Halo"
{
    Properties
    {
        _DmxColor ("DMX Color", Color) = (1, 1, 1, 1)
        _DmxDimmer ("DMX Dimmer", Range(0, 1)) = 1
        _HaloTint ("Halo Tint", Color) = (0.8, 0.88, 1, 1)
        _HaloIntensity ("Halo Intensity", Range(0, 4)) = 0.4
        [HideInInspector] _IrisShape ("Iris Shape", Vector) = (1, 0, 0, 0)
        [HideInInspector] _IrisLensInfluence ("Iris Influence", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/LightAsset/Lens Shader/LensHalo.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DmxColor;
                float _DmxDimmer;
                float4 _HaloTint;
                float _HaloIntensity;
                float4 _IrisShape;
                float _IrisLensInfluence;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = EvaluateLensHalo(input.uv, _DmxColor.rgb, _DmxDimmer,
                    _HaloTint.rgb, _HaloIntensity, _IrisShape.x, _IrisLensInfluence);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
