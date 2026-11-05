#nullable enable

using System.Collections.Generic;

namespace Kern.Core.Models;

/// <summary>
/// Геометрия превью установки предмета из <c>SelectItemPacket (18)</c>: сетка
/// Width×Height, дистанция от робота и маска клеток по строкам — индекс
/// <c>row * Width + column</c> (контракт разворота на сервере, ProtocolInventory).
/// Нулевая геометрия (Width или Height = 0) означает «предмет не устанавливается»:
/// для таких предметов (бомбы, расходники) превью не строится.
/// </summary>
public sealed record ItemPlacement(
    int Width,
    int Height,
    int Distance,
    bool Rotating,
    IReadOnlyList<bool> Cells);
