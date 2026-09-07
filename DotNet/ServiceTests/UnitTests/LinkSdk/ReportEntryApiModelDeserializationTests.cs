using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace UnitTests.LinkSdk;

/// <summary>
/// Deserializes a payload captured from the running Report service into the SDK's models.
/// </summary>
/// <remarks>
/// The SDK models are hand-written and decoupled from the service's own, so nothing at compile time ties
/// them to what the service actually serializes. A renamed or re-cased property does not fail to build --
/// it silently deserializes as null or zero, and an SDK consumer sees a patient with no indicators rather
/// than an error. This is the payload the service returned for the fixture patient whose HSLOC mapping is
/// partial.
/// </remarks>
[Trait("Category", "UnitTests")]
public class ReportEntryApiModelDeserializationTests
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNameCaseInsensitive = true };

    private const string DetailPayload = """
        {
          "id": "6662f72c-d1fc-40f0-9414-2189b05c7c0a",
          "facilityId": "leglink-1076",
          "patientId": "mapping-patient-b",
          "reportingStatus": 2,
          "submissionStatus": 0,
          "locationOrgStatus": 2,
          "encounterMappingStatus": 2,
          "hslocMappingStatus": 3,
          "acquisitionEvaluatedAt": "2026-08-28T21:01:50.166Z",
          "normalizationEvaluatedAt": "2026-08-28T21:02:00.099Z",
          "measureReports": [],
          "acquisition": {
            "locationOrg": {
              "status": 1,
              "encounterCount": 2,
              "orgEncounterCount": 2,
              "assumedOrgEncounterCount": 0,
              "matches": [
                { "locationId": "icu-a", "locationName": "5 West Medical ICU", "locationAlias": "5 West Medical ICU", "partOfValue": "hosp-a", "isOrgLocation": true },
                { "locationId": "pharm-a", "locationName": "Inpatient Pharmacy", "locationAlias": "Inpatient Pharmacy", "partOfValue": "hosp-a", "isOrgLocation": true }
              ]
            }
          },
          "normalization": {
            "codeMaps": [
              {
                "sourceSystem": "http://hospital.example.org/locations",
                "targetSystem": "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                "status": 2,
                "mappedCount": 1,
                "unmappedCount": 1,
                "failureCount": 0,
                "unmappedCodes": [ "PHARMACY" ]
              }
            ],
            "passes": [
              { "correlationId": "c1", "queryType": "Initial", "codeMaps": [] },
              { "correlationId": "c1", "queryType": "Supplemental", "codeMaps": [] }
            ]
          }
        }
        """;

    [Fact]
    public void TheIndicatorsDeserializeOntoTheEntryModel()
    {
        var entry = JsonSerializer.Deserialize<ReportEntryDetailApiModel>(DetailPayload, Options)!;

        Assert.Equal("mapping-patient-b", entry.PatientId);
        Assert.Equal(MappingIndicatorStatus.Mapped, entry.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Mapped, entry.EncounterMappingStatus);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, entry.HslocMappingStatus);
        Assert.NotNull(entry.AcquisitionEvaluatedAt);
        Assert.NotNull(entry.NormalizationEvaluatedAt);
    }

    [Fact]
    public void TheAcquisitionEvidenceDeserializes()
    {
        var entry = JsonSerializer.Deserialize<ReportEntryDetailApiModel>(DetailPayload, Options)!;
        var org = entry.Acquisition!.LocationOrg;

        Assert.Equal(2, org.EncounterCount);
        Assert.Equal(2, org.OrgEncounterCount);
        Assert.Equal(0, org.AssumedOrgEncounterCount);

        var pharmacy = Assert.Single(org.Matches, m => m.LocationId == "pharm-a");
        Assert.Equal("hosp-a", pharmacy.PartOfValue);
        Assert.True(pharmacy.IsOrgLocation);
    }

    [Fact]
    public void TheUnmappedCodesReachTheConsumer()
    {
        var entry = JsonSerializer.Deserialize<ReportEntryDetailApiModel>(DetailPayload, Options)!;

        // The payoff of the whole feature: the code an operator would go and add to the facility's map.
        var codeMap = Assert.Single(entry.Normalization!.CodeMaps);
        Assert.Equal(1, codeMap.MappedCount);
        Assert.Equal(1, codeMap.UnmappedCount);
        Assert.Equal("PHARMACY", Assert.Single(codeMap.UnmappedCodes));
    }

    [Fact]
    public void EachAcquisitionPassIsRetained()
    {
        var entry = JsonSerializer.Deserialize<ReportEntryDetailApiModel>(DetailPayload, Options)!;

        Assert.Equal(
            ["Initial", "Supplemental"],
            entry.Normalization!.Passes.Select(p => p.QueryType));
    }

    [Fact]
    public void ASourceThatNeverReportedDeserializesAsAbsent()
    {
        const string payload = """
            {
              "patientId": "mapping-patient-c",
              "locationOrgStatus": 4,
              "hslocMappingStatus": 8,
              "acquisitionEvaluatedAt": "2026-08-28T21:01:50.166Z",
              "normalizationEvaluatedAt": null,
              "acquisition": { "locationOrg": { "encounterCount": 1, "orgEncounterCount": 0, "matches": [] } },
              "normalization": null
            }
            """;

        var entry = JsonSerializer.Deserialize<ReportEntryDetailApiModel>(payload, Options)!;

        // Absent, not an empty object -- the distinction the two timestamps exist to carry, and it has to
        // survive into the SDK or a consumer cannot tell "reported nothing" from "never reported".
        Assert.Null(entry.Normalization);
        Assert.Null(entry.NormalizationEvaluatedAt);
        Assert.NotNull(entry.Acquisition);
        Assert.Equal(MappingIndicatorStatus.Excluded, entry.HslocMappingStatus);
    }

    [Fact]
    public void ThePagedSearchResponseDeserializes()
    {
        const string payload = """
            {
              "records": [
                { "patientId": "mapping-patient-a", "locationOrgStatus": 2, "encounterMappingStatus": 2, "hslocMappingStatus": 2 },
                { "patientId": "mapping-patient-d", "locationOrgStatus": 6, "encounterMappingStatus": 4, "hslocMappingStatus": 7 }
              ],
              "metadata": { "pageSize": 10, "pageNumber": 1, "totalCount": 2, "totalPages": 1 }
            }
            """;

        var page = JsonSerializer.Deserialize<PagedConfigModel<ReportEntryApiModel>>(payload, Options)!;

        Assert.Equal(2, page.Records.Count);
        Assert.Equal(MappingIndicatorStatus.Assumed, page.Records[1].LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.NothingToEvaluate, page.Records[1].HslocMappingStatus);

        // The grid carries no evidence; that is the per-patient operation's job.
        Assert.IsNotType<ReportEntryDetailApiModel>(page.Records[0]);
    }
}
