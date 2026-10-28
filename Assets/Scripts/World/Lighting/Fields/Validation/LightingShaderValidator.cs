#nullable enable

using System;
using Kern.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World.Lighting;

internal static class LightingShaderValidator
{
    public readonly record struct LoadedLightingCompute(
        ComputeShader Compute,
        int SolveCascadeKernel,
        int ScrollRadianceAtlasKernel,
        int ClearCascadeChangedMaskKernel,
        int SolveDynamicLightingKernel,
        int ComposeDynamicLightingKernel,
        int TraceDynamicPolarKernel,
        int ClearDynamicDirectKernel,
        int ResolveDirectKernel,
        int ResolveTransmissionDebugKernel,
        int CompositeLightingKernel,
        int BuildCellSolidMaskKernel,
        int BuildSurfaceAirCacheKernel,
        int TraceDynamicPolarBatchKernel,
        int SolveDynamicLightingBatchKernel,
        int SeedDynamicDistanceFieldKernel,
        int JumpFloodDynamicDistanceFieldKernel,
        int ResolveDynamicDistanceFieldKernel);

    public static LoadedLightingCompute LoadComputeShader()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            throw new NotSupportedException("Radiance Cascades requires compute shader support.");
        }

        ComputeShader compute = Resources.Load<ComputeShader>(
            ResourcePaths.WorldLightingCompute) ??
            throw new InvalidOperationException(
                "Required compute shader Resources/Shaders/Lighting/Pipeline/WorldLighting.compute is missing.");

        int solveCascadeKernel = FindAndValidateKernel(compute, LightingKernelNames.SolveCascade);
        int scrollRadianceAtlasKernel = FindAndValidateKernel(compute, LightingKernelNames.ScrollRadianceAtlas);
        int clearCascadeChangedMaskKernel = FindAndValidateKernel(compute, LightingKernelNames.ClearCascadeChangedMask);
        int solveDynamicLightingKernel = FindAndValidateKernel(compute, LightingKernelNames.SolveDynamicLighting);
        int composeDynamicLightingKernel = FindAndValidateKernel(compute, LightingKernelNames.ComposeDynamicLighting);
        int traceDynamicPolarKernel = FindAndValidateKernel(compute, LightingKernelNames.TraceDynamicPolar);
        int clearDynamicDirectKernel = FindAndValidateKernel(compute, LightingKernelNames.ClearDynamicDirect);
        int resolveDirectKernel = FindAndValidateKernel(compute, LightingKernelNames.ResolveDirect);
        int resolveTransmissionDebugKernel = FindAndValidateKernel(compute, LightingKernelNames.ResolveTransmissionDebug);
        int compositeLightingKernel = FindAndValidateKernel(compute, LightingKernelNames.CompositeLighting);
        int buildCellSolidMaskKernel = FindAndValidateKernel(compute, LightingKernelNames.BuildCellSolidMask);
        int buildSurfaceAirCacheKernel = FindAndValidateKernel(compute, LightingKernelNames.BuildSurfaceAirCache);

        return new LoadedLightingCompute(
            compute,
            solveCascadeKernel,
            scrollRadianceAtlasKernel,
            clearCascadeChangedMaskKernel,
            solveDynamicLightingKernel,
            composeDynamicLightingKernel,
            traceDynamicPolarKernel,
            clearDynamicDirectKernel,
            resolveDirectKernel,
            resolveTransmissionDebugKernel,
            compositeLightingKernel,
            buildCellSolidMaskKernel,
            buildSurfaceAirCacheKernel,
            FindAndValidateKernel(compute, "TraceDynamicPolarBatch"),
            FindAndValidateKernel(compute, "SolveDynamicLightingBatch"),
            FindAndValidateKernel(compute, "SeedDynamicDistanceField"),
            FindAndValidateKernel(compute, "JumpFloodDynamicDistanceField"),
            FindAndValidateKernel(compute, "ResolveDynamicDistanceField"));
    }

    private static int FindAndValidateKernel(ComputeShader compute, string kernelName)
    {
        if (!compute.HasKernel(kernelName))
        {
            throw new InvalidOperationException(
                $"Radiance Cascades compute shader is missing kernel '{kernelName}'.");
        }

        int kernelIndex = compute.FindKernel(kernelName);
        if (!compute.IsSupported(kernelIndex))
        {
            throw new InvalidOperationException(
                $"Radiance Cascades kernel '{kernelName}' failed to compile for {SystemInfo.graphicsDeviceType}.");
        }

        return kernelIndex;
    }

    public static void ValidateGPURequirements()
    {
        if (SystemInfo.supportedRenderTargetCount < 2 ||
            !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) ||
            !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ||
            !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
        {
            throw new NotSupportedException(
                "Radiance Cascades requires two MRTs, RGBA8 material, and random-write lighting targets.");
        }
    }

    public static void ValidateTerrainFieldPasses(Action<UnityEngine.Object> destroyObject)
    {
        Shader terrainShader = Shader.Find(ShaderNames.Terrain) ??
            throw new InvalidOperationException("The terrain shader required by lighting is missing.");

        var validationMaterial = new Material(terrainShader);

        try
        {
            if (validationMaterial.FindPass(
                    ShaderPassNames.LightingMaterialField) < 0)
            {
                throw new InvalidOperationException(
                    "The terrain shader is missing the LightingMaterialField pass.");
            }

            if (validationMaterial.FindPass(
                    ShaderPassNames.LightingAmbientOcclusionField) < 0)
            {
                throw new InvalidOperationException(
                    "The terrain shader is missing the LightingAmbientOcclusionField pass.");
            }
        }
        finally
        {
            destroyObject(validationMaterial);
        }
    }
}
