namespace Rainbow;

internal static class SolarSpectrum
{
    private const double PlanckConstant = 6.62607015e-34;
    private const double LightSpeed = 299792458.0;
    private const double BoltzmannConstant = 1.380649e-23;

    public static double Radiance(double wavelengthNanometers, double temperature)
    {
        var wavelength = wavelengthNanometers * 1e-9;
        var exponent = PlanckConstant * LightSpeed / (wavelength * BoltzmannConstant * temperature);
        return 2.0 * PlanckConstant * LightSpeed * LightSpeed / (Math.Pow(wavelength, 5) * (Math.Exp(exponent) - 1.0));
    }

    public static (double X, double Y, double Z) ColorMatching(double wavelengthNanometers)
    {
        var x = 0.362 * Lobe(wavelengthNanometers, 442.0, 0.0624, 0.0374)
            + 1.056 * Lobe(wavelengthNanometers, 599.8, 0.0264, 0.0323)
            - 0.065 * Lobe(wavelengthNanometers, 501.1, 0.0490, 0.0382);
        var y = 0.821 * Lobe(wavelengthNanometers, 568.8, 0.0213, 0.0247)
            + 0.286 * Lobe(wavelengthNanometers, 530.9, 0.0613, 0.0322);
        var z = 1.217 * Lobe(wavelengthNanometers, 437.0, 0.0845, 0.0278)
            + 0.681 * Lobe(wavelengthNanometers, 459.0, 0.0385, 0.0725);
        return (x, y, z);
    }

    public static (double R, double G, double B) ToLinearSrgb(double x, double y, double z)
        => (
            3.2406 * x - 1.5372 * y - 0.4986 * z,
            -0.9689 * x + 1.8758 * y + 0.0415 * z,
            0.0557 * x - 0.2040 * y + 1.0570 * z);

    private static double Lobe(double wavelength, double center, double leftScale, double rightScale)
    {
        var t = (wavelength - center) * (wavelength < center ? leftScale : rightScale);
        return Math.Exp(-0.5 * t * t);
    }
}

internal sealed class RainbowPhysicalTable
{
    private RainbowPhysicalTable(float[] primary, float[] secondary, float[] shadow)
    {
        Primary = primary;
        Secondary = secondary;
        Shadow = shadow;
    }

    public float[] Primary { get; }

    public float[] Secondary { get; }

    public float[] Shadow { get; }

    public static RainbowPhysicalTable Build(double radiusMillimeters, double sunDiameterDegrees)
    {
        var width = RainbowSettings.TableWidth;
        var count = RainbowSettings.GetWavelengthCount();
        var weights = new (double X, double Y, double Z)[count];
        var primary = new PreparedRainbow[count];
        var secondary = new PreparedRainbow[count];
        var white = (X: 0.0, Y: 0.0, Z: 0.0);
        for (var index = 0; index < count; index++)
        {
            var wavelength = RainbowSettings.WavelengthStartNanometers + index * RainbowSettings.WavelengthStepNanometers;
            var radiance = SolarSpectrum.Radiance(wavelength, RainbowSettings.SolarTemperature);
            var (x, y, z) = SolarSpectrum.ColorMatching(wavelength);
            weights[index] = (radiance * x, radiance * y, radiance * z);
            white = (white.X + weights[index].X, white.Y + weights[index].Y, white.Z + weights[index].Z);
            var refractiveIndex = WaterOptics.RefractiveIndex(wavelength);
            var sizeParameter = RainbowSettings.GetSizeParameter(radiusMillimeters, wavelength);
            primary[index] = RainbowOptics.Prepare(RainbowOptics.Solve(RainbowOptics.PrimaryReflections, refractiveIndex), sizeParameter);
            secondary[index] = RainbowOptics.Prepare(RainbowOptics.Solve(RainbowOptics.SecondaryReflections, refractiveIndex), sizeParameter);
        }

        var (whiteRed, whiteGreen, whiteBlue) = SolarSpectrum.ToLinearSrgb(white.X, white.Y, white.Z);
        var primaryColors = new double[width * 3];
        var secondaryColors = new double[width * 3];
        for (var index = 0; index < width; index++)
        {
            var scattering = Math.PI - RainbowSettings.GetTableAngleDegrees(index) * RainbowSettings.RadiansPerDegree;
            var primaryXyz = (X: 0.0, Y: 0.0, Z: 0.0);
            var secondaryXyz = (X: 0.0, Y: 0.0, Z: 0.0);
            for (var wave = 0; wave < count; wave++)
            {
                var first = primary[wave].Evaluate(scattering);
                var second = secondary[wave].Evaluate(scattering);
                primaryXyz = (primaryXyz.X + first * weights[wave].X, primaryXyz.Y + first * weights[wave].Y, primaryXyz.Z + first * weights[wave].Z);
                secondaryXyz = (secondaryXyz.X + second * weights[wave].X, secondaryXyz.Y + second * weights[wave].Y, secondaryXyz.Z + second * weights[wave].Z);
            }

            var (pr, pg, pb) = SolarSpectrum.ToLinearSrgb(primaryXyz.X, primaryXyz.Y, primaryXyz.Z);
            var (sr, sg, sb) = SolarSpectrum.ToLinearSrgb(secondaryXyz.X, secondaryXyz.Y, secondaryXyz.Z);
            primaryColors[index * 3] = pr / whiteRed;
            primaryColors[index * 3 + 1] = pg / whiteGreen;
            primaryColors[index * 3 + 2] = pb / whiteBlue;
            secondaryColors[index * 3] = sr / whiteRed;
            secondaryColors[index * 3 + 1] = sg / whiteGreen;
            secondaryColors[index * 3 + 2] = sb / whiteBlue;
        }

        var kernel = SunKernel(sunDiameterDegrees);
        var smoothedPrimary = Convolve(primaryColors, kernel);
        var smoothedSecondary = Convolve(secondaryColors, kernel);
        var peak = 0.0;
        foreach (var value in smoothedPrimary)
            peak = Math.Max(peak, value);
        var scale = peak > 0.0 ? 1.0 / peak : 0.0;

        var shadow = BuildShadow(radiusMillimeters, sunDiameterDegrees);
        var taper = new double[width];
        for (var index = 0; index < width; index++)
        {
            var angle = RainbowSettings.GetTableAngleDegrees(index);
            taper[index] = SmoothStep(RainbowSettings.TableStartDegrees, RainbowSettings.TableStartDegrees + RainbowSettings.TableInnerTaperDegrees, angle)
                * (1.0 - SmoothStep(RainbowSettings.TableEndDegrees - RainbowSettings.TableOuterTaperDegrees, RainbowSettings.TableEndDegrees, angle));
        }

        return new RainbowPhysicalTable(
            smoothedPrimary.Select((value, index) => (float)(Math.Max(value, 0.0) * scale * taper[index / 3])).ToArray(),
            smoothedSecondary.Select((value, index) => (float)(Math.Max(value, 0.0) * scale * taper[index / 3])).ToArray(),
            shadow);
    }

    private static double[] SunKernel(double sunDiameterDegrees)
    {
        var radius = Math.Clamp(sunDiameterDegrees, 0.0, RainbowSettings.MaximumSunDiameterDegrees) / 2.0;
        var step = RainbowSettings.GetTableStepDegrees();
        var half = (int)Math.Floor(radius / step);
        if (half == 0)
            return [1.0];

        var kernel = new double[2 * half + 1];
        var total = 0.0;
        for (var offset = -half; offset <= half; offset++)
        {
            var distance = offset * step / radius;
            var weight = Math.Sqrt(Math.Max(1.0 - distance * distance, 0.0));
            kernel[offset + half] = weight;
            total += weight;
        }

        for (var index = 0; index < kernel.Length; index++)
            kernel[index] /= total;
        return kernel;
    }

    private static double[] Convolve(double[] colors, double[] kernel)
    {
        var width = RainbowSettings.TableWidth;
        var half = kernel.Length / 2;
        var result = new double[colors.Length];
        for (var index = 0; index < width; index++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                var sum = 0.0;
                for (var tap = -half; tap <= half; tap++)
                {
                    var source = Math.Clamp(index + tap, 0, width - 1);
                    sum += kernel[tap + half] * colors[source * 3 + channel];
                }

                result[index * 3 + channel] = sum;
            }
        }

        return result;
    }

    private static float[] BuildShadow(double radiusMillimeters, double sunDiameterDegrees)
    {
        const double RedWavelength = 700.0;
        var index = WaterOptics.RefractiveIndex(RedWavelength);
        var sizeParameter = RainbowSettings.GetSizeParameter(radiusMillimeters, RedWavelength);
        var primary = RainbowOptics.Solve(RainbowOptics.PrimaryReflections, index);
        var secondary = RainbowOptics.Solve(RainbowOptics.SecondaryReflections, index);
        var primaryEdge = 180.0 - primary.RainbowAngle / RainbowSettings.RadiansPerDegree;
        var secondaryEdge = 180.0 - secondary.RainbowAngle / RainbowSettings.RadiansPerDegree;
        var sunRadius = Math.Clamp(sunDiameterDegrees, 0.0, RainbowSettings.MaximumSunDiameterDegrees) / 2.0;
        var primaryWidth = 1.0 / RainbowOptics.SizeScale(primary, sizeParameter) / RainbowSettings.RadiansPerDegree + sunRadius;
        var secondaryWidth = 1.0 / RainbowOptics.SizeScale(secondary, sizeParameter) / RainbowSettings.RadiansPerDegree + sunRadius;
        var shadow = new float[RainbowSettings.TableWidth];
        for (var position = 0; position < shadow.Length; position++)
        {
            var angle = RainbowSettings.GetTableAngleDegrees(position);
            var rising = SmoothStep(primaryEdge + RainbowSettings.ShadowRampStart * primaryWidth, primaryEdge + RainbowSettings.ShadowRampEnd * primaryWidth, angle);
            var falling = 1.0 - SmoothStep(secondaryEdge - RainbowSettings.ShadowRampEnd * secondaryWidth, secondaryEdge - RainbowSettings.ShadowRampStart * secondaryWidth, angle);
            shadow[position] = (float)(rising * falling);
        }

        return shadow;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}

internal static class RainbowTable
{
    public static Half[] Compose(RainbowPhysicalTable physical, double secondaryStrength, double darkness, double saturation)
    {
        var width = RainbowSettings.TableWidth;
        var texels = new Half[width * 4];
        var secondary = Math.Clamp(secondaryStrength, 0.0, 1.0);
        var band = Math.Clamp(darkness, 0.0, 1.0);
        var chroma = Math.Clamp(saturation, 0.0, 2.0);
        for (var index = 0; index < width; index++)
        {
            var red = physical.Primary[index * 3] + secondary * physical.Secondary[index * 3];
            var green = physical.Primary[index * 3 + 1] + secondary * physical.Secondary[index * 3 + 1];
            var blue = physical.Primary[index * 3 + 2] + secondary * physical.Secondary[index * 3 + 2];
            var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
            texels[index * 4] = (Half)Math.Max(luminance + chroma * (red - luminance), 0.0);
            texels[index * 4 + 1] = (Half)Math.Max(luminance + chroma * (green - luminance), 0.0);
            texels[index * 4 + 2] = (Half)Math.Max(luminance + chroma * (blue - luminance), 0.0);
            texels[index * 4 + 3] = (Half)(1.0 - band * physical.Shadow[index]);
        }

        return texels;
    }

    public static (float Red, float Green, float Blue, float Gain) Sample(Half[] texels, double angleDegrees)
    {
        var position = (angleDegrees - RainbowSettings.TableStartDegrees) / (RainbowSettings.TableEndDegrees - RainbowSettings.TableStartDegrees) * (RainbowSettings.TableWidth - 1);
        if (position < 0.0 || position > RainbowSettings.TableWidth - 1)
            return (0f, 0f, 0f, 1f);

        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, RainbowSettings.TableWidth - 1);
        var fraction = (float)(position - lower);
        return (
            Mix((float)texels[lower * 4], (float)texels[upper * 4], fraction),
            Mix((float)texels[lower * 4 + 1], (float)texels[upper * 4 + 1], fraction),
            Mix((float)texels[lower * 4 + 2], (float)texels[upper * 4 + 2], fraction),
            Mix((float)texels[lower * 4 + 3], (float)texels[upper * 4 + 3], fraction));
    }

    private static float Mix(float first, float second, float fraction)
        => first + (second - first) * fraction;
}
