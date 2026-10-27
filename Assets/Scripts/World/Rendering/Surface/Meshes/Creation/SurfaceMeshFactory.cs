#nullable enable

using System;
using Kern.Core;
using Kern.Core.Lifecycle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World;

internal static class SurfaceMeshFactory
{
    public static Mesh CreateDynamic(string meshName)
    {
        var mesh = new Mesh
        {
            name = meshName,
            hideFlags = HideFlags.DontSave,
        };
        mesh.MarkDynamic();
        return mesh;
    }
}
