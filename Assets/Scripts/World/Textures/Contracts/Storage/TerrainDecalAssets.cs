#nullable enable

using System;

namespace Kern.Core.Interfaces;

/// <summary>Published names of the terrain decal atlases in texture storage.</summary>
public static class TerrainDecalAssets
{
    public const string Ground = "terrain-decals.png";
    public const string Rock = "terrain-decals-rock.png";
    public const string Sand = "terrain-decals-sand.png";

    public static bool Contains(string filename) =>
        string.Equals(filename, Ground, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(filename, Rock, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(filename, Sand, StringComparison.OrdinalIgnoreCase);
}
