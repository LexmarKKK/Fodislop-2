#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kern;
using Kern.Core;
using Kern.Core.Interfaces;
using UnityEngine;

using Kern.AssetPipeline.Loading;

namespace Kern.World.Textures;

internal sealed class TerrainDecalAtlasLoader
{
    private readonly string _filename;
    private readonly string _operationKey;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _loadStarted;
    private bool _disposed;

    public Texture2D? AtlasTexture { get; private set; }

    public TerrainDecalAtlasLoader(string filename, string operationKey)
    {
        _filename = filename;
        _operationKey = operationKey;
    }

    public void StartLoad(
        ITextureStorageService textureStorage,
        IAsyncOperationSupervisor operations,
        Action? onTextureLoaded)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TerrainDecalAtlasLoader));
        }

        if (_loadStarted)
        {
            return;
        }

        _loadStarted = true;
        CancellationToken lifetimeToken = _lifetime.Token;
        operations.Run(
            _operationKey,
            cancellationToken => LoadAsync(textureStorage, onTextureLoaded, cancellationToken, lifetimeToken));
    }

    private async UniTask LoadAsync(
        ITextureStorageService textureStorage,
        Action? onTextureLoaded,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
        try
        {
            Texture2D? texture = await textureStorage.GetTextureAsync(
                _filename,
                linkedCancellation.Token);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            if (texture == null)
            {
                throw new InvalidOperationException(
                    $"Required terrain decal atlas '{_filename}' could not be decoded.");
            }

            if (texture.width != 512 || texture.height != 32)
            {
                throw new InvalidOperationException(
                    $"Terrain decal atlas must be 512x32, got {texture.width}x{texture.height}.");
            }

            RuntimeTextureFactory.ApplySampling(
                texture,
                FilterMode.Point,
                TextureWrapMode.Clamp);

            AtlasTexture = texture;
            onTextureLoaded?.Invoke();
        }
        catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        AtlasTexture = null;
    }
}
