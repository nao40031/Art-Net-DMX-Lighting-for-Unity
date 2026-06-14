Shader "ArtNet/Prism Pseudo Beam"
{
    Properties
    {
        _DmxColor ("DMX Color", Color) = (1, 1, 1, 1)
        _DmxDimmer ("DMX Dimmer", Float) = 1
        _BeamIntensity ("Beam Intensity", Float) = 1
        _BeamLength ("Beam Length", Float) = 10
        _BeamStartRadius ("Beam Start Radius", Float) = 0.05
        _BeamEndRadius ("Beam End Radius", Float) = 1.5
        _GoboTexture ("Gobo Texture", 2D) = "white" {}
        _GoboRotationDeg ("Gobo Rotation Deg", Float) = 0
        _GoboEnabled ("Gobo Enabled", Float) = 0
        _DmxPrismEnabled ("Prism Enabled", Float) = 0
        _DmxPrismFacetCount ("Prism Facet Count", Float) = 1
        _DmxPrismSpread ("Prism Spread", Float) = 1
        _DmxPrismRotation ("Prism Rotation", Float) = 0
        _DmxPrismIntensity ("Prism Intensity", Float) = 1
        _FacetSharpness ("Facet Sharpness", Float) = 24
        _BeamLengthFade ("Beam Length Fade", Float) = 1
        _BeamStartFade ("Beam Start Fade", Range(0, 1)) = 0
        _BeamEndFade ("Beam End Fade", Range(0, 1)) = 1
        _BeamTipOpacity ("Beam Tip Opacity", Range(0, 1)) = 0
        _BeamFalloffPower ("Beam Falloff Power", Float) = 1
        _BeamEdgeSoftness ("Beam Edge Softness", Range(0, 1)) = 0.35
        _BeamEdgePower ("Beam Edge Power", Float) = 1.5
        _BeamNoiseStrength ("Beam Noise Strength", Range(0, 1)) = 0
        _BeamNoiseScale ("Beam Noise Scale", Float) = 6
        _BeamNoiseSpeed ("Beam Noise Speed", Float) = 0.2
        _BeamNoiseContrast ("Beam Noise Contrast", Float) = 1
        _BeamNoiseVolume ("Beam Noise Volume", 3D) = "" {}
        _BeamNoiseVolumeEnabled ("Beam Noise Volume Enabled", Range(0, 1)) = 0
        _BeamNoiseVolumeStrength ("Beam Noise Volume Strength", Range(0, 1)) = 1
        _BeamNoiseVolumeScale ("Beam Noise Volume Scale", Float) = 1
        _BeamNoiseVolumeRadialScale ("Beam Noise Volume Radial Scale", Float) = 4
        _BeamNoiseVolumeLengthScale ("Beam Noise Volume Length Scale", Float) = 12
        _BeamNoiseVolumeSpeed ("Beam Noise Volume Speed", Float) = 0.1
        _BeamNoiseVolumeContrast ("Beam Noise Volume Contrast", Float) = 1
        _BeamNoiseVolumeOffset ("Beam Noise Volume Offset", Vector) = (0, 0, 0, 0)
        _BeamNoiseVolumeScrollDirection ("Beam Noise Volume Scroll Direction", Vector) = (0, 0, 1, 0)
        _BeamGoboProjection ("Beam Gobo Projection", Range(0, 1)) = 1
        _BeamGoboInfluence ("Beam Gobo Influence", Range(0, 1)) = 0.6
        _BeamGoboContrast ("Beam Gobo Contrast", Float) = 1
        _BeamGoboMinLight ("Beam Gobo Min Light", Range(0, 1)) = 0.2
        _GoboInfluence ("Gobo Influence", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PrismPseudoBeam"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };

            sampler2D _GoboTexture;
            float4 _DmxColor;
            float _DmxDimmer;
            float _BeamIntensity;
            float _BeamLength;
            float _BeamStartRadius;
            float _BeamEndRadius;
            float4 _GoboTexture_ST;
            float _GoboRotationDeg;
            float _GoboEnabled;
            float _DmxPrismEnabled;
            float _DmxPrismFacetCount;
            float _DmxPrismSpread;
            float _DmxPrismRotation;
            float _DmxPrismIntensity;
            float _FacetSharpness;
            float _BeamLengthFade;
            float _BeamStartFade;
            float _BeamEndFade;
            float _BeamTipOpacity;
            float _BeamFalloffPower;
            float _BeamEdgeSoftness;
            float _BeamEdgePower;
            float _BeamNoiseStrength;
            float _BeamNoiseScale;
            float _BeamNoiseSpeed;
            float _BeamNoiseContrast;
            sampler3D _BeamNoiseVolume;
            float _BeamNoiseVolumeEnabled;
            float _BeamNoiseVolumeStrength;
            float _BeamNoiseVolumeScale;
            float _BeamNoiseVolumeRadialScale;
            float _BeamNoiseVolumeLengthScale;
            float _BeamNoiseVolumeSpeed;
            float _BeamNoiseVolumeContrast;
            float4 _BeamNoiseVolumeOffset;
            float4 _BeamNoiseVolumeScrollDirection;
            float _BeamGoboProjection;
            float _BeamGoboInfluence;
            float _BeamGoboContrast;
            float _BeamGoboMinLight;
            float _GoboInfluence;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normalOS);
                output.viewDirWS = _WorldSpaceCameraPos.xyz - positionWS;
                output.positionOS = input.positionOS;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float CircularDistance01(float a, float b)
            {
                float d = abs(frac(a) - frac(b));
                return min(d, 1.0 - d);
            }

            float SampleGobo(float2 uv)
            {
                float2 centered = uv - 0.5;
                float rad = radians(-_GoboRotationDeg);
                float s = sin(rad);
                float c = cos(rad);
                float2 rotated = float2(
                    centered.x * c - centered.y * s,
                    centered.x * s + centered.y * c
                ) + 0.5;

                float inside = step(0.0, rotated.x) * step(rotated.x, 1.0) *
                               step(0.0, rotated.y) * step(rotated.y, 1.0);
                float gobo = tex2D(_GoboTexture, rotated).r;
                return lerp(1.0, gobo * inside, saturate(_GoboEnabled) * saturate(_GoboInfluence));
            }

            float SampleProjectedGobo(float2 uv)
            {
                float angle = uv.x * 6.28318530718 + radians(-_GoboRotationDeg);
                float2 dir = float2(cos(angle), sin(angle));
                float s1 = tex2D(_GoboTexture, 0.5 + dir * 0.12).r;
                float s2 = tex2D(_GoboTexture, 0.5 + dir * 0.24).r;
                float s3 = tex2D(_GoboTexture, 0.5 + dir * 0.36).r;
                float s4 = tex2D(_GoboTexture, 0.5 + dir * 0.48).r;
                float gobo = max(max(s1, s2), max(s3, s4));
                gobo = saturate((gobo - 0.5) * max(0.01, _BeamGoboContrast) + 0.5);
                gobo = lerp(saturate(_BeamGoboMinLight), 1.0, gobo);
                return lerp(1.0, gobo, saturate(_GoboEnabled) * saturate(_BeamGoboInfluence));
            }

            float SampleBeamVolumeNoise(float3 positionOS)
            {
                float depth01 = saturate(positionOS.z / max(0.001, _BeamLength));
                float radiusAtDepth = lerp(max(0.001, _BeamStartRadius), max(0.001, _BeamEndRadius), depth01);
                float2 cross01 = positionOS.xy / max(0.001, radiusAtDepth * 2.0) + 0.5;
                float masterScale = max(0.001, _BeamNoiseVolumeScale);
                float radialScale = masterScale * max(0.001, _BeamNoiseVolumeRadialScale);
                float lengthScale = masterScale * max(0.001, _BeamNoiseVolumeLengthScale);
                float3 volumeUv = float3(cross01 * radialScale, depth01 * lengthScale);
                volumeUv += _BeamNoiseVolumeOffset.xyz;
                float3 scrollDirection = _BeamNoiseVolumeScrollDirection.xyz;
                float scrollLengthSq = dot(scrollDirection, scrollDirection);
                scrollDirection = scrollLengthSq > 0.000001 ? scrollDirection * rsqrt(scrollLengthSq) : float3(0.0, 0.0, 1.0);
                volumeUv += scrollDirection * _Time.y * _BeamNoiseVolumeSpeed;

                float volumeNoise = tex3D(_BeamNoiseVolume, frac(volumeUv)).r;
                return saturate((volumeNoise - 0.5) * max(0.01, _BeamNoiseVolumeContrast) + 0.5);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float dimmer = saturate(_DmxDimmer);
                float prismEnabled = saturate(_DmxPrismEnabled);
                float facetCount = clamp(round(_DmxPrismFacetCount), 1.0, 8.0);
                float spread = max(0.0, _DmxPrismSpread);
                float rotation01 = _DmxPrismRotation / 360.0;
                float sharpness = max(1.0, _FacetSharpness);

                float ringUv = frac(uv.x + rotation01);
                float facetMask = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float active = step((float)i + 0.5, facetCount);
                    float center = ((float)i + 0.5) / facetCount;
                    float distanceToFacet = CircularDistance01(ringUv, center);
                    float width = lerp(0.09, 0.022, saturate(spread));
                    float facet = exp2(-distanceToFacet * distanceToFacet * sharpness / max(width, 0.001));
                    facetMask += facet * active;
                }

                facetMask = saturate(facetMask);
                float legacyLengthFade = pow(saturate(1.0 - uv.y), max(0.01, _BeamLengthFade));
                float fadeStart = saturate(_BeamStartFade);
                float fadeEnd = max(fadeStart + 0.001, saturate(_BeamEndFade));
                float fadeT = saturate((uv.y - fadeStart) / max(0.001, fadeEnd - fadeStart));
                fadeT = pow(fadeT, max(0.01, _BeamFalloffPower));
                float distanceFade = lerp(1.0, saturate(_BeamTipOpacity), fadeT);
                float lengthFade = legacyLengthFade * distanceFade;
                float normalFacing = saturate(abs(dot(normalize(input.normalWS), normalize(input.viewDirWS))));
                float edgeFade = lerp(1.0, pow(normalFacing, max(0.01, _BeamEdgePower)), saturate(_BeamEdgeSoftness));
                float noiseScale = max(0.001, _BeamNoiseScale);
                float2 noiseUv = float2(
                    uv.x * noiseScale + _Time.y * _BeamNoiseSpeed,
                    uv.y * noiseScale * 0.37 - _Time.y * _BeamNoiseSpeed * 0.23
                );
                float noise = ValueNoise(noiseUv);
                noise = saturate((noise - 0.5) * max(0.01, _BeamNoiseContrast) + 0.5);
                float volumeBlend = saturate(_BeamNoiseVolumeEnabled * _BeamNoiseVolumeStrength);
                if (volumeBlend > 0.0001)
                {
                    float volumeNoise = SampleBeamVolumeNoise(input.positionOS);
                    noise = lerp(noise, volumeNoise, volumeBlend);
                }
                float noiseMask = lerp(1.0, noise, saturate(_BeamNoiseStrength));
                float uvGobo = SampleGobo(uv);
                float projectedGobo = SampleProjectedGobo(uv);
                float gobo = lerp(uvGobo, projectedGobo, saturate(_BeamGoboProjection));
                float beam = lerp(1.0, facetMask, prismEnabled) * lengthFade * edgeFade * noiseMask * gobo;
                float intensity = dimmer * max(0.0, _DmxPrismIntensity) * max(0.0, _BeamIntensity) * beam;

                return float4(_DmxColor.rgb * intensity, intensity);
            }
            ENDCG
        }
    }
}
