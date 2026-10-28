#nullable enable

using System;
using System.Collections.Generic;

namespace Kern.World.Lighting;

// Local ray bounds are only one input to a cascade dispatch. Every near tier
// also reads four far probes at its receiver position. FillCascadeChain carries
// those dependencies from far to near before any tier is skipped or clipped.
// The shader's changed-entry mask still avoids tracing unchanged ray paths.
public readonly record struct ProbeRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public static class CascadeProbeRects
{
    public static void FillCascadeChain(
        int fieldMinX,
        int fieldMinY,
        int fieldMaxXExclusive,
        int fieldMaxYExclusive,
        int fieldWidth,
        int fieldHeight,
        IReadOnlyList<CascadeLayout> cascades,
        float materialMarginTexels,
        Span<ProbeRect> rectangles)
    {
        if (rectangles.Length < cascades.Count)
        {
            throw new ArgumentException("Every cascade needs a dispatch rectangle.", nameof(rectangles));
        }

        for (int index = cascades.Count - 1; index >= 0; index--)
        {
            CascadeLayout cascade = cascades[index];
            if (cascade.ProbeSpacing <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cascades), "Probe spacing must be positive.");
            }

            ProbeRect rect = ForCascade(
                fieldMinX, fieldMinY, fieldMaxXExclusive, fieldMaxYExclusive,
                fieldWidth, fieldHeight, cascade, materialMarginTexels + cascade.ProbeSpacing);
            if (index + 1 < cascades.Count && !rectangles[index + 1].IsEmpty)
            {
                CascadeLayout far = cascades[index + 1];
                ProbeRect farRect = rectangles[index + 1];

                // floor(origin / farSpacing - .5) and its +1 neighbour are
                // sampled with clamping in SolveCascade. Include their entire
                // receiver support, including the outer clamped probe rows.
                int minX = farRect.X == 0 ? 0 : Clamp(
                    (int)Math.Floor((farRect.X - 0.5) * far.ProbeSpacing / cascade.ProbeSpacing - 0.5),
                    0, cascade.ProbeWidth);
                int minY = farRect.Y == 0 ? 0 : Clamp(
                    (int)Math.Floor((farRect.Y - 0.5) * far.ProbeSpacing / cascade.ProbeSpacing - 0.5),
                    0, cascade.ProbeHeight);
                int maxX = farRect.X + farRect.Width >= far.ProbeWidth ? cascade.ProbeWidth : Clamp(
                    (int)Math.Ceiling((farRect.X + farRect.Width + 0.5) * far.ProbeSpacing / cascade.ProbeSpacing - 0.5),
                    0, cascade.ProbeWidth);
                int maxY = farRect.Y + farRect.Height >= far.ProbeHeight ? cascade.ProbeHeight : Clamp(
                    (int)Math.Ceiling((farRect.Y + farRect.Height + 0.5) * far.ProbeSpacing / cascade.ProbeSpacing - 0.5),
                    0, cascade.ProbeHeight);
                if (!rect.IsEmpty)
                {
                    minX = Math.Min(minX, rect.X);
                    minY = Math.Min(minY, rect.Y);
                    maxX = Math.Max(maxX, rect.X + rect.Width);
                    maxY = Math.Max(maxY, rect.Y + rect.Height);
                }

                rect = new ProbeRect(minX, minY, maxX - minX, maxY - minY);
            }

            rectangles[index] = rect;
        }
    }

    public static ProbeRect ForCascade(
        int fieldMinX,
        int fieldMinY,
        int fieldMaxXExclusive,
        int fieldMaxYExclusive,
        int fieldWidth,
        int fieldHeight,
        CascadeLayout cascade,
        float marginTexels)
    {
        if (fieldWidth <= 0 || fieldHeight <= 0 ||
            cascade.ProbeWidth <= 0 || cascade.ProbeHeight <= 0)
        {
            return Full(cascade);
        }

        float reach = cascade.IntervalEnd > 0f ? cascade.IntervalEnd : 0f;
        float expand = reach + (marginTexels > 0f ? marginTexels : 0f);
        float minX = fieldMinX - expand;
        float minY = fieldMinY - expand;
        float maxX = fieldMaxXExclusive + expand;
        float maxY = fieldMaxYExclusive + expand;

        float scaleX = (float)cascade.ProbeWidth / fieldWidth;
        float scaleY = (float)cascade.ProbeHeight / fieldHeight;
        int probeMinX = Clamp((int)System.Math.Floor(minX * scaleX), 0, cascade.ProbeWidth);
        int probeMinY = Clamp((int)System.Math.Floor(minY * scaleY), 0, cascade.ProbeHeight);
        int probeMaxX = Clamp((int)System.Math.Ceiling(maxX * scaleX), 0, cascade.ProbeWidth);
        int probeMaxY = Clamp((int)System.Math.Ceiling(maxY * scaleY), 0, cascade.ProbeHeight);

        if (probeMaxX <= probeMinX || probeMaxY <= probeMinY)
        {
            return new ProbeRect(0, 0, 0, 0);
        }

        return new ProbeRect(probeMinX, probeMinY, probeMaxX - probeMinX, probeMaxY - probeMinY);
    }

    public static ProbeRect Full(CascadeLayout cascade)
    {
        return new ProbeRect(0, 0, cascade.ProbeWidth, cascade.ProbeHeight);
    }

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : (value > max ? max : value);
    }
}
