#nullable enable

namespace Kern.Core;

public static class NetworkRuntimeLimits
{
    public const int MaximumPacketBatchPerFrame = 250;
    public const int MaximumQueuedPacketCount = 4096;
    public const long MaximumQueuedPacketBytes = 16L * 1024 * 1024;

}
