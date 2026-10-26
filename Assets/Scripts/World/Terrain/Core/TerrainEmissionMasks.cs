#nullable enable

using System;
using System.Threading;
using UnityEngine;

namespace Kern.World.Terrain;

/// <summary>Source-only scalar masks; no material identities or authored color lists.</summary>
internal static class TerrainEmissionMasks
{
    private const int ChromaBins = 8;
    private const int DetailRadiusPixels = 2;

    public static void Refine(float[] mask, Color[] source, int width, int height,
        float alphaCutoff, TerrainEmissionMethod method, CancellationToken token)
    {
        if (method == TerrainEmissionMethod.LocalDetail)
        {
            RefineDetails(mask, source, width, height, alphaCutoff, token);
        }
        else if (method == TerrainEmissionMethod.ChromaSeparation)
        {
            SeparateChroma(mask, source, alphaCutoff, token);
        }
    }

    private static void RefineDetails(float[] mask, Color[] source, int width, int height,
        float alphaCutoff, CancellationToken token)
    {
        var brightness = new float[source.Length];
        var horizontal = new float[source.Length];
        var opened = new float[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            brightness[index] = source[index].a >= alphaCutoff
                ? Math.Max(source[index].r, Math.Max(source[index].g, source[index].b)) : 0f;
        }
        Filter(brightness, horizontal, width, height, horizontalAxis: true, minimum: true, token);
        Filter(horizontal, opened, width, height, horizontalAxis: false, minimum: true, token);
        Filter(opened, horizontal, width, height, horizontalAxis: true, minimum: false, token);
        Filter(horizontal, opened, width, height, horizontalAxis: false, minimum: false, token);
        bool hasDetails = false;
        for (int index = 0; index < source.Length; index++)
        {
            hasDetails |= brightness[index] > opened[index];
        }
        // A constant local feature has no separable detail class. Its scalar
        // weight is unity, including uniform authored glow and flat RGB mosaics.
        if (!hasDetails)
        {
            return;
        }
        for (int index = 0; index < source.Length; index++)
        {
            float value = brightness[index];
            float detail = value > 0f ? Math.Max(0f, value - opened[index]) / value : 0f;
            // Equal base/detail layers retain broad emitters while attenuating
            // broad highlights relative to small bright inclusions. RGB ratios
            // remain unchanged; this is a coefficient, not an HDR color clamp.
            mask[index] *= (1f + detail) * 0.5f;
        }
    }

    private static void Filter(float[] source, float[] destination, int width, int height,
        bool horizontalAxis, bool minimum, CancellationToken token)
    {
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int coordinate = horizontalAxis ? x : y;
                int start = coordinate / 32 * 32;
                float value = source[y * width + x];
                for (int offset = -DetailRadiusPixels; offset <= DetailRadiusPixels; offset++)
                {
                    int sample = Math.Max(start, Math.Min(start + 31, coordinate + offset));
                    float next = horizontalAxis ? source[y * width + sample] : source[sample * width + x];
                    value = minimum ? Math.Min(value, next) : Math.Max(value, next);
                }
                destination[y * width + x] = value;
            }
        }
    }

    private static void SeparateChroma(float[] mask, Color[] source, float alphaCutoff, CancellationToken token)
    {
        var populations = new double[ChromaBins * ChromaBins * ChromaBins];
        var sums = new Vector3[populations.Length];
        double totalWeight = 0;
        int occupiedBins = 0;
        for (int index = 0; index < source.Length; index++)
        {
            Color color = source[index];
            if (color.a < alphaCutoff || color.r + color.g + color.b <= 0f)
            {
                continue;
            }
            Vector3 chroma = Chromaticity(color);
            int bin = ChromaBin(chroma);
            if (populations[bin] == 0)
            {
                occupiedBins++;
            }
            populations[bin] += color.a;
            sums[bin] += chroma * color.a;
            totalWeight += color.a;
        }
        if (occupiedBins <= 1)
        {
            return;
        }
        double strongest = 0;
        Vector3 hostCentroids = Vector3.zero;
        int strongestRegions = 0;
        // Symmetric RGB histogram density, rather than an rg projection:
        // rotating RGB channels must rotate the result, not change the mask.
        for (int blue = 0; blue < ChromaBins; blue++)
        {
            token.ThrowIfCancellationRequested();
            for (int green = 0; green < ChromaBins; green++)
            {
                for (int red = 0; red < ChromaBins; red++)
                {
                    double weight = 0;
                    Vector3 sum = Vector3.zero;
                    for (int b = Math.Max(0, blue - 1); b <= Math.Min(ChromaBins - 1, blue + 1); b++)
                    {
                        for (int g = Math.Max(0, green - 1); g <= Math.Min(ChromaBins - 1, green + 1); g++)
                        {
                            for (int r = Math.Max(0, red - 1); r <= Math.Min(ChromaBins - 1, red + 1); r++)
                            {
                                int bin = r + g * ChromaBins + b * ChromaBins * ChromaBins;
                                weight += populations[bin];
                                sum += sums[bin];
                            }
                        }
                    }
                    if (weight > strongest)
                    {
                        strongest = weight;
                        hostCentroids = sum / (float)weight;
                        strongestRegions = 1;
                    }
                    else if (weight == strongest && weight > 0)
                    {
                        hostCentroids += sum / (float)weight;
                        strongestRegions++;
                    }
                }
            }
        }
        // No majority hue means no identified host class. The color model adds
        // no exclusion; a balanced RGB mixture is not assigned an arbitrary host.
        if (totalWeight == 0 || strongest <= totalWeight * 0.5)
        {
            return;
        }
        Vector3 host = hostCentroids / strongestRegions;
        var distances = new float[source.Length];
        double peak = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if ((index & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }
            Color color = source[index];
            if (color.a < alphaCutoff || color.r + color.g + color.b <= 0f)
            {
                continue;
            }
            distances[index] = (Chromaticity(color) - host).sqrMagnitude;
            peak = Math.Max(peak, distances[index]);
        }
        if (peak <= 0)
        {
            return;
        }
        var histogram = new double[256];
        for (int index = 0; index < source.Length; index++)
        {
            Color color = source[index];
            if (color.a >= alphaCutoff && color.r + color.g + color.b > 0f)
            {
                histogram[(int)Math.Round(distances[index] / peak * 255)] += color.a;
            }
        }
        (double low, double high, bool separated) = ClassMeans(histogram, peak);
        if (!separated)
        {
            return;
        }
        for (int index = 0; index < source.Length; index++)
        {
            Color color = source[index];
            double t = color.a >= alphaCutoff && color.r + color.g + color.b > 0f
                ? Math.Max(0, Math.Min(1, (distances[index] - low) / (high - low))) : 0;
            // This model uses color separation, independently of the absolute
            // brightness of the minority detail. A dim green/red inclusion in
            // a bright blue host must not be lost to a brightness precondition.
            mask[index] = (float)(t * t * (3 - 2 * t));
        }
    }

    private static Vector3 Chromaticity(Color color)
    {
        double sum = (double)color.r + color.g + color.b;
        return new Vector3((float)(color.r / sum), (float)(color.g / sum), (float)(color.b / sum));
    }

    private static int ChromaBin(Vector3 color) =>
        Math.Min(ChromaBins - 1, (int)(color.x * ChromaBins)) +
        Math.Min(ChromaBins - 1, (int)(color.y * ChromaBins)) * ChromaBins +
        Math.Min(ChromaBins - 1, (int)(color.z * ChromaBins)) * ChromaBins * ChromaBins;

    private static (double Low, double High, bool Separated) ClassMeans(double[] histogram, double scale)
    {
        double weight = 0;
        double moment = 0;
        for (int index = 0; index < histogram.Length; index++)
        {
            weight += histogram[index];
            moment += histogram[index] * index;
        }
        double leftWeight = 0;
        double leftMoment = 0;
        double best = -1;
        double low = 0;
        double high = 0;
        for (int split = 0; split < 255; split++)
        {
            leftWeight += histogram[split];
            leftMoment += histogram[split] * split;
            double rightWeight = weight - leftWeight;
            if (leftWeight <= 0 || rightWeight <= 0)
            {
                continue;
            }
            double a = leftMoment / leftWeight;
            double b = (moment - leftMoment) / rightWeight;
            double score = leftWeight * rightWeight * (b - a) * (b - a);
            if (score > best)
            {
                best = score;
                low = a * scale / 255;
                high = b * scale / 255;
            }
        }
        return (low, high, best >= 0);
    }
}
