#nullable enable

using System;
using System.Collections.Generic;
using Kern.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;

namespace Kern.Networking.Offline;

internal static class DummyCellConfigurationFactory
{
    public static BlockDefinition GetBlockDefinition(CellType type) =>
        BlockRegistry.Get(type);

    // Правило экономики сервера, а не свойство блока: добыча кристалла кладёт
    // его в корзину, слот — номер CrystalType в BasketPacket.

    public static byte[][] CreateTileGroups()
    {
        var pack = new List<byte>();
        foreach ((CellType type, BlockDefinition def) in BlockRegistry.Blocks)
        {
            if (def.Outline is CellOutline.Wall or CellOutline.Corner or CellOutline.Door)
            {
                pack.Add((byte)type);
            }
        }

        pack.Sort();
        return new[] { pack.ToArray() };
    }

    public static CellConfigurationPacket[] CreateCellConfigurations()
    {
        var configs = new CellConfigurationPacket[256];
        for (int i = 0; i < 256; i++)
        {
            var type = (CellType)i;
            BlockDefinition def = BlockRegistry.Get(type);

            CellConfigFlags flags = CellConfigFlags.None;
            if (def.IsPassable)
            {
                flags |= CellConfigFlags.Passable;
            }

            if (def.Emission)
            {
                flags |= CellConfigFlags.Emission;
            }

            configs[i] = new CellConfigurationPacket(
                Flags: flags,
                DrawLayer: (CellRenderLayer)def.DrawLayer,
                Outline: (CellOutlineKind)def.Outline,
                TextureAnchor: (CellTextureAnchorKind)def.TextureAnchor,
                SurfaceEffect: (CellSurfaceEffectKind)def.SurfaceEffect,
                SurfaceEffectPalette: def.SurfaceEffectPalette,
                ShaderAnimation: def.ShaderAnimation,
                FrameAnimationSpeed: def.FrameAnimationSpeed,
                DecalAtlas: (CellDecalAtlasKind)def.DecalAtlas,
                ReliefGroup: def.ReliefGroup,
                MapColor: DummyMapColors.Get(i));
        }

        return configs;
    }

    public static Dictionary<CellType, ushort> CreateMovementSpeeds(
        CellConfigurationPacket[] configurations)
    {
        var speeds = new Dictionary<CellType, ushort>(BlockRegistry.Blocks.Count);
        foreach ((CellType type, BlockDefinition def) in BlockRegistry.Blocks)
        {
            // Правило сервера: по проходимому — быстро, по остальному — медленно.
            speeds[type] = (ushort)(def.IsPassable ? 20 : 100);
        }

        return speeds;
    }
}
