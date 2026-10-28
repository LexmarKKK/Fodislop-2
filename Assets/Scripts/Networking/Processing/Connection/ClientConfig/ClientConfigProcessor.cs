#nullable enable

using System;
using Kern.Core;
using Kern.Core.Interfaces;
using MinesServer.Networking.Server.Packets.Information;

namespace Kern.Networking.Processing;

public sealed class ClientConfigProcessor : IPacketProcessor<ClientConfigPacket>
{
    private readonly ITextureStorageService _textureStorage;
    private readonly IAsyncOperationSupervisor _operations;
    private readonly IAudioSystem _audio;
    private readonly IClientConfigManager _clientConfig;
    public ClientConfigProcessor(
    ITextureStorageService textureStorage,
    IAsyncOperationSupervisor operations,
    IAudioSystem audio,
    IClientConfigManager clientConfig)
    {
        _textureStorage = textureStorage;
        _operations = operations;
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _clientConfig = clientConfig ?? throw new ArgumentNullException(nameof(clientConfig));
    }

    public void Process(ClientConfigPacket packet)
    {
        // Громкость принадлежит локальным настройкам пользователя.
        // Серверные значения не должны перезаписывать их при каждом входе.
        foreach (AudioBusRegistry.BusBinding binding in AudioBusRegistry.Buses)
        {
            _audio.SetBusVolume(binding.Bus, binding.Read(_clientConfig.Config.Audio));
        }

        if (packet.UnrenderedTextures != null && packet.UnrenderedTextures.Count > 0)
        {
            _operations.Run(
                "preload_textures",
                cancellationToken => _textureStorage.PreloadTexturesAsync(packet.UnrenderedTextures, cancellationToken));
        }
    }
}
