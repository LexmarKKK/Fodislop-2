#nullable enable

namespace Kern.World.Terrain;

// Зеркало Assets/Shaders/Terrain/Cells/TerrainCellFormat.hlsl — только данные.
// Источник правды — шейдер: имя здесь — имя оттуда без KERN_TERRAIN_ в
// PascalCase, значение то же. TerrainCellFormatMirrorTests читает HLSL и
// роняет сборку, если хоть одна константа разошлась или пропала.
public static class TerrainCellFormat
{
    // Клетка.
    public const uint CellBits = 8u;
    public const uint CellsPerWord = 4u;

    // Строка типа.
    public const float CellTexels = 32f;
    public const uint TypePixelMask = 0xFFFu;
    public const int TypePixelHighShift = 12;
    public const int TypeByteShift = 24;

    public const uint TypeSlotMask = 7u;
    public const uint TypeBackground = 1u << 3;
    public const uint TypeOpaqueOwn = 1u << 4;
    public const uint TypeOpaqueAny = 1u << 5;
    public const int TypeTextureAnchorShift = 6;
    public const int TypeOutlineShift = 7;
    public const int TypeAnimationTypeShift = 10;
    public const int TypeSurfaceEffectShift = 12;
    public const int TypeSurfaceEffectPaletteShift = 14;
    public const uint TypeSurfaceEffectPaletteMask = 7u;
    public const int TypeDecalAtlasShift = 17;

    public const int TypeGlowShift = 16;

    // Правило декали.
    public const int DecalRuleSeedShift = 7;
    public const uint DecalRuleRock = 1u << 15;

    // Код декали клетки.
    public const uint DecalVariants = 16u;
    public const int DecalRotationShift = 4;
    public const int DecalMirrorShift = 6;
    public const int DecalOffsetXShift = 7;
    public const int DecalOffsetYShift = 9;
    public const uint DecalRockAtlas = 1u << 12;

    // Флаги света.
    public const float GlowFractionScale = 0.25f;
}
