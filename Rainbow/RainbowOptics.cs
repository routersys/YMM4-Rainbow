namespace Rainbow;

internal static class WaterOptics
{
    private const double MicrometersPerNanometer = 1e-3;
    private const double AirRefractiveIndex = 1.000272;

    private static readonly (double Strength, double Resonance)[] SellmeierTerms =
    [
        (5.684027565e-1, 5.101829712e-3),
        (1.726177391e-1, 1.821153936e-2),
        (2.086189578e-2, 2.620722293e-2),
        (1.130748688e-1, 1.069792721e1),
    ];

    public static double RefractiveIndex(double wavelengthNanometers)
    {
        var micrometers = wavelengthNanometers * MicrometersPerNanometer;
        var squared = micrometers * micrometers;
        var sum = 0.0;
        foreach (var (strength, resonance) in SellmeierTerms)
            sum += strength * squared / (squared - resonance);
        return Math.Sqrt(1.0 + sum) / AirRefractiveIndex;
    }
}

internal static class AiryFunction
{
    private const double AiAtZero = 0.355028053887817239;
    private const double NegativeAiPrimeAtZero = 0.258819403792806798;
    private const double SeriesLimit = 5.0;
    private const int AsymptoticTerms = 12;

    private const double SquaredStart = -100.0;
    private const double SquaredEnd = 12.0;
    private const double SquaredStep = 0.002;
    private static readonly Lazy<double[]> SquaredTable = new(BuildSquaredTable);

    public static double AiSquared(double x)
    {
        if (x > SquaredEnd)
            return 0.0;
        if (x < SquaredStart + SquaredStep)
        {
            var value = Ai(x);
            return value * value;
        }

        var table = SquaredTable.Value;
        var position = (x - SquaredStart) / SquaredStep;
        var index = (int)position;
        var t = position - index;
        var y0 = table[index - 1];
        var y1 = table[index];
        var y2 = table[index + 1];
        var y3 = table[index + 2];
        return y0 * (-t * (t - 1.0) * (t - 2.0) / 6.0)
            + y1 * ((t + 1.0) * (t - 1.0) * (t - 2.0) / 2.0)
            + y2 * (-(t + 1.0) * t * (t - 2.0) / 2.0)
            + y3 * ((t + 1.0) * t * (t - 1.0) / 6.0);
    }

    private static double[] BuildSquaredTable()
    {
        var count = (int)Math.Round((SquaredEnd - SquaredStart) / SquaredStep) + 3;
        var table = new double[count];
        for (var index = 0; index < count; index++)
        {
            var value = Ai(SquaredStart + index * SquaredStep);
            table[index] = value * value;
        }

        return table;
    }

    public static double Ai(double x)
    {
        if (x > SeriesLimit)
            return DecayingAsymptotic(x);
        if (x < -SeriesLimit - 1.0)
            return OscillatoryAsymptotic(-x);
        return MaclaurinSeries(x);
    }

    private static double MaclaurinSeries(double x)
    {
        var cube = x * x * x;
        var f = 1.0;
        var g = x;
        var sumF = f;
        var sumG = g;
        for (var k = 0; k < 80; k++)
        {
            f *= cube / ((3 * k + 2) * (3 * k + 3));
            g *= cube / ((3 * k + 3) * (3 * k + 4));
            sumF += f;
            sumG += g;
            if (Math.Abs(f) + Math.Abs(g) < 1e-18 * (Math.Abs(sumF) + Math.Abs(sumG)) && k > 4)
                break;
        }

        return AiAtZero * sumF - NegativeAiPrimeAtZero * sumG;
    }

    private static double DecayingAsymptotic(double x)
    {
        var zeta = 2.0 / 3.0 * x * Math.Sqrt(x);
        var term = 1.0;
        var sum = 1.0;
        for (var k = 1; k < AsymptoticTerms; k++)
        {
            term *= -AsymptoticRatio(k) / zeta;
            sum += term;
        }

        return Math.Exp(-zeta) / (2.0 * Math.Sqrt(Math.PI) * Math.Sqrt(Math.Sqrt(x))) * sum;
    }

    private static double OscillatoryAsymptotic(double z)
    {
        var zeta = 2.0 / 3.0 * z * Math.Sqrt(z);
        var even = 0.0;
        var odd = 0.0;
        var term = 1.0;
        for (var k = 0; k < AsymptoticTerms; k++)
        {
            if (k > 0)
                term *= AsymptoticRatio(k) / zeta;
            switch (k % 4)
            {
                case 0:
                    even += term;
                    break;
                case 1:
                    odd += term;
                    break;
                case 2:
                    even -= term;
                    break;
                default:
                    odd -= term;
                    break;
            }
        }

        var phase = zeta + Math.PI / 4.0;
        return (Math.Sin(phase) * even - Math.Cos(phase) * odd) / (Math.Sqrt(Math.PI) * Math.Sqrt(Math.Sqrt(z)));
    }

    private static double AsymptoticRatio(int k)
        => (6.0 * k - 5.0) * (6.0 * k - 3.0) * (6.0 * k - 1.0) / (216.0 * k * (2.0 * k - 1.0));
}

internal readonly record struct RainbowRay(
    int Reflections,
    double Index,
    double Incidence,
    double Refraction,
    double MinimumDeviation,
    double RainbowAngle,
    double Curvature,
    double Transmission);

internal static class RainbowOptics
{
    public const int PrimaryReflections = 1;
    public const int SecondaryReflections = 2;

    public static RainbowRay Solve(int reflections, double index)
    {
        var cosine = Math.Sqrt((index * index - 1.0) / ((reflections + 1.0) * (reflections + 1.0) - 1.0));
        var incidence = Math.Acos(cosine);
        var refraction = Math.Asin(Math.Sin(incidence) / index);
        var deviation = Deviation(reflections, incidence, refraction);
        var rainbowAngle = reflections % 2 == 1 ? deviation : 2.0 * Math.PI - deviation;
        var curvature = 2.0 * (reflections + 1.0) * Math.Sin(incidence) * (1.0 - 1.0 / ((reflections + 1.0) * (reflections + 1.0))) / (index * Math.Cos(refraction));
        return new RainbowRay(reflections, index, incidence, refraction, deviation, rainbowAngle, curvature, Transmission(reflections, incidence, refraction, index));
    }

    public static double Deviation(int reflections, double incidence, double refraction)
        => reflections * Math.PI + 2.0 * incidence - 2.0 * (reflections + 1.0) * refraction;

    public static double SizeScale(RainbowRay ray, double sizeParameter)
    {
        var cosine = Math.Cos(ray.Incidence);
        return Math.Cbrt(2.0 * sizeParameter * sizeParameter * cosine * cosine / ray.Curvature);
    }

    public static double Intensity(RainbowRay ray, double sizeParameter, double scatteringAngle)
    {
        var offset = ray.Reflections % 2 == 1 ? scatteringAngle - ray.RainbowAngle : ray.RainbowAngle - scatteringAngle;
        var scale = SizeScale(ray, sizeParameter);
        var geometric = Math.Sin(ray.Incidence) * Math.Cos(ray.Incidence) * ray.Transmission
            / (Math.Sin(ray.RainbowAngle) * Math.Sqrt(2.0 * ray.Curvature));
        var airy = AiryFunction.Ai(-scale * offset);
        return 4.0 * Math.PI * geometric * Math.Sqrt(scale) * airy * airy;
    }

    public static PreparedRainbow Prepare(RainbowRay ray, double sizeParameter)
    {
        var scale = SizeScale(ray, sizeParameter);
        var geometric = Math.Sin(ray.Incidence) * Math.Cos(ray.Incidence) * ray.Transmission
            / (Math.Sin(ray.RainbowAngle) * Math.Sqrt(2.0 * ray.Curvature));
        return new PreparedRainbow(ray.Reflections % 2 == 1 ? 1.0 : -1.0, ray.RainbowAngle, scale, 4.0 * Math.PI * geometric * Math.Sqrt(scale));
    }

    private static double Transmission(int reflections, double incidence, double refraction, double index)
    {
        var cosI = Math.Cos(incidence);
        var cosR = Math.Cos(refraction);
        var rs = Math.Pow((cosI - index * cosR) / (cosI + index * cosR), 2);
        var rp = Math.Pow((index * cosI - cosR) / (index * cosI + cosR), 2);
        var perpendicular = Math.Pow(1.0 - rs, 2) * Math.Pow(rs, reflections);
        var parallel = Math.Pow(1.0 - rp, 2) * Math.Pow(rp, reflections);
        return 0.5 * (perpendicular + parallel);
    }
}

internal readonly record struct PreparedRainbow(double Direction, double RainbowAngle, double Scale, double Amplitude)
{
    public double Evaluate(double scatteringAngle)
    {
        return Amplitude * AiryFunction.AiSquared(-Scale * Direction * (scatteringAngle - RainbowAngle));
    }
}
