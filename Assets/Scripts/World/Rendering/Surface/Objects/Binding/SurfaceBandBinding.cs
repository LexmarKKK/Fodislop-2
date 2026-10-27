#nullable enable

using System;
using Kern.Core;
using Kern.Core.Lifecycle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kern.World;

internal static class SurfaceBandBinding
{
    public static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        bool found = gameObject.TryGetComponent(out T? component);
        if (!found || component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        if (component == null)
        {
            throw new MissingComponentException(
                $"Failed to attach required component {typeof(T).Name} to " +
                $"surface object '{gameObject.name}'.");
        }

        return component;
    }

    public static MeshRenderer BindBandObject(
        ISceneObjectFactory sceneObjects,
        Transform owner,
        int layer,
        string objectName,
        Mesh mesh,
        Material material,
        int sortingOrder)
    {
        Transform? existing = owner.Find(objectName);
        GameObject bandObject;
        if (existing == null)
        {
            bandObject = sceneObjects.Create(objectName, RuntimeOwner.General);
            bandObject.transform.SetParent(owner, worldPositionStays: false);
        }
        else
        {
            bandObject = existing.gameObject;
        }

        bandObject.layer = layer;
        bandObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        bandObject.transform.localScale = Vector3.one;
        MeshFilter meshFilter = GetOrAddComponent<MeshFilter>(bandObject);
        MeshRenderer meshRenderer = GetOrAddComponent<MeshRenderer>(bandObject);
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = sortingOrder;
        bandObject.SetActive(true);
        return meshRenderer;
    }
}
