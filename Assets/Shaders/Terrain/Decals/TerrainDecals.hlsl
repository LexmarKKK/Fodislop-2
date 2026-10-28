#ifndef KERN_TERRAIN_DECALS_INCLUDED
#define KERN_TERRAIN_DECALS_INCLUDED

#include "Assets/Shaders/Terrain/Cells/TerrainCellFormat.hlsl"

TEXTURE2D(_TerrainDecalAtlas);
SAMPLER(sampler_TerrainDecalAtlas);

TEXTURE2D(_TerrainDecalRockAtlas);
SAMPLER(sampler_TerrainDecalRockAtlas);

TEXTURE2D(_TerrainDecalSandAtlas);
SAMPLER(sampler_TerrainDecalSandAtlas);

float2 TerrainTransformDecalUV(float2 uv, uint rotation, bool mirror)
{
    if (mirror)
    {
        uv.x = 1.0 - uv.x;
    }

    if (rotation == 1u)
    {
        return float2(uv.y, 1.0 - uv.x);
    }

    if (rotation == 2u)
    {
        return 1.0 - uv;
    }

    if (rotation == 3u)
    {
        return float2(1.0 - uv.y, uv.x);
    }

    return uv;
}

// Код декали — DecalCode в TerrainCellFormat.hlsl.

// All atlases use hard light on linear surface color before illumination.

float3 ApplyTerrainDecal(float3 baseColor, float2 localUV, float packedFamily, float packedPlacement)
{
    if (packedPlacement < 0.5)
    {
        return baseColor;
    }

    uint family = (uint)round(packedFamily);
    bool useSand = family == KERN_TERRAIN_DECAL_ATLAS_SAND;
    bool useRock = family == KERN_TERRAIN_DECAL_ATLAS_ROCK;
    uint code = (uint)round(packedPlacement) - 1u;

    uint variant   = code & KERN_TERRAIN_DECAL_VARIANT_MASK;
    uint rotation  = (code >> KERN_TERRAIN_DECAL_ROTATION_SHIFT) & 3u;
    bool mirror    = ((code >> KERN_TERRAIN_DECAL_MIRROR_SHIFT) & 1u) != 0u;
    uint offsetX   = (code >> KERN_TERRAIN_DECAL_OFFSET_X_SHIFT) & 3u;
    uint offsetY   = (code >> KERN_TERRAIN_DECAL_OFFSET_Y_SHIFT) & 3u;

    float2 transformedUV = TerrainTransformDecalUV(localUV, rotation, mirror);
    float2 placementOffset = float2(offsetX, offsetY) / 3.0 - 0.5;
    // Rock artwork fits the rounded cell only while centred. Ground artwork
    // has enough padding for the material's bounded two-pixel translation.
    transformedUV += placementOffset * (useRock ? 0.0 : _DecalPlacementOffset);

    // Sixteen horizontal 32x32 slots in a 512x32 atlas. Sampling texel centres
    // prevents a transformed edge from crossing into the neighbouring slot.
    float2 pixel   = float2(variant * 32.0, 0.0) +
        clamp(saturate(transformedUV) * 32.0, 0.5, 31.5);
    float2 atlasUV = pixel / float2(512.0, 32.0);

    half4 decal;
    if (useRock)
    {
        decal = SAMPLE_TEXTURE2D_LOD(
            _TerrainDecalRockAtlas,
            sampler_TerrainDecalRockAtlas,
            atlasUV,
            0.0);
    }
    else if (useSand)
    {
        decal = SAMPLE_TEXTURE2D_LOD(
            _TerrainDecalSandAtlas,
            sampler_TerrainDecalSandAtlas,
            atlasUV,
            0.0);
    }
    else
    {
        decal = SAMPLE_TEXTURE2D_LOD(
            _TerrainDecalAtlas,
            sampler_TerrainDecalAtlas,
            atlasUV,
            0.0);
    }

    float strength = (useRock || useSand) ? _RockDecalStrength : _GroundDecalStrength;
    float3 darken = baseColor * (2.0 * decal.rgb);
    float3 lighten = 1.0 - (1.0 - baseColor) * (2.0 - 2.0 * decal.rgb);
    float3 hardLight = lerp(darken, lighten, step(0.5, decal.rgb));
    return lerp(baseColor, hardLight, decal.a * strength);
}

#endif
