#nullable enable

using Kern.Core;
using UnityEngine;

namespace Kern.UI;

public static class MenuStarfieldLook
{
    public const float Density = 96f;
    public const float Brightness = 1.6f;
    public const float CoreSize = 0.010f;
    public const float GlowSize = 0.06f;
    public const float TwinkleAmount = 0.1f;
    public const float TwinkleSpeed = 0.8f;
    public static Color SkyColor => new(0.012f, 0.018f, 0.032f, 1f);
    public const float NebulaIntensity = 0.65f;
    public static Color NebulaColor1 => new(0.025f, 0.055f, 0.11f, 1f);
    public static Color NebulaColor2 => new(0.09f, 0.045f, 0.025f, 1f);

    public const float NebulaFbmStartAmplitude = 0.5f;
    public const float NebulaFbmFrequencyScale = 2.1f;
    public const float NebulaFbmAmplitudeDecay = 0.5f;
    public const int NebulaFbmOctaves = 3;
    public const float NebulaCoordinateScale = 1.5f;
    public static Vector2 NebulaFbmShift => new(100f, 100f);
    public static Vector2 NebulaWarpOffset => new(5.2f, 1.3f);
    public const float NebulaWarpStrength = 0.7f;
    public const float NebulaDustThresholdStart = 0.34f;
    public const float NebulaDustThresholdEnd = 0.76f;
    public const float NebulaGasThresholdStart = 0.42f;
    public const float NebulaGasThresholdEnd = 0.82f;
    public const float NebulaGasContrast = 1.15f;
    public const float NebulaGasMix = 0.65f;

    public const float StarPresenceThreshold = 0.62f;
    public const float StarMagnitudePower = 6f;
    public const float StarMinimumRadiusScale = 0.55f;
    public const float StarMagnitudeRadiusScale = 1.9f;
    public const float StarWingDistanceScale = 42f;
    public const float StarWingMix = 0.1f;
    public const float StarRateMinimum = 0.5f;
    public const float StarRateRange = 1.5f;
    public const float StarSensorBreathingAmplitude = 0.05f;
    public const float StarMagnitudeFloor = 0.03f;
    public const float StarPresenceHashOffset = 7.13f;
    public const float StarMagnitudeHashOffset = 19.7f;
    public const float StarPhaseHashOffset = 3.77f;
    public const float StarRateHashOffset = 11.3f;
    public const float StarColorHashOffset = 5.19f;
    public const float SecondaryStarDensityScale = 0.43f;
    public const float SecondaryStarSizeScale = 0.22f;
    public const float SecondaryStarSeed = 41.7f;
    public const float SecondaryStarBrightness = 1.6f;

    public static Vector3 StarColorBlue => new(0.72f, 0.80f, 1f);
    public static Vector3 StarColorWhite => new(1f, 0.98f, 0.96f);
    public static Vector3 StarColorYellow => new(1f, 0.93f, 0.78f);
    public static Vector3 StarColorOrange => new(1f, 0.80f, 0.60f);
    public static Vector3 StarColorRed => new(1f, 0.68f, 0.50f);
    public const float StarColorBlueEnd = 0.22f;
    public const float StarColorYellowEnd = 0.48f;
    public const float StarColorOrangeEnd = 0.75f;
}
