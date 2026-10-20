namespace Rainbow;

internal readonly record struct Tristimulus(double X, double Y, double Z)
{
    public static Tristimulus operator +(Tristimulus left, Tristimulus right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Tristimulus operator *(double scale, Tristimulus value)
        => new(scale * value.X, scale * value.Y, scale * value.Z);
}

internal readonly record struct ColorLobe(double Amplitude, double Center, double LeftScale, double RightScale)
{
    public double Evaluate(double wavelength)
    {
        var t = (wavelength - Center) * (wavelength < Center ? LeftScale : RightScale);
        return Amplitude * Math.Exp(-0.5 * t * t);
    }
}

internal static class SolarSpectrum
{
    private const double PlanckConstant = 6.62607015e-34;
    private const double LightSpeed = 299792458.0;
    private const double BoltzmannConstant = 1.380649e-23;

    private static readonly ColorLobe[] MatchingLobesX =
    [
        new(0.362, 442.0, 0.0624, 0.0374),
        new(1.056, 599.8, 0.0264, 0.0323),
        new(-0.065, 501.1, 0.0490, 0.0382),
    ];

    private static readonly ColorLobe[] MatchingLobesY =
    [
        new(0.821, 568.8, 0.0213, 0.0247),
        new(0.286, 530.9, 0.0613, 0.0322),
    ];

    private static readonly ColorLobe[] MatchingLobesZ =
    [
        new(1.217, 437.0, 0.0845, 0.0278),
        new(0.681, 459.0, 0.0385, 0.0725),
    ];

    public static double Radiance(double wavelengthNanometers, double temperature)
    {
        var wavelength = wavelengthNanometers * 1e-9;
        var exponent = PlanckConstant * LightSpeed / (wavelength * BoltzmannConstant * temperature);
        return 2.0 * PlanckConstant * LightSpeed * LightSpeed / (Math.Pow(wavelength, 5) * (Math.Exp(exponent) - 1.0));
    }

    public static Tristimulus ColorMatching(double wavelengthNanometers)
        => new(Sum(MatchingLobesX, wavelengthNanometers), Sum(MatchingLobesY, wavelengthNanometers), Sum(MatchingLobesZ, wavelengthNanometers));

    public static (double R, double G, double B) ToLinearSrgb(Tristimulus value)
        => (
            3.2406 * value.X - 1.5372 * value.Y - 0.4986 * value.Z,
            -0.9689 * value.X + 1.8758 * value.Y + 0.0415 * value.Z,
            0.0557 * value.X - 0.2040 * value.Y + 1.0570 * value.Z);

    private static double Sum(ColorLobe[] lobes, double wavelength)
    {
        var sum = 0.0;
        foreach (var lobe in lobes)
            sum += lobe.Evaluate(wavelength);
        return sum;
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
        var weights = new Tristimulus[count];
        var primary = new PreparedRainbow[count];
        var secondary = new PreparedRainbow[count];
        var white = new Tristimulus();
        for (var index = 0; index < count; index++)
        {
            var wavelength = RainbowSettings.WavelengthStartNanometers + index * RainbowSettings.WavelengthStepNanometers;
            var radiance = SolarSpectrum.Radiance(wavelength, RainbowSettings.SolarTemperature);
            weights[index] = radiance * SolarSpectrum.ColorMatching(wavelength);
            white += weights[index];
            var refractiveIndex = WaterOptics.RefractiveIndex(wavelength);
            var sizeParameter = RainbowSettings.GetSizeParameter(radiusMillimeters, wavelength);
            primary[index] = RainbowOptics.Prepare(RainbowOptics.Solve(RainbowOptics.PrimaryReflections, refractiveIndex), sizeParameter);
            secondary[index] = RainbowOptics.Prepare(RainbowOptics.Solve(RainbowOptics.SecondaryReflections, refractiveIndex), sizeParameter);
        }

        var (whiteRed, whiteGreen, whiteBlue) = SolarSpectrum.ToLinearSrgb(white);
        var primaryColors = new double[width * RainbowSettings.ColorChannels];
        var secondaryColors = new double[width * RainbowSettings.ColorChannels];
        for (var index = 0; index < width; index++)
        {
            var scattering = Math.PI - RainbowSettings.GetTableAngleDegrees(index) * RainbowSettings.RadiansPerDegree;
            var primaryXyz = new Tristimulus();
            var secondaryXyz = new Tristimulus();
            for (var wave = 0; wave < count; wave++)
            {
                primaryXyz += primary[wave].Evaluate(scattering) * weights[wave];
                secondaryXyz += secondary[wave].Evaluate(scattering) * weights[wave];
            }

            var (pr, pg, pb) = SolarSpectrum.ToLinearSrgb(primaryXyz);
            var (sr, sg, sb) = SolarSpectrum.ToLinearSrgb(secondaryXyz);
            var offset = index * RainbowSettings.ColorChannels;
            primaryColors[offset] = pr / whiteRed;
            primaryColors[offset + 1] = pg / whiteGreen;
            primaryColors[offset + 2] = pb / whiteBlue;
            secondaryColors[offset] = sr / whiteRed;
            secondaryColors[offset + 1] = sg / whiteGreen;
            secondaryColors[offset + 2] = sb / whiteBlue;
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
            smoothedPrimary.Select((value, index) => (float)(Math.Max(value, 0.0) * scale * taper[index / RainbowSettings.ColorChannels])).ToArray(),
            smoothedSecondary.Select((value, index) => (float)(Math.Max(value, 0.0) * scale * taper[index / RainbowSettings.ColorChannels])).ToArray(),
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
            for (var channel = 0; channel < RainbowSettings.ColorChannels; channel++)
            {
                var sum = 0.0;
                for (var tap = -half; tap <= half; tap++)
                {
                    var source = Math.Clamp(index + tap, 0, width - 1);
                    sum += kernel[tap + half] * colors[source * RainbowSettings.ColorChannels + channel];
                }

                result[index * RainbowSettings.ColorChannels + channel] = sum;
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
        var texels = new Half[width * RainbowSettings.TexelChannels];
        var secondary = Math.Clamp(secondaryStrength, 0.0, 1.0);
        var band = Math.Clamp(darkness, 0.0, 1.0);
        var chroma = Math.Clamp(saturation, 0.0, 2.0);
        for (var index = 0; index < width; index++)
        {
            var source = index * RainbowSettings.ColorChannels;
            var target = index * RainbowSettings.TexelChannels;
            var red = physical.Primary[source] + secondary * physical.Secondary[source];
            var green = physical.Primary[source + 1] + secondary * physical.Secondary[source + 1];
            var blue = physical.Primary[source + 2] + secondary * physical.Secondary[source + 2];
            var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
            texels[target] = (Half)Math.Max(luminance + chroma * (red - luminance), 0.0);
            texels[target + 1] = (Half)Math.Max(luminance + chroma * (green - luminance), 0.0);
            texels[target + 2] = (Half)Math.Max(luminance + chroma * (blue - luminance), 0.0);
            texels[target + 3] = (Half)(1.0 - band * physical.Shadow[index]);
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
        var first = lower * RainbowSettings.TexelChannels;
        var second = upper * RainbowSettings.TexelChannels;
        return (
            Mix((float)texels[first], (float)texels[second], fraction),
            Mix((float)texels[first + 1], (float)texels[second + 1], fraction),
            Mix((float)texels[first + 2], (float)texels[second + 2], fraction),
            Mix((float)texels[first + 3], (float)texels[second + 3], fraction));
    }

    private static float Mix(float first, float second, float fraction)
        => first + (second - first) * fraction;
}
