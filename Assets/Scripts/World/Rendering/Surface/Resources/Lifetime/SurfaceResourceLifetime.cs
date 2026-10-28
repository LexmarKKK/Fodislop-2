#nullable enable

using System;
using Kern.Core;
using Kern.Core.Lifecycle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World;

internal static class SurfaceResourceLifetime
{
    public static void DestroyOwned(UnityEngine.Object? ownedObject)
    {
        if (ownedObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(ownedObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(ownedObject);
        }
    }

    public static void DestroyOwnedChild(Transform owner, string objectName)
    {
        Transform? ownedChild = owner.Find(objectName);
        if (ownedChild != null)
        {
            DestroyOwned(ownedChild.gameObject);
        }
    }
}
