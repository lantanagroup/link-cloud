using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Settings;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class KafkaDeadLetterContractTests
{
    [Theory]
    [InlineData("ResourcesNormalized-Error", true)]
    [InlineData("ResourcesNormalized-Retry", true)]
    [InlineData("ResourceNormalized-Error", true)]
    [InlineData("ReadyForValidation-Error", false)]
    [InlineData("ResourcesNormalized", false)]
    public void IsResourcesNormalizedFailureTopic_matches_current_and_legacy_names(string topic, bool expected)
    {
        KafkaDeadLetterContract.IsResourcesNormalizedFailureTopic(topic).Should().Be(expected);
    }

    [Fact]
    public void Exception_payload_headers_include_dotnet_and_spring_kafka_names()
    {
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaConstants.HeaderConstants.ExceptionMessage).Should().BeTrue();
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaConstants.HeaderConstants.RetryExceptionMessage).Should().BeTrue();
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaDeadLetterContract.SpringKafkaHeaders.DltExceptionMessage).Should().BeTrue();
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaDeadLetterContract.SpringKafkaHeaders.DltExceptionStackTrace).Should().BeTrue();
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaDeadLetterContract.SpringKafkaHeaders.ExceptionMessage).Should().BeTrue();
        KafkaDeadLetterContract.IsExceptionPayloadHeader(KafkaConstants.HeaderConstants.ExceptionService).Should().BeFalse();
        KafkaDeadLetterContract.HeaderPreviewLength(KafkaConstants.HeaderConstants.ExceptionMessage, false)
            .Should().Be(KafkaDeadLetterContract.ExceptionHeaderPreviewLength);
    }

    [Fact]
    public void TryParseFacilityIdFromKey_reads_raw_string_and_json_resource_key()
    {
        KafkaDeadLetterContract.TryParseFacilityIdFromKey("fac-1").Should().Be("fac-1");
        KafkaDeadLetterContract.TryParseFacilityIdFromKey("{\"facilityId\":\"fac-2\",\"patientId\":\"p1\"}")
            .Should().Be("fac-2");
        KafkaDeadLetterContract.TryParseFacilityIdFromKey("{\"FacilityId\":\"fac-3\"}")
            .Should().Be("fac-3");
        KafkaDeadLetterContract.TryParseFacilityIdFromKey(null).Should().BeNull();
    }

    [Fact]
    public void MatchesFacility_uses_exception_facility_header_over_key()
    {
        var headers = new Dictionary<string, string>
        {
            [KafkaConstants.HeaderConstants.ExceptionFacilityId] = "fac-header"
        };

        KafkaDeadLetterContract.MatchesFacility("other-key", headers, "fac-header").Should().BeTrue();
        KafkaDeadLetterContract.MatchesFacility("other-key", headers, "fac-other").Should().BeFalse();
    }

    [Fact]
    public void MatchesFacility_accepts_json_resource_key_for_java_dead_letters()
    {
        var headers = new Dictionary<string, string>();
        var key = "{\"facilityId\":\"fac-json\",\"patientId\":\"patient-1\"}";

        KafkaDeadLetterContract.MatchesFacility(key, headers, "fac-json").Should().BeTrue();
        KafkaDeadLetterContract.MatchesFacility(key, headers, "other").Should().BeFalse();
        KafkaDeadLetterContract.MatchesFacility(null, headers, "fac-json").Should().BeTrue();
    }

    [Fact]
    public void TrySummarizeResourcesNormalized_reads_current_payload_and_key()
    {
        var key = "{\"facilityId\":\"fac-1\",\"patientId\":\"pat-9\"}";
        var value = """
            {
              "queryType": "Initial",
              "reportableEvent": "Discharge",
              "cacheType": "Redis",
              "cacheKey": "corr-1",
              "scheduledReports": [ { "reportTrackingId": "r1" }, { "reportTrackingId": "r2" } ]
            }
            """;

        var summary = KafkaDeadLetterContract.TrySummarizeResourcesNormalized(key, value);

        summary.Should().Contain("facilityId=fac-1");
        summary.Should().Contain("patientId=pat-9");
        summary.Should().Contain("queryType=Initial");
        summary.Should().Contain("cacheType=Redis");
        summary.Should().Contain("cacheKey=corr-1");
        summary.Should().Contain("scheduledReports=2");
        summary.Should().Contain("resourceType=(null)");
    }

    [Fact]
    public void ReadHeaders_decodes_utf8_dotnet_and_spring_values()
    {
        var headers = new Headers
        {
            { KafkaConstants.HeaderConstants.ExceptionMessage, Encoding.UTF8.GetBytes("dotnet boom") },
            { KafkaDeadLetterContract.SpringKafkaHeaders.DltExceptionStackTrace, Encoding.UTF8.GetBytes("at Foo.Bar()") }
        };

        var map = KafkaDeadLetterContract.ReadHeaders(headers);
        map[KafkaConstants.HeaderConstants.ExceptionMessage].Should().Be("dotnet boom");
        map[KafkaDeadLetterContract.SpringKafkaHeaders.DltExceptionStackTrace].Should().Be("at Foo.Bar()");
    }
}
