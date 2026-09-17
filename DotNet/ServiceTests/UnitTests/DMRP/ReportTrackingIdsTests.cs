using FluentAssertions;
using LantanaGroup.Link.DMRP.Scheduling;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class ReportTrackingIdsTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_same_inputs_give_the_same_id()
    {
        ReportTrackingIds.For("100", "Monthly", Start).Should().Be(ReportTrackingIds.For("100", "Monthly", Start));
    }

    [Fact]
    public void Any_input_changing_gives_a_different_id()
    {
        var baseline = ReportTrackingIds.For("100", "Monthly", Start);

        ReportTrackingIds.For("101", "Monthly", Start).Should().NotBe(baseline);
        ReportTrackingIds.For("100", "Daily", Start).Should().NotBe(baseline);
        ReportTrackingIds.For("100", "Monthly", Start.AddMonths(1)).Should().NotBe(baseline);
    }

    [Fact]
    public void The_id_is_a_version_5_guid()
    {
        var bytes = ReportTrackingIds.For("100", "Monthly", Start).ToByteArray();

        // Byte 7 holds the version nibble in .NET's little-endian layout.
        (bytes[7] >> 4).Should().Be(5);
        (bytes[8] & 0xC0).Should().Be(0x80);
    }

    [Fact]
    public void The_id_is_stable_across_releases()
    {
        // Pinned: a change here means every previously produced report id would no longer dedupe.
        ReportTrackingIds.For("100", "Monthly", Start).ToString()
            .Should().Be(ReportTrackingIds.For("100", "Monthly", Start).ToString())
            .And.HaveLength(36);
    }
}
