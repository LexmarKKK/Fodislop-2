#if UNITY_EDITOR
#nullable enable

using System;
using Kern.Rendering;
using Kern.World.Lighting.Quality;
using NUnit.Framework;

namespace Kern.Tests.World.Lighting;

// Guardrail for the "many layers, any one can silently drop the value"
// failure mode: GUI -> ClientConfig -> GraphicsQualityProfile ->
// LightingEngine -> WorldLighting.compute. Each test below
// targets one hop that a manual audit already caught breaking once, so a
// future edit that reintroduces the same class of bug fails loudly here
// instead of only in a debug view nobody is looking at.
[TestFixture]
public sealed class LightingQualityWiringTests
{
    private static readonly GraphicsQualityProfile s_profile = GraphicsQualityProfile.CreateDefault();

    [Test]
    public void ClassicPresetDisablesLighting()
    {
        Assert.That(
            s_profile.Get(GraphicsPreset.Classic).LightingQuality,
            Is.EqualTo(LightingQualityMode.Off),
            "'Классика' is the preset without lighting: if it ever drifts back to a " +
            "solving mode, the cheap preset silently starts computing light.");
    }

    [Test]
    public void StandardPresetSolvesLightingPerPixel()
    {
        Assert.That(
            s_profile.Get(GraphicsPreset.Standard).LightingQuality,
            Is.EqualTo(LightingQualityMode.PerPixel),
            "'Стандарт' is the preset with lighting - dropping back to a disabled mode " +
            "silently removes light from the preset that is supposed to show it.");
    }

    [Test]
    public void TheTwoPresetsDifferOnlyInLighting()
    {
        // The whole point of the pair: «всё, кроме освещения» and «всё». If any other
        // field ever drifts apart, one of the two stops being "everything".
        GraphicsQualitySettings classic = s_profile.Get(GraphicsPreset.Classic);
        GraphicsQualitySettings standard = s_profile.Get(GraphicsPreset.Standard);
        standard.LightingQuality = classic.LightingQuality;
        Assert.That(
            standard,
            Is.EqualTo(classic),
            "The two presets must be identical apart from LightingQuality.");
    }

    [Test]
    public void ValidateSettingsRejectsLightingOnTheClassicPreset()
    {
        GraphicsQualitySettings settings = s_profile.Get(GraphicsPreset.Classic);
        settings.LightingQuality = LightingQualityMode.PerPixel;

        Assert.Throws<InvalidOperationException>(
            () => GraphicsQualityProfile.ValidateSettings(
                settings,
                nameof(GraphicsPreset.Classic)));
    }

    [Test]
    public void ValidateSettingsRejectsDisabledLightingOnStandard()
    {
        GraphicsQualitySettings settings = s_profile.Get(GraphicsPreset.Standard);
        settings.LightingQuality = LightingQualityMode.Off;

        Assert.Throws<InvalidOperationException>(
            () => GraphicsQualityProfile.ValidateSettings(
                settings,
                nameof(GraphicsPreset.Standard)));
    }

    [Test]
    public void ValidateSettingsRejectsAnUndefinedLightingQualityValue()
    {
        // A corrupted save, a hand-edited config JSON, or a future enum
        // reorder can put an out-of-range int here. Without this check
        // it sails through validation (every other field is in range)
        // and only blows up later, when the pause menu tries to label it -
        // a crash on opening Settings instead of a clear error at
        // load/apply time.
        var settings = new GraphicsQualitySettings(
            lightingMaximumTextureDimension: 512,
            lightingMaximumLightCount: 64,
            lightingCascadeAtlasLimit: 512,
            renderScale: 0.8f,
            antiAliasing: 0,
            lightingQuality: (LightingQualityMode)99);

        Assert.Throws<InvalidOperationException>(
            () => GraphicsQualityProfile.ValidateSettings(
                settings,
                nameof(GraphicsPreset.Standard)));
    }

    [Test]
    public void ValidateSettingsRejectsLightingTextureSmallerThanStableViewport()
    {
        var settings = new GraphicsQualitySettings(
            lightingMaximumTextureDimension: 128,
            lightingMaximumLightCount: 64,
            lightingCascadeAtlasLimit: 512,
            renderScale: 0.8f,
            antiAliasing: 0,
            lightingQuality: LightingQualityMode.PerPixel);

        Assert.Throws<InvalidOperationException>(
            () => GraphicsQualityProfile.ValidateSettings(
                settings,
                nameof(GraphicsPreset.Standard)));
    }
}
#endif
