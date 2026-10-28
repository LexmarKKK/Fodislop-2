#nullable enable

using UnityEngine;

namespace Kern.Effects;

/// <summary>One sprite submission produced by the simulation. The sink owns the actual rendering.</summary>
public readonly struct EffectSpriteDraw
{
    public EffectSpriteDraw(
        string spriteSheet,
        int spriteSheetColumns,
        int spriteSheetRows,
        int frameIndex,
        Vector2 position,
        float sizeUnits,
        Color color,
        int sortingOrder)
    {
        SpriteSheet = spriteSheet;
        SpriteSheetColumns = spriteSheetColumns;
        SpriteSheetRows = spriteSheetRows;
        FrameIndex = frameIndex;
        Position = position;
        SizeUnits = sizeUnits;
        Color = color;
        SortingOrder = sortingOrder;
    }

    public string SpriteSheet { get; }
    public int SpriteSheetColumns { get; }
    public int SpriteSheetRows { get; }
    public int FrameIndex { get; }
    public Vector2 Position { get; }
    public float SizeUnits { get; }
    public Color Color { get; }
    public int SortingOrder { get; }
}
