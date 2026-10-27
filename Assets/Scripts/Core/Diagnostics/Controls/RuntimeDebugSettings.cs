#nullable enable
using Kern.Core.Interfaces;


namespace Kern.Core.Diagnostics;

public sealed class RuntimeDebugSettings : IRuntimeDebugSettings
{
    public bool IgnoreCollision { get; set; }
    public bool BypassLightingCompute { get; set; }
    public bool BypassTerrainDraw { get; set; }
    public bool BypassCPUMeshRebuild { get; set; }
    public bool ShowRobotDebugVisuals { get; set; }
    public bool BypassGameUI { get; set; }
    public TailMotionDebugMode TailMotionDebugMode { get; set; } = TailMotionDebugMode.Normal;
}
