Shader "ArtNet/HDRP/Lens Glass Coating"
{
    Properties
    {
        _GlassTint ("Glass Tint", Color) = (0.22, 0.36, 0.52, 1)
        _CenterOpacity ("Center Opacity", Range(0, 1)) = 0.055
        _EdgeOpacity ("Edge Opacity", Range(0, 1)) = 0.2
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.8
        _SurfaceMetallic ("Surface Metallic", Range(0, 1)) = 0
        _SurfaceSmoothness ("Surface Smoothness", Range(0, 1)) = 0.5
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
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+20" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            // Additive optical glints cannot attenuate the DMX/gobo layer.
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
            #include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/LightAsset/Lens Shader/LensGlassCoating_HDRP.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
    FallBack Off
}
