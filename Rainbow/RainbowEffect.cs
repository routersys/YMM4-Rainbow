using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace Rainbow;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(nameof(Texts.Rainbow), [VideoEffectCategories.Filtering, VideoEffectCategories.Decoration], [nameof(Texts.TagRainbow), nameof(Texts.TagSky), nameof(Texts.TagLight)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class RainbowEffect : VideoEffectBase
{
    public override string Label => Texts.Rainbow;

    public RainbowEffect()
    {
        RainbowTelemetry.EnsureStartedOnce();
        RainbowUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Amount { get; } = new Animation(70, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Saturation), Description = nameof(Texts.SaturationDescription), Order = 1, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 200)]
    public Animation Saturation { get; } = new Animation(100, 0, 200);

    [Display(GroupName = nameof(Texts.SunGroup), Name = nameof(Texts.SunElevation), Description = nameof(Texts.SunElevationDescription), Order = 10, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", 0, 90)]
    public Animation SunElevation { get; } = new Animation(25, 0, 90);

    [Display(GroupName = nameof(Texts.SunGroup), Name = nameof(Texts.AntisolarDirection), Description = nameof(Texts.AntisolarDirectionDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", -180, 180)]
    public Animation AntisolarDirection { get; } = new Animation(0, -180, 180);

    [Display(GroupName = nameof(Texts.SunGroup), Name = nameof(Texts.SunDiameter), Description = nameof(Texts.SunDiameterDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "°", 0, 2)]
    public Animation SunDiameter { get; } = new Animation(0.53, 0, 2);

    [Display(GroupName = nameof(Texts.CameraGroup), Name = nameof(Texts.FieldOfView), Description = nameof(Texts.FieldOfViewDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", 10, 150)]
    public Animation FieldOfView { get; } = new Animation(60, 10, 150);

    [Display(GroupName = nameof(Texts.CameraGroup), Name = nameof(Texts.CameraPitch), Description = nameof(Texts.CameraPitchDescription), Order = 21, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", -60, 60)]
    public Animation CameraPitch { get; } = new Animation(0, -60, 60);

    [Display(GroupName = nameof(Texts.DropGroup), Name = nameof(Texts.DropRadius), Description = nameof(Texts.DropRadiusDescription), Order = 30, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "mm", 0.05, 0.4)]
    public Animation DropRadius { get; } = new Animation(0.2, 0.05, 0.4);

    [Display(GroupName = nameof(Texts.DropGroup), Name = nameof(Texts.SecondaryBow), Description = nameof(Texts.SecondaryBowDescription), Order = 31, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation SecondaryBow { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.DropGroup), Name = nameof(Texts.DarkBand), Description = nameof(Texts.DarkBandDescription), Order = 32, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation DarkBand { get; } = new Animation(30, 0, 100);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new RainbowEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            RainbowTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Amount, Saturation, SunElevation, AntisolarDirection, SunDiameter, FieldOfView, CameraPitch, DropRadius, SecondaryBow, DarkBand];
}
