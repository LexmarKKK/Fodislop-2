#nullable enable

using Kern.Core.Interfaces;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;
using NUnit.Framework;

using Kern.Networking.Offline;

namespace Kern.Tests.Player;

public sealed class CellInteractionRulesTests
{
    [TestCase(CellConfigProperties.None, true)]
    [TestCase(CellConfigProperties.Passable, false)]
    [TestCase(CellConfigProperties.Glowing, true)]
    [TestCase(CellConfigProperties.Glowing | CellConfigProperties.Passable, false)]
    [TestCase((CellConfigProperties)254, true)]
    public void OfflineExcavation_DependsOnlyOnPassability(CellConfigProperties properties, bool expected)
    {
        var config = new CellConfigurationPacket(properties, CellDistortionType.Block,
            CellAnimationType.None, 0, 0, 0, 0);
        Assert.That(CellInteractionRules.CanMine(config), Is.EqualTo(expected));
    }
}
