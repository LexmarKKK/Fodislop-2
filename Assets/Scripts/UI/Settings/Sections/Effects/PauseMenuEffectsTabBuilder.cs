#nullable enable

using System;
using System.Collections.Generic;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Core.Localization;
using Kern.Rendering;
using Kern.Rendering.PostProcessing;
using UnityEngine;
using UnityEngine.UIElements;

using Kern.Rendering.Display;

namespace Kern.UI.Settings;

internal sealed class PauseMenuEffectsTabBuilder
{
    private readonly GraphicsSettingsController _graphicsSettings;
    private readonly PostProcessController _postProcessController;
    private readonly IClientConfigManager _clientConfig;
    private readonly ICollection<Action> _refreshers;
    private readonly ILocalizationService _loc;

    public PauseMenuEffectsTabBuilder(
        GraphicsSettingsController graphicsSettings,
        PostProcessController postProcessController,
        IClientConfigManager clientConfig,
        ICollection<Action> refreshers,
        ILocalizationService loc)
    {
        _graphicsSettings = graphicsSettings;
        _postProcessController = postProcessController;
        _clientConfig = clientConfig;
        _refreshers = refreshers;
        _loc = loc;
    }

    public VisualElement Build(ScrollView effectsScroll)
    {
        VisualElement postProcessSection = effectsScroll.Q<VisualElement>("EffectsSection") ??
            throw new InvalidOperationException("[PauseMenu] EffectsSection is missing from PauseMenu.uxml.");
        VisualElement bloomGroup = effectsScroll.Q<VisualElement>("EffectsGroupBloom") ??
            throw new InvalidOperationException("[PauseMenu] EffectsGroupBloom is missing from PauseMenu.uxml.");
        VisualElement cameraGroup = effectsScroll.Q<VisualElement>("EffectsGroupCamera") ??
            throw new InvalidOperationException("[PauseMenu] EffectsGroupCamera is missing from PauseMenu.uxml.");

        _postProcessController.EnsureVolumeSetup();

        // Тумблеры сохраняют авторскую силу соответствующих эффектов.
        Toggle Switch(string fieldName, Func<bool> read, Action<ClientConfig, bool> write) =>
            PauseMenuUIFactory.CreateBoundToggle(
                _loc.Get(SettingSchema.LabelOf<EffectSettings>(fieldName)),
                read,
                value => _graphicsSettings.UpdatePostProcessSettings(
                    config => write(config, value)),
                _refreshers);

        ClientConfig Cfg() => _clientConfig.Config;

        bloomGroup.Add(Switch(
            nameof(EffectSettings.BloomEnabled),
            () => Cfg().Effects.BloomEnabled,
            (config, value) => config.Effects.BloomEnabled = value));

        cameraGroup.Add(Switch(
            nameof(EffectSettings.FisheyeEnabled),
            () => Cfg().Effects.FisheyeEnabled,
            (config, value) => config.Effects.FisheyeEnabled = value));

        cameraGroup.Add(PauseMenuUIFactory.CreateBoundSlider<EffectSettings>(
            nameof(EffectSettings.FisheyeScale),
            _loc,
            () => Cfg().Effects.FisheyeScale,
            value => _graphicsSettings.UpdatePostProcessSettings(
                config => config.Effects.FisheyeScale = Mathf.Clamp(value, 0f, 10f)),
            _refreshers));

        cameraGroup.Add(Switch(
            nameof(EffectSettings.VignetteEnabled),
            () => Cfg().Effects.VignetteEnabled,
            (config, value) => config.Effects.VignetteEnabled = value));

        cameraGroup.Add(Switch(
            nameof(EffectSettings.EigengrauEnabled),
            () => Cfg().Effects.EigengrauEnabled,
            (config, value) => config.Effects.EigengrauEnabled = value));

        return effectsScroll;
    }
}
