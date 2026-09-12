Shader "ArtNet/HDRP/Gobo Lens Surface"
{
    Properties
    {
        _DmxColor ("DMX Color", Color) = (1, 1, 1, 1)
        _DmxDimmer ("DMX Dimmer", Range(0, 1)) = 1
        _GoboTexture ("Gobo Texture", 2D) = "white" {}
        _GoboEnabled ("Gobo Enabled", Float) = 0
        _GoboRotationDeg ("Gobo Rotation", Float) = 0
        _GoboOffset ("Gobo Offset", Vector) = (0, 0, 0, 0)
        _GoboLensInfluence ("Gobo Influence", Range(0, 1)) = 1
        _GoboLensEmission ("Gobo Emission", Float) = 2
        _GoboLensScale ("Gobo Scale", Float) = 1
        _GoboLensBlur ("Gobo Blur", Range(0, 0.02)) = 0
        _LensApertureFeather ("Lens Aperture Feather", Range(0, 0.5)) = 0
        _GoboLensHotspotStrength ("Hotspot Strength", Float) = 0.35
        _GoboLensHotspotInner ("Hotspot Inner", Range(0, 1)) = 0.12
        _GoboLensHotspotOuter ("Hotspot Outer", Range(0, 1)) = 0.55
        _GoboLensFresnelStrength ("Fresnel Strength", Float) = 0.25
        _GoboLensFresnelPower ("Fresnel Power", Float) = 4
        _EmissiveExposureWeight ("Emissive Exposure Weight", Range(0, 1)) = 1
        [HideInInspector] _SurfaceType ("Surface Type", Float) = 0
        [HideInInspector] _BlendMode ("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _AlphaSrcBlend ("Alpha Src Blend", Float) = 1
        [HideInInspector] _AlphaDstBlend ("Alpha Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _CullMode ("Cull Mode", Float) = 2
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
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "HDUnlitShader" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_AlphaSrcBlend] [_AlphaDstBlend]
            ZWrite [_ZWrite]
            Cull [_CullMode]
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitSharePass.hlsl"
            #include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/ArtNet/Runtime/Resources/ArtNet/GoboLensSurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    FallBack Off
}
