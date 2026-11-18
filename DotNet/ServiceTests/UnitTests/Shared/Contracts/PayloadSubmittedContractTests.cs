using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the PayloadSubmitted key and value. Submission produces them and Report reads them, both with the
/// shared classes.
/// </summary>
[Trait("Category", "UnitTests")]
public class PayloadSubmittedContractTests
{
    private const string KeyFixture = "payload-submitted-key.json";
    private const string ValueFixture = "payload-submitted-patient.json";

    [Fact]
    public void Serialize_Key_MatchesFixture()
    {
        AssertProducesFixture(CreateKey(), KeyFixture);
    }

    [Fact]
    public void Deserialize_Key_RoundTrips()
    {
        var consumed = Consume<PayloadSubmittedKey>(KeyFixture);

        consumed.Should().BeEquivalentTo(CreateKey());
    }

    [Fact]
    public void Serialize_PatientValue_MatchesFixture()
    {
        AssertProducesFixture(CreateValue(), ValueFixture);
    }

    [Fact]
    public void Deserialize_PatientValue_RoundTrips()
    {
        var consumed = Consume<PayloadSubmittedValue>(ValueFixture);

        consumed.Should().BeEquivalentTo(CreateValue());
    }

    private static PayloadSubmittedKey CreateKey()
    {
        return new PayloadSubmittedKey
        {
            FacilityId = "Hospital1",
            ReportScheduleId = Guid.Parse("9d2e4b7a-1c3f-4a58-b6e0-7f8a2c5d1e93")
        };
    }

    private static PayloadSubmittedValue CreateValue()
    {
        return new PayloadSubmittedValue
        {
            PayloadType = PayloadType.MeasureReportSubmissionEntry,
            PatientId = "pat-001"
        };
    }
}
