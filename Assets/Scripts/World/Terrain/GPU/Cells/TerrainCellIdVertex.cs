#nullable enable

using System.Runtime.InteropServices;

namespace Kern.World.Terrain;

// Вершина меша идентификаторов: POSITION = (x, y, слой, номер угла квада)
// в половинной точности — 8 байт. Половинка хранит целые до 2048 точно,
// поэтому сетка ограничена TerrainCellIdVertex.MaxAddress по каждой оси.
[StructLayout(LayoutKind.Sequential)]
public readonly struct TerrainCellIdVertex
{
    public const int MaxAddress = 2048;

    public readonly ushort X;
    public readonly ushort Y;
    public readonly ushort Layer;
    public readonly ushort Corner;

    public TerrainCellIdVertex(int x, int y, int layer, int corner)
    {
        X = Half(x);
        Y = Half(y);
        Layer = Half(layer);
        Corner = Half(corner);
    }

    // Точная половинка неотрицательного целого не больше MaxAddress: без
    // Mathf, чтобы Burst-джоб собирал меш сам.
    public static ushort Half(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        int exponent = 0;
        while ((value >> (exponent + 1)) != 0)
        {
            exponent++;
        }

        int mantissa = exponent <= 10 ? (value << (10 - exponent)) & 0x3FF : (value >> (exponent - 10)) & 0x3FF;
        return (ushort)(((exponent + 15) << 10) | mantissa);
    }
}
