#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/SampleUVMapping.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"

TEXTURE2D(_GoboTexture);
#include "IrisMask.hlsl"
SAMPLER(sampler_GoboTexture);

CBUFFER_START(UnityPerMaterial)
float4 _DmxColor;
float _DmxDimmer;
float4 _IrisShape;
float _IrisLensInfluence;
float _GoboEnabled;
float _GoboRotationDeg;
float4 _GoboOffset;
float _GoboLensInfluence;
float _GoboLensEmission;
float _GoboLensScale;
float _GoboLensBlur;
float _LensApertureFeather;
float _GoboLensHotspotStrength;
float _GoboLensHotspotInner;
float _GoboLensHotspotOuter;
float _GoboLensFresnelStrength;
float _GoboLensFresnelPower;
float _LensPrismGoboMode;
float _LensPrismFacetCount;
float _LensPrismSpread;
float _LensPrismGoboSpacingScale;
float _LensPrismRotationDeg;
float _EmissiveExposureWeight;
CBUFFER_END

float2 RotateGoboLensUv(float2 uv, float degrees)
{
    float angle = radians(-degrees);
    float sineValue;
    float cosineValue;
    sincos(angle, sineValue, cosineValue);
    return mul(float2x2(cosineValue, -sineValue, sineValue, cosineValue), uv - 0.5) + 0.5;
}

float SampleGoboLensTexture(float2 uv)
{
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    return SAMPLE_TEXTURE2D(_GoboTexture, sampler_GoboTexture, uv).r * inside;
}

float SampleBlurredGoboLensTexture(float2 uv, float blurRadius)
{
    if (blurRadius <= 0.00001)
        return SampleGoboLensTexture(uv);

    float2 offset = float2(blurRadius, blurRadius);
    float center = SampleGoboLensTexture(uv) * 0.20;
    float cardinal =
        SampleGoboLensTexture(uv + float2( offset.x, 0.0)) +
        SampleGoboLensTexture(uv + float2(-offset.x, 0.0)) +
        SampleGoboLensTexture(uv + float2(0.0,  offset.y)) +
        SampleGoboLensTexture(uv + float2(0.0, -offset.y));
    float diagonal =
        SampleGoboLensTexture(uv + float2( offset.x,  offset.y)) +
        SampleGoboLensTexture(uv + float2(-offset.x,  offset.y)) +
        SampleGoboLensTexture(uv + float2( offset.x, -offset.y)) +
        SampleGoboLensTexture(uv + float2(-offset.x, -offset.y));
    return center + cardinal * 0.12 + diagonal * 0.08;
}

float SamplePrismGoboLensTexture(float2 lensUv, float goboScale)
{
    int facetCount = clamp((int)round(_LensPrismFacetCount), 1, 8);
    float prismRadius = saturate(_LensPrismSpread) * max(0.0, _LensPrismGoboSpacingScale) * max(0.01, goboScale) * 0.25;
    float facetScale = max(0.1, 1.0 - (2.0 * prismRadius));
    float combinedScale = max(0.01, goboScale * facetScale);
    float result = 0.0;

    [unroll]
    for (int facet = 0; facet < 8; facet++)
    {
        if (facet >= facetCount)
            continue;

        float angle = ((6.28318530718 * facet) / facetCount) + radians(_LensPrismRotationDeg);
        float2 center = 0.5 + float2(cos(angle), sin(angle)) * prismRadius;
        float2 copyUv = ((lensUv - center) / combinedScale) + 0.5 + _GoboOffset.xy;
        copyUv = RotateGoboLensUv(copyUv, _GoboRotationDeg);
        result = max(result, SampleBlurredGoboLensTexture(copyUv, max(0.0, _GoboLensBlur)));
    }

    return result;
}

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
{
    float2 lensUv = input.texCoord0.xy;
    float scale = max(0.01, _GoboLensScale);
    float2 goboUv = RotateGoboLensUv(((lensUv - 0.5) / scale) + 0.5 + _GoboOffset.xy, _GoboRotationDeg);
    float gobo = SampleBlurredGoboLensTexture(goboUv, max(0.0, _GoboLensBlur));
    float prismActive = saturate(_LensPrismGoboMode) * step(1.5, _LensPrismFacetCount);
    if (prismActive > 0.5)
        gobo = SamplePrismGoboLensTexture(lensUv, scale);
    float edgeDistance = min(min(lensUv.x, 1.0 - lensUv.x), min(lensUv.y, 1.0 - lensUv.y));
    float edgeFeather = saturate(_LensApertureFeather);
    float apertureTransmission = edgeFeather > 0.0001 ? smoothstep(0.0, edgeFeather, edgeDistance) : 1.0;
    float goboActive = saturate(_GoboEnabled) * saturate(_GoboLensInfluence);
    float goboMask = lerp(1.0, lerp(1.0, gobo, apertureTransmission), goboActive);

    float lensDistance = length((lensUv - 0.5) * 2.0);
    float hotspot = 1.0 - smoothstep(_GoboLensHotspotInner, max(_GoboLensHotspotInner + 0.0001, _GoboLensHotspotOuter), lensDistance);
    float fresnel = pow(1.0 - saturate(abs(dot(normalize(input.tangentToWorld[2]), normalize(V)))), max(0.01, _GoboLensFresnelPower));
    float brightness = saturate(_DmxDimmer) * (1.0 + hotspot * max(0.0, _GoboLensHotspotStrength));
    float3 lensColor = _DmxColor.rgb * brightness * goboMask;

    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.color = lensColor;
    surfaceData.normalWS = 0.0;

    ZERO_BUILTIN_INITIALIZE(builtinData);
    builtinData.opacity = 1.0;
    builtinData.emissiveColor = lensColor * max(0.0, _GoboLensEmission);
    builtinData.emissiveColor += _DmxColor.rgb * saturate(_DmxDimmer) * fresnel * max(0.0, _GoboLensFresnelStrength);
    float3 emissiveRcpExposure = builtinData.emissiveColor * GetInverseCurrentExposureMultiplier();
    float iris = lerp(1.0, IrisTransmission(lensUv, _IrisShape), saturate(_IrisLensInfluence));
    surfaceData.color *= iris;
    builtinData.emissiveColor *= iris;
    emissiveRcpExposure *= iris;
    builtinData.emissiveColor = lerp(emissiveRcpExposure, builtinData.emissiveColor, _EmissiveExposureWeight);

    ApplyDebugToBuiltinData(builtinData);
    RAY_TRACING_OPTIONAL_ALPHA_TEST_PASS
}
