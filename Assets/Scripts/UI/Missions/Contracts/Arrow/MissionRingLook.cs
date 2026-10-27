#nullable enable

using Kern.Core;
using UnityEngine;

namespace Kern.UI;

public static class MissionRingLook
{
    public const float ViewportRadiusFraction = 0.34f;
    public const float CenterFadeDistance = 0.6f;
    public const float FullOpacityDistance = 2.2f;
    public const float MinimumVisibleOpacity = 0.001f;
    public const float Radius = 0.74f;
    public const float VerticalSquash = 0.90f;
    public const float ArcHalfAngle = 0.50f;
    public const float ArcThickness = 0.030f;
    public const float EndTaper = 0.45f;
    public const float CoreWidth = 0.35f;
    public const float ArcEnergy = 1f;
    public const float CoreEnergy = 0.85f;
    public static Color CoreColor => new(1f, 0.95f, 0.86f, 1f);
}
