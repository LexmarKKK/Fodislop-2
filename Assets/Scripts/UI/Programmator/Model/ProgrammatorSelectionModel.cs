#nullable enable

using System;
using System.Collections.Generic;
using MinesServer.Data;
using UnityEngine;

namespace Kern.UI.Programmator;

// Selection state for the programmator grid: which cells are currently
// selected (either a single rectangular drag range or an arbitrary
// ctrl-clicked set) and the border highlighting that reflects it.
// Visual updates go through the setSelectionBorder callback supplied by
// the owner so this class never touches VisualElements directly.
internal sealed class ProgrammatorSelectionModel
{
    private readonly Action<int, int, bool> _setSelectionBorder;
    private readonly ProgrammatorData _data;

    private bool _hasSelection;
    private bool _dragging;
    private bool _dragMoved;
    private bool _hadSelectionAtPress;
    private int _selStartRow;
    private int _selStartCol;
    private int _selEndRow;
    private int _selEndCol;
    private readonly HashSet<long> _selectedCells = new HashSet<long>();

    public ProgrammatorSelectionModel(
        Action<int, int, bool> setSelectionBorder,
        ProgrammatorData data)
    {
        _setSelectionBorder = setSelectionBorder ?? throw new ArgumentNullException(nameof(setSelectionBorder));
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public bool HasSelection
    {
        get => _hasSelection;
        set => _hasSelection = value;
    }

    public int SelStartRow
    {
        get => _selStartRow;
        set => _selStartRow = value;
    }

    public int SelStartCol
    {
        get => _selStartCol;
        set => _selStartCol = value;
    }

    public int SelEndRow
    {
        get => _selEndRow;
        set => _selEndRow = value;
    }

    public int SelEndCol
    {
        get => _selEndCol;
        set => _selEndCol = value;
    }

    public HashSet<long> SelectedCells => _selectedCells;

    public bool IsSelected(int row, int col)
    {
        if (_selectedCells.Count > 0)
        {
            return _selectedCells.Contains(((long)row * ProgrammatorData.COLS) + col);
        }

        if (!_hasSelection)
        {
            return false;
        }

        int minRow = Mathf.Min(_selStartRow, _selEndRow);
        int maxRow = Mathf.Max(_selStartRow, _selEndRow);
        int minCol = Mathf.Min(_selStartCol, _selEndCol);
        int maxCol = Mathf.Max(_selStartCol, _selEndCol);
        return row >= minRow && row <= maxRow && col >= minCol && col <= maxCol;
    }

    public void SetSelectionBorder(int row, int col, bool selected)
    {
        _setSelectionBorder(row, col, selected);
    }

    public void RefreshSelectionBorders()
    {
        for (int r = 0; r < ProgrammatorData.ROWS; r++)
        {
            for (int c = 0; c < ProgrammatorData.COLS; c++)
            {
                SetSelectionBorder(r, c, IsSelected(r, c));
            }
        }
    }

    public void ToggleCellSelection(int row, int col)
    {
        long key = ((long)row * ProgrammatorData.COLS) + col;
        if (!_selectedCells.Remove(key))
        {
            if (_hasSelection)
            {
                int minR = Mathf.Min(_selStartRow, _selEndRow);
                int maxR = Mathf.Max(_selStartRow, _selEndRow);
                int minC = Mathf.Min(_selStartCol, _selEndCol);
                int maxC = Mathf.Max(_selStartCol, _selEndCol);
                for (int r = minR; r <= maxR; r++)
                {
                    for (int c = minC; c <= maxC; c++)
                    {
                        _selectedCells.Add(((long)r * ProgrammatorData.COLS) + c);
                        SetSelectionBorder(r, c, true);
                    }
                }

                _hasSelection = false;
            }

            _selectedCells.Add(key);
            SetSelectionBorder(row, col, true);
        }
        else
        {
            SetSelectionBorder(row, col, false);
        }
    }

    public void SelectCell(int row, int col)
    {
        ClearSelection();
        _selStartRow = _selEndRow = row;
        _selStartCol = _selEndCol = col;
        _hasSelection = true;
        SetSelectionBorder(row, col, true);
    }

    // ─── Выделение протяжкой (зажать ЛКМ и вести) ───
    // Клик без протяжки — переключатель: не было выделения — выделяется
    // кликнутая ячейка; было — выделение снимается («нажал — и его нет»).
    // Протяжка от точки нажатия всегда выделяет прямоугольник.

    public bool IsDragging => _dragging;

    public void BeginDragSelect(int row, int col)
    {
        _hadSelectionAtPress = HasAnySelection();
        ClearSelection();
        _dragging = true;
        _dragMoved = false;
        _selStartRow = _selEndRow = row;
        _selStartCol = _selEndCol = col;
        _hasSelection = true;
        SetSelectionBorder(row, col, true);
    }

    public void DragSelectTo(int row, int col)
    {
        if (!_dragging)
        {
            return;
        }

        row = Mathf.Clamp(row, 0, ProgrammatorData.ROWS - 1);
        col = Mathf.Clamp(col, 0, ProgrammatorData.COLS - 1);
        if (row == _selEndRow && col == _selEndCol)
        {
            return;
        }

        _selEndRow = row;
        _selEndCol = col;
        _dragMoved = true;
        RefreshSelectionBorders();
    }

    public void EndDragSelect()
    {
        _dragging = false;
        if (_dragMoved)
        {
            // Протяжка — прямоугольник остаётся выделенным.
            return;
        }

        if (_hadSelectionAtPress)
        {
            // Клик без протяжки при активном выделении — снимаем его.
            ClearSelection();
        }

        // Иначе клик по свободной сетке — одиночная ячейка остаётся выделенной.
    }

    public void ClearSelection()
    {
        // Рамки снимаем со всех ячеек без исключений: пропуск ячейки под
        // курсором оставлял ей класс выделения навсегда (hover-подсветка —
        // отдельный класс и от --selected не зависит).
        if (_selectedCells.Count > 0)
        {
            foreach (long key in _selectedCells)
            {
                int r = (int)(key / ProgrammatorData.COLS);
                int c = (int)(key % ProgrammatorData.COLS);
                SetSelectionBorder(r, c, false);
            }

            _selectedCells.Clear();
        }

        if (_hasSelection)
        {
            int minRow = Mathf.Min(_selStartRow, _selEndRow);
            int maxRow = Mathf.Max(_selStartRow, _selEndRow);
            int minCol = Mathf.Min(_selStartCol, _selEndCol);
            int maxCol = Mathf.Max(_selStartCol, _selEndCol);
            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    SetSelectionBorder(r, c, false);
                }
            }

            _hasSelection = false;
        }
    }

    public (int minRow, int maxRow, int minCol, int maxCol) GetSetBounds()
    {
        int minR = int.MaxValue, maxR = int.MinValue;
        int minC = int.MaxValue, maxC = int.MinValue;
        foreach (long key in _selectedCells)
        {
            int r = (int)(key / ProgrammatorData.COLS);
            int c = (int)(key % ProgrammatorData.COLS);
            if (r < minR)
            {
                minR = r;
            }

            if (r > maxR)
            {
                maxR = r;
            }

            if (c < minC)
            {
                minC = c;
            }

            if (c > maxC)
            {
                maxC = c;
            }
        }

        return (minR, maxR, minC, maxC);
    }

    public bool HasAnySelection() => _hasSelection || _selectedCells.Count > 0;
}
