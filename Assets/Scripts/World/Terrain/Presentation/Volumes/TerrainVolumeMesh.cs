#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Kern.Diagnostics.Frames;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World.Terrain;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct TerrainVolumeVertex
{
    public readonly Vector4 Address;

    public TerrainVolumeVertex(int x, int y, int layer, int corner, Vector2 start, Vector2 end)
    {
        if (layer != 3)
        {
            Address = new Vector4(x, y, layer, corner);
            return;
        }
        int startX = PixelCode(start.x);
        int startY = PixelCode(start.y);
        int endX = PixelCode(end.x);
        int endY = PixelCode(end.y);
        // All values are exact float32 integers/fractions. POSITION remains the
        // only required attribute, including old recorded warmup declarations.
        Address = new Vector4((x * 128) + startX, (y * 128) + startY,
            3f + ((endX * 128 + endY) / 16384f), corner);
    }

    private static int PixelCode(float cells)
    {
        int code = Mathf.RoundToInt(cells * 32f) + 32;
        if (code < 0 || code >= 128)
        {
            throw new ArgumentOutOfRangeException(nameof(cells), "Volume contour exceeds its exact 7-bit pixel coordinate.");
        }
        return code;
    }
}

// Screen-only mesh: actual side quads join z=0 to the raised face. Vertices
// retain physical coordinates; the shader projects height from the active eye.
internal sealed class TerrainVolumeMesh : IDisposable
{
    internal static readonly VertexAttributeDescriptor[] VertexLayout =
    {
        new(VertexAttribute.Position, VertexAttributeFormat.Float32, 4),
    };
    private readonly List<TerrainVolumeVertex> _vertices = new();
    private readonly List<int> _indices = new();
    private int _width;
    private int _height;
    private ulong _version;
    private int _indexCount;
    private Mesh? _mesh;

    public Mesh? Mesh => _mesh;

    public int WallQuadCount { get; private set; }

    public void EnsureSize(int width, int height, float cellSize, int boundsWidth, int boundsHeight,
        TerrainVolumeTopology topology)
    {
        if (_mesh != null && _width == width && _height == height &&
            _version == topology.Version)
        {
            return;
        }
        _vertices.Clear();
        _indices.Clear();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                AddQuad(x, y, 0, Vector2.zero, Vector2.zero);
            }
        }
        int backgroundIndices = _indices.Count;
        WallQuadCount = 0;
        foreach (TerrainVolumeEdge edge in topology.Edges)
        {
            // Wall addresses belong to the committed window, like door overlays.
            // The vertex shader rejects cells outside the visible presentation
            // before decoding them. Smooth camera travel never rewrites this mesh.
            AddQuad(edge.X, edge.Y, 3, edge.Start, edge.End);
            WallQuadCount++;
        }
        int foregroundStart = _indices.Count;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                AddQuad(x, y, 1, Vector2.zero, Vector2.zero);
            }
        }
        _mesh ??= new Mesh
        {
            name = "TerrainVolumeMesh",
            indexFormat = IndexFormat.UInt32,
            hideFlags = HideFlags.DontSave,
        };
        if (_mesh.vertexCount != _vertices.Count)
        {
            _mesh.Clear();
            _mesh.SetVertexBufferParams(_vertices.Count, VertexLayout);
        }
        _mesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Count, 0, MeshUpdateFlags.DontRecalculateBounds);
        if (_indexCount != _indices.Count)
        {
            _mesh.SetIndexBufferParams(_indices.Count, IndexFormat.UInt32);
        }
        _mesh.SetIndexBufferData(_indices, 0, 0, _indices.Count, MeshUpdateFlags.DontRecalculateBounds);
        var bounds = new Bounds(new Vector3(boundsWidth * cellSize * 0.5f, boundsHeight * cellSize * 0.5f, 0f),
            new Vector3((boundsWidth + 2) * cellSize, (boundsHeight + 2) * cellSize, 2f));
        _mesh.bounds = bounds;
        _mesh.subMeshCount = 3;
        SetDraw(0, 0, backgroundIndices, bounds);
        SetDraw(1, backgroundIndices, foregroundStart - backgroundIndices, bounds);
        SetDraw(2, foregroundStart, _indices.Count - foregroundStart, bounds);
        _width = width;
        _height = height;
        _version = topology.Version;
        _indexCount = _indices.Count;
        FrameEventLog.Record($"меш объёма: {_vertices.Count} вершин, {WallQuadCount} боковых граней, " +
            $"{(_vertices.Count * Marshal.SizeOf<TerrainVolumeVertex>()) + (_indices.Count * 4L)} Б");
    }

    private void SetDraw(int draw, int start, int count, Bounds bounds) =>
        _mesh!.SetSubMesh(draw, new SubMeshDescriptor(start, count, MeshTopology.Triangles)
        {
            bounds = bounds,
        }, MeshUpdateFlags.DontRecalculateBounds);

    private void AddQuad(int x, int y, int layer, Vector2 start, Vector2 end)
    {
        int vertex = _vertices.Count;
        for (int corner = 0; corner < 4; corner++)
        {
            _vertices.Add(new TerrainVolumeVertex(x, y, layer, corner, start, end));
        }
        _indices.Add(vertex);
        _indices.Add(vertex + 1);
        _indices.Add(vertex + 2);
        _indices.Add(vertex);
        _indices.Add(vertex + 2);
        _indices.Add(vertex + 3);
    }

    public void Dispose()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_mesh);
            }
        }
        _mesh = null;
        _indexCount = 0;
        _version = 0;
        _width = 0;
        _height = 0;
    }
}
