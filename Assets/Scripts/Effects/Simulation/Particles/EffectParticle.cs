#nullable enable

using UnityEngine;

namespace Kern.Effects;

internal struct EffectParticle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float AgeSeconds;
    public float LifetimeSeconds;
    public float StartSizeUnits;
    public float EndSizeUnits;
    public int EmitterIndex;
    public int FrameOffset;
}
