using System.Numerics;

namespace Rainbow;

internal static class RainbowSettings
{
    public const int TableWidth = 1024;
    public const int ColorChannels = 3;
    public const int TexelChannels = 4;
    public const int TexelBytes = TexelChannels * sizeof(ushort);
    public const double TableStartDegrees = 20.0;
    public const double TableEndDegrees = 64.0;
    public const double TableInnerTaperDegrees = 10.0;
    public const double TableOuterTaperDegrees = 4.0;
    public const double WavelengthStartNanometers = 380.0;
    public const double WavelengthEndNanometers = 780.0;
    public const double WavelengthStepNanometers = 10.0;
    public const double SolarTemperature = 5772.0;
    public const double RedEdgeWavelengthNanometers = 700.0;
    public const double NanometersPerMillimeter = 1e6;
    public const double ShadowRampStart = 0.5;
    public const double ShadowRampEnd = 2.5;
    public const double MinimumRadiusMillimeters = 0.05;
    public const double MaximumRadiusMillimeters = 0.4;
    public const double MaximumSunDiameterDegrees = 2.0;
    public const double MinimumFieldOfViewDegrees = 10.0;
    public const double MaximumFieldOfViewDegrees = 150.0;
    public const double RadiansPerDegree = Math.PI / 180.0;

    public static readonly float MinimumHalfFieldTangent = GetHalfFieldOfViewTangent(MinimumFieldOfViewDegrees);
    public static readonly float MaximumHalfFieldTangent = GetHalfFieldOfViewTangent(MaximumFieldOfViewDegrees);

    public static double GetTableStepDegrees()
        => (TableEndDegrees - TableStartDegrees) / (TableWidth - 1);

    public static double GetTableAngleDegrees(int index)
        => TableStartDegrees + index * GetTableStepDegrees();

    public static double GetSunRadiusDegrees(double sunDiameterDegrees)
        => Math.Clamp(sunDiameterDegrees, 0.0, MaximumSunDiameterDegrees) / 2.0;

    public static int GetWavelengthCount()
        => (int)Math.Round((WavelengthEndNanometers - WavelengthStartNanometers) / WavelengthStepNanometers) + 1;

    public static double GetSizeParameter(double radiusMillimeters, double wavelengthNanometers)
        => 2.0 * Math.PI * radiusMillimeters * NanometersPerMillimeter / wavelengthNanometers;

    public static float GetHalfFieldOfViewTangent(double fieldOfViewDegrees)
    {
        var field = Math.Clamp(fieldOfViewDegrees, MinimumFieldOfViewDegrees, MaximumFieldOfViewDegrees);
        return (float)Math.Tan(field * RadiansPerDegree / 2.0);
    }

    public static Vector3 GetAntisolarDirection(double sunElevationDegrees, double directionDegrees, double pitchDegrees)
    {
        var elevation = -Math.Clamp(sunElevationDegrees, -90.0, 90.0) * RadiansPerDegree;
        var direction = directionDegrees * RadiansPerDegree;
        var pitch = pitchDegrees * RadiansPerDegree;
        var x = Math.Cos(elevation) * Math.Sin(direction);
        var y = -Math.Sin(elevation);
        var z = Math.Cos(elevation) * Math.Cos(direction);
        var cameraY = y * Math.Cos(pitch) + z * Math.Sin(pitch);
        var cameraZ = -y * Math.Sin(pitch) + z * Math.Cos(pitch);
        return new Vector3((float)x, (float)cameraY, (float)cameraZ);
    }
}
