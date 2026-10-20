using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace Rainbow.Tests;

public sealed class RainbowEffectTests
{
    static PropertyInfo Property(string name) => typeof(RainbowEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(RainbowEffect effect)
        => [effect.Amount, effect.Saturation, effect.SunElevation, effect.AntisolarDirection, effect.SunDiameter, effect.FieldOfView, effect.CameraPitch, effect.DropRadius, effect.SecondaryBow, effect.DarkBand];

    [Theory]
    [InlineData(nameof(RainbowEffect.Amount), 70d, 0d, 100d)]
    [InlineData(nameof(RainbowEffect.Saturation), 100d, 0d, 200d)]
    [InlineData(nameof(RainbowEffect.SunElevation), 25d, 0d, 90d)]
    [InlineData(nameof(RainbowEffect.AntisolarDirection), 0d, -180d, 180d)]
    [InlineData(nameof(RainbowEffect.SunDiameter), 0.53d, 0d, 2d)]
    [InlineData(nameof(RainbowEffect.FieldOfView), 60d, 10d, 150d)]
    [InlineData(nameof(RainbowEffect.CameraPitch), 0d, -60d, 60d)]
    [InlineData(nameof(RainbowEffect.DropRadius), 0.2d, 0.05d, 0.4d)]
    [InlineData(nameof(RainbowEffect.SecondaryBow), 100d, 0d, 100d)]
    [InlineData(nameof(RainbowEffect.DarkBand), 30d, 0d, 100d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new RainbowEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheDropRadiusRangeMatchesTheSphericalDropsTheModelCovers()
    {
        var effect = new RainbowEffect();

        Assert.Equal(RainbowSettings.MinimumRadiusMillimeters, effect.DropRadius.MinValue);
        Assert.Equal(RainbowSettings.MaximumRadiusMillimeters, effect.DropRadius.MaxValue);
        Assert.Equal(RainbowSettings.MaximumSunDiameterDegrees, effect.SunDiameter.MaxValue);
        Assert.Equal(RainbowSettings.MinimumFieldOfViewDegrees, effect.FieldOfView.MinValue);
        Assert.Equal(RainbowSettings.MaximumFieldOfViewDegrees, effect.FieldOfView.MaxValue);
    }

    [Fact]
    public void TheAnimatedParametersAreListedOnceAndReused()
    {
        var effect = new RainbowEffect();
        var method = typeof(RainbowEffect).GetMethod("GetAnimatables", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var first = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;
        var second = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;

        Assert.Same(first, second);
        Assert.Equal(Animations(effect), first);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        Assert.Equal(Texts.Rainbow, new RainbowEffect().Label);
    }

    [Fact]
    public void TheTenNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new RainbowEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.Equal(10, Animations(effect).Length);
        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new RainbowEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(RainbowEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Fact]
    public void TheEffectIsRegisteredForFilteringAndDecorationWithoutAviUtlSupport()
    {
        var attribute = typeof(RainbowEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.Rainbow), attribute.Name);
        Assert.Equal([VideoEffectCategories.Filtering, VideoEffectCategories.Decoration], attribute.Categories);
        Assert.Equal([nameof(Texts.TagRainbow), nameof(Texts.TagSky), nameof(Texts.TagLight)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.Rainbow, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(RainbowEffect.Amount), nameof(Texts.BasicGroup), nameof(Texts.Amount), nameof(Texts.AmountDescription), 0)]
    [InlineData(nameof(RainbowEffect.Saturation), nameof(Texts.BasicGroup), nameof(Texts.Saturation), nameof(Texts.SaturationDescription), 1)]
    [InlineData(nameof(RainbowEffect.SunElevation), nameof(Texts.SunGroup), nameof(Texts.SunElevation), nameof(Texts.SunElevationDescription), 10)]
    [InlineData(nameof(RainbowEffect.AntisolarDirection), nameof(Texts.SunGroup), nameof(Texts.AntisolarDirection), nameof(Texts.AntisolarDirectionDescription), 11)]
    [InlineData(nameof(RainbowEffect.SunDiameter), nameof(Texts.SunGroup), nameof(Texts.SunDiameter), nameof(Texts.SunDiameterDescription), 12)]
    [InlineData(nameof(RainbowEffect.FieldOfView), nameof(Texts.CameraGroup), nameof(Texts.FieldOfView), nameof(Texts.FieldOfViewDescription), 20)]
    [InlineData(nameof(RainbowEffect.CameraPitch), nameof(Texts.CameraGroup), nameof(Texts.CameraPitch), nameof(Texts.CameraPitchDescription), 21)]
    [InlineData(nameof(RainbowEffect.DropRadius), nameof(Texts.DropGroup), nameof(Texts.DropRadius), nameof(Texts.DropRadiusDescription), 30)]
    [InlineData(nameof(RainbowEffect.SecondaryBow), nameof(Texts.DropGroup), nameof(Texts.SecondaryBow), nameof(Texts.SecondaryBowDescription), 31)]
    [InlineData(nameof(RainbowEffect.DarkBand), nameof(Texts.DropGroup), nameof(Texts.DarkBand), nameof(Texts.DarkBandDescription), 32)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(RainbowEffect.Amount), "F1", "%", 0d, 100d)]
    [InlineData(nameof(RainbowEffect.Saturation), "F1", "%", 0d, 200d)]
    [InlineData(nameof(RainbowEffect.SunElevation), "F1", "°", 0d, 90d)]
    [InlineData(nameof(RainbowEffect.AntisolarDirection), "F1", "°", -180d, 180d)]
    [InlineData(nameof(RainbowEffect.SunDiameter), "F2", "°", 0d, 2d)]
    [InlineData(nameof(RainbowEffect.FieldOfView), "F1", "°", 10d, 150d)]
    [InlineData(nameof(RainbowEffect.CameraPitch), "F1", "°", -60d, 60d)]
    [InlineData(nameof(RainbowEffect.DropRadius), "F2", "mm", 0.05d, 0.4d)]
    [InlineData(nameof(RainbowEffect.SecondaryBow), "F1", "%", 0d, 100d)]
    [InlineData(nameof(RainbowEffect.DarkBand), "F1", "%", 0d, 100d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new RainbowEffect();
        var values = new[] { 55d, 140d, 33d, -70d, 1.25d, 80d, -12d, 0.31d, 44d, 77d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
    }
}
