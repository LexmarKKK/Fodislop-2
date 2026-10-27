#nullable enable

using System;
using Kern.Effects;
using Kern.Game.VisualEffects.Engine;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.Effects;

[TestFixture]
public sealed class EffectEngineServiceTests
{
    [Test]
    public void PlayAndUpdate_DrivesEngineAndSlots()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var service = new EffectEngineService(
            new EffectCatalog([EffectTestData.SheetSpark()]), vfx, sheets);

        Assert.That(service.ActiveCount, Is.Zero);
        service.Play("sheet-spark", Vector2.zero);
        service.Update(EffectInstance.SimulationStepSeconds);

        Assert.That(service.ActiveCount, Is.EqualTo(1));
        Assert.That(vfx.Slots.Count, Is.EqualTo(6));

        service.Update(1.5f);
        service.Update(EffectInstance.SimulationStepSeconds);

        Assert.That(service.ActiveCount, Is.Zero);
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => !slot.Enabled));
    }

    [Test]
    public void DisposedService_RejectsPlayAndUpdate()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        var service = new EffectEngineService(
            new EffectCatalog([EffectTestData.SheetSpark()]), vfx, sheets);
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Play("sheet-spark", Vector2.zero));
        Assert.Throws<ObjectDisposedException>(() => service.Update(EffectInstance.SimulationStepSeconds));
    }
}
