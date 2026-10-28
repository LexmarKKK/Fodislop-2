#nullable enable

using System;
using UnityEngine;

namespace Kern.Effects;

public enum EffectEmitterShape
{
    Point = 0,
    Circle = 1,
    Box = 2,
}

/// <summary>Authored effect graph: one effect owns a set of emitters.</summary>
[Serializable]
public sealed class EffectDefinition
{
    public string Id = string.Empty;
    public float DurationSeconds = 1f;
    public bool Loop = false;
    public EffectEmitterDefinition[] Emitters = Array.Empty<EffectEmitterDefinition>();
}

/// <summary>Authored emitter. All ranges are inclusive pairs and are sampled deterministically.</summary>
[Serializable]
public sealed class EffectEmitterDefinition
{
    public string Id = string.Empty;
    public string SpriteSheet = string.Empty;
    public int SpriteSheetColumns = 1;
    public int SpriteSheetRows = 1;
    public int FrameCount = 1;
    public float FramesPerSecond = 0f;
    public int SortingOrder = 0;
    public float RatePerSecond = 0f;
    public int BurstCount = 0;
    public Vector2 LifetimeSeconds = new(0.5f, 0.5f);
    public Vector2 SpeedUnitsPerSecond = new(1f, 1f);
    public Vector2 DirectionDegrees = new(0f, 360f);
    public float GravityUnitsPerSecondSquared = 0f;
    public float DragPerSecond = 0f;
    public Vector2 StartSizeRange = new(1f, 1f);
    public Vector2 EndSizeRange = new(1f, 1f);
    public Color StartColor = Color.white;
    public Color EndColor = new(1f, 1f, 1f, 0f);
    public EffectEmitterShape Shape = EffectEmitterShape.Point;
    public Vector2 ShapeExtents = Vector2.zero;
}
