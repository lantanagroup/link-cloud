using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static UnitTests.Shared.Contracts.KafkaContract;
using AdminBffMessage = LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration.DataAcquisitionRequestedMessage;
using AdminBffScheduledReport = LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration.ScheduledReport;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the DataAcquisitionRequested message. Report and QueryDispatch produce it with the shared value class,
/// Admin BFF's integration endpoint produces its own shape, and DataAcquisition reads both with its own class.
/// MeasureEval also produces it for the supplemental pass and is not covered here.
/// </summary>
[Trait("Category", "UnitTests")]
public class DataAcquisitionRequestedContractTests
{
    private const string Fixture = "data-acquisition-requested.json";
    private const string AdminBffFixture = "data-acquisition-requested-admin-bff.json";

    [Fact]
    public void Serialize_SharedValue_MatchesFixture()
    {
        AssertProducesFixture(CreateSharedValue(), Fixture);
    }

    [Fact]
    public void Deserialize_SharedValue_RoundTrips()
    {
        var consumed = Consume<DataAcquisitionRequestedValue>(Fixture);

        consumed.Should().BeEquivalentTo(CreateSharedValue());
    }

    [Fact]
    public void Deserialize_DataAcquisitionConsumer_ReadsEveryField()
    {
        var consumed = Consume<DataAcquisitionRequested>(Fixture);

        Assert.Equal("pat-001", consumed.PatientId);
        Assert.Equal("Initial", consumed.QueryType);
        Assert.Equal(ReportableEvent.Discharge, consumed.ReportableEvent);
        consumed.ScheduledReports.Should().BeEquivalentTo(CreateSharedValue().ScheduledReports);
    }

    [Fact]
    public void Serialize_AdminBffMessage_MatchesFixture()
    {
        // Admin BFF produces through IProducer<string, object>, so the runtime type decides the shape.
        AssertProducesFixture<object>(CreateAdminBffMessage(), AdminBffFixture);
    }

    [Fact]
    public void Deserialize_AdminBffMessageInDataAcquisition_ReportableEventDefaultsToDischarge()
    {
        var consumed = Consume<DataAcquisitionRequested>(AdminBffFixture);

        Assert.Equal("pat-001", consumed.PatientId);
        Assert.Equal("Initial", consumed.QueryType);
        Assert.Equal(ReportableEvent.Discharge, consumed.ReportableEvent);
        var report = Assert.Single(consumed.ScheduledReports);
        Assert.Equal(["NHSNdQMAcuteCareHospitalInitialPopulation"], report.ReportTypes);
        Assert.Equal(Frequency.Discharge, report.Frequency);
        Assert.Null(report.ReportTrackingId);
    }

    private static DataAcquisitionRequestedValue CreateSharedValue()
    {
        return new DataAcquisitionRequestedValue
        {
            PatientId = "pat-001",
            QueryType = "Initial",
            ReportableEvent = "Discharge",
            ScheduledReports =
            [
                new ScheduledReport
                {
                    ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
                    Frequency = Frequency.Monthly,
                    StartDate = Utc("2026-10-01T05:00:00Z"),
                    EndDate = Utc("2026-11-01T04:59:59Z"),
                    ReportTrackingId = "3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61"
                }
            ]
        };
    }

    private static AdminBffMessage CreateAdminBffMessage()
    {
        return new AdminBffMessage
        {
            PatientId = "pat-001",
            QueryType = "Initial",
            ScheduledReports =
            [
                new AdminBffScheduledReport
                {
                    ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
                    StartDate = Utc("2026-10-01T05:00:00Z"),
                    EndDate = Utc("2026-11-01T04:59:59Z")
                }
            ]
        };
    }
}
