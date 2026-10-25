using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    // ============================================================================
    // Заглушки подсистемы Debugging для вендоренной копии URP.
    //
    // Вендоренная копия (Packages/com.unity.render-pipelines.universal) содержит
    // только часть stock-URP: папка Runtime/Debugging отсутствует, при этом код
    // Passes/UniversalRenderer* всё ещё ссылается на DebugHandler,
    // DebugRendererLists и DebugDisplaySettings (CS0246).
    //
    // Перечисления HDRDebugMode, DebugFullScreenMode и DebugSceneOverrideMode
    // здесь НЕ переопределяются: их канонические версии лежат в
    // ShaderLibrary/Debug/DebugViewEnums.cs (сборка ShaderLibrary), а числовые
    // значения обязаны совпадать с генерированным DebugViewEnums.cs.hlsl,
    // по которому ветвятся Shaders/Debug/*.shader.
    //
    // Заглушки неактивны по построению: IsActiveForCamera() всегда false, а
    // ScriptableRenderer.DebugHandler создаётся только под #if UNITY_ENABLE_CHECKS.
    // Поэтому ни одна debug-ветка рендера не выполняется — поведение такое же,
    // как у stock-URP без включённого Rendering Debugger.
    // ============================================================================

    /// <summary>
    /// Настройки rendering-debugger (ранее URP Debugging/DebugDisplaySettingsRendering.cs).
    /// Все значения нейтральные: debugger неактивен, рендер идёт штатно.
    /// </summary>
    public class DebugDisplaySettingsRendering
    {
        public enum TaaDebugMode
        {
            Off = 0,
            ShowClampedHistory = 1,
            ShowRawFrame = 2,
            ShowRawFrameNoJitter = 3,
        }

        public TaaDebugMode taaDebugMode { get; set; } = TaaDebugMode.Off;
        public DebugSceneOverrideMode sceneOverrideMode { get; set; } = DebugSceneOverrideMode.None;
        public bool enableMsaa { get; set; } = true;
        public bool enableHDR { get; set; } = true;
    }

    /// <summary>
    /// Синглтон debug-настроек пайплайна (ранее URP Debugging).
    /// Регистрируется в DebugDisplaySettingsUI под UNITY_ENABLE_CHECKS,
    /// поэтому реализует core-интерфейс IDebugDisplaySettings.
    /// AreAnySettingsActive == false - все debug-ветки остаются неактивными.
    /// </summary>
    public class UniversalRenderPipelineDebugDisplaySettings : IDebugDisplaySettings
    {
        public static UniversalRenderPipelineDebugDisplaySettings Instance { get; }
            = new UniversalRenderPipelineDebugDisplaySettings();

        public DebugDisplaySettingsRendering renderingSettings { get; }
            = new DebugDisplaySettingsRendering();

        public bool AreAnySettingsActive => false;
        public bool IsPostProcessingAllowed => true;

        public void UpdateDisplayStats() { }
        public void UpdateMaterials() { }

        /// <inheritdoc cref="IDebugDisplaySettings.Reset" />
        public void Reset() { }

        /// <inheritdoc cref="IDebugDisplaySettings.ForEach" />
        public void ForEach(Action<IDebugDisplaySettingsData> onExecute) { }

        // Add(IDebugDisplaySettingsData) не переопределяем: у интерфейса есть
        // default-реализация (возвращает null), DebugDisplaySettingsUI.RegisterDebug
        // результат не использует.
    }

    /// <summary>
    /// Настройки отображения (ранее URP Debugging/DebugDisplaySettings.cs).
    /// Нужен живой экземпляр: UniversalRendererDebug читает
    /// gpuResidentDrawerSettings на активном пути рендера.
    /// </summary>
    public class DebugDisplaySettings
    {
        public UnityEngine.Rendering.DebugDisplayGPUResidentDrawer gpuResidentDrawerSettings { get; set; }
            = new UnityEngine.Rendering.DebugDisplayGPUResidentDrawer();
    }

    /// <summary>Инертный обработчик Rendering Debugger.</summary>
    public class DebugHandler : IDisposable
    {
        public DebugDisplaySettings DebugDisplaySettings { get; } = new DebugDisplaySettings();

        public bool IsActiveForCamera(bool isPreviewCamera) => false;
        public bool IsScreenClearNeeded => false;
        public bool IsActiveModeUnsupportedForDeferred => false;
        public bool IsDepthPrimingCompatible => false;
        public bool IsLightingActive => true;
        public int stpDebugViewIndex => -1;

        public bool TryGetFullscreenDebugMode(out DebugFullScreenMode mode)
        {
            mode = default;
            return false;
        }

        public bool TryGetFullscreenDebugMode(out DebugFullScreenMode mode, out int textureHeightPercent)
        {
            mode = default;
            textureHeightPercent = 0;
            return false;
        }

        public bool WriteToDebugScreenTexture(bool resolveFinalTarget) => false;
        public bool HDRDebugViewIsActive(bool resolveFinalTarget) => false;

        public bool TryGetScreenClearColor(ref Color clearColor)
        {
            clearColor = default;
            return false;
        }

        public void UpdateShaderGlobalPropertiesForFinalValidationPass(
            RenderGraph renderGraph, UniversalCameraData cameraData, bool isFinalPass) { }

        public void Setup(RenderGraph renderGraph, bool isPreviewCamera) { }

        public void Render(RenderGraph renderGraph, UniversalCameraData cameraData,
            TextureHandle debugScreenTexture, TextureHandle overlayUITexture, TextureHandle backBufferColor) { }

        public void SetDebugRenderTarget(RTHandle texture, Rect uvRect, bool supportsStereo, Vector4 dataRangeRemap) { }

        public void ResetDebugRenderTarget() { }

        public DebugRendererLists CreateRendererListsWithDebugRenderState(
            RenderGraph renderGraph, ref CullingResults cullResults, ref DrawingSettings drawSettings,
            ref FilteringSettings filterSettings, ref RenderStateBlock renderStateBlock) => null;

        public void Dispose() { }

        public static void ConfigureColorDescriptorForDebugScreen(ref RenderTextureDescriptor descriptor, int width, int height)
        {
            descriptor.width = width;
            descriptor.height = height;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            descriptor.msaaSamples = 1;
        }

        public static void ConfigureDepthDescriptorForDebugScreen(
            ref RenderTextureDescriptor descriptor, GraphicsFormat depthStencilFormat, int width, int height)
        {
            descriptor.width = width;
            descriptor.height = height;
            descriptor.depthStencilFormat = depthStencilFormat;
            descriptor.msaaSamples = 1;
        }
    }

    /// <summary>Инертный набор renderer-list'ов для debug-режимов отрисовки.</summary>
    public class DebugRendererLists
    {
        // Дженерики вместо конкретных типов (CommandBuffer/RasterCommandBuffer,
        // разные типы builder'ов RenderGraph): тела недостижимы, важна только
        // разрешимость вызовов на существующих call-site.
        public void DrawWithRendererList<T>(T cmd) { }
        public void PrepareRendererListForRasterPass<T>(T builder) { }
    }
}
