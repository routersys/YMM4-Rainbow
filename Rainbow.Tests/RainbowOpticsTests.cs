namespace Rainbow.Tests;

public sealed class RainbowOpticsTests
{
    const double Degrees = 180.0 / Math.PI;

    [Theory]
    [InlineData(589.3, 1.3330, 0.0006)]
    [InlineData(486.1, 1.3371, 0.0006)]
    public void TheRefractiveIndexOfWaterAtTheSpectralLinesIsRelativeToAir(double wavelength, double expected, double tolerance)
    {
        Assert.Equal(expected, WaterOptics.RefractiveIndex(wavelength), tolerance);
    }

    [Fact]
    public void TheRefractiveIndexFallsAsTheWavelengthGrows()
    {
        var wavelengths = Enumerable.Range(0, 41).Select(step => 380.0 + step * 10.0).ToArray();

        var indices = wavelengths.Select(WaterOptics.RefractiveIndex).ToArray();

        Assert.All(indices.Zip(indices.Skip(1)), pair => Assert.True(pair.First > pair.Second));
        Assert.InRange(indices[0], 1.343, 1.346);
        Assert.InRange(indices[^1], 1.3285, 1.3305);
    }

    [Theory]
    [InlineData(0.0, 0.3550280538878172)]
    [InlineData(1.0, 0.1352924163128814)]
    [InlineData(-1.0, 0.5355608832923521)]
    [InlineData(-2.338107410459767, 0.0)]
    [InlineData(-4.087949444130970, 0.0)]
    [InlineData(-5.520559828095551, 0.0)]
    [InlineData(-6.786708090071759, 0.0)]
    [InlineData(-7.944133587120853, 0.0)]
    public void TheAiryFunctionMatchesItsTabulatedValuesAndZeros(double x, double expected)
    {
        Assert.Equal(expected, AiryFunction.Ai(x), 1e-9);
    }

    [Fact]
    public void TheAiryFunctionSolvesItsDifferentialEquationOnBothSidesOfEveryChangeOfMethod()
    {
        const double Step = 1e-3;

        for (var x = -14.0; x <= 8.0; x += 0.0137)
        {
            var second = (AiryFunction.Ai(x + Step) - 2.0 * AiryFunction.Ai(x) + AiryFunction.Ai(x - Step)) / (Step * Step);

            Assert.Equal(x * AiryFunction.Ai(x), second, 1e-3 + 1e-4 * Math.Abs(x * AiryFunction.Ai(x)));
        }
    }

    [Theory]
    [InlineData(-6.0)]
    [InlineData(5.0)]
    public void TheAiryFunctionIsContinuousWhereTheMethodChanges(double boundary)
    {
        var below = AiryFunction.Ai(boundary - 1e-9);
        var above = AiryFunction.Ai(boundary + 1e-9);

        Assert.Equal(below, above, 1e-7 * Math.Max(Math.Abs(below), 1e-3));
    }

    [Fact]
    public void TheAiryFunctionDecaysOnTheShadowSideAndOscillatesOnTheLitSide()
    {
        Assert.True(AiryFunction.Ai(3.0) > 0.0 && AiryFunction.Ai(3.0) < 0.01);
        Assert.True(AiryFunction.Ai(12.0) > 0.0 && AiryFunction.Ai(12.0) < 1e-12);
        Assert.True(AiryFunction.Ai(-3.0) < 0.0);
        Assert.True(AiryFunction.Ai(-4.8) > 0.0);
        Assert.InRange(AiryFunction.Ai(-12.0), -0.5, 0.5);
    }

    [Fact]
    public void TheSquaredAiryTableMatchesTheDirectSquareEverywhere()
    {
        for (var x = -99.9; x <= 12.5; x += 0.00137)
        {
            var direct = AiryFunction.Ai(x) * AiryFunction.Ai(x);

            Assert.Equal(direct, AiryFunction.AiSquared(x), 1e-8 + 1e-9 * direct);
        }

        Assert.Equal(0.0, AiryFunction.AiSquared(12.5));
        Assert.Equal(AiryFunction.Ai(-120.0) * AiryFunction.Ai(-120.0), AiryFunction.AiSquared(-120.0));
    }

    [Theory]
    [InlineData(1, 1.3333333333)]
    [InlineData(1, 1.3300)]
    [InlineData(1, 1.3450)]
    [InlineData(2, 1.3333333333)]
    [InlineData(2, 1.3300)]
    [InlineData(2, 1.3450)]
    public void TheMinimumDeviationFollowsFromTheLawOfRefraction(int reflections, double index)
    {
        var ray = RainbowOptics.Solve(reflections, index);

        var cosineSquared = Math.Cos(ray.Incidence) * Math.Cos(ray.Incidence);
        Assert.Equal((index * index - 1.0) / ((reflections + 1.0) * (reflections + 1.0) - 1.0), cosineSquared, 1e-12);
        Assert.Equal(Math.Sin(ray.Incidence), index * Math.Sin(ray.Refraction), 1e-12);

        var best = double.MaxValue;
        for (var incidence = 0.01; incidence < Math.PI / 2.0; incidence += 1e-4)
        {
            var refraction = Math.Asin(Math.Sin(incidence) / index);
            best = Math.Min(best, RainbowOptics.Deviation(reflections, incidence, refraction));
        }

        Assert.Equal(best, ray.MinimumDeviation, 1e-7);
    }

    [Fact]
    public void ThePrimaryRainbowOfWaterWithTheIndexOfFourThirdsIsAt137Point97Degrees()
    {
        var ray = RainbowOptics.Solve(RainbowOptics.PrimaryReflections, 4.0 / 3.0);

        Assert.Equal(137.97, ray.RainbowAngle * Degrees, 0.01);
        Assert.Equal(180.0 - 137.97, 180.0 - ray.RainbowAngle * Degrees, 0.01);
    }

    [Fact]
    public void RedLightFormsTheOuterEdgeOfThePrimaryAndTheInnerEdgeOfTheSecondary()
    {
        var red = WaterOptics.RefractiveIndex(700.0);
        var violet = WaterOptics.RefractiveIndex(400.0);

        var primaryRed = 180.0 - RainbowOptics.Solve(1, red).RainbowAngle * Degrees;
        var primaryViolet = 180.0 - RainbowOptics.Solve(1, violet).RainbowAngle * Degrees;
        var secondaryRed = 180.0 - RainbowOptics.Solve(2, red).RainbowAngle * Degrees;
        var secondaryViolet = 180.0 - RainbowOptics.Solve(2, violet).RainbowAngle * Degrees;

        Assert.InRange(primaryRed, 42.2, 42.7);
        Assert.InRange(primaryViolet, 40.4, 40.9);
        Assert.InRange(secondaryRed, 49.9, 50.5);
        Assert.InRange(secondaryViolet, 53.3, 53.9);
        Assert.True(primaryRed > primaryViolet);
        Assert.True(secondaryRed < secondaryViolet);
        Assert.True(primaryRed < secondaryRed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheCurvatureOfTheDeviationMatchesItsNumericalSecondDerivative(int reflections)
    {
        var ray = RainbowOptics.Solve(reflections, 1.3335);
        const double Step = 1e-4;

        double At(double incidence) => RainbowOptics.Deviation(reflections, incidence, Math.Asin(Math.Sin(incidence) / ray.Index));
        var numerical = (At(ray.Incidence + Step) - 2.0 * At(ray.Incidence) + At(ray.Incidence - Step)) / (Step * Step);

        Assert.Equal(numerical, ray.Curvature, 1e-4);
    }

    [Fact]
    public void TheSecondaryRainbowTransmitsLessThanThePrimary()
    {
        var primary = RainbowOptics.Solve(1, 1.3335);
        var secondary = RainbowOptics.Solve(2, 1.3335);

        Assert.InRange(primary.Transmission, 0.03, 0.06);
        Assert.InRange(secondary.Transmission, 0.01, 0.03);
        Assert.True(secondary.Transmission < primary.Transmission);
    }

    [Fact]
    public void TheSizeScaleGrowsWithTheTwoThirdsPowerOfTheSizeParameter()
    {
        var ray = RainbowOptics.Solve(1, 1.3335);

        var ratio = RainbowOptics.SizeScale(ray, 4000.0) / RainbowOptics.SizeScale(ray, 500.0);

        Assert.Equal(Math.Pow(8.0, 2.0 / 3.0), ratio, 1e-9);
    }

    [Fact]
    public void ThePreparedRainbowEqualsTheDirectIntensity()
    {
        var ray = RainbowOptics.Solve(1, 1.3335);
        var prepared = RainbowOptics.Prepare(ray, 2200.0);

        for (var angle = 136.5; angle <= 141.0; angle += 0.137)
        {
            var radians = angle / Degrees;
            var direct = RainbowOptics.Intensity(ray, 2200.0, radians);
            Assert.Equal(direct, prepared.Evaluate(radians), 1e-7 * Math.Max(direct, 1e-3));
        }
    }

    [Fact]
    public void ThePrimaryIsBrightOnTheInsideAndDarkOnTheOutsideOfTheRainbowAngle()
    {
        var ray = RainbowOptics.Solve(1, 1.3335);
        var prepared = RainbowOptics.Prepare(ray, 2200.0);

        var inside = prepared.Evaluate(ray.RainbowAngle + 1.0 / Degrees);
        var outside = prepared.Evaluate(ray.RainbowAngle - 1.0 / Degrees);

        Assert.True(inside > 30.0 * outside);
    }

    [Fact]
    public void TheSecondaryIsBrightOnTheOutsideAndDarkOnTheInsideOfTheRainbowAngle()
    {
        var ray = RainbowOptics.Solve(2, 1.3335);
        var prepared = RainbowOptics.Prepare(ray, 2200.0);

        var outside = prepared.Evaluate(ray.RainbowAngle - 1.0 / Degrees);
        var inside = prepared.Evaluate(ray.RainbowAngle + 1.0 / Degrees);

        Assert.True(outside > 8.0 * inside);
    }

    public static readonly TheoryData<int, double, double, double, double, double> MieComparisons = new()
    {
        { 1, 0.05, 550.0, 0.5, 0.9, 1.30 },
        { 1, 0.10, 550.0, 0.4, 0.9, 1.25 },
        { 1, 0.20, 550.0, 0.10, 0.9, 1.25 },
        { 1, 0.40, 550.0, 0.05, 0.9, 1.25 },
        { 1, 0.20, 450.0, 0.10, 0.9, 1.25 },
        { 1, 0.20, 650.0, 0.15, 0.9, 1.25 },
        { 2, 0.20, 550.0, 0.3, 0.75, 1.35 },
        { 2, 0.40, 550.0, 0.2, 0.8, 1.30 },
    };

    [Theory]
    [MemberData(nameof(MieComparisons))]
    public void TheAiryIntensityFollowsLorenzMieTheory(int reflections, double radiusMillimeters, double wavelength, double peakTolerance, double correlationLimit, double sumRatioLimit)
    {
        var index = WaterOptics.RefractiveIndex(wavelength);
        var size = RainbowSettings.GetSizeParameter(radiusMillimeters, wavelength);
        var ray = RainbowOptics.Solve(reflections, index);
        var geometric = ray.RainbowAngle * Degrees;
        var from = geometric - (reflections == 1 ? 1.0 : 5.0);
        var to = geometric + (reflections == 1 ? 5.0 : 1.0);
        var angles = new List<double>();
        for (var angle = from; angle <= to; angle += 0.004)
            angles.Add(angle / Degrees);

        var mie = MieScattering.Unpolarized(index, size, angles);
        var airy = angles.Select(angle => RainbowOptics.Intensity(ray, size, angle)).ToArray();

        var peakOfMie = angles[Array.IndexOf(mie, mie.Max())] * Degrees;
        var peakOfAiry = angles[Array.IndexOf(airy, airy.Max())] * Degrees;
        Assert.Equal(peakOfMie, peakOfAiry, peakTolerance);
        var meanMie = mie.Average();
        var meanAiry = airy.Average();
        var covariance = mie.Zip(airy).Sum(pair => (pair.First - meanMie) * (pair.Second - meanAiry));
        var correlation = covariance / Math.Sqrt(mie.Sum(value => (value - meanMie) * (value - meanMie)) * airy.Sum(value => (value - meanAiry) * (value - meanAiry)));
        Assert.True(correlation >= correlationLimit, $"correlation {correlation}");
        var ratio = mie.Sum() / airy.Sum();
        Assert.InRange(ratio, 1.0, sumRatioLimit);
    }

    [Fact]
    public void TheMieOracleReproducesRayleighScatteringOfASmallSphere()
    {
        const double Index = 1.33;
        const double Size = 0.02;
        var angles = new List<double> { 0.0, Math.PI / 2.0 };

        var result = MieScattering.Intensities(Index, Size, angles);

        var rayleigh = Math.Pow(Size, 6) * Math.Pow((Index * Index - 1.0) / (Index * Index + 2.0), 2);
        Assert.Equal(result[0].Perpendicular, result[0].Parallel, 1e-6 * result[0].Perpendicular);
        Assert.Equal(rayleigh, result[0].Perpendicular, 1e-3 * rayleigh);
        Assert.Equal(rayleigh, result[1].Perpendicular, 1e-3 * rayleigh);
        Assert.True(result[1].Parallel < 1e-6 * result[1].Perpendicular);
    }
}
