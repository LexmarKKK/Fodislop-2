#nullable enable

using System;
using Kern.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kern.Editor;

public sealed class UnityRenderLayerValidator : IPreprocessBuildWithReport
{
    public int callbackOrder => -900;

    public void OnPreprocessBuild(BuildReport report)
    {
        Validate();
    }

    [MenuItem("Kern/Diagnostics/Validate Unity Render Layers")]
    public static void Validate()
    {
        int gameObjectLayer = LayerMask.NameToLayer(WorldRenderLayers.WorldUI);
        if (gameObjectLayer < 0)
        {
            throw new BuildFailedException(
                $"Required GameObject layer '{WorldRenderLayers.WorldUI}' is missing.");
        }

        int sortingLayer = SortingLayer.NameToID(
            WorldRenderLayers.WorldUISortingLayer);
        if (sortingLayer == 0 && !string.Equals(
                SortingLayer.IDToName(sortingLayer),
                WorldRenderLayers.WorldUISortingLayer,
                StringComparison.Ordinal))
        {
            throw new BuildFailedException(
                $"Required Sorting Layer '{WorldRenderLayers.WorldUISortingLayer}' is missing.");
        }

        Debug.Log(
            $"[RenderLayers] World UI: GameObject layer={gameObjectLayer} " +
            $"('{WorldRenderLayers.WorldUI}'), " +
            $"Sorting Layer ID={sortingLayer} " +
            $"('{WorldRenderLayers.WorldUISortingLayer}').");
    }
}
