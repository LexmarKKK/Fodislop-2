#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kern.Core.Diagnostics;
using Kern.Core.Interfaces;
using MinesServer.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World.Terrain;

/// <summary>Terrain-owned source readback, CPU analysis and GPU pyramid lifetime.</summary>
internal sealed class AdaptiveTerrainEmissionCache : IDisposable
{
    private sealed class Entry
    {
        public readonly EntityId TextureId;
        public readonly RectInt Rect;
        public readonly CancellationTokenSource Cancellation = new();
        public Texture2D? Atlas;
        public bool Started;
        public ComputeBuffer? Source;
        public AsyncGPUReadbackRequest Readback;
        public Task<AdaptiveTerrainEmissionData>? Analysis;
        public AdaptiveTerrainEmissionData? Data;
        public string? Error;
        public bool Published;
        public Entry(EntityId textureId, RectInt rect)
        {
            TextureId = textureId;
            Rect = rect;
        }
    }

    private readonly TerrainEmissionMethod _method;

    public AdaptiveTerrainEmissionCache(TerrainEmissionMethod method = TerrainEmissionMethod.AdaptiveTexture)
    {
        if (!TerrainEmissionDebugState.UsesCachedSource(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }
        _method = method;
    }

    private readonly Dictionary<CellType, Entry> _entries = new();
    private readonly List<Entry> _retiredEntries = new();
    private ComputeShader? _extract;
    private ComputeBuffer? _headers;
    private ComputeBuffer? _samples;
    private bool _gpuDirty = true;
    private readonly Vector4[] _publishedHeaders = new Vector4[256 * AdaptiveTerrainEmissionData.LevelCount];
    private readonly List<Color> _publishedSamples = new();
    private bool _disposed;
    private static readonly int s_headersId = Shader.PropertyToID("_TerrainEmissionHeaders");
    private static readonly int s_samplesId = Shader.PropertyToID("_TerrainEmissionSamples");

    public int PendingCount
    {
        get; private set;
    }
    public int PreparedCount
    {
        get; private set;
    }
    public int SourceDispatchCount
    {
        get; private set;
    }
    public long SourceTexelCount
    {
        get; private set;
    }
    public int PublishedSampleCount => _publishedSamples.Count;
    public int SampleCapacity => _samples?.count ?? 0;
    public bool HasPendingWork => PendingCount > 0 || _retiredEntries.Count > 0;

    public string? Error
    {
        get; private set;
    }

    public void EnsureSources(IReadOnlyList<IAtlasDescriptor> atlases, ITextureService textures)
    {
        for (int value = 0; value < 256; value++)
        {
            var type = (CellType)value;
            foreach (IAtlasDescriptor atlas in atlases)
            {
                if (!atlas.ContainsCell(type) || BlockRegistry.Get(type).Glow <= 0f)
                {
                    continue;
                }
                Texture2D texture = atlas.Texture ?? throw new InvalidOperationException("Emission source atlas is absent.");
                Vector4 rect = textures.GetCellFrameRect(type);
                int frames = textures.GetAnimationFrameCount(type);
                var pixels = new RectInt(Mathf.RoundToInt(rect.x * atlas.Size), Mathf.RoundToInt(rect.y * atlas.Size),
                    Mathf.RoundToInt(rect.z * atlas.Size), Mathf.RoundToInt(rect.w * atlas.Size) * frames);
                if (pixels.width <= 0 || pixels.height <= 0)
                {
                    break;
                }
                if (pixels.width % 32 != 0 || pixels.height % 32 != 0 || pixels.x < 0 || pixels.y < 0 ||
                    pixels.xMax > texture.width || pixels.yMax > texture.height)
                {
                    throw new InvalidOperationException($"Invalid emission source rectangle for {type}: {pixels} in {texture.width}x{texture.height}.");
                }
                if (_entries.TryGetValue(type, out Entry previous) &&
                    previous.TextureId == texture.GetEntityId() && previous.Rect == pixels)
                {
                    break;
                }
                Invalidate(type);
                Begin(type, texture, pixels);
                break;
            }
        }
    }

    private void Begin(CellType type, Texture2D texture, RectInt rect)
    {
        _entries.Add(type, new Entry(texture.GetEntityId(), rect) { Atlas = texture });
        PendingCount++;
    }

    private void StartSource(Entry entry)
    {
        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            throw new InvalidOperationException("Adaptive emission requires asynchronous GPU readback.");
        }
        Texture2D texture = entry.Atlas ?? throw new InvalidOperationException("Queued emission atlas is absent.");
        RectInt rect = entry.Rect;
        _extract ??= Resources.Load<ComputeShader>("Shaders/Terrain/ExtractEmissionSource") ??
            throw new InvalidOperationException("ExtractEmissionSource compute shader is missing.");
        MemoryAllocationGuard.Require("Adaptive emission source", checked((long)rect.width * rect.height * 96));
        var source = new ComputeBuffer(checked(rect.width * rect.height), 16);
        entry.Source = source;
        int kernel = _extract.FindKernel("ExtractSource");
        using (var commands = new CommandBuffer { name = "Terrain.Emission.ExtractSource" })
        {
            commands.SetComputeTextureParam(_extract, kernel, "_EmissionSourceAtlas", texture);
            commands.SetComputeBufferParam(_extract, kernel, "_EmissionSourcePixels", source);
            commands.SetComputeIntParams(_extract, "_EmissionSourceOrigin", rect.x, rect.y);
            commands.SetComputeIntParams(_extract, "_EmissionSourceSize", rect.width, rect.height);
            commands.SetComputeVectorParam(_extract, "_EmissionAtlasInverseSize", new Vector4(1f / texture.width, 1f / texture.height, 0, 0));
            commands.DispatchCompute(_extract, kernel, (rect.width + 7) / 8, (rect.height + 7) / 8, 1);
            Graphics.ExecuteCommandBuffer(commands);
        }
        SourceDispatchCount++;
        SourceTexelCount += (long)rect.width * rect.height;
        entry.Readback = AsyncGPUReadback.Request(source);
        entry.Started = true;
        entry.Atlas = null;
    }

    public bool Poll()
    {
        bool changed = false;
        for (int index = _retiredEntries.Count - 1; index >= 0; index--)
        {
            Entry retired = _retiredEntries[index];
            if ((retired.Source != null && !retired.Readback.done) ||
                retired.Analysis is { IsCompleted: false })
            {
                continue;
            }
            // Observe stale-version task failures before releasing their owner.
            if (retired.Analysis is { IsFaulted: true } stale)
            {
                _ = stale.Exception;
            }
            Release(retired);
            _retiredEntries.RemoveAt(index);
        }
        int pending = 0;
        foreach (KeyValuePair<CellType, Entry> pair in _entries)
        {
            Entry entry = pair.Value;
            if (entry.Data != null || entry.Error != null)
            {
                continue;
            }
            if (entry.Source != null && entry.Readback.done)
            {
                if (entry.Readback.hasError)
                {
                    entry.Error = $"GPU readback failed for {pair.Key}.";
                    Error = entry.Error;
                }
                else
                {
                    Color[] pixels = entry.Readback.GetData<Color>().ToArray();
                    CancellationToken token = entry.Cancellation.Token;
                    entry.Analysis = Task.Run(() => AdaptiveTerrainEmissionData.Build(pixels,
                        entry.Rect.width, entry.Rect.height, TerrainConfigHolder.AlphaCutoff, token, _method), token);
                }
                entry.Source.Dispose();
                entry.Source = null;
            }
            if (entry.Analysis is { IsCompleted: true } analysis)
            {
                if (analysis.IsFaulted)
                {
                    entry.Error = $"Adaptive emission analysis failed for {pair.Key}: {analysis.Exception}";
                    Error = entry.Error;
                }
                else if (!analysis.IsCanceled)
                {
                    entry.Data = analysis.GetAwaiter().GetResult();
                    PreparedCount++;
                    _gpuDirty = true;
                    changed = true;
                }
                entry.Analysis = null;
            }
            if (entry.Data == null && entry.Error == null)
            {
                pending++;
            }
        }
        PendingCount = pending;
        int running = _retiredEntries.Count;
        foreach (Entry entry in _entries.Values)
        {
            if (entry.Source != null || entry.Analysis != null)
            {
                running++;
            }
        }
        foreach (Entry entry in _entries.Values)
        {
            if (running >= 2)
            {
                break;
            }
            if (!entry.Started && entry.Data == null && entry.Error == null)
            {
                StartSource(entry);
                running++;
            }
        }
        return changed;
    }

    public bool Bind(Material material)
    {
        if (_gpuDirty)
        {
            int appended = 0;
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Data == null || entry.Published)
                {
                    continue;
                }
                foreach (Color[] level in entry.Data.Levels)
                {
                    appended = checked(appended + level.Length);
                }
            }
            int firstNew = _publishedSamples.Count;
            int needed = checked(firstNew + appended);
            if (needed >= 1 << 24)
            {
                throw new InvalidOperationException("Emission cache exceeds exact float header addressing.");
            }
            int capacity = Mathf.NextPowerOfTwo(Math.Max(1, needed));
            MemoryAllocationGuard.Require("Adaptive emission cache publication", checked(capacity * 48L + _publishedHeaders.Length * 16L));
            foreach (KeyValuePair<CellType, Entry> pair in _entries)
            {
                Entry entry = pair.Value;
                if (entry.Data == null || entry.Published)
                {
                    continue;
                }
                entry.Data.Append((byte)pair.Key, _publishedSamples, _publishedHeaders);
                entry.Published = true;
            }
            _headers ??= new ComputeBuffer(_publishedHeaders.Length, 16);
            if (_samples == null || _samples.count < capacity)
            {
                var replacement = new ComputeBuffer(capacity, 16);
                // Empty set has zero headers. GPU buffers themselves cannot be
                // zero-sized; no source lookup can address this unused element.
                try
                {
                    if (_publishedSamples.Count > 0)
                    {
                        replacement.SetData(_publishedSamples);
                    }
                    material.SetBuffer(s_samplesId, replacement);
                }
                catch
                {
                    replacement.Dispose();
                    throw;
                }
                _samples?.Dispose();
                _samples = replacement;
            }
            else if (appended > 0)
            {
                // New streamed types append once. Existing cached sources are
                // not regenerated or re-uploaded unless capacity must grow.
                _samples.SetData(_publishedSamples, firstNew, firstNew, appended);
            }
            _headers.SetData(_publishedHeaders);
            _gpuDirty = false;
        }
        if (_headers == null || _samples == null)
        {
            return false;
        }
        material.SetBuffer(s_headersId, _headers);
        material.SetBuffer(s_samplesId, _samples);
        return true;
    }

    public void Invalidate(CellType type)
    {
        if (_entries.Remove(type, out Entry entry))
        {
            if (entry.Data != null)
            {
                PreparedCount--;
            }
            entry.Cancellation.Cancel();
            _retiredEntries.Add(entry);
            Array.Clear(_publishedHeaders, (byte)type * AdaptiveTerrainEmissionData.LevelCount, AdaptiveTerrainEmissionData.LevelCount);
            _gpuDirty = true;
            Error = null;
            foreach (Entry remaining in _entries.Values)
            {
                if (remaining.Error != null)
                {
                    Error = remaining.Error;
                    break;
                }
            }
        }
    }

    private static void Release(Entry entry)
    {
        entry.Cancellation.Cancel();
        if (entry.Source != null)
        {
            // Teardown only: retain the GPU destination until readback finishes.
            entry.Readback.WaitForCompletion();
            entry.Source.Dispose();
        }
        if (entry.Analysis is { } analysis)
        {
            if (analysis.IsFaulted)
            {
                _ = analysis.Exception;
            }
            else if (!analysis.IsCompleted)
            {
                _ = analysis.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        entry.Cancellation.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        foreach (Entry entry in _entries.Values)
        {
            Release(entry);
        }
        _entries.Clear();
        foreach (Entry entry in _retiredEntries)
        {
            Release(entry);
        }
        _retiredEntries.Clear();
        _headers?.Dispose();
        _samples?.Dispose();
        _headers = null;
        _samples = null;
    }
}
