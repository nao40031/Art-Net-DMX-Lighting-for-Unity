Shader "ArtNet/HDRP/Lens Halo"
{
    Properties
    {
        _DmxColor ("DMX Color", Color) = (1, 1, 1, 1)
        _DmxDimmer ("DMX Dimmer", Range(0, 1)) = 1
        _HaloTint ("Halo Tint", Color) = (0.8, 0.88, 1, 1)
        _HaloIntensity ("Halo Intensity", Range(0, 4)) = 0.4
        [HideInInspector] _IrisShape ("Iris Shape", Vector) = (1, 0, 0, 0)
        [HideInInspector] _IrisLensInfluence ("Iris Influence", Range(0, 1)) = 0
        [HideInInspector] _CullMode ("Cull Mode", Float) = 0
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+5" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull [_CullMode]
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitSharePass.hlsl"
            #include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/LightAsset/Lens Shader/LensHalo_HDRP.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    FallBack Off
}
