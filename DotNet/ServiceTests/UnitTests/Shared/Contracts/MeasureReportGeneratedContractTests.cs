using FluentAssertions;
using LantanaGroup.Link.Report.Models;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the MeasureReportGenerated message. MeasureEval produces it in Java with camelCase names, and Report
/// produces its own PascalCase copy on its retry path; Report has to read both.
/// </summary>
/// <remarks>
/// The MeasureEval fixture has the shape of a message read from a test environment's topic, with made-up
/// values. The Java record's Lombok boolean <c>isReportable</c> goes on the wire as <c>reportable</c>.
/// </remarks>
[Trait("Category", "UnitTests")]
public class MeasureReportGeneratedContractTests
{
    private const string MeasureEvalFixture = "measure-report-generated-measureeval.json";
    private const string ReportFixture = "measure-report-generated-report.json";

    [Fact]
    public void Deserialize_MeasureEvalMessageInReport_ReadsEveryField()
    {
        var consumed = Consume<MeasureReportGeneratedValue>(MeasureEvalFixture);

        consumed.Should().BeEquivalentTo(CreateValue());
    }

    [Fact]
    public void Serialize_ReportRetryValue_MatchesFixture()
    {
        AssertProducesFixture(CreateValue(), ReportFixture);
    }

    [Fact]
    public void Deserialize_ReportRetryValue_RoundTrips()
    {
        var consumed = Consume<MeasureReportGeneratedValue>(ReportFixture);

        consumed.Should().BeEquivalentTo(CreateValue());
    }

    private static MeasureReportGeneratedValue CreateValue()
    {
        return new MeasureReportGeneratedValue
        {
            FacilityId = "Hospital1",
            ReportTrackingId = "3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61",
            PatientId = "pat-001",
            ReportType = "NHSNdQMAcuteCareHospitalInitialPopulation",
            MeasureReportId = "mr-001",
            MeasureReportURI = "http://azurite:10000/devstoreaccount1/internal/Hospital1/pat-001.json",
            MeasureReportBlobName = "Hospital1/pat-001.json",
            Reportable = true
        };
    }
}
