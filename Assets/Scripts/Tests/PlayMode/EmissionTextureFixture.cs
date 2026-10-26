#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Rendering;
using Kern.World;
using MinesServer.Data;
using UnityEngine;

namespace Kern.Tests.PlayMode;

internal sealed class EmissionTextureFixture : ITextureService, IAtlasDescriptor
{
    private readonly Texture2D _texture;
    private readonly Vector4 _rect;
    public EmissionTextureFixture(Texture2D texture, Vector4 rect)
    {
        _texture = texture;
        _rect = rect;
    }
    public event Action<string, Texture2D>? OnTextureLoaded
    {
        add
        {
        }
        remove
        {
        }
    }
    public int PendingCellTextureRequests => 0;
    public Texture2D Texture => _texture;
    public int Size => _texture.width;
    public bool ContainsCell(CellType type) => type == CellType.Green;
    public bool IsFullyOpaque(CellType type) => type == CellType.Green;
    public Vector4 GetCellFrameRect(CellType type) => type == CellType.Green ? _rect : Vector4.zero;
    public int GetAnimationFrameCount(CellType type) => 1;
    public IReadOnlyList<IAtlasDescriptor> GetAllAtlases() => new IAtlasDescriptor[] { this };
    public void RequestTexture(CellType type) => throw new NotSupportedException();
    public AtlasCoordinate GetCellTextureCoordinate(CellType type) => throw new NotSupportedException();
    public UniTask<AtlasCoordinate> GetCellTextureCoordinate(CellType type, int x, int y) => throw new NotSupportedException();
    public Texture2D? PrismaticFlowMapTexture => null;
    public Texture2D? FlowMapTexture => null;
    public Texture2D? TerrainDecalAtlasTexture => null;
    public Texture2D? TerrainDecalRockAtlasTexture => null;
    public string GetCacheStats() => "Static emission texture fixture";
    public void FlushDirtyAtlases()
    {
    }
    public void Clear() => throw new NotSupportedException();
}

internal sealed class EmissionClientConfigFixture : IClientConfigManager
{
    public ClientConfig Config { get; } = new();
    public string ConfigFilePath => string.Empty;
    public GraphicsPreset SelectedGraphicsPreset => GraphicsPreset.Classic;
    public void EnsureInitialized()
    {
    }
    public void SelectGraphicsPreset(GraphicsPreset preset) => throw new NotSupportedException();
    public void UpdateSection<TSection>(Func<ClientConfig, TSection> select, Action<TSection> update)
        where TSection : class, new() => update(select(Config));
    public void UpdatePostProcessAndSave(Action<ClientConfig> update) => update(Config);
    public void UpdateAndSave(Action<ClientConfig> update) => update(Config);
    public void Load() => throw new NotSupportedException();
    public void Save() => throw new NotSupportedException();
    public void SaveDeferred() => throw new NotSupportedException();
}
