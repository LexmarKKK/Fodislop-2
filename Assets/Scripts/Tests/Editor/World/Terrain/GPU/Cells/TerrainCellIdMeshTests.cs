#nullable enable

using System.Runtime.InteropServices;
using Unity.Collections;
using Kern.World.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.World;

[TestFixture]
public class TerrainCellIdMeshTests
{
    [Test]
    public void EnsureSizeBuildsCorrectMeshGeometry()
    {
        using var meshManager = new TerrainCellIdMesh();
        const int width = 16;
        const int height = 16;
        const float cellSize = 1f;

        bool changed = meshManager.EnsureSize(width, height, cellSize);
        Assert.That(changed, Is.True);
        Assert.That(meshManager.Mesh, Is.Not.Null);

        Mesh mesh = meshManager.Mesh!;
        int expectedQuads = width * height * TerrainCellData.LayersPerCell;
        int expectedVerts = expectedQuads * 4;
        int expectedIndices = expectedQuads * 6;

        Assert.That(mesh.vertexCount, Is.EqualTo(expectedVerts));
        Assert.That(mesh.GetIndexCount(0), Is.EqualTo(expectedIndices));

        // Re-call with same parameters should return false and not reallocate
        bool recheck = meshManager.EnsureSize(width, height, cellSize);
        Assert.That(recheck, Is.False);
    }

    [Test]
    public void PresentationMeshHasOrderedBackgroundWallAndForegroundDraws()
    {
        using var presentation = new TerrainCellIdMesh(includeDepth: true);
        Assert.That(presentation.EnsureSize(3, 2, 1f), Is.True);
        Mesh mesh = presentation.Mesh!;
        Assert.That(mesh.subMeshCount, Is.EqualTo(3));
        Assert.That(mesh.vertexCount, Is.EqualTo(72));
        for (int draw = 0; draw < 3; draw++)
        {
            Assert.That(mesh.GetSubMesh(draw).indexStart, Is.EqualTo(draw * 36));
            Assert.That(mesh.GetIndexCount(draw), Is.EqualTo(36));
        }
        Assert.That(presentation.EnsureSize(3, 2, 1f), Is.False,
            "A stable frame must retain its mesh and buffers.");
    }

    [Test]
    public void PresentationJobUsesWallAddressBetweenPhysicalLayers()
    {
        using var vertices = new NativeArray<TerrainCellIdVertex>(12, Allocator.Temp);
        using var indices = new NativeArray<int>(18, Allocator.Temp);
        var job = new GenerateCellIdMeshJob
        {
            Width = 1,
            Height = 1,
            IncludeDepth = true,
            Vertices = vertices,
            Indices = indices,
        };
        for (int quad = 0; quad < 3; quad++)
        {
            job.Execute(quad);
        }
        int[] expectedLayers = { 0, 3, 1 };
        for (int quad = 0; quad < 3; quad++)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                TerrainCellIdVertex vertex = vertices[(quad * 4) + corner];
                Assert.That(vertex.Layer, Is.EqualTo(Mathf.FloatToHalf(expectedLayers[quad])));
                Assert.That(vertex.Corner, Is.EqualTo(Mathf.FloatToHalf(corner)));
            }
        }
    }

    // Адрес вершины — половинки: каждое целое сетки обязано кодироваться
    // ровно так, как его закодирует сама Unity.
    [Test]
    public void VertexAddressIsExactHalfOfEveryGridInteger()
    {
        Assert.That(Marshal.SizeOf<TerrainCellIdVertex>(), Is.EqualTo(8));
        for (int value = 0; value <= TerrainCellIdVertex.MaxAddress; value++)
        {
            Assert.That(TerrainCellIdVertex.Half(value), Is.EqualTo(Mathf.FloatToHalf(value)), $"{value}");
        }
    }
}
