using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace UnitTests.Shared;

/// <summary>
/// The resource cache store is chosen by each service's own configuration, and reads go Redis-first
/// with a blob fallback (LEGLINK-1118). The CacheType field that once routed reads carried no
/// information, so LEGLINK-1279 removed it from the Kafka contract in both runtimes.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourceCacheMessageContractTests
{
    [Fact]
    public void ResourcesAcquired_FromDataAcquisition_DoesNotCarryCacheType()
    {
        var message = new ResourcesAcquired
        {
            QueryType = "Initial",
            CacheKeys = new List<string> { "corr-1:Encounter" }
        };

        AssertNoCacheTypeProperty(Serialize(message));
    }

    [Fact]
    public void ResourcesNormalized_FromNormalization_DoesNotCarryCacheType()
    {
        var message = new ResourcesNormalizedValue
        {
            QueryType = "Initial",
            ReportableEvent = "Discharge",
            ScheduledReports = new List<ScheduledReport>(),
            CacheKey = "corr-1"
        };

        AssertNoCacheTypeProperty(Serialize(message));
    }

    [Fact]
    public void ResourcesAcquiredValue_LegacyMessageCarryingCacheType_StillDeserializes()
    {
        // Messages produced before the removal sit in the retry and dead-letter topics, and an old
        // Data Acquisition pod can still be producing during a rolling deploy.
        const string legacy = """
            {
              "QueryType": "Initial",
              "ReportableEvent": "Discharge",
              "ScheduledReports": [],
              "CacheType": "Redis",
              "CacheKeys": [ "corr-1:Encounter", "corr-1:Condition" ]
            }
            """;

        var value = new JsonWithFhirMessageDeserializer<ResourcesAcquiredValue>()
            .Deserialize(Encoding.UTF8.GetBytes(legacy), isNull: false, SerializationContext.Empty);

        Assert.Equal("Initial", value.QueryType);
        Assert.Equal(new[] { "corr-1:Encounter", "corr-1:Condition" }, value.CacheKeys);
    }

    private static string Serialize<T>(T message) =>
        Encoding.UTF8.GetString(new JsonWithFhirMessageSerializer<T>().Serialize(message, SerializationContext.Empty));

    private static void AssertNoCacheTypeProperty(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToList();

        Assert.DoesNotContain(names, name => string.Equals(name, "CacheType", StringComparison.OrdinalIgnoreCase));
    }
}
