#nullable enable

using System.Linq;
using Kern.Effects;
using Kern.Game.VisualEffects.Engine;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.Effects;

[TestFixture]
public sealed class WorldEffectSpriteSinkTests
{
    [Test]
    public void Render_DrawsBurstThroughPoolSlots()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.SheetSpark()]));
        using var sink = new WorldEffectSpriteSink(vfx, sheets);
        engine.Spawn("sheet-spark", new Vector2(2f, 3f));

        sink.Render(engine, EffectInstance.SimulationStepSeconds);

        Assert.That(vfx.Slots.Count, Is.EqualTo(6));
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => slot.Enabled));
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => slot.Sprite != null));
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => slot.SortingOrder == 0));
        Assert.That(
            vfx.Slots,
            Has.All.Matches<RecordingVFXSlot>(slot =>
                Mathf.Approximately(slot.GameObject!.transform.position.x, 2f) &&
                Mathf.Approximately(slot.GameObject.transform.position.y, 3f)));
        Assert.That(
            vfx.Slots,
            Has.All.Matches<RecordingVFXSlot>(slot =>
            {
                float scale = slot.GameObject!.transform.localScale.x;
                return scale >= 0.5f && scale <= 0.9f;
            }));
    }

    [Test]
    public void Render_DisablesSlotsAfterParticlesExpire()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.SheetSpark()]));
        using var sink = new WorldEffectSpriteSink(vfx, sheets);
        engine.Spawn("sheet-spark", Vector2.zero);

        for (int step = 0; step < 60; step++)
        {
            sink.Render(engine, EffectInstance.SimulationStepSeconds);
        }

        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => !slot.Enabled));
    }

    [Test]
    public void Render_RespectsSlotBudget()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.SheetSpark()]));
        using var sink = new WorldEffectSpriteSink(vfx, sheets, maximumSlots: 2);
        engine.Spawn("sheet-spark", Vector2.zero);

        sink.Render(engine, EffectInstance.SimulationStepSeconds);

        Assert.That(vfx.Slots.Count, Is.EqualTo(2));
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => slot.Enabled));
    }

    [Test]
    public void Render_KeepsSeparateSlotsPerSortingOrder()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var engine = new EffectEngine(new EffectCatalog([
            EffectTestData.SheetSpark(0),
            EffectTestData.SheetSpark(5),
        ]));
        using var sink = new WorldEffectSpriteSink(vfx, sheets);
        engine.Spawn("sheet-spark-0", Vector2.zero);
        engine.Spawn("sheet-spark-5", Vector2.zero);

        sink.Render(engine, EffectInstance.SimulationStepSeconds);

        Assert.That(vfx.Slots.Count(slot => slot.SortingOrder == 0), Is.EqualTo(6));
        Assert.That(vfx.Slots.Count(slot => slot.SortingOrder == 5), Is.EqualTo(6));
    }

    [Test]
    public void Dispose_ReleasesEveryAcquiredSlot()
    {
        using var sheets = new EffectSpriteSheetCache();
        using var vfx = new RecordingVFXService();
        using var engine = new EffectEngine(new EffectCatalog([EffectTestData.SheetSpark()]));
        var sink = new WorldEffectSpriteSink(vfx, sheets);
        engine.Spawn("sheet-spark", Vector2.zero);
        sink.Render(engine, EffectInstance.SimulationStepSeconds);

        sink.Dispose();

        Assert.That(vfx.ReleasedCount, Is.EqualTo(vfx.Slots.Count));
        Assert.That(vfx.Slots, Has.All.Matches<RecordingVFXSlot>(slot => slot.Released));
    }
}
