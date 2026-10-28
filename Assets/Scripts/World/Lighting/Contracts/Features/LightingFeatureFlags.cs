#nullable enable

using Kern.Core;
using UnityEngine;

namespace Kern.World.Lighting;

public enum LightingFeatureFlags
{
    None = 0,
    StaticRC = 1 << 0,
    DynamicLights = 1 << 1,
}
