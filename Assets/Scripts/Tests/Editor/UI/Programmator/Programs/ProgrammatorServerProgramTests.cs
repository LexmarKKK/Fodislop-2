#nullable enable

using System;
using System.Linq;
using Kern.UI.Programmator;
using MinesServer.Data;
using NUnit.Framework;

namespace Kern.Tests.UI;

[TestFixture]
public sealed class ProgrammatorServerProgramTests
{
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(192, 1)]
    [TestCase(193, 2)]
    public void LoadServerInstructions_ProducesCompleteEditablePages(int instructionCount, int expectedPages)
    {
        var data = new ProgrammatorData();
        data.PushUndo();
        data.CurrentPage = 5;
        data.HoveredCell = 7;
        var instructions = Enumerable.Range(0, instructionCount)
            .Select(index => ((ProgAction)1, $"label{index}", $"value{index}"))
            .ToArray();

        data.LoadInstructions(instructions);

        Assert.That(data.PageCount, Is.EqualTo(expectedPages));
        Assert.That(data.Codes.Count, Is.EqualTo(expectedPages * 192));
        Assert.That(data.Labels.Count, Is.EqualTo(data.Codes.Count));
        Assert.That(data.Values.Count, Is.EqualTo(data.Codes.Count));
        Assert.That(data.Codes.Take(instructionCount), Is.All.EqualTo(1));
        Assert.That(data.Codes.Skip(instructionCount), Is.All.Zero);
        if (instructionCount > 0)
        {
            Assert.That(data.Labels[instructionCount - 1], Is.EqualTo($"label{instructionCount - 1}"));
            Assert.That(data.Values[instructionCount - 1], Is.EqualTo($"value{instructionCount - 1}"));
        }

        Assert.That(data.CurrentPage, Is.Zero);
        Assert.That(data.HoveredCell, Is.EqualTo(-1));
        Assert.That(data.Undo(), Is.False, "Undo must not restore another server program.");
        Assert.That(data.Redo(), Is.False);
    }

    [Test]
    public void OversizedServerProgram_RejectsWithoutReplacingCurrentData()
    {
        var data = new ProgrammatorData();
        data.Codes[0] = 7;
        var instructions = new (ProgAction, string, string)[4801];

        Assert.Throws<ArgumentOutOfRangeException>(() => data.LoadInstructions(instructions));
        Assert.That(data.Codes[0], Is.EqualTo(7));
        Assert.That(data.PageCount, Is.EqualTo(1));
    }
}
