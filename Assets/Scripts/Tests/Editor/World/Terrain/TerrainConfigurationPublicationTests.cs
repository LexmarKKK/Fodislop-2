#nullable enable

using System.Collections.Generic;
using Kern.Core;
using Kern.Core.Bootstrap.WorldLighting;
using Kern.Core.Interfaces.WorldLighting;
using Kern.World.Terrain;
using UnityEngine;
using NUnit.Framework;

namespace Kern.Tests.World.Terrain;

public sealed class TerrainConfigurationPublicationTests
{
    [Test]
    public void ConfigurationChangeSchedulesMaterialPublicationWithoutCameraOrCellChanges()
    {
        var window = new TerrainWindow();
        using var telemetry = new FrameTelemetry();
        var exchange = new TerrainLightingExchange();
        var publisher = new TerrainLightingFramePublisher(exchange);
        publisher.BeginWorldGeneration();
        publisher.PublishCommittedChanges(1, new List<RectInt>(), textureRefreshOutstanding: false);
        var changes = new TerrainWorldChangeHandler(window, telemetry,
            () => null, () => null, () => null, () => null, () => publisher);
        try
        {
            Assert.That(window.NeedsRefresh, Is.False);
            Assert.That(window.Dirty.IsEmpty, Is.True);
            Assert.That(window.PendingTextureCellTypes.Count, Is.Zero);
            ulong revision = changes.RecordConfigurationChange();
            Assert.That(revision, Is.EqualTo(2));
            Assert.That(window.NeedsRefresh, Is.True,
                "A revision alone does not enter TerrainWindow.Process rebuild: configuration changes must request publication.");
            // The changed material must remain pending until a terrain build
            // consumes the refresh, even with an unchanged camera and cell set.
            Assert.That(window.Dirty.IsEmpty, Is.True);
            Assert.That(window.PendingTextureCellTypes.Count, Is.Zero);
        }
        finally
        {
            window.Dispose();
        }
    }
}
