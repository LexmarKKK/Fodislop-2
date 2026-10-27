#nullable enable

using Kern.UI.Programmator;
using MinesServer.Data;
using NUnit.Framework;

namespace Kern.Tests.UI;

public class ProgrammatorInputSpecTests
{
    [Test]
    public void TransitionOperators_HaveTextFieldOnly()
    {
        ProgAction[] transitions =
        {
            ProgAction.Goto, ProgAction.Call, ProgAction.CallArg, ProgAction.CallState,
            ProgAction.YesNoGoto, ProgAction.NoYesGoto, ProgAction.CallWhenDied,
            ProgAction.Label, ProgAction.DebugPause, ProgAction.DebugShow,
        };

        foreach (ProgAction action in transitions)
        {
            Assert.IsTrue(ProgrammatorInputSpec.TryGet(action, out var spec), $"{action} must have input");
            Assert.AreEqual(ProgrammatorInputSpec.InputKind.Text, spec.Kind, action.ToString());
        }
    }

    [Test]
    public void NumberComparisons_HaveTextAndNumber_MaxLengthThree()
    {
        ProgAction[] comparisons =
        {
            ProgAction.VarGreaterThanNumber, ProgAction.VarLessThanNumber, ProgAction.VarEqualsNumber,
        };

        foreach (ProgAction action in comparisons)
        {
            Assert.IsTrue(ProgrammatorInputSpec.TryGet(action, out var spec), $"{action} must have input");
            Assert.AreEqual(ProgrammatorInputSpec.InputKind.TextAndNumber, spec.Kind, action.ToString());
            Assert.AreEqual(3, spec.TextMaxLength, action.ToString());
        }
    }

    [Test]
    public void PlainOperators_HaveNoInput()
    {
        Assert.IsFalse(ProgrammatorInputSpec.TryGet(ProgAction.Dig, out _));
        Assert.IsFalse(ProgrammatorInputSpec.TryGet(ProgAction.None, out _));
        Assert.IsFalse(ProgrammatorInputSpec.TryGet(ProgAction.SetNumberToVar, out _));
    }
}
