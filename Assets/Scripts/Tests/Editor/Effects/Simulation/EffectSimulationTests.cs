#nullable enable

using System;
using Kern.Effects;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.Effects;

[TestFixture]
public sealed class EffectSimulationTests
{
    [Test]
    public void SameSeed_ProducesIdenticalDraws()
    {
        var first = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 12345);
        var second = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 12345);
        var firstSink = new RecordingEffectSink();
        var secondSink = new RecordingEffectSink();

        for (int step = 0; step < 30; step++)
        {
            first.Update(EffectInstance.SimulationStepSeconds, firstSink);
            second.Update(EffectInstance.SimulationStepSeconds, secondSink);
        }

        Assert.That(firstSink.Draws.Count, Is.EqualTo(secondSink.Draws.Count));
        Assert.That(firstSink.Draws.Count, Is.GreaterThan(0));
        for (int index = 0; index < firstSink.Draws.Count; index++)
        {
            EffectSpriteDraw expected = firstSink.Draws[index];
            EffectSpriteDraw actual = secondSink.Draws[index];
            Assert.That(actual.SpriteSheet, Is.EqualTo(expected.SpriteSheet));
            Assert.That(actual.SpriteSheetColumns, Is.EqualTo(expected.SpriteSheetColumns));
            Assert.That(actual.SpriteSheetRows, Is.EqualTo(expected.SpriteSheetRows));
            Assert.That(actual.FrameIndex, Is.EqualTo(expected.FrameIndex));
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.SizeUnits, Is.EqualTo(expected.SizeUnits));
            Assert.That(actual.Color, Is.EqualTo(expected.Color));
            Assert.That(actual.SortingOrder, Is.EqualTo(expected.SortingOrder));
        }
    }

    [Test]
    public void Burst_EmitsDeclaredCountOnFirstStep()
    {
        var instance = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 7);
        var sink = new RecordingEffectSink();

        instance.Update(EffectInstance.SimulationStepSeconds, sink);

        Assert.That(instance.ParticleCount, Is.EqualTo(6));
        Assert.That(sink.Draws.Count, Is.EqualTo(6));
        Assert.That(instance.EffectId, Is.EqualTo("spark"));
    }

    [Test]
    public void Instance_CompletesAfterDurationAndStopsDrawing()
    {
        var instance = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 3);
        var sink = new RecordingEffectSink();

        for (int step = 0; step < 120; step++)
        {
            instance.Update(EffectInstance.SimulationStepSeconds, sink);
        }

        Assert.That(instance.IsComplete, Is.True);
        Assert.That(instance.ParticleCount, Is.Zero);
        int drawn = sink.Draws.Count;
        sink.Clear();
        instance.Update(EffectInstance.SimulationStepSeconds, sink);
        Assert.That(sink.Draws, Is.Empty);
        Assert.That(drawn, Is.GreaterThan(0));
    }

    [Test]
    public void Loop_KeepsEmittingPastDuration()
    {
        var instance = new EffectInstance(EffectTestData.LoopingSmoke(), Vector2.zero, 11);
        var sink = new RecordingEffectSink();

        for (int step = 0; step < 60; step++)
        {
            instance.Update(EffectInstance.SimulationStepSeconds, sink);
        }

        Assert.That(instance.IsComplete, Is.False);
        Assert.That(instance.ParticleCount, Is.GreaterThan(0));
    }

    [Test]
    public void NonPositiveDelta_DoesNothing()
    {
        var instance = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 5);
        var sink = new RecordingEffectSink();

        instance.Update(0f, sink);
        instance.Update(-1f, sink);

        Assert.That(sink.Draws, Is.Empty);
    }

    [Test]
    public void NullSink_Throws()
    {
        var instance = new EffectInstance(EffectTestData.Spark(), Vector2.zero, 5);

        Assert.Throws<ArgumentNullException>(() => instance.Update(EffectInstance.SimulationStepSeconds, null!));
    }
}
