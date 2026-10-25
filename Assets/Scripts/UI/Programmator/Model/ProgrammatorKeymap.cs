#nullable enable

using MinesServer.Data;
using UnityEngine.InputSystem;

namespace Kern.UI.Programmator;

// Раскладка символов программатора с клавиатуры, переведённая из старого
// клиента (ProgrammatorView): клавиша (+Shift) → цикл ProgAction, по которому
// шагает предпросмотр при повторных нажатиях над ячейкой. Числовые значения
// ProgAction совпадают со старыми id, поэтому циклы 1:1 переносятся по
// именам; дубликаты старых Shift-последовательностей (shw/sha/shs/shd)
// схлопнуты до уникальных членов в порядке первого появления.
// R в старой раскладке — повороты; в Tick ProgrammatorGrid она закрывает окно
// только вне ячейки сетки. U в старом клиенте была мёртвой (id 89–96 не
// сериализовались) — здесь это валидные Box-операторы.
internal static class ProgrammatorKeymap
{
    internal readonly struct Binding(Key key, bool shift, ProgAction[] cycle)
    {
        internal Key Key { get; } = key;

        internal bool Shift { get; } = shift;

        internal ProgAction[] Cycle { get; } = cycle;
    }

    internal static readonly Binding[] Bindings =
    [
        new(Key.W, false, [ProgAction.MoveUp, ProgAction.RotateUp, ProgAction.InventoryUp]),
        new(Key.A, false, [ProgAction.MoveLeft, ProgAction.RotateLeft, ProgAction.InventoryLeft]),
        new(Key.S, false, [ProgAction.MoveDown, ProgAction.RotateDown, ProgAction.InventoryDown]),
        new(Key.D, false, [ProgAction.MoveRight, ProgAction.RotateRight, ProgAction.InventoryRight]),
        new(Key.W, true,
            [ProgAction.CellUp, ProgAction.CellLeft, ProgAction.CellUpLeft, ProgAction.CellRight,
             ProgAction.CellUpRight, ProgAction.ShiftUp]),
        new(Key.A, true,
            [ProgAction.CellLeft, ProgAction.CellUp, ProgAction.CellUpLeft, ProgAction.CellDown,
             ProgAction.CellDownLeft, ProgAction.ShiftLeft]),
        new(Key.S, true,
            [ProgAction.CellDown, ProgAction.CellLeft, ProgAction.CellDownLeft, ProgAction.CellRight,
             ProgAction.CellDownRight, ProgAction.ShiftDown]),
        new(Key.D, true,
            [ProgAction.CellRight, ProgAction.CellDown, ProgAction.CellDownRight, ProgAction.CellUp,
             ProgAction.CellUpRight, ProgAction.ShiftRight]),
        new(Key.F, false, [ProgAction.MoveForward]),
        new(Key.F, true,
            [ProgAction.CellForward, ProgAction.ShiftForward, ProgAction.CellLefthand,
             ProgAction.CellRighthand]),
        new(Key.Z, false,
            [ProgAction.Dig, ProgAction.BuildBlock, ProgAction.UseGeo, ProgAction.BuildRoad,
             ProgAction.Heal, ProgAction.BuildQuadro]),
        new(Key.Z, true, [ProgAction.BuildWar, ProgAction.UseBoom, ProgAction.UseProt, ProgAction.UseRaz]),
        new(Key.X, false, [ProgAction.UseGeopack, ProgAction.UseZZ, ProgAction.UsePoly, ProgAction.UseC190]),
        new(Key.X, true, [ProgAction.RefillCraft, ProgAction.Upgrade, ProgAction.UseNano, ProgAction.UseRem]),
        new(Key.C, false,
            [ProgAction.IsNotEmpty, ProgAction.IsEmpty, ProgAction.IsFalling, ProgAction.IsCrystal,
             ProgAction.IsAliveCrystal, ProgAction.IsFallingLikeBoulder, ProgAction.IsFallingLikeLiquid,
             ProgAction.IsBreakable, ProgAction.IsUnbreakable, ProgAction.IsAcid]),
        new(Key.C, true,
            [ProgAction.IsBlackRock, ProgAction.IsRedRock, ProgAction.IsGreenBlock,
             ProgAction.IsYellowBlock, ProgAction.IsRedBlock, ProgAction.IsStructure, ProgAction.IsQuadro,
             ProgAction.IsRoad, ProgAction.IsBox]),
        new(Key.V, false,
            [ProgAction.VarEqualsNumber, ProgAction.VarLessThanNumber, ProgAction.VarGreaterThanNumber]),
        new(Key.T, false, [ProgAction.STDDig, ProgAction.STDBlock, ProgAction.STDHeal, ProgAction.STDTunnel]),
        new(Key.G, false, [ProgAction.Goto, ProgAction.Call, ProgAction.CallArg, ProgAction.CallState]),
        new(Key.Q, false, [ProgAction.Return, ProgAction.ReturnArg, ProgAction.ReturnState]),
        new(Key.E, false, [ProgAction.SetStart, ProgAction.Terminate, ProgAction.CallWhenDied]),
        new(Key.R, false, [ProgAction.RotateLefthand, ProgAction.RotateRighthand, ProgAction.RotateRandom]),
        new(Key.B, false, [ProgAction.PlaySound, ProgAction.DebugPause, ProgAction.DebugShow]),
        new(Key.B, true, [ProgAction.EnableHand, ProgAction.DisableHand]),
        new(Key.M, false,
            [ProgAction.EnableAutoDig, ProgAction.DisableAutoDig, ProgAction.EnableAggression,
             ProgAction.DisableAggression]),
        new(Key.O, false, [ProgAction.BooleanOR, ProgAction.BooleanAND]),
        new(Key.I, false, [ProgAction.YesNoGoto, ProgAction.NoYesGoto]),
        new(Key.H, false, [ProgAction.IsHealthNotFull, ProgAction.IsHealthLessThanHalf]),
        new(Key.J, false, [ProgAction.IsInsideGun, ProgAction.ChargeGun]),
        new(Key.L, false, [ProgAction.Label]),
        new(Key.Y, false, [ProgAction.Flip]),
        new(Key.U, false,
            [ProgAction.BoxAll, ProgAction.BoxHalf, ProgAction.BoxWhite, ProgAction.BoxGreen,
             ProgAction.BoxRed, ProgAction.BoxBlue, ProgAction.BoxCyan, ProgAction.BoxViolet]),
        new(Key.Backspace, false, [ProgAction.NextLine]),
    ];

    internal static bool TryGetPlainCycle(Key key, out ProgAction[]? cycle)
    {
        foreach (ProgrammatorKeymap.Binding binding in Bindings)
        {
            if (!binding.Shift && binding.Key == key)
            {
                cycle = binding.Cycle;
                return true;
            }
        }

        cycle = null;
        return false;
    }

    // Человекочитаемый хоткей оператора: первый элемент цикла — «W»,
    // второй — «W x 2» (сколько раз нажать для этого символа), с Shift —
    // «Shift+W». Оператор вне раскладки — null («нет горячей клавиши»).
    internal static string? DescribeHotkey(ProgAction action)
    {
        foreach (ProgrammatorKeymap.Binding binding in Bindings)
        {
            int index = System.Array.IndexOf(binding.Cycle, action);
            if (index < 0)
            {
                continue;
            }

            string key = binding.Shift ? $"Shift+{binding.Key}" : binding.Key.ToString();
            return index == 0 ? key : $"{key} x {index + 1}";
        }

        return null;
    }
}
