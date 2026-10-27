#nullable enable

using MinesServer.Networking.Server.Packets.Connection;

namespace Kern.Networking.Offline;

internal readonly record struct DummyWorldDescriptor(
    int Width,
    int Height,
    CellConfigurationPacket[] CellConfigurations);
