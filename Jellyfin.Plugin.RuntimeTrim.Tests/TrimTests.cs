using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public class TrimTests
{
    private static readonly TimeSpan HalfHour = TimeSpan.FromMinutes(30);

    // An MP4 whose video lasts 100 seconds.
    private const string HundredSeconds = "[STREAM]\r\nstart_time=0.000000\r\nduration=100.000000\r\n[/STREAM]\r\n";

    [Theory]
    [InlineData("DURATION")]
    [InlineData("DURATION-eng")]
    public void MatroskaReadsDurationTag(string tag)
        => Assert.Equal(new Trim(HalfHour.Ticks, new TimeSpan(0, 0, 23, 40, 87).Ticks), Trim.Parse($"[STREAM]\r\nstart_time=0.000000\r\nduration=N/A\r\nTAG:{tag}=00:23:40.087000000\r\n[/STREAM]\r\n", HalfHour));

    [Fact]
    public void Mp4ReadsStreamDuration()
        => Assert.Equal(new Trim(HalfHour.Ticks, TimeSpan.FromSeconds(1420.007).Ticks), Trim.Parse("[STREAM]\r\nstart_time=0.000000\r\nduration=1420.007000\r\n[/STREAM]\r\n", HalfHour));

    [Fact]
    public void LateVideoStartIsAdded()
        => Assert.Equal(new Trim(TimeSpan.FromHours(1).Ticks, new TimeSpan(0, 46, 6).Ticks), Trim.Parse("[STREAM]\r\nstart_time=6.000000\r\nduration=2760.000000\r\n[/STREAM]\r\n[FORMAT]\r\nstart_time=0.000000\r\n[/FORMAT]\r\n", TimeSpan.FromHours(1)));

    // MPEG-TS prints the stream twice, once inside its program.
    [Fact]
    public void TransportStreamIsMeasuredFromContainerStart()
        => Assert.Equal(new Trim(HalfHour.Ticks, TimeSpan.FromSeconds(1500.5).Ticks), Trim.Parse("[PROGRAM]\r\n[STREAM]\r\nstart_time=3600.500000\r\nduration=1500.000000\r\n[/STREAM]\r\n[/PROGRAM]\r\n[STREAM]\r\nstart_time=3600.500000\r\nduration=1500.000000\r\n[/STREAM]\r\n[FORMAT]\r\nstart_time=3600.000000\r\n[/FORMAT]\r\n", HalfHour));

    [Fact]
    public void NoVideoStreamIsNull()
        => Assert.Null(Trim.Parse("[FORMAT]\r\nstart_time=0.000000\r\n[/FORMAT]\r\n", HalfHour));

    [Theory]
    [InlineData(104.9, false)]
    [InlineData(105, true)]
    public void TrimsFromFiveSecondsPastTheVideo(double runtime, bool trimmed)
        => Assert.Equal(trimmed, Trim.Parse(HundredSeconds, TimeSpan.FromSeconds(runtime)) is not null);

    // A still image over a long audio track.
    [Theory]
    [InlineData(200, true)]
    [InlineData(200.1, false)]
    public void KeepsVideoEndingBeforeHalfTheRuntime(double runtime, bool trimmed)
        => Assert.Equal(trimmed, Trim.Parse(HundredSeconds, TimeSpan.FromSeconds(runtime)) is not null);
}
