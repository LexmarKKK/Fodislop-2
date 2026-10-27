using System;
using System.Linq;
using Kern.World.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Kern.TerrainTests.Volume;

public sealed class VolumeTopologyTests
{
    [Test]
    public void SingleBlockHasFourPhysicalEdgesAndAdjacentBlocksHaveNoInternalWall()
    {
        var buffers = new TerrainCellBuffers();
        buffers.Type(1, 0);
        buffers.Set(2, 2, 1);
        var topology = new TerrainVolumeTopology();
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Edges.Count, Is.EqualTo(4));
        Assert.That(topology.Edges.All(edge => edge.X == 2 && edge.Y == 2), Is.True);
        buffers.Set(3, 2, 1);
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Edges.Count, Is.EqualTo(6));
        Assert.That(topology.Edges.Any(edge => edge.X == 2 && edge.Start.x == 1 && edge.End.x == 1), Is.False);
        Assert.That(topology.Edges.Any(edge => edge.X == 3 && edge.Start.x == 0 && edge.End.x == 0), Is.False);
        buffers.Set(3, 2, 2);
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Edges.Count, Is.EqualTo(4));
        Assert.That(topology.Edges.Any(edge => edge.Start.x == 1 && edge.End.x == 1), Is.True);
    }

    [Test]
    public void RoundBlockEdgesEncloseTheIndependentPixelAreaAndAreOnArtGrid()
    {
        var buffers = new TerrainCellBuffers();
        buffers.Type(1, 3u << 7);
        buffers.Set(2, 2, 1);
        var topology = new TerrainVolumeTopology();
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        int pixels = 0;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                double dx = (x + .5) / 32 - .5;
                double dy = (y + .5) / 32 - .5;
                if (dx * dx + dy * dy <= .51 * .51 || Math.Abs(Math.Abs(dx) - Math.Abs(dy)) >= .4)
                {
                    pixels++;
                }
            }
        }
        double area = 0;
        foreach (TerrainVolumeEdge edge in topology.Edges)
        {
            area += (edge.Start.x * edge.End.y - edge.End.x * edge.Start.y) * .5;
            float lengthPixels = (Math.Abs(edge.Start.x - edge.End.x) + Math.Abs(edge.Start.y - edge.End.y)) * 32;
            Assert.That(lengthPixels, Is.GreaterThanOrEqualTo(1));
            Assert.That(lengthPixels, Is.EqualTo(MathF.Round(lengthPixels)));
            Assert.That(edge.Start.x * 32, Is.EqualTo(MathF.Round(edge.Start.x * 32)));
            Assert.That(edge.Start.y * 32, Is.EqualTo(MathF.Round(edge.Start.y * 32)));
        }
        Assert.That(area, Is.EqualTo(pixels / 1024.0));
        Assert.That(topology.Edges.Count, Is.GreaterThan(4));
    }

    [Test]
    public void NegativeWindowOriginsPublishLocalAddressesAndNewTopologyVersion()
    {
        var buffers = new TerrainCellBuffers();
        buffers.Type(1, 0);
        buffers.Set(-3, -4, 1);
        var topology = new TerrainVolumeTopology();
        topology.Publish(buffers, new Vector2Int(-5, -5), 8, 8, true);
        Assert.That(topology.Edges.All(edge => edge.X == 2 && edge.Y == 1), Is.True);
        Assert.That(topology.Version, Is.EqualTo(1));
    }
    [Test]
    public void UnchangedCellsAndMaterialOnlyTypeChangesDoNotRepublishWallTopology()
    {
        var buffers = new TerrainCellBuffers();
        buffers.Type(1, 0);
        buffers.Set(2, 2, 1);
        var topology = new TerrainVolumeTopology();
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        ulong version = topology.Version;
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Version, Is.EqualTo(version));
        buffers.Type(1, 1u << 12);
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Version, Is.EqualTo(version));
        buffers.Type(1, TerrainCellFormat.TypeBackground);
        topology.Publish(buffers, Vector2Int.zero, 8, 8, false);
        Assert.That(topology.Version, Is.EqualTo(version + 1));
        Assert.That(topology.Edges, Is.Empty);
    }

}
