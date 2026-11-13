using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static UnitTests.Shared.Contracts.KafkaContract;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the ResourcesAcquired message and its <see cref="ResourceKey"/>. DataAcquisition produces it;
/// Normalization reads it with its own class and produces that class again on its retry path.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredContractTests
{
    private const string Fixture = "resources-acquired.json";
    private const string KeyFixture = "resource-key.json";

    [Fact]
    public void Serialize_DataAcquisitionValue_MatchesFixture()
    {
        AssertProducesFixture(CreateDataAcquisitionValue(), Fixture);
    }

    [Fact]
    public void Deserialize_DataAcquisitionValue_RoundTrips()
    {
        var consumed = Consume<ResourcesAcquired>(Fixture);

        consumed.Should().BeEquivalentTo(CreateDataAcquisitionValue());
    }

    [Fact]
    public void Deserialize_NormalizationConsumer_ReadsEveryField()
    {
        var consumed = Consume<ResourcesAcquiredValue>(Fixture);

        consumed.Should().BeEquivalentTo(CreateNormalizationValue());
    }

    [Fact]
    public void Serialize_NormalizationRetryValue_MatchesFixture()
    {
        AssertProducesFixture(CreateNormalizationValue(), Fixture);
    }

    [Fact]
    public void Serialize_ResourceKey_MatchesFixture()
    {
        AssertProducesFixture(CreateKey(), KeyFixture);
    }

    [Fact]
    public void Deserialize_ResourceKey_RoundTrips()
    {
        var consumed = Consume<ResourceKey>(KeyFixture);

        consumed.Should().BeEquivalentTo(CreateKey());
    }

    private static ResourcesAcquired CreateDataAcquisitionValue()
    {
        return new ResourcesAcquired
        {
            QueryType = "Supplemental",
            ScheduledReports = [CreateScheduledReport()],
            ReportableEvent = ReportableEvent.Discharge,
            CacheType = ResourceCacheType.Redis,
            CacheKeys = ["corr-001:Patient", "corr-001:Encounter"]
        };
    }

    private static ResourcesAcquiredValue CreateNormalizationValue()
    {
        return new ResourcesAcquiredValue
        {
            QueryType = "Supplemental",
            ScheduledReports = [CreateScheduledReport()],
            ReportableEvent = "Discharge",
            CacheType = ResourceCacheType.Redis,
            CacheKeys = ["corr-001:Patient", "corr-001:Encounter"]
        };
    }

    private static ScheduledReport CreateScheduledReport()
    {
        return new ScheduledReport
        {
            ReportTypes = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
            Frequency = Frequency.Monthly,
            StartDate = Utc("2026-10-01T05:00:00Z"),
            EndDate = Utc("2026-11-01T04:59:59Z"),
            ReportTrackingId = "3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61"
        };
    }

    private static ResourceKey CreateKey()
    {
        return new ResourceKey
        {
            FacilityId = "Hospital1",
            PatientId = "pat-001"
        };
    }
}
