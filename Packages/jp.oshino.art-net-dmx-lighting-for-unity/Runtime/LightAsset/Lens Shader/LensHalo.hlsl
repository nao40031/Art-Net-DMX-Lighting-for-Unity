#ifndef ARTNET_LENS_HALO_INCLUDED
#define ARTNET_LENS_HALO_INCLUDED

float3 EvaluateLensHalo(float2 uv, float3 dmxColor, float dmxDimmer,
    float3 haloTint, float haloIntensity, float irisDiameter, float irisInfluence)
{
    float radius = length(uv * 2.0 - 1.0);
    float core = exp2(-10.0 * radius * radius) * 0.18;
    float ringDistance = radius - 0.55;
    float ring = exp2(-18.0 * ringDistance * ringDistance) * 0.7;
    // Keep the visible halo inside the source lens aperture at the MAC Ultra
    // prefab's default halo size (1.55), while preserving the ring at 0.55.
    float edgeFade = 1.0 - smoothstep(0.56, 0.64, radius);
    float aperture = lerp(1.0, saturate(irisDiameter * irisDiameter), saturate(irisInfluence));
    return max(0.0, dmxColor) * max(0.0, haloTint) *
        (core + ring) * edgeFade * max(0.0, haloIntensity) *
        max(0.0, dmxDimmer) * aperture;
}

#endif
