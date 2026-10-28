#nullable enable

using System;
using System.IO;
using System.Reflection;
using Kern.AssetPipeline.Loading;
using Kern.Core;
using UnityEditor;
using UnityEngine;

namespace Kern.Editor;

public static class RepositoryLayoutVerification
{
    public static void Run()
    {
        string root = Path.GetDirectoryName(Application.dataPath)!;
        if (!File.Exists(Path.Combine(root, ".kern-taxonomy-verification")))
        {
            throw new InvalidOperationException("Run taxonomy verification in a marked disposable project copy.");
        }

        PlayerSettings.companyName = "KernVerification";
        PlayerSettings.productName = "FodislopTaxonomyVerification";
        AssetDatabase.SaveAssets();

        var paths = new RuntimeAssetPaths();
        int textures = 0;
        foreach (var entry in AuthoredTextureCatalog.Read(Path.Combine(root, AuthoredTextureCatalog.AssetPath)))
        {
            string? actual = paths.FindBundledTextureFile(entry.Value);
            string expected = Path.GetFullPath(Path.Combine(Application.dataPath, "Textures", entry.Key));
            if (actual == null || !string.Equals(Path.GetFullPath(actual), expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Published texture '{entry.Value}' did not resolve to '{expected}'.");
            }

            textures++;
        }

        int resources = 0;
        foreach (FieldInfo field in typeof(ResourcePaths).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(string))
            {
                continue;
            }

            string path = (string)field.GetRawConstantValue()!;
            if (Resources.Load(path) == null)
            {
                throw new FileNotFoundException($"Required runtime resource {field.Name} is missing: {path}");
            }

            resources++;
        }

        ProductionSceneContractValidator.ValidateForCommandLine();
        Debug.Log($"[RepositoryLayout] Verified {textures} published texture paths, {resources} runtime resources and all production scene bindings.");
    }
}
