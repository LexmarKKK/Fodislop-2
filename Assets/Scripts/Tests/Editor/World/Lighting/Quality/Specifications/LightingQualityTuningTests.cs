#nullable enable

using System;
using Kern.World.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests;

[TestFixture]
public sealed class LightingQualityTuningTests
{
    [Test]
    public void UnchangedSettingsDoNotAdvanceRevision()
    {
        ulong revision = LightingQualityTuningController.Revision;
        LightingQualityTuningController.Apply(LightingQualityTuningController.Current);
        Assert.That(LightingQualityTuningController.Revision, Is.EqualTo(revision));
    }

    [Test]
    public void ValidSettingsArePublishedAtomically()
    {
        LightingQualityTuning original = LightingQualityTuningController.Current;
        try
        {
            var requested = new LightingQualityTuning(8, 4, 2, 32, 1f, 4, 2, 32);
            if (requested == original)
            {
                requested = new LightingQualityTuning(8, 4, 2, 32, 1f, 8, 2, 32);
            }
            ulong revision = LightingQualityTuningController.Revision;
            LightingQualityTuningController.Apply(requested);
            Assert.That(LightingQualityTuningController.Current, Is.EqualTo(requested));
            Assert.That(LightingQualityTuningController.Revision, Is.EqualTo(revision + 1));
            Assert.That(LightingConfigHolder.AmbientOcclusionPixelsPerCell, Is.EqualTo(32));
        }
        finally
        {
            LightingQualityTuningController.Apply(original);
        }
    }

    [TestCase("field")]
    [TestCase("light")]
    [TestCase("light above field")]
    [TestCase("probe")]
    [TestCase("static angles")]
    [TestCase("near zero")]
    [TestCase("near nan")]
    [TestCase("near infinity")]
    [TestCase("samples")]
    [TestCase("emitter")]
    [TestCase("polar")]
    public void InvalidSettingsPreserveCurrentValuesAndRevision(string input)
    {
        LightingQualityTuning original = LightingQualityTuningController.Current;
        ulong revision = LightingQualityTuningController.Revision;
        LightingQualityTuning invalid = input switch
        {
            "field" => CreateTuning(original, field: 3),
            "light" => CreateTuning(original, light: 3),
            "light above field" => CreateTuning(original, field: 4, light: 8),
            "probe" => CreateTuning(original, probes: 32),
            "static angles" => CreateTuning(original, staticAngles: 128),
            "near zero" => CreateTuning(original, near: 0f),
            "near nan" => CreateTuning(original, near: float.NaN),
            "near infinity" => CreateTuning(original, near: float.PositiveInfinity),
            "samples" => CreateTuning(original, samples: 0),
            "emitter" => CreateTuning(original, emitter: 5),
            "polar" => CreateTuning(original, polar: 63),
            _ => throw new ArgumentException(input),
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => LightingQualityTuningController.Apply(invalid));
        Assert.That(LightingQualityTuningController.Current, Is.EqualTo(original));
        Assert.That(LightingQualityTuningController.Revision, Is.EqualTo(revision));
    }

    [Test]
    public void MaximumRequestedQualityIsPreservedExactly()
    {
        LightingQualityTuning original = LightingQualityTuningController.Current;
        try
        {
            var requested = new LightingQualityTuning(32, 32, 16, 64, 6f, 64, 4, 1024);
            LightingQualityTuningController.Apply(requested);
            Assert.That(LightingQualityTuningController.Current, Is.EqualTo(requested));
            Assert.That(LightingQualityTuningController.DynamicPolarDirectionCount, Is.EqualTo(1024));
        }
        finally
        {
            LightingQualityTuningController.Apply(original);
        }
    }

    [Test]
    public void VisibleLightsBeyondConfiguredCapacityAreRejectedInsteadOfDiscarded()
    {
        var lights = new DynamicLightManager();
        lights.EnsureCapacity(1);
        lights.SetDynamicLight(1, new Vector2(4f, 4f), Color.white, 1f);
        lights.SetDynamicLight(2, new Vector2(5f, 4f), Color.white, 1f);

        Assert.Throws<InvalidOperationException>(() => lights.UploadDynamicLights(
            null!, null, new Vector4(0f, 0f, 16f, 16f), 1f, out _));
    }

    [TestCase(1024, 1024)]
    [TestCase(2048, 1024)]
    [TestCase(8192, 8192)]
    public void DynamicSdfRequestDoesNotRejectPositiveDimensionsByEstimatedWork(int width, int height)
    {
        Assert.DoesNotThrow(() => LightingResourceManager.ValidateDynamicDistanceFieldRequest(width, height));
    }

    [TestCase(0, 1024)]
    [TestCase(1024, 0)]
    [TestCase(-1, 1024)]
    [TestCase(1024, -1)]
    public void DynamicSdfRequestRejectsEmptyDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LightingResourceManager.ValidateDynamicDistanceFieldRequest(width, height));
    }

    private static LightingQualityTuning CreateTuning(
        LightingQualityTuning source, int? field = null, int? light = null, int? probes = null,
        int? staticAngles = null, float? near = null, int? samples = null,
        int? emitter = null, int? polar = null) => new(
        field ?? source.FieldPixelsPerCell,
        light ?? source.LightPixelsPerCell,
        probes ?? source.CascadeProbePixelsPerCell,
        staticAngles ?? source.MaximumStaticCascadeDirections,
        near ?? source.DynamicNearCells,
        samples ?? source.DynamicAngularSampleCount,
        emitter ?? source.DynamicEmitterPointsPerAxis,
        polar ?? source.DynamicPolarDirectionCount);
}
