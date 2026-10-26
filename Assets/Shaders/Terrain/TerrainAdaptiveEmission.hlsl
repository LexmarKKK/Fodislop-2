#ifndef KERN_TERRAIN_ADAPTIVE_EMISSION_INCLUDED
#define KERN_TERRAIN_ADAPTIVE_EMISSION_INCLUDED
#if defined(KERN_EMISSION_ADAPTIVE)
StructuredBuffer<float4> _TerrainEmissionHeaders;
StructuredBuffer<float4> _TerrainEmissionSamples;

float4 TerrainAdaptiveEmissionLevel(uint type, float2 sourceUv, uint level, out float gain)
{
    float4 header = _TerrainEmissionHeaders[type * 6u + level];
    gain = header.w;
    if (header.y <= 0.0 || header.z <= 0.0) { return 0.0; }
    uint2 size = (uint2)round(header.yz);
    uint2 pixel = min((uint2)floor(saturate(sourceUv) * float2(size)), size - 1u);
    return _TerrainEmissionSamples[(uint)round(header.x) + pixel.y * size.x + pixel.x];
}

float4 TerrainAdaptiveEmission(uint type, float2 sourceUv, float lod, out float gain)
{
    float level = clamp(lod, 0.0, 5.0);
    uint first = (uint)floor(level);
    float4 a = TerrainAdaptiveEmissionLevel(type, sourceUv, first, gain);
    float fraction = frac(level);
    if (fraction < 0.0001 || first == 5u) { return a; }
    float secondGain;
    float4 b = TerrainAdaptiveEmissionLevel(type, sourceUv, first + 1u, secondGain);
    return lerp(a, b, fraction);
}
#endif
#endif
