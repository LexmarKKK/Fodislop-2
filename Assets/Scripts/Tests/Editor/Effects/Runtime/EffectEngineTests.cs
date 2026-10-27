#nullable enable

using System;
using Kern.Effects;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.Effects;

[TestFixture]
public sealed class EffectEngineTests
{
    [Test]
    public void Spawn_UnknownEffect_Throws()
    {
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.Spark()]));

        Assert.Throws<ArgumentException>(() => engine.Spawn("missing", Vector2.zero));
    }

    [Test]
    public void Spawn_ReturnsRunningInstance()
    {
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.Spark()]));
        var sink = new RecordingEffectSink();

        EffectInstance instance = engine.Spawn("spark", new Vector2(2f, 3f));
        engine.Update(EffectInstance.SimulationStepSeconds, sink);

        Assert.That(instance.EffectId, Is.EqualTo("spark"));
        Assert.That(engine.ActiveCount, Is.EqualTo(1));
        Assert.That(sink.Draws.Count, Is.EqualTo(6));
    }

    [Test]
    public void Update_RemovesCompletedInstances()
    {
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.Spark()]));
        var sink = new RecordingEffectSink();
        engine.Spawn("spark", Vector2.zero);

        for (int step = 0; step < 120; step++)
        {
            engine.Update(EffectInstance.SimulationStepSeconds, sink);
        }

        Assert.That(engine.ActiveCount, Is.Zero);
    }

    [Test]
    public void Update_NullSink_Throws()
    {
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.Spark()]));

        Assert.Throws<ArgumentNullException>(() => engine.Update(EffectInstance.SimulationStepSeconds, null!));
    }

    [Test]
    public void Dispose_RejectsFurtherSpawns()
    {
        var engine = new EffectEngine(new EffectCatalog([EffectTestData.Spark()]));
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.Spawn("spark", Vector2.zero));
    }
}
