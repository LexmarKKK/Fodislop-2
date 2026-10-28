#nullable enable

using UnityEngine;

namespace Kern;
public readonly record struct AnimatedSpriteData(Sprite[] Frames, float FPS, int FrameHeight, SpriteEmissionFrame[] FrameEmission)
{
    public float FrameDuration => 1f / Mathf.Max(1f, FPS);
}
