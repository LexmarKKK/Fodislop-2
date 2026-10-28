#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kern.World.Lighting;
public static class CascadeLayoutBuilder
{
    // Safe default for every caller. A larger angular cap must be an explicit
    // experiment; an omitted optional argument must never create a frame-sized
    // transport burst by accident.
    public const int DefaultMaximumCascadeDirections = 64;

    public static int GetMaximumCascadeCount(long atlasDimension)
    {
        // A fourth interval is cheaper than extending the last cascade over
        // the whole remaining diagonal. Resource validation rejects layouts
        // that do not fit the explicitly configured atlas.
        return 4;
    }

    public static long CalculateCascadeEntryCount(
        int width,
        int height,
        int maximumCascadeCount,
        int maximumDirections = DefaultMaximumCascadeDirections)
    {
        if (maximumCascadeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCascadeCount));
        }

        float requiredDistance = (float)Math.Sqrt(
            (double)width * width + (double)height * height);
        long entryCount = 0;
        int spacing = 1;
        int directions = 4;
        float intervalEnd = 1f;

        while (true)
        {
            int probeWidth = Mathf.CeilToInt(width / (float)spacing);
            int probeHeight = Mathf.CeilToInt(height / (float)spacing);
            entryCount += (long)probeWidth * probeHeight * directions;

            if (intervalEnd >= requiredDistance || maximumCascadeCount == 1)
            {
                return entryCount;
            }

            maximumCascadeCount--;
            spacing *= 2;
            directions = Mathf.Min(maximumDirections, directions * 4);
            intervalEnd *= 4f;
        }
    }

    public static void BuildCascadeLayouts(
        int width,
        int height,
        long atlasDimension,
        List<CascadeLayout> cascades,
        int maximumDirections = DefaultMaximumCascadeDirections,
        int initialProbeSpacing = 1)
    {
        if (initialProbeSpacing < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(initialProbeSpacing));
        }
        cascades.Clear();
        float requiredDistance = (float)Math.Sqrt(
            (double)width * width + (double)height * height);
        int maxCascades = GetMaximumCascadeCount(atlasDimension);
        int offset = 0;
        int spacing = initialProbeSpacing;
        int directions = 4;
        float intervalStart = 0f;
        float intervalEnd = initialProbeSpacing;

        while (true)
        {
            int probeWidth = Mathf.CeilToInt(width / (float)spacing);
            int probeHeight = Mathf.CeilToInt(height / (float)spacing);
            long entryCountLong = (long)probeWidth * probeHeight * directions;

            if (entryCountLong > int.MaxValue - offset)
            {
                throw new InvalidOperationException("Radiance cascade atlas exceeds the supported buffer size.");
            }

            int entryCount = (int)entryCountLong;
            // The final interval covers the remaining field diagonal with
            // the explicitly selected angular and spatial resolution.
            float traceEnd = cascades.Count + 1 >= maxCascades
                ? Mathf.Max(intervalEnd, requiredDistance)
                : intervalEnd;
            cascades.Add(new CascadeLayout(
                offset,
                entryCount,
                probeWidth,
                probeHeight,
                spacing,
                directions,
                intervalStart,
                traceEnd));

            offset += entryCount;
            if (cascades.Count >= maxCascades || intervalEnd >= requiredDistance)
            {
                break;
            }

            spacing *= 2;
            directions = Mathf.Min(maximumDirections, directions * 4);
            intervalStart = intervalEnd;
            intervalEnd *= 4f;
        }
    }
}
