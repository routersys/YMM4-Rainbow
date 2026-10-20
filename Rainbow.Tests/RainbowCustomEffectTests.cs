using YukkuriMovieMaker.Commons;
using static Rainbow.RainbowCustomEffect;

namespace Rainbow.Tests;

[Collection("Direct2D")]
public sealed class RainbowCustomEffectTests
{
    static float Read(RainbowCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, Action<RainbowCustomEffect> configure)
    {
        using var table = SourceImage.Solid(devices, RainbowSettings.TableWidth, 1, Bgra.Opaque(200, 40, 40));
        using var effect = new RainbowCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, table.Bitmap, true);
        configure(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        Assert.Equal(0f, Read(effect, PropertyIndex.Amount));
        Assert.Equal(0f, Read(effect, PropertyIndex.AntisolarX));
        Assert.Equal(0f, Read(effect, PropertyIndex.AntisolarY));
        Assert.Equal(1f, Read(effect, PropertyIndex.AntisolarZ));
        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheAmountIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.Amount = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Amount));
    }

    [Theory]
    [InlineData(-2f, -1f)]
    [InlineData(-0.5f, -0.5f)]
    [InlineData(0f, 0f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(3f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, -1f)]
    [InlineData(float.NaN, 0f)]
    public void TheAntisolarDirectionStaysInsideTheUnitCube(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.AntisolarX = value;
        effect.AntisolarY = value;
        effect.AntisolarZ = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.AntisolarX));
        Assert.Equal(expected, Read(effect, PropertyIndex.AntisolarY));
        Assert.Equal(expected, Read(effect, PropertyIndex.AntisolarZ));
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(3.7f)]
    public void TheHalfFieldTangentKeepsValuesInsideTheFieldOfViewLimits(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(value, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(float.NegativeInfinity)]
    public void TheHalfFieldTangentNeverFallsBelowTheNarrowestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(RainbowSettings.MinimumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(1e5f)]
    [InlineData(float.PositiveInfinity)]
    public void TheHalfFieldTangentNeverExceedsTheWidestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(RainbowSettings.MaximumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Fact]
    public void ANonNumberLeavesTheHalfFieldTangentAtItsDefault()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new RainbowCustomEffect(context);

        effect.HalfFieldTangent = float.NaN;

        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.6f)]
    [InlineData(1f)]
    public void TheOutputBoundsEqualTheInput(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));
        using var table = SourceImage.Solid(context, RainbowSettings.TableWidth, 1, Bgra.Opaque(10, 20, 30));
        using var effect = new RainbowCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, table.Bitmap, true);
        effect.Amount = amount;
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal((0f, 0f, 40f, 24f), (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    [Fact]
    public void WithoutAnAmountTheInputPassesThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 32, (x, y) => new Bgra((byte)(x * 5), (byte)(y * 7), (byte)(x + y), (byte)(100 + x)));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 0f;
            effect.AntisolarZ = 1f;
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void PixelsLookingAwayFromTheAntisolarPointPassThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 32, (x, y) => new Bgra((byte)(x * 5), (byte)(y * 7), (byte)(x + y), 255));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 1f;
            effect.AntisolarX = 0f;
            effect.AntisolarY = 0f;
            effect.AntisolarZ = -1f;
            effect.HalfFieldTangent = 0.5f;
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void ARainbowAroundTheCenterAddsLightToTheOpaquePixels()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 64, 64, Bgra.Opaque(40, 40, 40));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 1f;
            effect.AntisolarX = 0f;
            effect.AntisolarY = 0f;
            effect.AntisolarZ = 1f;
            effect.HalfFieldTangent = 1f;
        });

        var untouched = Rendering.Capture(context, source.Bitmap);
        var lit = rendered.Coordinates().Count(point => rendered[point.X, point.Y].Red > untouched[point.X, point.Y].Red);

        Assert.True(lit > 0);
    }

    [Fact]
    public void TransparentPixelsStayTransparent()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 64, 64, (x, _) => x < 32 ? Bgra.Transparent : Bgra.Opaque(40, 40, 40));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = 1f;
        });

        Assert.All(rendered.Coordinates().Where(point => point.X < 32), point => Assert.Equal(Bgra.Transparent, rendered[point.X, point.Y]));
    }
}
