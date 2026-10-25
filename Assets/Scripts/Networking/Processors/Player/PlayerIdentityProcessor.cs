#nullable enable

using System;
using Kern.Core.Interfaces;
using MinesServer.Networking.Server.Packets;
using MinesServer.Networking.Server.Packets.Information;
using VContainer.Unity;

namespace Kern.Networking.Processors;

/// <summary>
/// Живые имя и уровень игрока вне игровой сцены. Сервер присылает
/// PlayerInfoPacket (Player.Init сразу после авторизации сокета) и
/// LevelPacket, когда клиент ещё в меню, но игровые процессоры
/// (PlayerInfoProcessor/PlayerStatsProcessor) живут в области MainGame
/// и там эти пакеты не обрабатывают — бейдж меню и досье показывали
/// статичные заглушки локализации. Подписка живёт, пока жива область меню.
/// </summary>
public sealed class PlayerIdentityProcessor : IStartable, IDisposable
{
    private readonly INetworkService _network;
    private readonly IPlayerStats _stats;

    public PlayerIdentityProcessor(INetworkService network, IPlayerStats stats)
    {
        _network = network;
        _stats = stats;
    }

    public void Start()
    {
        _network.Subscribe<PlayerInfoPacket>(Process);
        _network.Subscribe<LevelPacket>(Process);
    }

    public void Dispose()
    {
        _network.Unsubscribe<PlayerInfoPacket>(Process);
        _network.Unsubscribe<LevelPacket>(Process);
    }

    public void Process(PlayerInfoPacket packet) => _stats.SetNickname(packet.Nickname);

    public void Process(LevelPacket packet) => _stats.SetLevel(packet.Level);
}
