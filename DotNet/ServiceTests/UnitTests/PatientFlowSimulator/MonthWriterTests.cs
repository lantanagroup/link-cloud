using LantanaGroup.Link.PatientFlowSimulator.Generation;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class MonthWriterTests
{
    [Fact]
    public void FileName_RoundTrips_ThroughTryParseFileHour()
    {
        var hour = new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.Zero);

        var name = MonthWriter.FileName(hour);
        var parsed = MonthWriter.TryParseFileHour(Path.Combine("data", "2026-11", name), out var back);

        Assert.Equal("2026-11-03T14Z.json", name);
        Assert.True(parsed);
        Assert.Equal(hour, back);
    }

    [Fact]
    public void TryParseFileHour_Manifest_IsNotAnHour()
    {
        Assert.False(MonthWriter.TryParseFileHour(MonthWriter.ManifestFileName, out _));
    }
}
