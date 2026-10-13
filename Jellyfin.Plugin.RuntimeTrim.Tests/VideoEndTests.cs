using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public class VideoEndTests
{
    [Theory]
    [InlineData("DURATION")]
    [InlineData("DURATION-eng")]
    public void MatroskaReadsDurationTag(string tag)
        => Assert.Equal(new TimeSpan(0, 0, 23, 40, 87), VideoEnd.Parse($"[STREAM]\r\nstart_time=0.000000\r\nduration=N/A\r\nTAG:{tag}=00:23:40.087000000\r\n[/STREAM]\r\n"));

    [Fact]
    public void Mp4ReadsStreamDuration()
        => Assert.Equal(TimeSpan.FromSeconds(8.007), VideoEnd.Parse("[STREAM]\r\nstart_time=0.000000\r\nduration=8.007000\r\n[/STREAM]\r\n"));

    [Fact]
    public void LateVideoStartIsAdded()
        => Assert.Equal(new TimeSpan(0, 46, 6), VideoEnd.Parse("[STREAM]\r\nstart_time=6.000000\r\nduration=2760.000000\r\n[/STREAM]\r\n[FORMAT]\r\nstart_time=0.000000\r\n[/FORMAT]\r\n"));

    // MPEG-TS prints the stream twice, once inside its program.
    [Fact]
    public void TransportStreamIsMeasuredFromContainerStart()
        => Assert.Equal(TimeSpan.FromSeconds(1500.5), VideoEnd.Parse("[PROGRAM]\r\n[STREAM]\r\nstart_time=3600.500000\r\nduration=1500.000000\r\n[/STREAM]\r\n[/PROGRAM]\r\n[STREAM]\r\nstart_time=3600.500000\r\nduration=1500.000000\r\n[/STREAM]\r\n[FORMAT]\r\nstart_time=3600.000000\r\n[/FORMAT]\r\n"));

    [Fact]
    public void NoVideoStreamIsNull()
        => Assert.Null(VideoEnd.Parse("[FORMAT]\r\nstart_time=0.000000\r\n[/FORMAT]\r\n"));
}
