using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the ReportScheduled message. Tenant produces <see cref="ReportScheduledMessage"/> through
/// IProducer&lt;string, object&gt;; Report and QueryDispatch read it as <see cref="ReportScheduledValue"/>,
/// which has an enum frequency, offset dates and a Guid tracking id.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportScheduledContractTests
{
    private const string Fixture = "report-scheduled.json";

    [Fact]
    public void Serialize_TenantMessage_MatchesFixture()
    {
        var message = new ReportScheduledMessage
        {
            ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
            Frequency = "Monthly",
            StartDate = Utc("2026-10-01T05:00:00Z"),
            EndDate = Utc("2026-11-01T04:59:59Z"),
            ReportTrackingId = "3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61"
        };

        AssertProducesFixture<object>(message, Fixture);
    }

    [Fact]
    public void Deserialize_ReportAndQueryDispatchConsumer_ReadsEveryField()
    {
        var consumed = Consume<ReportScheduledValue>(Fixture);

        Assert.Equal(["NHSNdQMAcuteCareHospitalInitialPopulation"], consumed.ReportTypes);
        Assert.Equal(Frequency.Monthly, consumed.Frequency);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), consumed.StartDate);
        Assert.Equal(TimeSpan.Zero, consumed.StartDate.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 4, 59, 59, TimeSpan.Zero), consumed.EndDate);
        Assert.Equal(Guid.Parse("3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61"), consumed.ReportTrackingId);
        Assert.True(consumed.IsValid());
    }
}
