#ifndef KERN_TERRAIN_DEPTH_INCLUDED
#define KERN_TERRAIN_DEPTH_INCLUDED

static const float KERN_TERRAIN_PRESENTATION_EYE_HEIGHT = 32.0;

// POSITION-only wire format for side vertices. Integer addresses and biased
// 1/32-cell endpoints are exactly representable; no optional mesh UV stream.
float4 TerrainVolumeDecodeAddress(float4 packedAddress, out float4 edgeEndpoints)
{
    float2 cell = floor(packedAddress.xy / 128.0);
    float2 start = (packedAddress.xy - cell * 128.0 - 32.0) / 32.0;
    float endCode = round(frac(packedAddress.z) * 16384.0);
    float2 end = float2(floor(endCode / 128.0), fmod(endCode, 128.0));
    edgeEndpoints = float4(start, (end - 32.0) / 32.0);
    return float4(cell, 2.0, packedAddress.w);
}

float2 TerrainDepthOffset(float2 fromCameraCells, float depthPixels)
{
    float pixels = min(max(round(depthPixels), 0.0), 16.0);
    float2 offsetPixels = round(fromCameraCells * pixels / KERN_TERRAIN_PRESENTATION_EYE_HEIGHT);
    if (dot(offsetPixels, offsetPixels) > 256.0)
    {
        offsetPixels = round(offsetPixels / length(offsetPixels) * 16.0);
        if (dot(offsetPixels, offsetPixels) > 256.0)
        {
            if (abs(offsetPixels.x) >= abs(offsetPixels.y))
            {
                offsetPixels.x -= sign(offsetPixels.x);
            }
            else
            {
                offsetPixels.y -= sign(offsetPixels.y);
            }
        }
    }
    return offsetPixels / KERN_TERRAIN_FACE_GRID_SIZE;
}

// Actual vertex projection of the solid's two height planes. The base stays
// fixed; both the side top edge and the face use this same projected vertex.
float2 TerrainVolumeProjectPoint(float2 geometryPosition, float2 camera, float heightFraction, float depthPixels)
{
    float pixels = min(max(round(depthPixels), 0.0), 16.0) * heightFraction;
    float height = pixels / KERN_TERRAIN_FACE_GRID_SIZE;
    float perspective = KERN_TERRAIN_PRESENTATION_EYE_HEIGHT /
        (KERN_TERRAIN_PRESENTATION_EYE_HEIGHT - height);
    return geometryPosition + TerrainDepthOffset((geometryPosition - camera) * perspective, pixels);
}

float2 TerrainVolumeOutwardNormal(float2 start, float2 end)
{
    float2 edge = end - start;
    return float2(edge.y, -edge.x) / length(edge);
}

float2 TerrainVolumeLightingPoint(float2 edgePoint, float2 outwardNormal)
{
    return edgePoint - outwardNormal / 64.0;
}

float2 TerrainVolumeMaterialPoint(float2 edgePoint, float2 outwardNormal, float heightFraction, float depthPixels)
{
    return TerrainVolumeLightingPoint(edgePoint, outwardNormal) - outwardNormal *
        (heightFraction * min(max(round(depthPixels), 0.0), 16.0) / 32.0);
}

float TerrainDepthFaceCoverage(TerrainSurfaceInputs surface)
{
    surface.anchored = 1.0;
    return EvaluateTerrainCellCoverage(surface, 1.0, 1.0);
}

#endif
