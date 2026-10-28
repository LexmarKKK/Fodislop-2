#nullable enable

using System;
using MinesServer.Data;
using UnityEngine.InputSystem;

namespace Kern.UI.Programmator;

// Вставка символа с клавиатуры: пока курсор над ячейкой сетки, нажатие
// клавиши из ProgrammatorKeymap записывает символ в ячейку — с undo, как у
// палитры и джойстика. Повторное нажатие той же клавиши шагает по её циклу,
// причём точка отсчёта — текущий символ ячейки (последнее вхождение в цикле,
// как в старом клиенте). Символ остаётся в данных после ухода курсора;
// HoveredCell при закрытии окна сбрасывается, чтобы «залипший» hover не
// блокировал закрытие по R.
internal sealed class ProgrammatorKeyboardInsert
{
    private readonly ProgrammatorData _data;
    private readonly Action<int, int> _updateCell;
    private readonly Func<bool> _isTextFieldFocused;

    public ProgrammatorKeyboardInsert(
        ProgrammatorData data,
        Action<int, int> updateCell,
        Func<bool> isTextFieldFocused)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _updateCell = updateCell ?? throw new ArgumentNullException(nameof(updateCell));
        _isTextFieldFocused = isTextFieldFocused ?? throw new ArgumentNullException(nameof(isTextFieldFocused));
    }

    // Курсор над ячейкой сетки: в этом состоянии R занята раскладкой
    // символов и не закрывает окно.
    public bool IsCellHovered => _data.HoveredCell >= 0;

    public void Tick(Keyboard? keyboard)
    {
        int hovered = _data.HoveredCell;
        if (hovered < 0 || keyboard == null || keyboard.ctrlKey.isPressed || _isTextFieldFocused())
        {
            return;
        }

        foreach (ProgrammatorKeymap.Binding binding in ProgrammatorKeymap.Bindings)
        {
            if (keyboard[binding.Key]?.wasPressedThisFrame != true ||
                keyboard.shiftKey.isPressed != binding.Shift)
            {
                continue;
            }

            int row = hovered / ProgrammatorData.COLS;
            int col = hovered % ProgrammatorData.COLS;
            int idx = (_data.CurrentPage * ProgrammatorData.CELLS_PER_PAGE) + hovered;

            ProgAction[] cycle = binding.Cycle;
            int currentId = _data.Codes[idx];
            int index = -1;
            for (int i = 0; i < cycle.Length; i++)
            {
                if ((int)cycle[i] == currentId)
                {
                    index = i;
                }
            }

            ProgAction next = cycle[(index + 1) % cycle.Length];
            _data.PushUndo();
            _data.Codes[idx] = (int)next;
            _updateCell(row, col);
            return;
        }
    }

    // Сброс при закрытии окна: display:none не всегда шлёт PointerLeave,
    // поэтому HoveredCell мог остаться выставленным.
    public void Reset()
    {
        _data.HoveredCell = -1;
    }
}
