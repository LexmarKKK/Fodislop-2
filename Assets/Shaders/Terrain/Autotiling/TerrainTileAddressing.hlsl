#ifndef KERN_TERRAIN_TILE_ADDRESSING_INCLUDED
#define KERN_TERRAIN_TILE_ADDRESSING_INCLUDED

static const float KernTileAddressEpsilon = 0.0001;

// Tile addresses are integers. Floating fmod is lowered to reciprocal/multiply
// on some GPUs and loses exact remainders at large world coordinates.
int KernPositiveCellModulo(int value, int modulus)
{
    int remainder = value % modulus;
    return remainder < 0 ? remainder + modulus : remainder;
}

float2 KernResolveTerrainTileIndex(
    float2 gridPosition,
    float2 tileCount,
    float tileGroupColumn,
    float useTileGroupColumn)
{
    int2 cell = (int2)round(gridPosition);
    int2 count = (int2)round(tileCount);
    int tileX = useTileGroupColumn > 0.5
        ? (int)round(tileGroupColumn)
        : KernPositiveCellModulo(cell.x, count.x);
    int tileY = count.y - 1 - KernPositiveCellModulo(cell.y, count.y);
    return (float2)clamp(int2(tileX, tileY), int2(0, 0), count - 1);
}

float2 KernResolveTerrainSheetUv(
    float2 gridPosition,
    float2 localUv,
    float2 tileCount,
    float tileGroupColumn,
    float useTileGroupColumn)
{
    float2 tileIndex = KernResolveTerrainTileIndex(
        gridPosition,
        tileCount,
        tileGroupColumn,
        useTileGroupColumn);
    float2 safeLocalUv = clamp(
        localUv,
        KernTileAddressEpsilon,
        1.0 - KernTileAddressEpsilon);
    return (tileIndex + safeLocalUv) / tileCount;
}

#endif
