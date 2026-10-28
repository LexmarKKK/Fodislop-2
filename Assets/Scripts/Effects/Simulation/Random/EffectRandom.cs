#nullable enable

using UnityEngine;

namespace Kern.Effects;

/// <summary>Deterministic 32-bit xorshift stream. The same seed always replays the same effect.</summary>
internal struct EffectRandom
{
    private uint _state;

    public EffectRandom(int seed)
    {
        _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
    }

    public float NextUnit()
    {
        uint value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return (value & 0xFFFFFFu) / 16777216f;
    }

    public float Range(Vector2 range) => range.x + ((range.y - range.x) * NextUnit());

    public int RangeInt(int maximumExclusive) =>
        maximumExclusive <= 0 ? 0 : (int)(NextUnit() * maximumExclusive);
}
