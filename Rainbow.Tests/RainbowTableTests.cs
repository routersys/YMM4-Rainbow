namespace Rainbow.Tests;

public sealed class RainbowTableTests
{
    const int Red = 0;
    const int Green = 1;
    const int Blue = 2;

    static readonly RainbowPhysicalTable Standard = RainbowPhysicalTable.Build(0.2, 0.53);

    static int IndexOf(double angleDegrees)
        => (int)Math.Round((angleDegrees - RainbowSettings.TableStartDegrees) / RainbowSettings.GetTableStepDegrees());

    static double AngleOf(int index) => RainbowSettings.GetTableAngleDegrees(index);

    static int PeakIndex(float[] table, int channel, double from, double to)
        => Enumerable.Range(IndexOf(from), IndexOf(to) - IndexOf(from) + 1).MaxBy(index => table[index * 3 + channel]);

    static double Width(float[] table, int channel, double from, double to)
    {
        var peak = PeakIndex(table, channel, from, to);
        var half = table[peak * 3 + channel] / 2f;
        var low = peak;
        while (low > 0 && table[low * 3 + channel] > half)
            low--;
        var high = peak;
        while (high < RainbowSettings.TableWidth - 1 && table[high * 3 + channel] > half)
            high++;
        return (high - low) * RainbowSettings.GetTableStepDegrees();
    }

    static double TotalVariation(float[] table, int channel, double from, double to)
    {
        var sum = 0.0;
        for (var index = IndexOf(from); index < IndexOf(to); index++)
            sum += Math.Abs(table[(index + 1) * 3 + channel] - table[index * 3 + channel]);
        return sum;
    }

    [Fact]
    public void TheTableHasOneColorForEveryAngle()
    {
        Assert.Equal(RainbowSettings.TableWidth * 3, Standard.Primary.Length);
        Assert.Equal(RainbowSettings.TableWidth * 3, Standard.Secondary.Length);
        Assert.Equal(RainbowSettings.TableWidth, Standard.Shadow.Length);
        Assert.All(Standard.Primary.Concat(Standard.Secondary).Concat(Standard.Shadow), value => Assert.True(float.IsFinite(value) && value >= 0f));
    }

    [Fact]
    public void TheAnglesRunFromTheStartToTheEndOfTheRange()
    {
        Assert.Equal(RainbowSettings.TableStartDegrees, AngleOf(0), 1e-12);
        Assert.Equal(RainbowSettings.TableEndDegrees, AngleOf(RainbowSettings.TableWidth - 1), 1e-9);
        Assert.Equal(44.0 / (RainbowSettings.TableWidth - 1), RainbowSettings.GetTableStepDegrees(), 1e-12);
    }

    [Fact]
    public void ThePrimaryIsNormalizedToOneAtItsBrightestChannel()
    {
        Assert.Equal(1f, Standard.Primary.Max(), 1e-5f);
    }

    [Fact]
    public void ThePrimaryPutsRedOutsideAndBlueInsideAndTheSecondaryReversesThem()
    {
        var primaryRed = PeakIndex(Standard.Primary, Red, 39.5, 43.5);
        var primaryGreen = PeakIndex(Standard.Primary, Green, 39.5, 43.5);
        var primaryBlue = PeakIndex(Standard.Primary, Blue, 39.5, 43.5);
        var secondaryRed = PeakIndex(Standard.Secondary, Red, 49.5, 56.0);
        var secondaryBlue = PeakIndex(Standard.Secondary, Blue, 49.5, 56.0);

        Assert.True(primaryRed > primaryGreen && primaryGreen > primaryBlue);
        Assert.True(secondaryRed < secondaryBlue);
        Assert.InRange(AngleOf(primaryRed), 41.0, 42.6);
        Assert.InRange(AngleOf(secondaryBlue), 52.5, 55.0);
    }

    [Fact]
    public void TheGreenPeakOfThePrimaryLiesAtTheAiryAngleOfGreenLight()
    {
        var peak = PeakIndex(Standard.Primary, Green, 39.5, 43.5);

        Assert.Equal(41.3, AngleOf(peak), 0.3);
    }

    [Fact]
    public void ThePrimaryFadesIntoTheDarkBandAndTheSecondaryIsDimmer()
    {
        var primaryPeak = Standard.Primary.Max();
        var secondaryPeak = Standard.Secondary.Max();

        Assert.True(secondaryPeak < 0.6f * primaryPeak);
        Assert.True(Standard.Primary[IndexOf(47.0) * 3 + Green] < 0.02f * primaryPeak);
        Assert.True(Standard.Primary[IndexOf(60.0) * 3 + Green] < 0.02f * primaryPeak);
        Assert.True(Standard.Secondary[IndexOf(44.0) * 3 + Green] < 0.02f * primaryPeak);
    }

    [Fact]
    public void LargerDropsMakeNarrowerBows()
    {
        var small = RainbowPhysicalTable.Build(0.08, 0.0);
        var large = RainbowPhysicalTable.Build(0.4, 0.0);

        Assert.True(Width(large.Primary, Green, 39.0, 43.5) < Width(small.Primary, Green, 39.0, 44.5));
    }

    [Fact]
    public void ALargerSunBlursTheSupernumeraryArcs()
    {
        var point = RainbowPhysicalTable.Build(0.3, 0.0);
        var wide = RainbowPhysicalTable.Build(0.3, 2.0);

        var sharp = TotalVariation(point.Primary, Green, 36.0, 40.5) / point.Primary.Max();
        var blurred = TotalVariation(wide.Primary, Green, 36.0, 40.5) / wide.Primary.Max();

        Assert.True(blurred < sharp * 0.7, $"{blurred} {sharp}");
    }

    [Fact]
    public void TheTableIsTheSameEveryTimeItIsBuilt()
    {
        var again = RainbowPhysicalTable.Build(0.2, 0.53);

        Assert.Equal(Standard.Primary, again.Primary);
        Assert.Equal(Standard.Secondary, again.Secondary);
        Assert.Equal(Standard.Shadow, again.Shadow);
    }

    [Fact]
    public void TheDarkBandLiesBetweenTheTwoBows()
    {
        Assert.True(Standard.Shadow[IndexOf(46.5)] > 0.999f);
        Assert.True(Standard.Shadow[IndexOf(38.0)] < 0.001f);
        Assert.True(Standard.Shadow[IndexOf(55.0)] < 0.001f);
        Assert.All(Standard.Shadow, value => Assert.InRange(value, 0f, 1f));
    }

    [Fact]
    public void TheSecondaryStrengthScalesOnlyTheSecondary()
    {
        var full = RainbowTable.Compose(Standard, 1.0, 0.0, 1.0);
        var none = RainbowTable.Compose(Standard, 0.0, 0.0, 1.0);
        var half = RainbowTable.Compose(Standard, 0.5, 0.0, 1.0);

        var secondary = IndexOf(52.0);
        var primary = IndexOf(41.2);
        Assert.Equal((float)full[primary * 4 + Green], (float)none[primary * 4 + Green], 2e-3f);
        Assert.True((float)none[secondary * 4 + Green] < 0.001f);
        Assert.Equal(0.5f * (float)full[secondary * 4 + Green], (float)half[secondary * 4 + Green], 3e-3f);
    }

    [Fact]
    public void ZeroSaturationGivesAGrayRainbow()
    {
        var gray = RainbowTable.Compose(Standard, 1.0, 0.0, 0.0);

        for (var index = 0; index < RainbowSettings.TableWidth; index++)
        {
            var red = (float)gray[index * 4];
            Assert.Equal(red, (float)gray[index * 4 + 1], 4e-3f * Math.Max(red, 0.01f));
            Assert.Equal(red, (float)gray[index * 4 + 2], 4e-3f * Math.Max(red, 0.01f));
        }
    }

    [Fact]
    public void DoubleSaturationPushesTheColorsApart()
    {
        var normal = RainbowTable.Compose(Standard, 0.0, 0.0, 1.0);
        var vivid = RainbowTable.Compose(Standard, 0.0, 0.0, 2.0);

        float Spread(Half[] texels) => Enumerable.Range(0, RainbowSettings.TableWidth).Sum(index => Math.Abs((float)texels[index * 4] - (float)texels[index * 4 + 2]));

        Assert.True(Spread(vivid) > 1.5f * Spread(normal));
    }

    [Fact]
    public void TheDarknessLowersTheBackgroundGainOnlyInsideTheBand()
    {
        var table = RainbowTable.Compose(Standard, 1.0, 0.6, 1.0);

        Assert.Equal(0.4f, (float)table[IndexOf(46.5) * 4 + 3], 2e-3f);
        Assert.Equal(1f, (float)table[IndexOf(38.0) * 4 + 3], 1e-3f);
        Assert.Equal(1f, (float)table[IndexOf(58.0) * 4 + 3], 1e-3f);
        Assert.Equal(1f, (float)RainbowTable.Compose(Standard, 1.0, 0.0, 1.0)[IndexOf(46.5) * 4 + 3], 1e-3f);
    }

    [Fact]
    public void SamplingBetweenTwoEntriesInterpolatesLinearly()
    {
        var table = RainbowTable.Compose(Standard, 1.0, 0.3, 1.0);
        var index = IndexOf(41.0);
        var angle = (AngleOf(index) + AngleOf(index + 1)) / 2.0;

        var exact = RainbowTable.Sample(table, AngleOf(index));
        var middle = RainbowTable.Sample(table, angle);
        var next = RainbowTable.Sample(table, AngleOf(index + 1));

        Assert.Equal((float)table[index * 4 + 1], exact.Green, 1e-6f);
        Assert.Equal(0.5f * (exact.Green + next.Green), middle.Green, 1e-5f);
        Assert.Equal(0.5f * (exact.Gain + next.Gain), middle.Gain, 1e-5f);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(19.99)]
    [InlineData(64.01)]
    [InlineData(120.0)]
    public void SamplingOutsideTheRangeAddsNothing(double angle)
    {
        var table = RainbowTable.Compose(Standard, 1.0, 0.3, 1.0);

        Assert.Equal((0f, 0f, 0f, 1f), RainbowTable.Sample(table, angle));
    }

    [Fact]
    public void ADifferentDropSizeGivesADifferentTable()
    {
        var other = RainbowPhysicalTable.Build(0.25, 0.53);

        Assert.NotEqual(Standard.Primary, other.Primary);
    }
}
