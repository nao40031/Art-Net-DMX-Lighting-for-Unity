Shader "ArtNet/URP/Gobo Lens Surface"
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
        _LensPrismGoboMode ("Lens Prism Gobo Mode", Float) = 1
        _LensPrismFacetCount ("Lens Prism Facet Count", Float) = 1
        _LensPrismSpread ("Lens Prism Spread", Float) = 0
        _LensPrismGoboSpacingScale ("Lens Prism Gobo Spacing Scale", Float) = 1
        _LensPrismRotationDeg ("Lens Prism Rotation", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_GoboTexture);
            SAMPLER(sampler_GoboTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _DmxColor;
                float _DmxDimmer;
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
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

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

            float2 RotateGoboLensUv(float2 uv, float degrees)
            {
                float sineValue; float cosineValue;
                sincos(radians(-degrees), sineValue, cosineValue);
                return mul(float2x2(cosineValue, -sineValue, sineValue, cosineValue), uv - 0.5) + 0.5;
            }

            float SampleGoboLensTexture(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return SAMPLE_TEXTURE2D(_GoboTexture, sampler_GoboTexture, uv).r * inside;
            }

            float SampleBlurredGoboLensTexture(float2 uv, float blurRadius)
            {
                if (blurRadius <= 0.00001) return SampleGoboLensTexture(uv);
                float2 offset = float2(blurRadius, blurRadius);
                float center = SampleGoboLensTexture(uv) * 0.20;
                float cardinal = SampleGoboLensTexture(uv + float2( offset.x, 0.0)) + SampleGoboLensTexture(uv + float2(-offset.x, 0.0)) + SampleGoboLensTexture(uv + float2(0.0,  offset.y)) + SampleGoboLensTexture(uv + float2(0.0, -offset.y));
                float diagonal = SampleGoboLensTexture(uv + float2( offset.x,  offset.y)) + SampleGoboLensTexture(uv + float2(-offset.x,  offset.y)) + SampleGoboLensTexture(uv + float2( offset.x, -offset.y)) + SampleGoboLensTexture(uv + float2(-offset.x, -offset.y));
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 lensUv = input.uv;
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
                float3 viewDirection = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), viewDirection))), max(0.01, _GoboLensFresnelPower));
                float brightness = saturate(_DmxDimmer) * (1.0 + hotspot * max(0.0, _GoboLensHotspotStrength));
                float3 lensColor = _DmxColor.rgb * brightness * goboMask;
                lensColor *= max(0.0, _GoboLensEmission);
                lensColor += _DmxColor.rgb * saturate(_DmxDimmer) * fresnel * max(0.0, _GoboLensFresnelStrength);
                return half4(lensColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
