using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;

namespace Rainbow.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, RainbowEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("sun-elevation-animated-frames", Create(effect => effect.SunElevation.CopyFrom(Linear(10d, 40d))), [0, 100, 200, 299]);
        yield return ("drop-radius-animated-frames", Create(effect => effect.DropRadius.CopyFrom(Linear(0.08d, 0.4d))), [0, 100, 200, 299]);
        yield return ("antisolar-direction-animated-frames", Create(effect => effect.AntisolarDirection.CopyFrom(Linear(-40d, 40d))), [0, 100, 200, 299]);
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0), [0]);
        yield return ("amount-40", Create(effect => effect.Amount.Values[0].Value = 40), [0]);
        yield return ("saturation-0", Create(effect => effect.Saturation.Values[0].Value = 0), [0]);
        yield return ("saturation-200", Create(effect => effect.Saturation.Values[0].Value = 200), [0]);
        yield return ("sun-elevation-10", Create(effect => effect.SunElevation.Values[0].Value = 10), [0]);
        yield return ("sun-elevation-45", Create(effect => effect.SunElevation.Values[0].Value = 45), [0]);
        yield return ("antisolar-direction-minus-30", Create(effect => effect.AntisolarDirection.Values[0].Value = -30), [0]);
        yield return ("antisolar-direction-30", Create(effect => effect.AntisolarDirection.Values[0].Value = 30), [0]);
        yield return ("sun-diameter-0", Create(effect => effect.SunDiameter.Values[0].Value = 0), [0]);
        yield return ("sun-diameter-2", Create(effect => effect.SunDiameter.Values[0].Value = 2), [0]);
        yield return ("field-of-view-30", Create(effect => effect.FieldOfView.Values[0].Value = 30), [0]);
        yield return ("field-of-view-120", Create(effect => effect.FieldOfView.Values[0].Value = 120), [0]);
        yield return ("camera-pitch-minus-20", Create(effect => effect.CameraPitch.Values[0].Value = -20), [0]);
        yield return ("camera-pitch-10", Create(effect => effect.CameraPitch.Values[0].Value = 10), [0]);
        yield return ("drop-radius-0.06", Create(effect => effect.DropRadius.Values[0].Value = 0.06), [0]);
        yield return ("drop-radius-0.4", Create(effect => effect.DropRadius.Values[0].Value = 0.4), [0]);
        yield return ("secondary-bow-0", Create(effect => { effect.FieldOfView.Values[0].Value = 100; effect.SecondaryBow.Values[0].Value = 0; }), [0]);
        yield return ("secondary-bow-100-wide", Create(effect => effect.FieldOfView.Values[0].Value = 100), [0]);
        yield return ("dark-band-0", Create(effect => { effect.FieldOfView.Values[0].Value = 100; effect.DarkBand.Values[0].Value = 0; }), [0]);
        yield return ("dark-band-100", Create(effect => { effect.FieldOfView.Values[0].Value = 100; effect.DarkBand.Values[0].Value = 100; }), [0]);
    }

    public static IEnumerable<(string Name, Func<RainbowEffect> Create, Action<RainbowEffect> Change, int Frame)> Transitions()
    {
        yield return ("amount-70-to-0", () => Create(), effect => effect.Amount.Values[0].Value = 0, 0);
        yield return ("amount-0-to-70", () => Create(effect => effect.Amount.Values[0].Value = 0), effect => effect.Amount.Values[0].Value = 70, 0);
        yield return ("amount-70-to-40", () => Create(), effect => effect.Amount.Values[0].Value = 40, 0);
        yield return ("saturation-100-to-0", () => Create(), effect => effect.Saturation.Values[0].Value = 0, 0);
        yield return ("sun-elevation-25-to-45", () => Create(), effect => effect.SunElevation.Values[0].Value = 45, 0);
        yield return ("antisolar-direction-0-to-30", () => Create(), effect => effect.AntisolarDirection.Values[0].Value = 30, 0);
        yield return ("sun-diameter-0.53-to-2", () => Create(), effect => effect.SunDiameter.Values[0].Value = 2, 0);
        yield return ("field-of-view-60-to-120", () => Create(), effect => effect.FieldOfView.Values[0].Value = 120, 0);
        yield return ("camera-pitch-0-to-20", () => Create(), effect => effect.CameraPitch.Values[0].Value = 20, 0);
        yield return ("drop-radius-0.2-to-0.4", () => Create(), effect => effect.DropRadius.Values[0].Value = 0.4, 0);
        yield return ("secondary-bow-100-to-0", () => Create(), effect => effect.SecondaryBow.Values[0].Value = 0, 0);
        yield return ("dark-band-30-to-100", () => Create(), effect => effect.DarkBand.Values[0].Value = 100, 0);
    }

    public static IEnumerable<(string Name, RainbowEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("drop-radius-animated", Create(effect => effect.DropRadius.CopyFrom(Linear(0.08d, 0.4d))));
        yield return ("antisolar-direction-animated", Create(effect => effect.AntisolarDirection.CopyFrom(Linear(-40d, 40d))));
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0));
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}""")) ?? throw new HarnessException("アニメーションを読み込めません。");

    public static RainbowEffect Create(Action<RainbowEffect>? configure = null)
    {
        var effect = new RainbowEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
