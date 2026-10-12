#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
#include "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/LightAsset/Lens Shader/LensGlassCoating.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _GlassTint;
float _CenterOpacity;
float _EdgeOpacity;
float _HighlightStrength;
CBUFFER_END

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput,
    out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
{
    float3 coatingColor;
    float coatingOpacity;
    EvaluateLensGlassCoating(input.texCoord0.xy, input.tangentToWorld[2], V,
        _GlassTint,
        _CenterOpacity, _EdgeOpacity, _HighlightStrength,
        coatingColor, coatingOpacity);

    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.color = 0.0;
    surfaceData.normalWS = 0.0;
    ZERO_BUILTIN_INITIALIZE(builtinData);
    builtinData.opacity = 1.0;
    builtinData.emissiveColor = coatingColor * coatingOpacity * GetInverseCurrentExposureMultiplier();
    ApplyDebugToBuiltinData(builtinData);
    RAY_TRACING_OPTIONAL_ALPHA_TEST_PASS
}
