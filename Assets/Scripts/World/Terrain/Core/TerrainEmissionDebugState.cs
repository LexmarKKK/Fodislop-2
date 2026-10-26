#nullable enable

using System;
using UnityEngine;

namespace Kern.World.Terrain;

public enum TerrainEmissionMethod
{
    Original,
    FixedThreshold,
    AdaptiveTexture,
    LocalDetail,
    ChromaSeparation,
}

public static class TerrainEmissionDebugState
{
    private static readonly int s_methodId = Shader.PropertyToID("_TerrainEmissionMethod");
    public const string FixedKeyword = "KERN_EMISSION_FIXED";
    public const string AdaptiveKeyword = "KERN_EMISSION_ADAPTIVE";
    public static TerrainEmissionMethod Requested
    {
        get; private set;
    }
    public static TerrainEmissionMethod Active
    {
        get; private set;
    }
    public static string Status { get; internal set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Requested = TerrainEmissionMethod.Original;
        Active = TerrainEmissionMethod.Original;
        Status = string.Empty;
    }

    public static void Request(TerrainEmissionMethod method)
    {
        if (!Enum.IsDefined(typeof(TerrainEmissionMethod), method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }
        if (!Application.isEditor && !Debug.isDebugBuild && method != TerrainEmissionMethod.Original)
        {
            throw new InvalidOperationException("Experimental emission methods are available only in debug builds.");
        }
        Requested = method;
        Status = method == Active ? string.Empty : "Переключение метода…";
    }

    public static void Apply(Material material, TerrainEmissionMethod method)
    {
        if (!Enum.IsDefined(typeof(TerrainEmissionMethod), method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }
        var fixedKeyword = material.shader.keywordSpace.FindKeyword(FixedKeyword);
        var adaptiveKeyword = material.shader.keywordSpace.FindKeyword(AdaptiveKeyword);
        if (!material.HasProperty(s_methodId) || !fixedKeyword.isValid || !adaptiveKeyword.isValid)
        {
            throw new InvalidOperationException($"Terrain shader '{material.shader.name}' does not expose the emission method contract.");
        }
        material.SetInteger(s_methodId, (int)method);
        material.SetKeyword(fixedKeyword, method == TerrainEmissionMethod.FixedThreshold);
        material.SetKeyword(adaptiveKeyword, UsesCachedSource(method));
    }

    public static TerrainEmissionMethod ReadMaterial(Material material)
    {
        if (!material.HasProperty(s_methodId) ||
            !material.shader.keywordSpace.FindKeyword(FixedKeyword).isValid ||
            !material.shader.keywordSpace.FindKeyword(AdaptiveKeyword).isValid)
        {
            throw new InvalidOperationException($"Visible shader '{material.shader.name}' has no emission method contract.");
        }
        bool fixedThreshold = material.IsKeywordEnabled(material.shader.keywordSpace.FindKeyword(FixedKeyword));
        bool adaptive = material.IsKeywordEnabled(material.shader.keywordSpace.FindKeyword(AdaptiveKeyword));
        if (fixedThreshold && adaptive)
        {
            throw new InvalidOperationException("Terrain material has two emission methods enabled simultaneously.");
        }
        TerrainEmissionMethod method = (TerrainEmissionMethod)material.GetInteger(s_methodId);
        if (!Enum.IsDefined(typeof(TerrainEmissionMethod), method) ||
            fixedThreshold != (method == TerrainEmissionMethod.FixedThreshold) || adaptive != UsesCachedSource(method))
        {
            throw new InvalidOperationException("Terrain material emission identity disagrees with its shader keywords.");
        }
        return method;
    }

    public static bool UsesCachedSource(TerrainEmissionMethod method) =>
        method is TerrainEmissionMethod.AdaptiveTexture or TerrainEmissionMethod.LocalDetail or TerrainEmissionMethod.ChromaSeparation;

    internal static void Commit(TerrainEmissionMethod method) => Active = method;

    public static string Describe(TerrainEmissionMethod method) => method switch
    {
        TerrainEmissionMethod.Original => "Исходный: цвет текстуры",
        TerrainEmissionMethod.FixedThreshold => "Общий порог яркости",
        TerrainEmissionMethod.AdaptiveTexture => "Адаптивно по текстуре",
        TerrainEmissionMethod.LocalDetail => "Локальные детали",
        TerrainEmissionMethod.ChromaSeparation => "Отделение по цвету",
        _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };
}
