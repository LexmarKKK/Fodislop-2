#nullable enable

using Kern.Rendering;

namespace Kern.World.Lighting;

// Translates explicitly selected settings into Unity's rendering representation.
internal static class LightingUnityQuality
{
    public static RenderingPlan ResolveRenderingPlan(GraphicsQualitySettings settings)
    {
        return new RenderingPlan(
            settings.AntiAliasing,
            settings.AntiAliasing == 0 ? 1 : settings.AntiAliasing,
            settings.RenderScale);
    }

    internal readonly record struct RenderingPlan(
        int AntiAliasing,
        int MsaaSampleCount,
        float RenderScale);
}
