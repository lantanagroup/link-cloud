using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the SubmitPayload key and both value flavors. Report produces them and Submission reads them, both
/// with the shared classes.
/// </summary>
[Trait("Category", "UnitTests")]
public class SubmitPayloadContractTests
{
    private const string KeyFixture = "submit-payload-key.json";
    private const string PatientFixture = "submit-payload-patient.json";
    private const string ReportScheduleFixture = "submit-payload-report-schedule.json";

    [Fact]
    public void Serialize_Key_MatchesFixture()
    {
        AssertProducesFixture(CreateKey(), KeyFixture);
    }

    [Fact]
    public void Deserialize_Key_RoundTrips()
    {
        var consumed = Consume<SubmitPayloadKey>(KeyFixture);

        consumed.Should().BeEquivalentTo(CreateKey());
    }

    [Fact]
    public void Serialize_PatientPayload_MatchesFixture()
    {
        AssertProducesFixture(CreatePatientValue(), PatientFixture);
    }

    [Fact]
    public void Deserialize_PatientPayload_RoundTrips()
    {
        var consumed = Consume<SubmitPayloadValue>(PatientFixture);

        consumed.Should().BeEquivalentTo(CreatePatientValue());
    }

    [Fact]
    public void Serialize_ReportSchedulePayload_MatchesFixture()
    {
        AssertProducesFixture(CreateReportScheduleValue(), ReportScheduleFixture);
    }

    [Fact]
    public void Deserialize_ReportSchedulePayload_RoundTrips()
    {
        var consumed = Consume<SubmitPayloadValue>(ReportScheduleFixture);

        consumed.Should().BeEquivalentTo(CreateReportScheduleValue());
    }

    private static SubmitPayloadKey CreateKey()
    {
        return new SubmitPayloadKey
        {
            FacilityId = "Hospital1",
            ReportScheduleId = Guid.Parse("9d2e4b7a-1c3f-4a58-b6e0-7f8a2c5d1e93")
        };
    }

    private static SubmitPayloadValue CreatePatientValue()
    {
        return new SubmitPayloadValue
        {
            PayloadType = PayloadType.MeasureReportSubmissionEntry,
            PayloadUri = "http://azurite:10000/devstoreaccount1/internal/Hospital1/patient-pat-001.ndjson",
            ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
            StartDate = Utc("2026-10-01T05:00:00Z"),
            EndDate = Utc("2026-11-01T04:59:59Z"),
            PatientId = "pat-001"
        };
    }

    private static SubmitPayloadValue CreateReportScheduleValue()
    {
        return new SubmitPayloadValue
        {
            PayloadType = PayloadType.ReportSchedule,
            PayloadUri = "http://azurite:10000/devstoreaccount1/internal/Hospital1/manifest.ndjson",
            ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
            StartDate = Utc("2026-10-01T05:00:00Z"),
            EndDate = Utc("2026-11-01T04:59:59Z")
        };
    }
}
