#nullable enable

using Kern.World;
using MinesServer.Data;

namespace Kern.Networking.Offline;

internal static class DummyMapColors
{
    public static int Get(int cellId)
    {
        if (cellId is < 0 or > byte.MaxValue)
        {
            return unchecked((int)0xFF808080);
        }

        return MapBlockColors.GetPackedColor((CellType)cellId);
    }
}
