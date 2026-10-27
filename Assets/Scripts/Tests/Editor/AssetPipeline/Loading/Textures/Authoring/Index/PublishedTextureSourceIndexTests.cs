#nullable enable

using System.Collections.Generic;
using System.IO;
using Kern.AssetPipeline.Loading;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests.AssetPipeline;

public sealed class PublishedTextureSourceIndexTests
{
    [Test]
    public void PublishedIdentityResolvesAuthoringCategoryWithCanonicalCaseAndFormatPriority()
    {
        var index = new PublishedTextureSourceIndex(new Dictionary<string, string>
        {
            ["Cells/Crystals/Blue/Blue.exr"] = "Cells/Blue.exr",
            ["Cells/Crystals/Blue/Blue.png"] = "Cells/Blue.png",
        });
        Assert.That(index.Resolve("cells/BLUE"), Is.EqualTo("Cells/Crystals/Blue/Blue.png"));
        Assert.That(index.Resolve("CELLS/blue.EXR"), Is.EqualTo("Cells/Crystals/Blue/Blue.exr"));
        Assert.That(index.Resolve("Cells/Absent.png"), Is.Null);
    }

    [Test]
    public void ProductionEditorPathsResolveEveryPublishedTexture()
    {
        var paths = new RuntimeAssetPaths();
        foreach (KeyValuePair<string, string> entry in AuthoredTextureCatalog.Read(AuthoredTextureCatalog.AssetPath))
        {
            string? actual = paths.FindBundledTextureFile(entry.Value);
            Assert.That(actual, Is.Not.Null, entry.Value);
            Assert.That(Path.GetFullPath(actual!), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "Textures", entry.Key))));
        }
    }
}
