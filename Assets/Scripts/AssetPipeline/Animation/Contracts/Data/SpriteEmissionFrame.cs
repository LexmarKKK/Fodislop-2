#nullable enable

using UnityEngine;

namespace Kern;

// Scene-linear RGB integrated over world-cell area; centroid is an effect-local position in cells.
public readonly record struct SpriteEmissionFrame(Color Radiance, Vector2 Centroid);
