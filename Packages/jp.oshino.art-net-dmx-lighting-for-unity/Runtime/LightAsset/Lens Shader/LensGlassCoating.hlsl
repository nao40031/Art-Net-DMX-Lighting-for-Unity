#ifndef ARTNET_LENS_GLASS_COATING_INCLUDED
#define ARTNET_LENS_GLASS_COATING_INCLUDED

// A package-contained optical coating. Highlights move with the camera, but do not
// pretend to be scene reflections; reflection probes/SSR remain optional scene features.
void EvaluateLensGlassCoating(float2 uv, float3 normalWS, float3 viewWS,
    float4 glassTint,
    float centerOpacity, float edgeOpacity, float highlightStrength,
    float surfaceMetallic, float surfaceSmoothness,
    out float3 color, out float opacity)
{
    float3 n = normalize(normalWS);
    float3 v = normalize(viewWS);
    float2 lens = (uv - 0.5) * 2.0;
    float radius = saturate(length(lens));
    float rim = smoothstep(0.58, 0.98, radius);
    float grazing = pow(1.0 - saturate(abs(dot(n, v))), 3.0);

    float2 parallax = reflect(-v, n).xy * 0.11;
    float2 broad = (lens - float2(-0.32, 0.42) - parallax) * float2(0.85, 1.8);
    float2 small = (lens - float2(0.55, 0.27) + parallax * 0.5) * float2(2.4, 2.0);
    // Metallic raises the reflected contribution and
    // smoothness tightens the optical highlight without requiring a screen grab.
    float lobeSharpness = lerp(1.0, 2.4, saturate(surfaceSmoothness));
    float reflection = exp(-dot(broad, broad) * 3.0 * lobeSharpness) * 0.7
                     + exp(-dot(small, small) * 5.0 * lobeSharpness) * 0.3;
    reflection *= max(0.0, highlightStrength) * lerp(1.0, 1.65, saturate(surfaceMetallic));

    opacity = saturate(centerOpacity + edgeOpacity * (rim * 0.7 + grazing * 0.3)
                       + reflection * 0.27);
    float3 coolHighlight = float3(0.62, 0.78, 0.9);
    color = glassTint.rgb * (0.12 + rim * 0.22 + grazing * 0.12)
          + coolHighlight * reflection * 0.7;
}

#endif
