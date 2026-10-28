#nullable enable

using System;
using UnityEngine;
using UnityEngine.Rendering;
using Kern.Core;

using Kern.Diagnostics.World;

namespace Kern.World.Lighting;

/// <summary>
/// Reanchors the cascade atlas while preserving matching world probes and dependencies.
/// </summary>
internal sealed class CascadeScrollRecorder
{
    private const int MaximumDispatchGroupsPerDimension = 65535;

    private readonly LightingResourceManager _resources;
    private readonly IFrameTelemetry _telemetry;

    public CascadeScrollRecorder(
        LightingResourceManager resources,
        IFrameTelemetry telemetry)
    {
        _resources = resources;
        _telemetry = telemetry;
    }

    public bool CanReuseWorldOverlap(Vector2Int cellDelta)
    {
        return _resources.CellGridWidth > 0 && _resources.CellGridHeight > 0 &&
            _resources.FieldWidth % _resources.CellGridWidth == 0 &&
            _resources.FieldHeight % _resources.CellGridHeight == 0 &&
            Math.Abs((long)cellDelta.x) < _resources.CellGridWidth &&
            Math.Abs((long)cellDelta.y) < _resources.CellGridHeight;
    }

    /// <summary>
    /// Move matching world probes, then solve far-to-near with per-entry
    /// dependencies. Incompatible lattice phases are rebuilt per cascade.
    /// Reads 12 and writes 16 bytes/entry before masked solves; stable frames
    /// do not record this stage or allocate its scratch atlas.
    /// </summary>
    public void RecordWorldReanchor(
        CommandBuffer cmd,
        ComputeShader compute,
        RenderTexture glow,
        Vector2Int cellDelta,
        int dirtyRegionCount,
        Action<CommandBuffer, ComputeShader, int, int, RenderTexture, RectInt, bool, int> recordCascade)
    {
        if (!CanReuseWorldOverlap(cellDelta))
        {
            throw new InvalidOperationException("Atlas reanchor requires overlapping fields on an integer texel lattice.");
        }
        Vector2Int texelDelta = new(
            checked(cellDelta.x * (_resources.FieldWidth / _resources.CellGridWidth)),
            checked(cellDelta.y * (_resources.FieldHeight / _resources.CellGridHeight)));
        _resources.EnsureScratchAtlas();
        _telemetry.LightingAtlasScrollCount++;
        cmd.BeginSample("Kern.Lighting.AtlasReanchor");
        cmd.SetComputeIntParam(compute, LightingComputeBinder.CascadeReanchorEnabledId, 1);
        cmd.SetComputeIntParams(compute, LightingComputeBinder.ReanchorDeltaTexelsId, texelDelta.x, texelDelta.y);
        cmd.BeginSample("Kern.Lighting.ReanchorFieldDependencies");
        int rowsKernel = _resources.BuildReanchorChangeRowsKernel;
        int columnsKernel = _resources.BuildReanchorChangeColumnsKernel;
        cmd.SetComputeTextureParam(compute, rowsKernel, "_ReanchorMaterial", _resources.ReanchorMaterial!);
        cmd.SetComputeTextureParam(compute, rowsKernel, "_ReanchorGlow", _resources.ReanchorGlow!);
        cmd.SetComputeTextureParam(compute, rowsKernel, LightingComputeBinder.MaterialFieldId, _resources.MaterialField!);
        cmd.SetComputeTextureParam(compute, rowsKernel, LightingComputeBinder.GlowFieldId, glow);
        cmd.SetComputeBufferParam(compute, rowsKernel, "_ReanchorChangeRowsOutput", _resources.ReanchorRows!);
        cmd.DispatchCompute(compute, rowsKernel, (_resources.FieldHeight + 63) / 64, 1, 1);
        cmd.SetComputeBufferParam(compute, columnsKernel, "_ReanchorChangeRows", _resources.ReanchorRows!);
        cmd.SetComputeBufferParam(compute, columnsKernel, "_ReanchorChangesOutput", _resources.ReanchorChanges!);
        cmd.DispatchCompute(compute, columnsKernel, (_resources.FieldWidth + 63) / 64, 1, 1);
        cmd.SetComputeBufferParam(compute, _resources.SolveCascadeKernel, "_ReanchorChanges", _resources.ReanchorChanges!);
        cmd.EndSample("Kern.Lighting.ReanchorFieldDependencies");
        int copyKernel = _resources.ScrollRadianceAtlasKernel;
        cmd.SetComputeBufferParam(compute, copyKernel, LightingComputeBinder.RadianceAtlasInputId, _resources.RadianceAtlas!);
        cmd.SetComputeBufferParam(compute, copyKernel, LightingComputeBinder.RadianceAtlasOutputId, _resources.RadianceScratchAtlas!);
        cmd.SetComputeBufferParam(compute, copyKernel, LightingComputeBinder.CascadeChangedMaskId, _resources.CascadeChangedMask!);
        foreach (CascadeLayout cascade in _resources.Cascades)
        {
            bool samePhase = texelDelta.x % cascade.ProbeSpacing == 0 && texelDelta.y % cascade.ProbeSpacing == 0;
            int deltaX = texelDelta.x / cascade.ProbeSpacing;
            int deltaY = texelDelta.y / cascade.ProbeSpacing;
            long copied = samePhase
                ? (long)Mathf.Max(0, cascade.ProbeWidth - Mathf.Abs(deltaX)) *
                    Mathf.Max(0, cascade.ProbeHeight - Mathf.Abs(deltaY)) * cascade.DirectionCount
                : 0;
            _telemetry.LightingAtlasReusedEntries += copied;
            _telemetry.LightingAtlasClearedEntries += cascade.EntryCount - copied;
            cmd.SetComputeIntParam(compute, LightingComputeBinder.CascadePhaseMatchesId, samePhase ? 1 : 0);
            cmd.SetComputeIntParam(compute, LightingComputeBinder.ScrollCascadeOffsetId, cascade.Offset);
            cmd.SetComputeIntParam(compute, LightingComputeBinder.ScrollCascadeEntryCountId, cascade.EntryCount);
            cmd.SetComputeIntParams(compute, LightingComputeBinder.ScrollProbeSizeId, cascade.ProbeWidth, cascade.ProbeHeight);
            cmd.SetComputeIntParam(compute, LightingComputeBinder.ScrollDirectionCountId, cascade.DirectionCount);
            cmd.SetComputeIntParams(compute, LightingComputeBinder.ScrollDeltaProbesId, deltaX, deltaY);
            int groups = (cascade.EntryCount + 63) / 64;
            int x = Mathf.Min(MaximumDispatchGroupsPerDimension, groups);
            cmd.SetComputeIntParam(compute, LightingComputeBinder.CascadeDispatchRowWidthId, x * 64);
            cmd.DispatchCompute(compute, copyKernel, x, (groups + x - 1) / x, 1);
        }
        _resources.SwapRadianceAtlases();
        cmd.SetComputeBufferParam(compute, _resources.SolveCascadeKernel,
            LightingComputeBinder.RadianceAtlasId, _resources.RadianceAtlas!);
        cmd.SetComputeTextureParam(compute, _resources.SolveCascadeKernel,
            LightingComputeBinder.CellSolidMaskId, _resources.CellSolidMask!);
        for (int i = _resources.Cascades.Count - 1; i >= 0; i--)
        {
            CascadeLayout cascade = _resources.Cascades[i];
            bool samePhase = texelDelta.x % cascade.ProbeSpacing == 0 && texelDelta.y % cascade.ProbeSpacing == 0;
            cmd.SetComputeIntParam(compute, LightingComputeBinder.CascadePhaseMatchesId, samePhase ? 1 : 0);
            CascadeLayout far = i + 1 < _resources.Cascades.Count ? _resources.Cascades[i + 1] : cascade;
            bool farPhase = texelDelta.x % far.ProbeSpacing == 0 && texelDelta.y % far.ProbeSpacing == 0;
            cmd.SetComputeIntParam(compute, LightingComputeBinder.ReanchorFarPhaseMatchesId, farPhase ? 1 : 0);
            cmd.SetComputeIntParams(compute, LightingComputeBinder.ReanchorFarDeltaProbesId,
                texelDelta.x / far.ProbeSpacing, texelDelta.y / far.ProbeSpacing);
            recordCascade(cmd, compute, _resources.SolveCascadeKernel, i, glow,
                new RectInt(0, 0, cascade.ProbeWidth, cascade.ProbeHeight), true, dirtyRegionCount);
        }
        cmd.SetComputeIntParam(compute, LightingComputeBinder.CascadeReanchorEnabledId, 0);
        cmd.EndSample("Kern.Lighting.AtlasReanchor");
    }
}
