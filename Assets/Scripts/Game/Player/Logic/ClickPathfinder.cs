#nullable enable

using System.Collections.Generic;
using Kern.Core.Interfaces;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;
using UnityEngine;

namespace Kern.Player.Logic;

// Поиск маршрута для клик-движения (ЛКМ): A* по 4 направлениям.
// Проходимая клетка стоит StepCost, сплошная ломаемая - DigCost (её придётся
// бурить Bz-пакетом по дороге, это заметно дольше шага). Ломаемые породы
// допускаются в маршрут только при включённом автокопании (allowDigging):
// без него путь строится лишь по проходимым (isEmpty/passable) клеткам,
// породы обходятся, как и паки со стройблоками.
// Проходимость и бурые-небурые решает только конфиг клетки (cells.json
// сервера: isEmpty, is_diggable + бит Structure у стен/углов пака и
// стройблоков) - собственных списков типов маршрут не ведёт. Клик по
// непроходимой клетке пака/стройблока ведёт робота к ближайшей достижимой
// клетке рядом с целью (подойти к паку вплотную).
public static class ClickPathfinder
{
    private const int MaxExpandedNodes = 24000;
    private const int MaxPathLength = 2048;

    // Стоимость входа в клетку: проходимая - StepCost, сплошная ломаемая - DigCost
    // (бурить Bz-пакетом по дороге заметно дольше шага). Heuristic умножает Манхэттен
    // на StepCost - дешевле клетки в маршруте не бывает, оценка остаётся допустимой.
    private const int StepCost = 10;
    private const int DigCost = 100;

    [System.ThreadStatic]
    private static Dictionary<int, int>? s_cachedCameFrom;
    [System.ThreadStatic]
    private static Dictionary<int, int>? s_cachedCostSoFar;
    [System.ThreadStatic]
    private static MinHeap? s_cachedOpen;

    // Возвращает клетки маршрута в серверных координатах БЕЗ стартовой клетки:
    // от первого шага до цели включительно. null - путь не найден.
    // allowDigging: включённое автокопание - ломаемые сплошные породы входят
    // в маршрут с DigCost (по дороге бурим Bz-пакетом); выключенное - маршрут
    // лишь по проходимым (isEmpty/passable) клеткам.
    public static List<Vector2Int>? FindPath(
        IWorldDataStorage storage,
        IMapDataProvider mapDataProvider,
        Vector2Int start,
        Vector2Int target,
        bool allowDigging)
    {
        int width = mapDataProvider.WorldWidth;
        int height = mapDataProvider.WorldHeight;
        if (width <= 0 || height <= 0 ||
            !PlayerMovementValidator.IsWithinWorldBounds(start, width, height) ||
            !PlayerMovementValidator.IsWithinWorldBounds(target, width, height))
        {
            return null;
        }

        if (start == target)
        {
            return null;
        }

        // Клетка цели: >0 - обычный маршрут до неё (ломаемая сплошная тоже -
        // бурим до неё); <0 и это пак/стройблок (бит Structure) - ведём к
        // ближайшей достижимой клетке рядом (подойти к паку); <0 у прочих
        // неломаемых пород - маршрута нет.
        CellType targetCell = storage.GetCell(target.x, target.y);
        int targetCost = CellCost(targetCell, mapDataProvider, allowDigging);
        bool targetIsStructure = mapDataProvider
            .GetCellConfig(targetCell).Properties.HasFlag(CellConfigProperties.Structure);
        if (targetCost < 0 && !targetIsStructure)
        {
            return null;
        }

        int startIndex = start.y * width + start.x;
        int targetIndex = target.y * width + target.x;

        var cameFrom = s_cachedCameFrom ??= new Dictionary<int, int>(1 << 10);
        var costSoFar = s_cachedCostSoFar ??= new Dictionary<int, int>(1 << 10);
        var open = s_cachedOpen ??= new MinHeap();

        cameFrom.Clear();
        costSoFar.Clear();
        open.Clear();

        cameFrom[startIndex] = -1;
        costSoFar[startIndex] = 0;
        open.Push(Heuristic(start, target), 0, startIndex);

        int expanded = 0;
        bool found = false;

        // Ближайшая к цели достигнутая клетка: если сама цель непроходима
        // (закрыта), маршрут строится до неё.
        int bestNode = startIndex;
        int bestH = int.MaxValue;

        while (open.Count > 0)
        {
            open.Pop(out int f, out int g, out int current);

            // Ленивое удаление: в куче могут лежать устаревшие записи.
            if (g > costSoFar[current])
            {
                continue;
            }

            var currentPosition = new Vector2Int(current % width, current / width);
            int currentH = Heuristic(currentPosition, target);
            if (currentH < bestH)
            {
                bestH = currentH;
                bestNode = current;
            }

            if (current == targetIndex)
            {
                found = true;
                break;
            }

            if (++expanded > MaxExpandedNodes)
            {
                break;
            }

            int cx = current % width;
            int cy = current / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0) == (dy == 0))
                    {
                        continue; // только 4 направления
                    }

                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    {
                        continue;
                    }

                    int neighbor = ny * width + nx;
                    int cellCost = CellCost(storage, mapDataProvider, nx, ny, allowDigging);
                    if (cellCost < 0)
                    {
                        continue;
                    }

                    int newCost = g + cellCost;
                    if (costSoFar.TryGetValue(neighbor, out int oldCost) && newCost >= oldCost)
                    {
                        continue;
                    }

                    costSoFar[neighbor] = newCost;
                    cameFrom[neighbor] = current;

                    var neighborPos = new Vector2Int(nx, ny);
                    open.Push(newCost + Heuristic(neighborPos, target), newCost, neighbor);
                }
            }
        }

        // Нашли цель - идём до неё; не нашли - до ближайшей
        // достигнутой клетки. Если и старт - ближайшая, пути нет.
        int finalNode = found ? targetIndex : bestNode;
        if (finalNode == startIndex)
        {
            return null;
        }

        var path = new List<Vector2Int>();
        int node = finalNode;
        while (node != startIndex && cameFrom.TryGetValue(node, out int previous))
        {
            path.Add(new Vector2Int(node % width, node / width));
            if (path.Count > MaxPathLength)
            {
                return null;
            }

            node = previous;
        }

        path.Reverse();
        return path.Count > 0 ? path : null;
    }

    // Стоимость входа в клетку: -1 непроходимо насовсем (обход), StepCost -
    // можно идти, DigCost - сплошная ломаемая (бурим по дороге). Всё решает
    // конфиг клетки: Passable/Diggable/Structure из серверного cells.json.
    private static int CellCost(IWorldDataStorage storage, IMapDataProvider mapDataProvider, int x, int y, bool allowDigging)
    {
        return CellCost(storage.GetCell(x, y), mapDataProvider, allowDigging);
    }

    private static int CellCost(CellType cell, IMapDataProvider mapDataProvider, bool allowDigging)
    {
        // Стены/углы пака и стройблоки (бит Structure в конфиге клетки):
        // бурением не убираются - только обход.
        if (mapDataProvider.GetCellConfig(cell).Properties.HasFlag(CellConfigProperties.Structure))
        {
            return -1;
        }

        if (PlayerMovementValidator.IsPassable(cell, mapDataProvider.GetCellConfig(cell)))
        {
            return StepCost;
        }

        // Автокопание выключено (allowDigging: false): вскрывать породы
        // нельзя - через сплошные клетки маршрут не строится, только обход.
        if (!allowDigging)
        {
            return -1;
        }

        // Буром берутся только is_diggable-клетки серверного cells.json (бит
        // Diggable в конфиге): is_destructible-породы (Черноскал - бомбой)
        // под ветку бурения не попадают, как и всё неломаемое.
        if (!mapDataProvider.GetCellConfig(cell).Properties.HasFlag(CellConfigProperties.Diggable))
        {
            return -1;
        }

        return DigCost;
    }

    private static int Heuristic(Vector2Int from, Vector2Int to)
    {
        // Манхэттен * StepCost: допустимая эвристика (бурение только дороже).
        return (Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y)) * StepCost;
    }

    // Компактная бинарная куча по f (затем g) - в netstandard 2.1 нет PriorityQueue.
    private sealed class MinHeap
    {
        private readonly List<(int F, int G, int Index)> _items = new();

        public int Count => _items.Count;
        public void Clear() => _items.Clear();

        public void Push(int f, int g, int index)
        {
            _items.Add((f, g, index));
            SiftUp(_items.Count - 1);
        }

        public void Pop(out int f, out int g, out int index)
        {
            (f, g, index) = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            if (last > 0)
            {
                SiftDown(0);
            }
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Compare(_items[i], _items[parent]) >= 0)
                {
                    break;
                }

                (_items[i], _items[parent]) = (_items[parent], _items[i]);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;
                if (left < _items.Count && Compare(_items[left], _items[smallest]) < 0)
                {
                    smallest = left;
                }

                if (right < _items.Count && Compare(_items[right], _items[smallest]) < 0)
                {
                    smallest = right;
                }

                if (smallest == i)
                {
                    break;
                }

                (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                i = smallest;
            }
        }

        private static int Compare((int F, int G, int Index) a, (int F, int G, int Index) b)
        {
            int byF = a.F.CompareTo(b.F);
            return byF != 0 ? byF : a.G.CompareTo(b.G);
        }
    }
}
