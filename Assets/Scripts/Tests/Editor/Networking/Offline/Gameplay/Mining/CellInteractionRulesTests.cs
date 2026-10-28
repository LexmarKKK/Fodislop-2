#nullable enable

using Kern.Core.Interfaces;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;
using NUnit.Framework;

using Kern.Networking.Offline;

namespace Kern.Tests.Player;

public sealed class CellInteractionRulesTests
{
    [TestCase(CellConfigFlags.None, true)]
    [TestCase(CellConfigFlags.Passable, false)]
    [TestCase(CellConfigFlags.Blending, true)]
    [TestCase(CellConfigFlags.Blending | CellConfigFlags.Passable, false)]
    [TestCase((CellConfigFlags)254, true)]
    public void OfflineExcavation_DependsOnlyOnPassability(CellConfigFlags properties, bool expected)
    {
        var config = CellPacket.Create(properties);
        Assert.That(CellInteractionRules.CanMine(config), Is.EqualTo(expected));
    }
}
