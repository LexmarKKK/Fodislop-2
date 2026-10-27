#nullable enable

using System.Collections.Generic;
using Kern.Core;
using Kern.World.Lighting.Quality;
using UnityEngine;

using Kern.Diagnostics.World;

namespace Kern.World.Lighting.Diagnostics;

/// <summary>Снимок кадра, по которому диагностика может о нём судить.</summary>
///
/// Передаётся параметром, а не читается из движка: диагностика обязана
/// описывать кадр, а не участвовать в нём.
internal readonly record struct LightingDiagnosticsContext(
    bool Initialized,
    LightingQualityMode Quality,
    Vector4 WorldRect,
    float CellSize,
    int MaximumIntervalSteps);

/// <summary>
/// Отчётность освещения: стоимость каскадов и дамп кадра.
/// </summary>
///
/// Вынесено из LightingEngine целиком. Это не путь кадра: ни один метод здесь
/// не участвует в решении света и не может на него повлиять. Держать их рядом
/// с кадром значило каждый раз перечитывать сто строк отчётности, чтобы найти
/// двадцать строк работы.
internal sealed class LightingDiagnosticsReporter
{
    private readonly LightingResourceManager _resources;
    private readonly IFrameTelemetry _telemetry;

    public LightingDiagnosticsReporter(
        LightingResourceManager resources,
        IFrameTelemetry telemetry)
    {
        _resources = resources;
        _telemetry = telemetry;
    }

    public void CollectCascadeCosts(
        List<CascadeCostSample> destination,
        in LightingDiagnosticsContext context)
    {
        CascadeCostCalculator.CollectCascadeCosts(
            _resources.Cascades, context.MaximumIntervalSteps, destination);
    }

    public string? DumpCurrentFrame(
        in LightingDiagnosticsContext context,
        string? targetDirectory)
    {
        if (!context.Initialized || _resources.Registry.Compute == null)
        {
            return null;
        }

        return LightingFrameDumper.DumpCurrentFrame(
            _resources.Registry,
            context.WorldRect,
            context.CellSize,
            context.Quality,
            _telemetry,
            _resources.LightingCounters,
            targetDirectory);
    }

    /// <summary>Раскладка каждого каскада строками — для окна инструментов.</summary>
    public static IReadOnlyList<string> DescribeCascadeUniforms(IReadOnlyList<CascadeLayout> cascades)
    {
        var summaries = new List<string>(cascades.Count);
        for (int index = 0; index < cascades.Count; index++)
        {
            CascadeLayout cascade = cascades[index];
            summaries.Add(
                $"Cascade {index}: offset={cascade.Offset}, entries={cascade.EntryCount}, " +
                $"probe={cascade.ProbeWidth}x{cascade.ProbeHeight}, spacing={cascade.ProbeSpacing}, " +
                $"directions={cascade.DirectionCount}, interval={cascade.IntervalStart:F2}..{cascade.IntervalEnd:F2}");
        }

        return summaries;
    }
}
