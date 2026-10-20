using System.Windows;
using Telemetry;

namespace Rainbow.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        RainbowTelemetry.EnsureStartedOnce();
        RainbowTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new RainbowEffect();

        Assert.Equal(Texts.Rainbow, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
