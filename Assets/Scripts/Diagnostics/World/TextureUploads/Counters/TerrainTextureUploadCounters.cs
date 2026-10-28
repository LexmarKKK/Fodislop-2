#nullable enable

using System;

namespace Kern.Diagnostics.World;

public interface ITerrainTextureUploadTelemetry
{
    long Generation { get; }
    long ApplyCalls { get; }
    long ApplyPayloadBytes { get; }
    int SourceFrameId { get; }
    bool HasSourceFrame { get; }
    bool IsAvailable { get; }
    TerrainTextureUploadSnapshot Capture(int observationFrameId);
}

public interface ITerrainTextureUploadTelemetryReceiver
{
    void BindTerrainTextureUploadTelemetry(ITerrainTextureUploadTelemetry? telemetry);
}

public readonly record struct TerrainTextureUploadSnapshot(
    long Generation,
    long ApplyCalls,
    long ApplyPayloadBytes,
    int SourceFrameId,
    bool HasSourceFrame,
    bool IsAvailable,
    int ObservationFrameId);

public readonly record struct TerrainTextureUploadDelta(
    long ApplyCalls,
    long ApplyPayloadBytes,
    int StartObservationFrameId,
    int EndObservationFrameId);

/// <summary>Counts terrain texture API payload estimates, not GPU bandwidth.</summary>
public sealed class TerrainTextureUploadCounters : ITerrainTextureUploadTelemetry
{
    private long _applyCalls;
    private long _applyPayloadBytes;

    public long Generation { get; private set; }

    public long ApplyCalls => _applyCalls;

    public long ApplyPayloadBytes => _applyPayloadBytes;

    public int SourceFrameId { get; private set; } = -1;

    public bool HasSourceFrame { get; private set; }

    public bool IsAvailable { get; private set; }

    public void BeginGeneration()
    {
        if (IsAvailable)
        {
            throw new InvalidOperationException("Terrain texture upload generation is already active.");
        }

        Generation = checked(Generation + 1);
        IsAvailable = true;
        SourceFrameId = -1;
        HasSourceFrame = false;
    }

    public void EndGeneration()
    {
        if (!IsAvailable)
        {
            return;
        }

        IsAvailable = false;
    }

    public void RecordApply(int width, int height, int bytesPerPixel, int sourceFrameId)
    {
        ValidateRecord(sourceFrameId);
        long payloadBytes = PayloadBytes(width, height, bytesPerPixel);
        long nextCalls = checked(_applyCalls + 1);
        long nextBytes = checked(_applyPayloadBytes + payloadBytes);
        _applyCalls = nextCalls;
        _applyPayloadBytes = nextBytes;
        RecordSourceFrameUnchecked(sourceFrameId);
    }

    public static long PayloadBytes(int width, int height, int bytesPerPixel)
    {
        if (width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        return checked((long)width * height * bytesPerPixel);
    }

    public TerrainTextureUploadSnapshot Capture(int observationFrameId) => new(
        Generation,
        ApplyCalls,
        ApplyPayloadBytes,
        SourceFrameId,
        HasSourceFrame,
        IsAvailable,
        observationFrameId);

    public static bool TryGetDelta(
        TerrainTextureUploadSnapshot previous,
        TerrainTextureUploadSnapshot current,
        out TerrainTextureUploadDelta delta)
    {
        delta = default;
        if (!previous.IsAvailable || !current.IsAvailable ||
            previous.Generation != current.Generation ||
            !IsValidEndpoint(previous) || !IsValidEndpoint(current) ||
            previous.ObservationFrameId == int.MaxValue ||
            current.ObservationFrameId != previous.ObservationFrameId + 1 ||
            current.ApplyCalls < previous.ApplyCalls || current.ApplyPayloadBytes < previous.ApplyPayloadBytes)
        {
            return false;
        }

        delta = new TerrainTextureUploadDelta(
            current.ApplyCalls - previous.ApplyCalls,
            current.ApplyPayloadBytes - previous.ApplyPayloadBytes,
            previous.ObservationFrameId,
            current.ObservationFrameId);
        return true;
    }

    private static bool IsValidEndpoint(TerrainTextureUploadSnapshot snapshot) =>
        snapshot.ObservationFrameId >= 0 &&
        (!snapshot.HasSourceFrame ||
            (snapshot.SourceFrameId >= 0 && snapshot.SourceFrameId <= snapshot.ObservationFrameId));

    private void ValidateRecord(int sourceFrameId)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Cannot record terrain texture uploads without an allocated generation.");
        }

        if (sourceFrameId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFrameId));
        }
    }

    private void RecordSourceFrameUnchecked(int sourceFrameId)
    {
        SourceFrameId = sourceFrameId;
        HasSourceFrame = true;
    }
}
