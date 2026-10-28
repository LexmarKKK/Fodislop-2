#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using Kern.Core;
using static Kern.World.Lighting.StaticLightingDirty;

using Kern.Diagnostics.World;

namespace Kern.World.Lighting;

/// <summary>
/// Records static radiance cascade tracing and atlas resolve commands.
/// Resource lifetime remains owned by <see cref="LightingResourceManager"/>.
/// </summary>
internal sealed class StaticLightingSolver
{
    private const int MaximumDispatchGroupsPerDimension = 65535;
    private static readonly uint[] s_zeroTransportCounters = new uint[LightingComputeBinder.LightingCounterCount];

    private static readonly ProfilerMarker s_cascadeMarker =
        new("Kern.Lighting.Cascades.Record.CPU");
    private static readonly ProfilerMarker s_resolveMarker =
        new("Kern.Lighting.Resolve.Record.CPU");

    private readonly LightingResourceManager _resources;
    private readonly IFrameTelemetry _telemetry;
    private readonly CascadeScrollRecorder _scrollRecorder;

    public StaticLightingSolver(
        LightingResourceManager resources,
        IFrameTelemetry telemetry)
    {
        _resources = resources;
        _telemetry = telemetry;
        _scrollRecorder = new CascadeScrollRecorder(resources, telemetry);
    }

    public bool CanReuseStaticAtlas(Vector2Int regionDelta) =>
        _scrollRecorder.CanReuseWorldOverlap(regionDelta);

    public void RecordTrace(
        CommandBuffer commandBuffer,
        RenderTexture glowField,
        bool reuseOverlap,
        Vector2Int regionDelta,
        IReadOnlyList<RectInt> dirtyRegions,
        bool allowDependencyMask,
        Vector4 worldRect)
    {
        using var cascadeMarker = s_cascadeMarker.Auto();
        long traceStart = System.Diagnostics.Stopwatch.GetTimestamp();
        using var radianceCascadesSample = new CommandBufferSampleScope(commandBuffer, "Kern.Lighting.RadianceCascades");
        ComputeShader compute = _resources.LightingCompute!;
        int solveKernel = _resources.SolveCascadeKernel;
        // The prefix table is read by this kernel on every solve. Bind it in
        // the dispatch command stream; cache preparation can be skipped on a
        // later radiance-only solve and command-buffer bindings are not a
        // durable resource lifetime.
        commandBuffer.SetComputeBufferParam(
            compute,
            solveKernel,
            LightingComputeBinder.CleanCellPrefixId,
            _resources.CleanCellPrefix!);
        // A reused region expands the change stream and replaces its buffer
        // after shared-parameter recording; bind that current identity here.
        _resources.EnsureReanchorChangeBinding();
        commandBuffer.SetComputeBufferParam(compute, solveKernel,
            "_ReanchorChanges", _resources.ReanchorChanges!);
        // Snapshot only this static solve. Dynamic frames must not reset or
        // overwrite its counters before the diagnostic GPU readback completes.
        if (LightingComputeBinder.DiagnosticTransportCounters)
        {
            commandBuffer.SetBufferData(_resources.LightingCounters!, s_zeroTransportCounters);
            commandBuffer.SetComputeIntParam(compute, LightingComputeBinder.LightingCountersEnabledId, 1);
        }

        commandBuffer.SetComputeIntParam(compute, LightingComputeBinder.CascadeReanchorEnabledId, 0);
        if (reuseOverlap)
        {
            DirtyRegionGPU[] edits = StaticLightingDirty.ConvertDirtyRegions(dirtyRegions, worldRect,
                _resources.FieldWidth, _resources.FieldHeight);
            _resources.EnsureDirtyRegionCapacity(Mathf.Max(1, edits.Length));
            if (edits.Length > 0)
            {
                _resources.DirtyRegions!.SetData(edits);
            }
            _telemetry.LightingStaticDependencyMaskSolveCount++;
            _scrollRecorder.RecordWorldReanchor(commandBuffer, compute, glowField, regionDelta,
                edits.Length, RecordCascade);
            _telemetry.LightingCascadeTraceTimeMs =
                (float)((System.Diagnostics.Stopwatch.GetTimestamp() - traceStart) *
                    1000.0 / System.Diagnostics.Stopwatch.Frequency);
            commandBuffer.SetComputeIntParam(compute, LightingComputeBinder.LightingCountersEnabledId, 0);
            return;
        }

        DirtyRegionGPU[] dirtyFieldRegions = allowDependencyMask
            ? StaticLightingDirty.ConvertDirtyRegions(
                dirtyRegions,
                worldRect,
                _resources.FieldWidth,
                _resources.FieldHeight)
            : Array.Empty<DirtyRegionGPU>();
        bool useDependencyMask = StaticLightingDirty.ShouldUseDependencyMask(
            dirtyFieldRegions, _resources.FieldWidth, _resources.FieldHeight);

        if (useDependencyMask)
        {
            _telemetry.LightingStaticDependencyMaskSolveCount++;
        }
        else if (dirtyRegions.Count > 0)
        {
            _telemetry.LightingStaticDenseFallbackCount++;
        }
        if (!useDependencyMask)
        {
            dirtyFieldRegions = Array.Empty<DirtyRegionGPU>();
        }
        _resources.EnsureDirtyRegionCapacity(Mathf.Max(1, dirtyFieldRegions.Length));
        if (useDependencyMask)
        {
            _resources.DirtyRegions!.SetData(dirtyFieldRegions);
            // The solve only dispatches a tight probe rect. A near tier can
            // still read any far-tier changed flag, so flags outside that rect
            // must be zero for this solve rather than left over from an older
            // edit (or uninitialized after a full solve).
            int maskCount = _resources.CascadeChangedMask!.count;
            int maskGroups = Mathf.CeilToInt(maskCount / 64f);
            int maskGroupsX = Mathf.Min(MaximumDispatchGroupsPerDimension, maskGroups);
            ComputeShader maskCompute = _resources.LightingCompute!;
            int clearMaskKernel = _resources.ClearCascadeChangedMaskKernel;
            commandBuffer.SetComputeBufferParam(
                maskCompute,
                clearMaskKernel,
                LightingComputeBinder.CascadeChangedMaskId,
                _resources.CascadeChangedMask);
            commandBuffer.SetComputeIntParam(
                maskCompute,
                LightingComputeBinder.CascadeChangedMaskCountId,
                maskCount);
            commandBuffer.SetComputeIntParam(
                maskCompute,
                LightingComputeBinder.CascadeDispatchRowWidthId,
                maskGroupsX * 64);
            commandBuffer.DispatchCompute(
                maskCompute,
                clearMaskKernel,
                maskGroupsX,
                Mathf.CeilToInt(maskGroups / (float)maskGroupsX),
                1);
        }

        commandBuffer.SetComputeBufferParam(compute, solveKernel,
            LightingComputeBinder.RadianceAtlasId, _resources.RadianceAtlas!);
        commandBuffer.SetComputeTextureParam(compute, solveKernel,
            LightingComputeBinder.CellSolidMaskId, _resources.CellSolidMask!);

        // At most four layouts: 64 bytes on the stack, no per-edit heap buffer.
        Span<ProbeRect> dirtyProbeRects = stackalloc ProbeRect[_resources.Cascades.Count];
        if (useDependencyMask && dirtyFieldRegions.Length > 0)
        {
            StaticLightingDirty.FillProbeRects(
                _resources.Cascades, dirtyFieldRegions, worldRect,
                _resources.FieldWidth, _resources.FieldHeight, dirtyProbeRects);
        }

        for (int cascadeIndex = _resources.Cascades.Count - 1;
             cascadeIndex >= 0;
             cascadeIndex--)
        {
            CascadeLayout cascade = _resources.Cascades[cascadeIndex];
            RectInt probeRect = new RectInt(0, 0, cascade.ProbeWidth, cascade.ProbeHeight);
            if (useDependencyMask && dirtyFieldRegions.Length > 0)
            {
                // Include changed far-probe support before clipping a near
                // dispatch. The GPU mask rejects unchanged entries inside it.
                ProbeRect tight = dirtyProbeRects[cascadeIndex];
                if (tight.IsEmpty)
                {
                    continue;
                }

                probeRect = new RectInt(tight.X, tight.Y, tight.Width, tight.Height);
            }

            RecordCascade(
                commandBuffer,
                compute,
                solveKernel,
                cascadeIndex,
                glowField,
                probeRect,
                useDependencyMask,
                dirtyFieldRegions.Length);
        }

        commandBuffer.SetComputeIntParam(compute, LightingComputeBinder.LightingCountersEnabledId, 0);
        _telemetry.LightingCascadeTraceTimeMs =
            (float)((System.Diagnostics.Stopwatch.GetTimestamp() - traceStart) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency);
    }


    public void RecordResolve(
        CommandBuffer commandBuffer,
        LightingEngine.DebugView debugView,
        RenderTexture glowField,
        RenderTexture directTarget)
    {
        using var resolveMarker = s_resolveMarker.Auto();
        long resolveStart = System.Diagnostics.Stopwatch.GetTimestamp();
        ComputeShader compute = _resources.LightingCompute!;
        bool transmissionDebug = debugView == LightingEngine.DebugView.Transmission;
        int resolveKernel = transmissionDebug
            ? _resources.ResolveTransmissionDebugKernel
            : _resources.ResolveDirectKernel;
        CascadeLayout first = _resources.Cascades[0];
        commandBuffer.SetComputeIntParam(compute, LightingComputeBinder.CascadeProbeSpacingId, first.ProbeSpacing);
        commandBuffer.SetComputeIntParams(compute, LightingComputeBinder.CascadeProbeSizeId, first.ProbeWidth, first.ProbeHeight);

        commandBuffer.SetComputeIntParam(
            compute,
            LightingComputeBinder.CascadeOffsetId,
            _resources.Cascades[0].Offset);
        commandBuffer.SetComputeBufferParam(
            compute,
            resolveKernel,
            LightingComputeBinder.RadianceAtlasId,
            _resources.RadianceAtlas!);
        commandBuffer.SetComputeTextureParam(
            compute,
            resolveKernel,
            LightingComputeBinder.DirectTextureId,
            directTarget);
        if (transmissionDebug)
        {
            BindFieldTextures(commandBuffer, compute, resolveKernel, glowField);
            commandBuffer.SetComputeTextureParam(
                compute,
                resolveKernel,
                LightingComputeBinder.CellSolidMaskId,
                _resources.CellSolidMask!);
        }

        commandBuffer.DispatchCompute(
            compute,
            resolveKernel,
            LightingComputeBinder.DispatchGroups(_resources.LightWidth),
            LightingComputeBinder.DispatchGroups(_resources.LightHeight),
            1);
        _telemetry.LightingCascadeMergeTimeMs =
            (float)((System.Diagnostics.Stopwatch.GetTimestamp() - resolveStart) *
                1000.0 / System.Diagnostics.Stopwatch.Frequency);
    }

    private void RecordCascade(
        CommandBuffer commandBuffer,
        ComputeShader compute,
        int solveKernel,
        int cascadeIndex,
        RenderTexture glowField,
        RectInt probeRect,
        bool useDependencyMask,
        int dirtyRegionCount)
    {
        string sampleName = cascadeIndex switch
        {
            3 => "Kern.Lighting.Cascade_3",
            2 => "Kern.Lighting.Cascade_2",
            1 => "Kern.Lighting.Cascade_1",
            _ => "Kern.Lighting.Cascade_0",
        };
        commandBuffer.BeginSample(sampleName);
        CascadeLayout cascade = _resources.Cascades[cascadeIndex];
        bool hasFarCascade = cascadeIndex + 1 < _resources.Cascades.Count;
        CascadeLayout farCascade = hasFarCascade
            ? _resources.Cascades[cascadeIndex + 1]
            : cascade;
        LightingComputeBinder.BindCascadeParameters(
            commandBuffer,
            compute,
            cascade,
            farCascade,
            hasFarCascade);
        BindFieldTextures(commandBuffer, compute, solveKernel, glowField);
        commandBuffer.SetComputeBufferParam(
            compute,
            solveKernel,
            LightingComputeBinder.DirtyRegionsId,
            _resources.DirtyRegions!);
        commandBuffer.SetComputeBufferParam(
            compute,
            solveKernel,
            LightingComputeBinder.CascadeChangedMaskId,
            _resources.CascadeChangedMask!);
        commandBuffer.SetComputeIntParam(
            compute,
            LightingComputeBinder.DirtyRegionCountId,
            dirtyRegionCount);
        commandBuffer.SetComputeIntParam(
            compute,
            LightingComputeBinder.CascadeMaskEnabledId,
            useDependencyMask ? 1 : 0);

        LightingComputeBinder.BindCascadeDispatch(
            commandBuffer,
            compute,
            probeRect.x,
            probeRect.y,
            probeRect.width,
            probeRect.height,
            cascade.DirectionCount);
        int dispatchEntryCount = checked(
            probeRect.width * probeRect.height * cascade.DirectionCount);
        int totalGroupCount = Mathf.CeilToInt(dispatchEntryCount / 64f);
        int groupCountX = Mathf.Min(
            MaximumDispatchGroupsPerDimension,
            totalGroupCount);
        int groupCountY = Mathf.CeilToInt(totalGroupCount / (float)groupCountX);
        commandBuffer.SetComputeIntParam(
            compute,
            LightingComputeBinder.CascadeDispatchRowWidthId,
            groupCountX * 64);
        commandBuffer.DispatchCompute(compute, solveKernel, groupCountX, groupCountY, 1);
        long dispatchEntries = (long)probeRect.width * probeRect.height * cascade.DirectionCount;
        if (probeRect.x == 0 && probeRect.y == 0 &&
            probeRect.width == cascade.ProbeWidth &&
            probeRect.height == cascade.ProbeHeight)
        {
            _telemetry.LightingCascadeFullEntries += dispatchEntries;
            _telemetry.LightingCascadeFullEntriesFrame += dispatchEntries;
        }
        else
        {
            _telemetry.LightingCascadePartialEntries += dispatchEntries;
            _telemetry.LightingCascadePartialEntriesFrame += dispatchEntries;
        }
        commandBuffer.EndSample(sampleName);
    }

    private void BindFieldTextures(
        CommandBuffer commandBuffer,
        ComputeShader compute,
        int kernel,
        RenderTexture glowField)
    {
        LightingComputeBinder.BindFieldTextures(
            commandBuffer,
            compute,
            kernel,
            _resources.MaterialField!,
            glowField,
            _resources.LightingCounters);
    }
}
