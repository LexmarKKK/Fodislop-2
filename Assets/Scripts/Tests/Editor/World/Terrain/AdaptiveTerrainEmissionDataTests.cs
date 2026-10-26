#nullable enable

using Kern.World.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.World;

[TestFixture]
public sealed class AdaptiveTerrainEmissionDataTests
{
    [Test]
    public void LocalDetailAttenuatesBroadHighlightRelativeToSmallInclusion()
    {
        Color[] source = SourceWithBlueHostAndGreenDetail(1f, 0.2f);
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(source, 32, 32, 0.05f,
            method: TerrainEmissionMethod.LocalDetail);
        Assert.That(data.Levels[0][3 * 32 + 3].g, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(data.Levels[0][3 * 32 + 24].b, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(data.Levels[0][0].maxColorComponent, Is.LessThan(0.000001f));
        AssertConserved(source, data);
    }

    [Test]
    public void ChromaSeparationRejectsBrightHostAndKeepsDimDifferentHue()
    {
        Color[] source = SourceWithBlueHostAndGreenDetail(0.1f, 0.5f);
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(source, 32, 32, 0.05f,
            method: TerrainEmissionMethod.ChromaSeparation);
        Assert.That(data.Levels[0][3 * 32 + 3].g, Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(data.Levels[0][3 * 32 + 24].b, Is.Zero);
        Assert.That(data.Levels[0][0].maxColorComponent, Is.LessThan(0.000001f));
        AssertConserved(source, data);
    }

    [TestCase(TerrainEmissionMethod.AdaptiveTexture)]
    [TestCase(TerrainEmissionMethod.LocalDetail)]
    [TestCase(TerrainEmissionMethod.ChromaSeparation)]
    public void CachedMethodsRetainBalancedRgbMixturesAndUniformDimEmitters(TerrainEmissionMethod method)
    {
        Color[] palette = { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.white };
        var pixels = new Color[32 * 32];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = palette[index % palette.Length];
        }
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f, method: method);
        for (int index = 0; index < pixels.Length; index++)
        {
            Assert.That(data.Levels[0][index], Is.EqualTo(pixels[index]));
            pixels[index] = new Color(0.04f, 0.02f, 0.01f, 1f);
        }
        data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f, method: method);
        Assert.That(data.Gain, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(data.Levels[0][0], Is.EqualTo(pixels[0]));
    }

    private static Color[] SourceWithBlueHostAndGreenDetail(float green, float host)
    {
        var source = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                source[y * 32 + x] = x >= 16 ? Color.blue : new Color(0, 0, host, 1);
                if (x >= 2 && x < 6 && y >= 2 && y < 6)
                {
                    source[y * 32 + x] = new Color(0, green, 0, 1);
                }
            }
        }
        return source;
    }

    private static void AssertConserved(Color[] source, AdaptiveTerrainEmissionData data)
    {
        double original = 0;
        double emitted = 0;
        for (int index = 0; index < source.Length; index++)
        {
            Color before = source[index];
            Color after = data.Levels[0][index];
            original += (before.r * 0.2126 + before.g * 0.7152 + before.b * 0.0722) * before.a;
            emitted += (after.r * 0.2126 + after.g * 0.7152 + after.b * 0.0722) * data.Gain;
        }
        Assert.That(emitted, Is.EqualTo(original).Within(0.0001));
    }

    [Test]
    public void UnrepresentableNormalizationFailsInsteadOfPublishingInfinity()
    {
        var source = new Color[32 * 32];
        for (int index = 0; index < source.Length; index++)
        {
            source[index] = index < 16 ? new Color(0, 0.001f, 0, 1) : new Color(0, 0, 3e38f, 1);
        }
        Assert.Throws<System.IO.InvalidDataException>(() => AdaptiveTerrainEmissionData.Build(source, 32, 32, 0.05f,
            method: TerrainEmissionMethod.ChromaSeparation));
    }

    [Test]
    public void ChromaMaskIsCovariantUnderRgbChannelRotation()
    {
        Color[] original = SourceWithBlueHostAndGreenDetail(0.3f, 0.2f);
        var rotated = new Color[original.Length];
        for (int index = 0; index < original.Length; index++)
        {
            Color color = original[index];
            rotated[index] = new Color(color.g, color.b, color.r, color.a);
        }
        AdaptiveTerrainEmissionData before = AdaptiveTerrainEmissionData.Build(original, 32, 32, 0.05f,
            method: TerrainEmissionMethod.ChromaSeparation);
        AdaptiveTerrainEmissionData after = AdaptiveTerrainEmissionData.Build(rotated, 32, 32, 0.05f,
            method: TerrainEmissionMethod.ChromaSeparation);
        for (int index = 0; index < original.Length; index++)
        {
            Assert.That(after.Levels[0][index].r, Is.EqualTo(before.Levels[0][index].g).Within(0.000001f));
            Assert.That(after.Levels[0][index].g, Is.EqualTo(before.Levels[0][index].b).Within(0.000001f));
            Assert.That(after.Levels[0][index].b, Is.EqualTo(before.Levels[0][index].r).Within(0.000001f));
        }
    }

    [Test]
    public void UniformDarkEmitterKeepsItsAuthoredEnergy()
    {
        var pixels = new Color[32 * 32];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = new Color(0.04f, 0.02f, 0.01f, 1f);
        }
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f);
        Assert.That(data.Gain, Is.EqualTo(1f).Within(0.00001f));
        foreach (Color sample in data.Levels[0])
        {
            Assert.That(sample.r, Is.EqualTo(0.04f));
            Assert.That(sample.g, Is.EqualTo(0.02f));
            Assert.That(sample.b, Is.EqualTo(0.01f));
        }
    }

    [Test]
    public void MaskSeparatesDetailsAndConservesLinearLuminousEnergy()
    {
        var pixels = new Color[32 * 32];
        double energy = 0;
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = index < 64 ? new Color(0, 1, 0, 1) : new Color(0, 0, 0.125f, 1);
            energy += pixels[index].g * 0.7152 + pixels[index].b * 0.0722;
        }
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f);
        double emitted = 0;
        foreach (Color sample in data.Levels[0])
        {
            emitted += (sample.r * 0.2126 + sample.g * 0.7152 + sample.b * 0.0722) * data.Gain;
        }
        Assert.That(emitted, Is.EqualTo(energy).Within(0.0001));
        Assert.That(data.Levels[0][0].g, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(data.Levels[0][100].b, Is.LessThan(0.0001f));
        Assert.That(data.Levels[5][0].g, Is.EqualTo(1f / 16f).Within(0.0001f));
    }

    [Test]
    public void EqualBrightnessRgbMixturesRetainTheirColorRatios()
    {
        Color[] palette = { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.white };
        foreach (Color color in palette)
        {
            var pixels = new Color[32 * 32];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }
            AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f);
            Color emitted = data.Levels[5][0] * data.Gain;
            Assert.That(emitted.r, Is.EqualTo(color.r).Within(0.0001f));
            Assert.That(emitted.g, Is.EqualTo(color.g).Within(0.0001f));
            Assert.That(emitted.b, Is.EqualTo(color.b).Within(0.0001f));
        }
    }

    [Test]
    public void MixedRgbMosaicUsesOneMaskWithoutChannelBias()
    {
        Color[] palette = { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.white };
        var pixels = new Color[32 * 32];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = palette[index % palette.Length];
        }
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 32, 32, 0.05f);
        Assert.That(data.Gain, Is.EqualTo(1f).Within(0.0001f));
        for (int index = 0; index < pixels.Length; index++)
        {
            Assert.That(data.Levels[0][index], Is.EqualTo(pixels[index]));
        }
    }

    [Test]
    public void ReductionStopsAtCellBoundariesAndKeepsDistinctRegions()
    {
        var pixels = new Color[64 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                pixels[y * 64 + x] = x < 32 ? Color.red : Color.blue;
            }
        }
        AdaptiveTerrainEmissionData data = AdaptiveTerrainEmissionData.Build(pixels, 64, 32, 0.05f);
        Assert.That(data.Levels[5], Has.Length.EqualTo(2));
        Assert.That(data.Levels[5][0], Is.EqualTo(Color.red));
        Assert.That(data.Levels[5][1], Is.EqualTo(Color.blue));
    }
}
