#nullable enable

using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;

namespace Kern.Tests;

internal static class CellPacket
{
    public static CellConfigurationPacket Create(
        CellConfigProperties properties = CellConfigProperties.None,
        CellAnimationType shaderAnimation = CellAnimationType.None,
        float frameAnimationSpeed = 0f,
        int mapColor = 0,
        byte reliefGroup = 0,
        CellOutlineKind outline = CellOutlineKind.Pliant) =>
        new(
            properties,
            properties.HasFlag(CellConfigProperties.Passable)
                ? CellRenderLayer.Background
                : CellRenderLayer.Foreground,
            outline,
            CellTextureAnchorKind.Cell,
            CellSurfaceEffectKind.Plain,
            0,
            shaderAnimation,
            frameAnimationSpeed,
            CellDecalAtlasKind.None,
            reliefGroup,
            false,
            mapColor);
}
