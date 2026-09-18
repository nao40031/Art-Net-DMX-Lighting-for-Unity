#ifndef ARTNET_IRIS_MASK
#define ARTNET_IRIS_MASK
// x: diameter ratio, y: edge feather, z: polygon sides (0 = circle), w: orientation in radians
float IrisBoundary(float angle, float4 shape)
{
    if (shape.z < 3.0) return shape.x;
    float sector = 6.28318530718 / shape.z;
    float a = frac((angle - shape.w) / sector + 0.5) * sector - sector * 0.5;
    return shape.x * cos(sector * 0.5) / max(0.0001, cos(a));
}
float IrisTransmission(float2 uv, float4 shape)
{
    if (shape.x >= 0.99999) return 1.0;
    if (shape.x <= 0.000001) return 0.0;
    float2 p = (uv - 0.5) * 2.0;
    float edge = IrisBoundary(atan2(p.y, p.x), shape);
    float feather = max(0.0001, min(shape.y, edge));
    return 1.0 - smoothstep(max(0.0, edge - feather), edge, length(p));
}
#endif
