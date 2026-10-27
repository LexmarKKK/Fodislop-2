#nullable enable

using System;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Core.Interfaces.Diagnostics;

using Kern.Diagnostics.World;

namespace Kern.World.Terrain;

internal static class TerrainFrameDiagnosticsRecorder
{
    public static void Record(
        TerrainFrameDiagnostics diagnostics,
        IFrameTelemetry telemetry,
        TerrainFramePlanner planner,
        long stallStart,
        float planMs,
        float dimensionsMs,
        float processMs,
        float uploadMs,
        int dirtyRectCount,
        long dirtyArea) =>
        diagnostics.Record(
            stallStart,
            telemetry,
            new TerrainFrameTimings(
                planMs,
                dimensionsMs,
                processMs,
                uploadMs,
                dirtyRectCount,
                dirtyArea,
                planner.LastResidencyProbeCalls,
                planner.LastResidencyChunkReads,
                planner.LastResidencyCacheHits,
                planner.LastResidencyLRUTouches));
}
