using System.Globalization;
using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace Rainbow.Tests;

[Collection("Direct2D")]
public sealed class RainbowEffectProcessorTests
{
    const int Width = 128;
    const int Height = 96;
    const int Length = 30;
    const int Tolerance = 3;

    static readonly Bgra Sky = Bgra.Opaque(230, 150, 100);

    static Bgra SkyBlue(int x, int y) => Sky;

    static Bgra HalfTransparent(int x, int y)
        => x < Width / 2 ? Sky : new Bgra(115, 75, 50, 128);

    static Bgra Transparent(int x, int y) => y < Height / 3 ? Bgra.Transparent : Sky;

    static RainbowEffect Wide(double amount = 100d)
    {
        var effect = new RainbowEffect();
        effect.Amount.Values[0].Value = amount;
        effect.FieldOfView.Values[0].Value = 90d;
        return effect;
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    sealed class Model
    {
        readonly Vector3 antisolar;
        readonly float halfFieldTangent;
        readonly Half[] table;
        readonly float amount;

        public Model(RainbowEffect effect, int frame)
        {
            double Value(Animation animation) => animation.GetValue(frame, Length, EffectDescriptions.Fps);
            antisolar = RainbowSettings.GetAntisolarDirection(Value(effect.SunElevation), Value(effect.AntisolarDirection), Value(effect.CameraPitch));
            halfFieldTangent = RainbowSettings.GetHalfFieldOfViewTangent(Value(effect.FieldOfView));
            var physical = RainbowPhysicalTable.Build(Value(effect.DropRadius), Value(effect.SunDiameter));
            table = RainbowTable.Compose(physical, Value(effect.SecondaryBow) / 100.0, Value(effect.DarkBand) / 100.0, Value(effect.Saturation) / 100.0);
            amount = (float)(Value(effect.Amount) / 100.0);
        }

        public Bgra Expected(Func<int, int, Bgra> source, int x, int y)
            => RainbowShaderModel.Shade(source(x, y).Premultiplied(), x, y, Width, Height, antisolar, halfFieldTangent, table, amount);
    }

    static void AssertClose(Bgra expected, Bgra actual, string where)
    {
        Assert.True(Math.Abs(expected.Blue - actual.Blue) <= Tolerance, $"{where} blue {expected} {actual}");
        Assert.True(Math.Abs(expected.Green - actual.Green) <= Tolerance, $"{where} green {expected} {actual}");
        Assert.True(Math.Abs(expected.Red - actual.Red) <= Tolerance, $"{where} red {expected} {actual}");
        Assert.Equal(expected.Alpha, actual.Alpha);
    }

    static int ChangedPixels(Rendering rendering, SourceImage source)
        => rendering.Coordinates().Count(point => rendering[point.X, point.Y] != source[point.X, point.Y]);

    [Fact]
    public void TheProcessorHandsTheDrawDescriptionBackUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = Wide().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Same(description.DrawDescription, draw);
    }

    [Fact]
    public void WithoutAmountTheProcessorPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = Wide(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void EveryPixelFollowsTheModelOfTheShader()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var model = new Model(effect, 0);
        Assert.All(rendering.Coordinates(), point => AssertClose(model.Expected(SkyBlue, point.X, point.Y), rendering[point.X, point.Y], $"{point}"));
        Assert.True(ChangedPixels(rendering, source) > Width * Height / 10);
    }

    [Fact]
    public void TheRainbowAppearsAsAnArcAroundTheAntisolarPoint()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        effect.SecondaryBow.Values[0].Value = 0d;
        effect.DarkBand.Values[0].Value = 0d;
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var column = Width / 2;
        var changed = Enumerable.Range(0, Height).Where(y => rendering[column, y] != source[column, y]).ToArray();
        Assert.NotEmpty(changed);
        var focal = 0.5f * Width / RainbowSettings.GetHalfFieldOfViewTangent(90d);
        var primaryEdge = 25d - 42.0;
        var expectedTop = Height / 2.0 + focal * Math.Tan(primaryEdge * Math.PI / 180.0);
        Assert.InRange(changed.Min(), expectedTop - 8.0, expectedTop + 8.0);
    }

    [Fact]
    public void ThePixelsAwayFromTheBowsKeepTheirColor()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = Wide().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal(source[Width / 2, Height - 1], rendering[Width / 2, Height - 1]);
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        effect.DropRadius.CopyFrom(Linear(0.1d, 0.4d));
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 5);
        RenderFrame(context, processor, 20);
        var again = RenderFrame(context, processor, 5);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void AnimatedDropRadiusIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        effect.DropRadius.CopyFrom(Linear(0.08d, 0.4d));
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.False(start.SamePixelsAs(end));
        var model = new Model(effect, 0);
        Assert.All(start.Coordinates(), point => AssertClose(model.Expected(SkyBlue, point.X, point.Y), start[point.X, point.Y], $"{point}"));
        model = new Model(effect, Length - 1);
        Assert.All(end.Coordinates(), point => AssertClose(model.Expected(SkyBlue, point.X, point.Y), end[point.X, point.Y], $"{point}"));
    }

    public static readonly TheoryData<string, Action<RainbowEffect>> Changes = new()
    {
        { nameof(RainbowEffect.Amount), effect => effect.Amount.Values[0].Value = 40d },
        { nameof(RainbowEffect.Saturation), effect => effect.Saturation.Values[0].Value = 0d },
        { nameof(RainbowEffect.SunElevation), effect => effect.SunElevation.Values[0].Value = 35d },
        { nameof(RainbowEffect.AntisolarDirection), effect => effect.AntisolarDirection.Values[0].Value = 25d },
        { nameof(RainbowEffect.SunDiameter), effect => effect.SunDiameter.Values[0].Value = 1.8d },
        { nameof(RainbowEffect.FieldOfView), effect => effect.FieldOfView.Values[0].Value = 120d },
        { nameof(RainbowEffect.CameraPitch), effect => effect.CameraPitch.Values[0].Value = 12d },
        { nameof(RainbowEffect.DropRadius), effect => effect.DropRadius.Values[0].Value = 0.07d },
        { nameof(RainbowEffect.SecondaryBow), effect => effect.SecondaryBow.Values[0].Value = 0d },
        { nameof(RainbowEffect.DarkBand), effect => effect.DarkBand.Values[0].Value = 100d },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheShader(string setting, Action<RainbowEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 3);
        change(effect);
        var after = RenderFrame(context, processor, 3);

        Assert.False(before.SamePixelsAs(after), setting);
        var model = new Model(effect, 3);
        Assert.All(after.Coordinates(), point => AssertClose(model.Expected(SkyBlue, point.X, point.Y), after[point.X, point.Y], $"{setting} {point}"));
    }

    [Fact]
    public void ReducingTheAmountToZeroRestoresTheSource()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var shown = RenderFrame(context, processor, 0);
        effect.Amount.Values[0].Value = 0d;
        var hidden = RenderFrame(context, processor, 0);

        Assert.False(shown.SamePixelsAs(hidden));
        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
    }

    [Fact]
    public void TransparentPixelsStayTransparentAndTheOthersKeepTheirOpacity()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        using var source = new SourceImage(context, Width, Height, Transparent);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var model = new Model(effect, 0);
        Assert.All(rendering.Coordinates(), point =>
        {
            Assert.Equal(source[point.X, point.Y].Alpha, rendering[point.X, point.Y].Alpha);
            AssertClose(model.Expected(Transparent, point.X, point.Y), rendering[point.X, point.Y], $"{point}");
        });
        Assert.Equal(Bgra.Transparent, rendering[Width / 2, 2]);
    }

    [Fact]
    public void ASemiTransparentSourceKeepsItsOpacityAndBlendsTheRainbowByCoverage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        using var source = new SourceImage(context, Width, Height, HalfTransparent);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var model = new Model(effect, 0);
        Assert.All(rendering.Coordinates(), point =>
        {
            AssertClose(model.Expected(HalfTransparent, point.X, point.Y), rendering[point.X, point.Y], $"{point}");
            Assert.InRange(rendering[point.X, point.Y].Blue, 0, rendering[point.X, point.Y].Alpha);
            Assert.InRange(rendering[point.X, point.Y].Green, 0, rendering[point.X, point.Y].Alpha);
            Assert.InRange(rendering[point.X, point.Y].Red, 0, rendering[point.X, point.Y].Alpha);
        });
    }

    [Fact]
    public void TheAmountBlendsLinearlyBetweenTheSourceAndTheFullRainbow()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide(50d);
        using var source = new SourceImage(context, Width, Height, SkyBlue);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var model = new Model(effect, 0);
        Assert.All(rendering.Coordinates(), point => AssertClose(model.Expected(SkyBlue, point.X, point.Y), rendering[point.X, point.Y], $"{point}"));
    }

    [Fact]
    public void TheImageSizeOnlyScalesTheArcAndKeepsItsAngularSize()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Wide();
        effect.SunDiameter.Values[0].Value = 2d;
        using var small = new SourceImage(context, Width, Height, SkyBlue);
        using var large = new SourceImage(context, Width * 2, Height * 2, SkyBlue);
        using var smallProcessor = effect.CreateVideoEffect(context);
        using var largeProcessor = effect.CreateVideoEffect(context);
        smallProcessor.SetInput(small.Bitmap);
        largeProcessor.SetInput(large.Bitmap);

        var first = RenderFrame(context, smallProcessor, 0);
        var second = RenderFrame(context, largeProcessor, 0);

        int Peak(Rendering rendering, int column, int rows)
            => Enumerable.Range(0, rows).MaxBy(y => rendering[column, y].Red - rendering[column, y].Blue);

        Assert.InRange(Math.Abs(Peak(first, Width / 2, Height) - Peak(second, Width, Height * 2) / 2.0), 0.0, 2.0);
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Wide().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        Assert.ThrowsAny<Exception>(() => Wide().CreateVideoEffect(null!));
    }
}
