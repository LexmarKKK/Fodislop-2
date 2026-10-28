#nullable enable

using System;
using Kern.Core;
using Kern.Core.Lifecycle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World;

internal static class SurfaceLightingFieldDraw
{
    public static void DrawLightingField(
        CommandBuffer commandBuffer,
        Mesh mesh,
        Material material,
        string shaderPassName)
    {
        if (mesh.vertexCount == 0)
        {
            return;
        }

        int pass = material.FindPass(shaderPassName);
        if (pass < 0)
        {
            throw new InvalidOperationException(
                $"Surface material '{material.name}' is missing {shaderPassName} pass.");
        }

        commandBuffer.DrawMesh(
            mesh,
            Matrix4x4.identity,
            material,
            submeshIndex: 0,
            shaderPass: pass);
    }
}
