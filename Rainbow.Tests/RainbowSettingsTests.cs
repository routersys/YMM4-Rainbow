using System.Numerics;

namespace Rainbow.Tests;

public sealed class RainbowSettingsTests
{
    static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-6f);
        Assert.Equal(expected.Y, actual.Y, 1e-6f);
        Assert.Equal(expected.Z, actual.Z, 1e-6f);
    }

    [Fact]
    public void TheAntisolarPointFacesForwardWhenTheSunIsOnTheHorizon()
    {
        AssertVector(new Vector3(0f, 0f, 1f), RainbowSettings.GetAntisolarDirection(0.0, 0.0, 0.0));
    }

    [Fact]
    public void TheAntisolarPointLiesBelowTheHorizonByTheElevationOfTheSun()
    {
        var direction = RainbowSettings.GetAntisolarDirection(30.0, 0.0, 0.0);

        AssertVector(new Vector3(0f, 0.5f, MathF.Cos(MathF.PI / 6f)), direction);
    }

    [Theory]
    [InlineData(90.0, 1f, 0f, 0f)]
    [InlineData(-90.0, -1f, 0f, 0f)]
    [InlineData(180.0, 0f, 0f, -1f)]
    public void TheDirectionTurnsTheAntisolarPointAroundTheCamera(double direction, float x, float y, float z)
    {
        AssertVector(new Vector3(x, y, z), RainbowSettings.GetAntisolarDirection(0.0, direction, 0.0));
    }

    [Fact]
    public void LookingUpMovesTheHorizonDownTheImage()
    {
        var direction = RainbowSettings.GetAntisolarDirection(0.0, 0.0, 30.0);

        AssertVector(new Vector3(0f, 0.5f, MathF.Cos(MathF.PI / 6f)), direction);
    }

    [Fact]
    public void LookingDownMovesTheHorizonUpTheImage()
    {
        var direction = RainbowSettings.GetAntisolarDirection(0.0, 0.0, -30.0);

        AssertVector(new Vector3(0f, -0.5f, MathF.Cos(MathF.PI / 6f)), direction);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(25.0, 40.0, -15.0)]
    [InlineData(90.0, 170.0, 60.0)]
    [InlineData(-45.0, -120.0, 20.0)]
    public void TheDirectionIsAUnitVector(double elevation, double direction, double pitch)
    {
        Assert.Equal(1f, RainbowSettings.GetAntisolarDirection(elevation, direction, pitch).Length(), 1e-6f);
    }

    [Fact]
    public void TheElevationIsHeldBetweenMinusNinetyAndNinety()
    {
        AssertVector(RainbowSettings.GetAntisolarDirection(90.0, 0.0, 0.0), RainbowSettings.GetAntisolarDirection(400.0, 0.0, 0.0));
        AssertVector(RainbowSettings.GetAntisolarDirection(-90.0, 0.0, 0.0), RainbowSettings.GetAntisolarDirection(-400.0, 0.0, 0.0));
    }

    [Theory]
    [InlineData(60.0, 0.5773503f)]
    [InlineData(90.0, 1f)]
    [InlineData(10.0, 0.0874887f)]
    [InlineData(1.0, 0.0874887f)]
    [InlineData(150.0, 3.7320508f)]
    [InlineData(179.0, 3.7320508f)]
    public void TheFieldOfViewIsHeldBetweenTenAndOneHundredFiftyDegrees(double field, float expected)
    {
        Assert.Equal(expected, RainbowSettings.GetHalfFieldOfViewTangent(field), 1e-5f);
    }

    [Fact]
    public void TheSizeParameterIsTheCircumferenceInWavelengths()
    {
        Assert.Equal(2.0 * Math.PI * 200000.0 / 550.0, RainbowSettings.GetSizeParameter(0.2, 550.0), 1e-9);
    }

    [Fact]
    public void TheSpectrumIsSampledEveryTenNanometersFromVioletToRed()
    {
        Assert.Equal(41, RainbowSettings.GetWavelengthCount());
        Assert.Equal(RainbowSettings.WavelengthEndNanometers, RainbowSettings.WavelengthStartNanometers + 40 * RainbowSettings.WavelengthStepNanometers, 1e-9);
    }

    [Fact]
    public void TheSunSpectrumFollowsPlancksLaw()
    {
        var blue = SolarSpectrum.Radiance(450.0, RainbowSettings.SolarTemperature);
        var red = SolarSpectrum.Radiance(650.0, RainbowSettings.SolarTemperature);
        var wien = 2.897771955e-3 / RainbowSettings.SolarTemperature * 1e9;

        Assert.True(blue > red);
        Assert.Equal(501.9, wien, 0.5);
        Assert.True(SolarSpectrum.Radiance(wien, RainbowSettings.SolarTemperature) > SolarSpectrum.Radiance(wien + 50.0, RainbowSettings.SolarTemperature));
        Assert.True(SolarSpectrum.Radiance(wien, RainbowSettings.SolarTemperature) > SolarSpectrum.Radiance(wien - 50.0, RainbowSettings.SolarTemperature));
    }

    [Theory]
    [InlineData(555.0, 1.0, 0.05)]
    [InlineData(450.0, 0.04, 0.03)]
    public void TheColorMatchingFunctionsFollowTheCieLuminousEfficiency(double wavelength, double expectedY, double tolerance)
    {
        var (_, y, _) = SolarSpectrum.ColorMatching(wavelength);

        Assert.Equal(expectedY, y, tolerance);
    }

    [Fact]
    public void AnEqualEnergySpectrumGivesAWhiteSrgbColor()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        for (var wavelength = 380.0; wavelength <= 780.0; wavelength += 5.0)
        {
            var (px, py, pz) = SolarSpectrum.ColorMatching(wavelength);
            x += px;
            y += py;
            z += pz;
        }

        var (red, green, blue) = SolarSpectrum.ToLinearSrgb(x / y, 1.0, z / y);

        Assert.Equal(1.0, green, 0.25);
        Assert.InRange(red / green, 0.8, 1.3);
        Assert.InRange(blue / green, 0.7, 1.3);
    }
}
