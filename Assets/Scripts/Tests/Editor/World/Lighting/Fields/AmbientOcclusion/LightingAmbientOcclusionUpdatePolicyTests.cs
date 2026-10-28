#nullable enable

using System;
using System.Collections.Generic;
using Kern.World.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.Editor;

public sealed class LightingAmbientOcclusionUpdatePolicyTests
{
    [Test]
    public void CommittedDig_IsPartialEvenThoughActivationMarksFieldDirty()
    {
        var state = new LightingRuntimeState { FieldDirty = false, StagedTerrainGeometryRevision = 7 };
        bool fieldWasDirty = state.FieldDirty;
        state.QueueRegionInvalidation(new RectInt(10, 20, 1, 1));
        state.ActivatePendingRegionIfVisible(new RectInt(0, 0, 192, 128));
        Assert.That(state.FieldDirty, Is.True);
        Assert.That(LightingAmbientOcclusionUpdatePolicy.CanUpdatePartially(
            state, 7, fieldWasDirty, false, false, false), Is.True);
    }

    [TestCase(true, false, false, false, 7UL)]
    [TestCase(false, true, false, false, 7UL)]
    [TestCase(false, false, true, false, 7UL)]
    [TestCase(false, false, false, true, 7UL)]
    [TestCase(false, false, false, false, 8UL)]
    public void InvalidRetainedField_RequiresFullRaster(
        bool fieldWasDirty, bool resized, bool moved, bool contributorChanged, ulong revision)
    {
        var state = new LightingRuntimeState { StagedTerrainGeometryRevision = 7 };
        state.QueueRegionInvalidation(new RectInt(10, 20, 1, 1));
        state.ActivatePendingRegionIfVisible(new RectInt(0, 0, 192, 128));
        Assert.That(LightingAmbientOcclusionUpdatePolicy.CanUpdatePartially(
            state, revision, fieldWasDirty, resized, moved, contributorChanged), Is.False);
    }

    [Test]
    public void StableFrame_DoesNotSchedulePartialRaster()
    {
        var state = new LightingRuntimeState { FieldDirty = false, StagedTerrainGeometryRevision = 7 };
        Assert.That(LightingAmbientOcclusionUpdatePolicy.CanUpdatePartially(
            state, 7, false, false, false, false), Is.False);
    }

    [Test]
    public void SingleDig_PreservesFieldOutsideSevenCellSupport()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(10, 20, 1, 1) }, new RectInt(0, 0, 192, 128), 32, false);
        AssertRect(rect, 224, 544, 224, 224);
        Assert.That((long)rect.width * rect.height, Is.EqualTo(50_176));
    }

    [Test]
    public void TopDownRenderTarget_MirrorsOnlyTheRowOfTheSameSupport()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(10, 20, 1, 1) }, new RectInt(0, 0, 192, 128), 32, true);
        AssertRect(rect, 224, 4096 - 544 - 224, 224, 224);
    }

    [Test]
    public void TopDownUpperEdge_MapsWorldTopToRowZero()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(0, 7, 8, 1) }, new RectInt(0, 0, 8, 8), 32, true);
        AssertRect(rect, 0, 0, 256, 128);
    }

    [Test]
    public void NegativeOriginAndLowerEdge_ClipInWorldSpaceBeforePixelMapping()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(-10, -20, 1, 1) }, new RectInt(-10, -20, 8, 8), 32, false);
        AssertRect(rect, 0, 0, 128, 128);
    }

    [Test]
    public void UpperEdge_UsesBottomLeftRasterRowsAndHalfOpenBounds()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(7, 7, 1, 1) }, new RectInt(0, 0, 8, 8), 32, false);
        AssertRect(rect, 128, 128, 128, 128);
    }

    [Test]
    public void NearbyEdits_MergeWhenTheUnionAddsNoRasterPixels()
    {
        IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            new[] { new RectInt(5, 5, 1, 1), new RectInt(9, 5, 1, 1) }, new RectInt(0, 0, 32, 32), 32, false);
        Assert.That(rects, Has.Count.EqualTo(1));
        AssertRect(rects[0], 64, 64, 352, 224);
    }

    [Test]
    public void DistantEdits_RasterizeSeparateSupports()
    {
        IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            new[] { new RectInt(5, 5, 1, 1), new RectInt(20, 20, 1, 1) }, new RectInt(0, 0, 32, 32), 32, false);
        Assert.That(rects, Has.Count.EqualTo(2));
        AssertRect(rects[0], 64, 64, 224, 224);
        AssertRect(rects[1], 544, 544, 224, 224);
    }

    [Test]
    public void NineDistantEdits_PreserveSeparateSupportsInsteadOfRasterizingTheirBoundingBox()
    {
        var edits = new RectInt[9];
        for (int i = 0; i < edits.Length; i++)
        {
            edits[i] = new RectInt(5 + (i * 10), 5 + (i * 10), 1, 1);
        }

        IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            edits, new RectInt(0, 0, 128, 128), 32, false);
        Assert.That(rects, Has.Count.EqualTo(9));
        long rasterPixels = 0;
        for (int i = 0; i < rects.Count; i++)
        {
            AssertRect(rects[i], 64 + (i * 320), 64 + (i * 320), 224, 224);
            rasterPixels += (long)rects[i].width * rects[i].height;
        }

        Assert.That(rasterPixels, Is.EqualTo(451_584));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MergingPreservesDirtySupportWithoutIncreasingTotalRasterArea(bool rowsTopDown)
    {
        var random = new System.Random(90210);
        var field = new RectInt(-8, -6, 32, 32);
        for (int iteration = 0; iteration < 32; iteration++)
        {
            var edits = new RectInt[16];
            long separatePixels = 0;
            for (int i = 0; i < edits.Length; i++)
            {
                edits[i] = new RectInt(random.Next(-14, 30), random.Next(-12, 32), random.Next(1, 6), random.Next(1, 6));
                int width = Math.Max(0, Math.Min(24, edits[i].xMax + 3) - Math.Max(-8, edits[i].xMin - 3));
                int height = Math.Max(0, Math.Min(26, edits[i].yMax + 3) - Math.Max(-6, edits[i].yMin - 3));
                separatePixels += (long)width * height * 4;
            }

            IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
                edits, field, 2, rowsTopDown);
            long mergedPixels = 0;
            var covered = new bool[64, 64];
            foreach (RectInt rect in rects)
            {
                mergedPixels += (long)rect.width * rect.height;
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    for (int x = rect.xMin; x < rect.xMax; x++)
                    {
                        covered[x, y] = true;
                    }
                }
            }

            Assert.That(mergedPixels, Is.LessThanOrEqualTo(separatePixels));
            for (int y = 0; y < 64; y++)
            {
                float worldY = -6f + ((rowsTopDown ? 63 - y : y) + 0.5f) / 2f;
                for (int x = 0; x < 64; x++)
                {
                    float worldX = -8f + (x + 0.5f) / 2f;
                    foreach (RectInt edit in edits)
                    {
                        if (worldX >= edit.xMin - 3 && worldX < edit.xMax + 3 &&
                            worldY >= edit.yMin - 3 && worldY < edit.yMax + 3)
                        {
                            Assert.That(covered[x, y], Is.True, $"Support lost at {x},{y}; iteration {iteration}.");
                            break;
                        }
                    }
                }
            }
        }
    }

    [Test]
    public void EditJustOutsideField_StillUpdatesItsContactSupport()
    {
        RectInt rect = ResolveSingleRect(new[] { new RectInt(-1, 4, 1, 1) }, new RectInt(0, 0, 8, 8), 32, false);
        AssertRect(rect, 0, 32, 96, 224);
    }

    [Test]
    public void DistantEdit_DoesNotTouchField()
    {
        IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            new[] { new RectInt(-10, -10, 1, 1) }, new RectInt(0, 0, 8, 8), 32, false);
        Assert.That(rects, Is.Empty);
    }

    [Test]
    public void InvalidEdit_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            new[] { new RectInt(0, 0, 0, 1) }, new RectInt(0, 0, 8, 8), 32, false));
    }

    private static RectInt ResolveSingleRect(
        IReadOnlyList<RectInt> edits,
        RectInt field,
        int pixelsPerCell,
        bool rowsTopDown)
    {
        IReadOnlyList<RectInt> rects = LightingAmbientOcclusionUpdatePolicy.ResolveRasterRects(
            edits, field, pixelsPerCell, rowsTopDown);
        Assert.That(rects, Has.Count.EqualTo(1));
        return rects[0];
    }

    private static void AssertRect(RectInt rect, int x, int y, int width, int height)
    {
        Assert.That(rect.x, Is.EqualTo(x));
        Assert.That(rect.y, Is.EqualTo(y));
        Assert.That(rect.width, Is.EqualTo(width));
        Assert.That(rect.height, Is.EqualTo(height));
    }
}
