#nullable enable

using Kern.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kern.World.Lighting;

/// <summary>
/// Applies explicitly selected antialiasing and URP render scale.
/// </summary>
internal static class LightingUnityQualityApplier
{
    public static void ApplyRenderingSettings(GraphicsQualitySettings settings)
    {
        LightingUnityQuality.RenderingPlan plan =
            LightingUnityQuality.ResolveRenderingPlan(settings);
        QualitySettings.antiAliasing = plan.AntiAliasing;
        float appliedScale = plan.RenderScale;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = plan.RenderScale;
            urp.msaaSampleCount = plan.MsaaSampleCount;
            appliedScale = urp.renderScale;
        }

        Debug.Log(
            $"[LightingUnityQuality] ApplyUnityRenderingSettings: AA={settings.AntiAliasing}, " +
            $"RenderScale={appliedScale} (запрошено {settings.RenderScale})");
    }
}
