using FluentAssertions;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the ResourcesNormalized message Normalization produces. MeasureEval reads it in Java with
/// case-insensitive property names, so the PascalCase names here are what it matches against.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesNormalizedContractTests
{
    private const string Fixture = "resources-normalized.json";

    [Fact]
    public void Serialize_NormalizationValue_MatchesFixture()
    {
        AssertProducesFixture(CreateValue(), Fixture);
    }

    [Fact]
    public void Deserialize_NormalizationValue_RoundTrips()
    {
        var consumed = Consume<ResourcesNormalizedValue>(Fixture);

        consumed.Should().BeEquivalentTo(CreateValue());
    }

    private static ResourcesNormalizedValue CreateValue()
    {
        return new ResourcesNormalizedValue
        {
            QueryType = "Supplemental",
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
            ],
            ReportableEvent = "Discharge",
            CacheType = ResourceCacheType.Redis,
            CacheKey = "corr-001"
        };
    }
}
