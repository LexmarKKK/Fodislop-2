#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kern.Effects;

/// <summary>Validated set of authored effects, addressed by id.</summary>
public sealed class EffectCatalog
{
    private readonly Dictionary<string, EffectDefinition> _definitionsById;

    public EffectCatalog(IEnumerable<EffectDefinition> definitions)
    {
        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        _definitionsById = new Dictionary<string, EffectDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (EffectDefinition definition in definitions)
        {
            Validate(definition);
            if (!_definitionsById.TryAdd(definition.Id, definition))
            {
                throw new InvalidDataException($"Duplicate effect definition '{definition.Id}'.");
            }
        }
    }

    public IReadOnlyCollection<EffectDefinition> Definitions => _definitionsById.Values;

    public bool TryGet(string effectId, out EffectDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            definition = null!;
            return false;
        }

        return _definitionsById.TryGetValue(effectId, out definition);
    }

    public static EffectCatalog Load(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("An effect catalog resource path is required.", nameof(resourcePath));
        }

        TextAsset asset = Resources.Load<TextAsset>(resourcePath)
            ?? throw new InvalidOperationException($"Effect catalog is missing at Resources/{resourcePath}.");
        EffectCatalogDocument document = JsonUtility.FromJson<EffectCatalogDocument>(asset.text)
            ?? throw new InvalidDataException($"Effect catalog at Resources/{resourcePath} is invalid.");
        if (document.Effects.Length == 0)
        {
            throw new InvalidDataException($"Effect catalog at Resources/{resourcePath} is empty.");
        }

        return new EffectCatalog(document.Effects);
    }

    public static void Validate(EffectDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidDataException("Every effect definition requires an id.");
        }

        if (!IsPositiveFinite(definition.DurationSeconds))
        {
            throw new InvalidDataException(
                $"Effect '{definition.Id}' requires a positive finite duration, got {definition.DurationSeconds}.");
        }

        if (definition.Emitters == null || definition.Emitters.Length == 0)
        {
            throw new InvalidDataException($"Effect '{definition.Id}' requires at least one emitter.");
        }

        var emitterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EffectEmitterDefinition emitter in definition.Emitters)
        {
            ValidateEmitter(definition.Id, emitter, emitterIds);
        }
    }

    private static void ValidateEmitter(
        string effectId,
        EffectEmitterDefinition emitter,
        HashSet<string> emitterIds)
    {
        if (emitter == null)
        {
            throw new InvalidDataException($"Effect '{effectId}' contains a null emitter.");
        }

        if (!string.IsNullOrWhiteSpace(emitter.Id) && !emitterIds.Add(emitter.Id))
        {
            throw new InvalidDataException($"Effect '{effectId}' has duplicate emitter '{emitter.Id}'.");
        }

        if (string.IsNullOrWhiteSpace(emitter.SpriteSheet))
        {
            throw new InvalidDataException($"Effect '{effectId}' has an emitter without a sprite sheet.");
        }

        if (emitter.SpriteSheetColumns <= 0 || emitter.SpriteSheetRows <= 0)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' requires positive sheet columns and rows.");
        }

        if (emitter.FrameCount <= 0 || emitter.FrameCount > emitter.SpriteSheetColumns * emitter.SpriteSheetRows)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' declares frame count {emitter.FrameCount} " +
                $"for {emitter.SpriteSheetColumns}x{emitter.SpriteSheetRows} sheet cells.");
        }

        if (!IsNonNegativeFinite(emitter.FramesPerSecond) || !IsNonNegativeFinite(emitter.RatePerSecond))
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has a negative or non-finite rate.");
        }

        if (emitter.BurstCount < 0 || (emitter.BurstCount == 0 && emitter.RatePerSecond <= 0f))
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' must emit at least one particle.");
        }

        RequireOrderedRange(effectId, emitter, "lifetime", emitter.LifetimeSeconds);
        if (emitter.LifetimeSeconds.x <= 0f)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' requires a positive lifetime.");
        }

        RequireOrderedRange(effectId, emitter, "speed", emitter.SpeedUnitsPerSecond);
        RequireOrderedRange(effectId, emitter, "direction", emitter.DirectionDegrees);
        RequireOrderedRange(effectId, emitter, "start size", emitter.StartSizeRange);
        RequireOrderedRange(effectId, emitter, "end size", emitter.EndSizeRange);
        if (emitter.StartSizeRange.x < 0f || emitter.EndSizeRange.x < 0f)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has a negative size.");
        }

        if (!IsFinite(emitter.GravityUnitsPerSecondSquared) ||
            !IsNonNegativeFinite(emitter.DragPerSecond) ||
            !IsNonNegativeFinite(emitter.ShapeExtents.x) ||
            !IsNonNegativeFinite(emitter.ShapeExtents.y))
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has a non-finite or negative motion value.");
        }

        if (!Enum.IsDefined(typeof(EffectEmitterShape), emitter.Shape))
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has an unknown shape {(int)emitter.Shape}.");
        }

        RequireColor(effectId, emitter, "start", emitter.StartColor);
        RequireColor(effectId, emitter, "end", emitter.EndColor);
    }

    private static void RequireOrderedRange(
        string effectId,
        EffectEmitterDefinition emitter,
        string name,
        Vector2 range)
    {
        if (!IsFinite(range.x) || !IsFinite(range.y) || range.x > range.y)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has an invalid {name} range " +
                $"{range.x}..{range.y}.");
        }
    }

    private static void RequireColor(
        string effectId,
        EffectEmitterDefinition emitter,
        string name,
        Color color)
    {
        if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b) || !IsFinite(color.a) ||
            color.r < 0f || color.g < 0f || color.b < 0f || color.a < 0f)
        {
            throw new InvalidDataException(
                $"Effect '{effectId}' emitter '{emitter.Id}' has an invalid {name} color.");
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsNonNegativeFinite(float value) => IsFinite(value) && value >= 0f;

    private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;

    [Serializable]
    private sealed class EffectCatalogDocument
    {
        public EffectDefinition[] Effects = Array.Empty<EffectDefinition>();
    }
}
