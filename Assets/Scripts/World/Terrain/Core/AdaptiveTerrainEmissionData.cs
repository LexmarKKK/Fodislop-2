#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Kern.World.Terrain;

/// <summary>Linear emission pyramid; built once per source image, without Unity object access.</summary>
public sealed class AdaptiveTerrainEmissionData
{
    public const int LevelCount = 6;
    public Color[][] Levels
    {
        get;
    }
    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public float Gain
    {
        get;
    }
    public float DarkMean
    {
        get;
    }
    public float BrightMean
    {
        get;
    }

    private AdaptiveTerrainEmissionData(int width, int height, Color[][] levels, float gain, float darkMean, float brightMean)
    {
        Width = width;
        Height = height;
        Levels = levels;
        Gain = gain;
        DarkMean = darkMean;
        BrightMean = brightMean;
    }

    public static AdaptiveTerrainEmissionData Build(Color[] source, int width, int height, float alphaCutoff,
        CancellationToken cancellationToken = default, TerrainEmissionMethod method = TerrainEmissionMethod.AdaptiveTexture)
    {
        if (method is not (TerrainEmissionMethod.AdaptiveTexture or TerrainEmissionMethod.LocalDetail or TerrainEmissionMethod.ChromaSeparation))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }
        if (alphaCutoff < 0f || alphaCutoff > 1f || !Finite(alphaCutoff))
        {
            throw new ArgumentOutOfRangeException(nameof(alphaCutoff));
        }
        if (width <= 0 || height <= 0 || width % 32 != 0 || height % 32 != 0 || source.Length != checked(width * height))
        {
            throw new ArgumentException("Emission source must contain complete 32-pixel cells.");
        }
        double peak = 0;
        foreach (Color color in source)
        {
            if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a) ||
                color.r < 0 || color.g < 0 || color.b < 0 || color.a < 0 || color.a > 1)
            {
                throw new InvalidDataException("Emission source contains invalid linear RGBA values.");
            }
            if (color.a >= alphaCutoff)
            {
                peak = Math.Max(peak, Brightness(color));
            }
        }
        var histogram = new double[256];
        if (peak > 0)
        {
            foreach (Color color in source)
            {
                if (color.a < alphaCutoff)
                {
                    continue;
                }
                int bin = (int)Math.Round(Brightness(color) / peak * 255);
                histogram[bin] += color.a;
            }
        }
        double totalWeight = 0;
        double totalMoment = 0;
        for (int index = 0; index < histogram.Length; index++)
        {
            totalWeight += histogram[index];
            totalMoment += histogram[index] * index;
        }
        double lowerWeight = 0;
        double lowerMoment = 0;
        double bestScore = -1;
        double dark = 0;
        double bright = 0;
        for (int split = 0; split < 255; split++)
        {
            lowerWeight += histogram[split];
            lowerMoment += histogram[split] * split;
            double upperWeight = totalWeight - lowerWeight;
            if (lowerWeight <= 0 || upperWeight <= 0)
            {
                continue;
            }
            double lowerMean = lowerMoment / lowerWeight;
            double upperMean = (totalMoment - lowerMoment) / upperWeight;
            double score = lowerWeight * upperWeight * (upperMean - lowerMean) * (upperMean - lowerMean);
            if (score > bestScore)
            {
                bestScore = score;
                dark = lowerMean * peak / 255;
                bright = upperMean * peak / 255;
            }
        }
        // A uniform source has no separable host/detail classes. Its authored
        // glow remains uniform; this is the defined one-class distribution.
        bool uniform = bestScore < 0;
        var mask = new float[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            double t = uniform ? 1 : Math.Max(0, Math.Min(1, (Brightness(source[index]) - dark) / (bright - dark)));
            mask[index] = source[index].a >= alphaCutoff ? (float)(t * t * (3 - 2 * t)) : 0f;
        }
        TerrainEmissionMasks.Refine(mask, source, width, height, alphaCutoff, method, cancellationToken);
        var levels = new Color[LevelCount][];
        levels[0] = new Color[source.Length];
        double originalEnergy = 0;
        double maskedEnergy = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if ((index & 4095) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            Color color = source[index];
            if (color.a < alphaCutoff)
            {
                continue;
            }
            float weight = mask[index] * color.a;
            levels[0][index] = new Color(color.r * weight, color.g * weight, color.b * weight, weight);
            originalEnergy += Luminance(color) * color.a;
            maskedEnergy += Luminance(levels[0][index]);
        }
        if (originalEnergy > 0 && maskedEnergy <= 0)
        {
            throw new InvalidDataException("Adaptive emission removed all energy from a nonzero source.");
        }
        float gain = originalEnergy > 0 ? (float)(originalEnergy / maskedEnergy) : 1f;
        if (!Finite(gain) || gain <= 0f)
        {
            throw new InvalidDataException("Emission normalization cannot be represented as a finite positive coefficient.");
        }
        for (int level = 1; level < LevelCount; level++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int w = width >> level;
            int h = height >> level;
            int previousWidth = w * 2;
            levels[level] = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int first = y * 2 * previousWidth + x * 2;
                    levels[level][y * w + x] = (levels[level - 1][first] + levels[level - 1][first + 1] +
                        levels[level - 1][first + previousWidth] + levels[level - 1][first + previousWidth + 1]) * 0.25f;
                }
            }
        }
        return new AdaptiveTerrainEmissionData(width, height, levels, gain, (float)dark, (float)bright);
    }

    public void Append(int type, List<Color> samples, Vector4[] headers)
    {
        for (int level = 0; level < LevelCount; level++)
        {
            headers[type * LevelCount + level] = new Vector4(samples.Count, Width >> level, Height >> level, Gain);
            samples.AddRange(Levels[level]);
        }
    }

    private static float Brightness(Color color) => Math.Max(color.r, Math.Max(color.g, color.b));
    private static double Luminance(Color color) => color.r * 0.2126 + color.g * 0.7152 + color.b * 0.0722;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
