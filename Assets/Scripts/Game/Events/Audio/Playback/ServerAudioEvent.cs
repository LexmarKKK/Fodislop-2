#nullable enable

using System;
using System.Collections.Generic;
using Kern.Core.Interfaces;
using Kern.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.World;
using MinesServer.Networking.Shared.Packets;
using UnityEngine;

using Kern.Game.Events.VisualEffects;
using Kern.Game.VisualEffects.Cells;
using Kern.Game.VisualEffects.Pooling;

namespace Kern.Game.Events.Audio;
public sealed class ServerAudioEvent : IDisposable, IServerWorldEffect
{
    private readonly SFX? _audioEffectType;
    private readonly IAudioSystem _audioSystem;
    private readonly ServerAudioVisualPlayback _visualPlayback;

    public ServerAudioEvent(
        AudioPacket packet,
        IVFXSlot? slot,
        IRobotService robotService,
        ILocalPlayerState localPlayer,
        IAudioSystem audioSystem,
        IAssetLoader assetLoader,
        MapManager mapManager,
        IVFXService vfxPool,
        IAsyncOperationSupervisor operations)
        : this(
            packet.EffectType,
            packet.EffectType.ToString(),
            packet.TargetBotId,
            packet.X,
            packet.Y,
            packet.Parameters,
            slot,
            robotService,
            localPlayer,
            audioSystem,
            assetLoader,
            mapManager,
            vfxPool,
            operations)
    {
    }

    public ServerAudioEvent(
        VFXPacket packet,
        IVFXSlot? slot,
        IRobotService robotService,
        ILocalPlayerState localPlayer,
        IAudioSystem audioSystem,
        IAssetLoader assetLoader,
        MapManager mapManager,
        IVFXService vfxPool,
        IAsyncOperationSupervisor operations)
        : this(
            null,
            packet.EffectType.ToString(),
            packet.TargetBotId,
            packet.X,
            packet.Y,
            packet.Parameters,
            slot,
            robotService,
            localPlayer,
            audioSystem,
            assetLoader,
            mapManager,
            vfxPool,
            operations)
    {
    }

    private ServerAudioEvent(
        SFX? audioEffectType,
        string visualEffectName,
        ushort targetBotId,
        ushort sourceX,
        ushort sourceY,
        IReadOnlyList<StringPairPacket> parameters,
        IVFXSlot? slot,
        IRobotService robotService,
        ILocalPlayerState localPlayer,
        IAudioSystem audioSystem,
        IAssetLoader assetLoader,
        MapManager mapManager,
        IVFXService vfxPool,
        IAsyncOperationSupervisor operations)
    {
        _audioEffectType = audioEffectType;
        _audioSystem = audioSystem;
        ServerAudioParameters parsedParams = ServerAudioParameters.Parse(parameters);
        _visualPlayback = new ServerAudioVisualPlayback(
            visualEffectName,
            targetBotId,
            sourceX,
            sourceY,
            parsedParams,
            slot,
            robotService,
            localPlayer,
            assetLoader,
            mapManager,
            vfxPool);
        if (_audioEffectType is SFX effectType)
        {
            PlayAudio(effectType, _visualPlayback.IntendedWorldPosition);
        }

        _visualPlayback.StartLoading(operations);
    }

    public bool IsDisposed => _visualPlayback.IsDisposed;

    public void Update() => _visualPlayback.Update();

    public void Dispose() => _visualPlayback.Dispose();

    private void PlayAudio(SFX effectType, Vector3 worldPosition)
    {
        string eventName = SfxEventNames.Get(effectType);
        long audioStart = System.Diagnostics.Stopwatch.GetTimestamp();
        IAudioPlaybackHandle? handle;
        if (CellDestructionEffect.IsCellDestruction(effectType))
        {
            var layer = Kern.Audio.Core.AudioLayer.SFXDefault();
            layer.IsSpatial = false; // Original SoundManager call: spatialBlend=0, pitch=1.
            handle = _audioSystem.Play2D(eventName, layer);
        }
        else
        {
            handle = _audioSystem.PlayAt(eventName, worldPosition);
        }
        ServerAudioVisualPlayback.RecordIfSlow(eventName, audioStart);
        if (handle == null)
        {
            Debug.LogError($"[ServerAudioEvent] FMOD could not play required event '{eventName}'.");
        }
    }

}
