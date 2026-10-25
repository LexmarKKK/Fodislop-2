#nullable enable

using System.Collections.Generic;
using Kern.UI.Programmator;
using MinesServer.Data;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Kern.Tests.UI;

public class ProgrammatorKeymapTests
{
    [Test]
    public void Bindings_AreUniquePerKeyAndShift()
    {
        HashSet<(Key Key, bool Shift)> seen = [];
        foreach (ProgrammatorKeymap.Binding binding in ProgrammatorKeymap.Bindings)
        {
            Assert.IsTrue(
                seen.Add((binding.Key, binding.Shift)),
                $"Дублирующийся биндинг: {binding.Key}, shift={binding.Shift}");
        }
    }

    [Test]
    public void Cycles_AreNotEmptyAndContainNoNoneOrDuplicates()
    {
        foreach (ProgrammatorKeymap.Binding binding in ProgrammatorKeymap.Bindings)
        {
            Assert.IsNotEmpty(binding.Cycle, $"{binding.Key}: пустой цикл");

            HashSet<ProgAction> seen = [];
            foreach (ProgAction action in binding.Cycle)
            {
                Assert.AreNotEqual(ProgAction.None, action, $"{binding.Key}: None в цикле");
                Assert.IsTrue(seen.Add(action), $"{binding.Key}: дубликат {action} в цикле");
            }
        }
    }

    [Test]
    public void Cycles_DoNotReuseKeyBetweenPlainAndShiftForSameGlyph()
    {
        // Базовые клавиши движения W/A/S/D и F должны оставаться на обычном
        // нажатии (мышечная память старого клиента), Shift-варианты — клетки
        // обзора и руки.
        Assert.IsTrue(ProgrammatorKeymap.TryGetPlainCycle(Key.W, out ProgAction[]? wCycle));
        Assert.Contains(ProgAction.MoveUp, wCycle!);
        Assert.IsTrue(ProgrammatorKeymap.TryGetPlainCycle(Key.F, out ProgAction[]? fCycle));
        Assert.Contains(ProgAction.MoveForward, fCycle!);
        Assert.IsFalse(ProgrammatorKeymap.TryGetPlainCycle(Key.Digit1, out _));
    }

    [Test]
    public void DescribeHotkey_ReportsPressCountForCyclePosition()
    {
        // W-цикл: MoveUp, RotateUp, InventoryUp — «поворот наверх» второй.
        Assert.AreEqual("W", ProgrammatorKeymap.DescribeHotkey(ProgAction.MoveUp));
        Assert.AreEqual("W x 2", ProgrammatorKeymap.DescribeHotkey(ProgAction.RotateUp));
        Assert.AreEqual("Shift+C x 2", ProgrammatorKeymap.DescribeHotkey(ProgAction.IsRedRock));
    }

    [Test]
    public void DescribeHotkey_NullForUnmappedOperator()
    {
        Assert.IsNull(ProgrammatorKeymap.DescribeHotkey(ProgAction.IsBoulder));
        Assert.IsNull(ProgrammatorKeymap.DescribeHotkey(ProgAction.None));
    }
}
