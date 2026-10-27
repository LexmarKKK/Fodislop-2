#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kern.Effects;

/// <summary>Owns active effect runs. Rendering stays outside and is supplied per update as a sink.</summary>
public sealed class EffectEngine : IDisposable
{
    private readonly EffectCatalog _catalog;
    private readonly List<EffectInstance> _instances = new();
    private uint _seed = 1u;
    private bool _disposed;

    public EffectEngine(EffectCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public int ActiveCount => _instances.Count;

    public EffectInstance Spawn(string effectId, Vector2 position)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EffectEngine));
        }

        if (!_catalog.TryGet(effectId, out EffectDefinition definition))
        {
            throw new ArgumentException($"Unknown effect '{effectId}'.", nameof(effectId));
        }

        _seed = unchecked((_seed * 1664525u) + 1013904223u);
        var instance = new EffectInstance(definition, position, unchecked((int)_seed));
        _instances.Add(instance);
        return instance;
    }

    public void Update(float deltaSeconds, IEffectSpriteSink sink)
    {
        if (sink == null)
        {
            throw new ArgumentNullException(nameof(sink));
        }

        for (int index = _instances.Count - 1; index >= 0; index--)
        {
            EffectInstance instance = _instances[index];
            instance.Update(deltaSeconds, sink);
            if (!instance.IsComplete)
            {
                continue;
            }

            instance.Dispose();
            _instances.RemoveAt(index);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (EffectInstance instance in _instances)
        {
            instance.Dispose();
        }

        _instances.Clear();
    }
}
