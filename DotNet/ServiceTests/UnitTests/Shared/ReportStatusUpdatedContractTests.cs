using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.Shared;

/// <summary>Verifies the same wire fixtures as Java/shared's ReportStatusUpdatedContractTest.</summary>
[Trait("Category", "UnitTests")]
public class ReportStatusUpdatedContractTests
{
    private static readonly SerializationContext Context = new(MessageComponentType.Value, "ReportStatusUpdated");

    [Fact]
    public void Key_SerializedBytesMatchJavaFixture_AndRoundTrip()
    {
        var key = new ReportStatusUpdatedKey
        {
            ReportId = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803",
            PatientId = "patient-123"
        };
        var context = new SerializationContext(MessageComponentType.Key, "ReportStatusUpdated");
        var bytes = new JsonWithFhirMessageSerializer<ReportStatusUpdatedKey>().Serialize(key, context);
        // Compare exact bytes, including property order, because Kafka hashes bytes rather than JSON values.
        Assert.Equal(Encoding.UTF8.GetBytes(File.ReadAllText(FixturePath("key")).Trim()), bytes);
        var consumed = new JsonWithFhirMessageDeserializer<ReportStatusUpdatedKey>().Deserialize(bytes, false, context);
        Assert.Equal(key.ReportId, consumed.ReportId);
        Assert.Equal(key.PatientId, consumed.PatientId);
    }

    [Theory]
    [InlineData("reportId")]
    [InlineData("patientId")]
    public void Key_MissingRequiredField_Throws(string property)
    {
        var json = JsonNode.Parse(File.ReadAllText(FixturePath("key")))!.AsObject();
        json.Remove(property);
        var context = new SerializationContext(MessageComponentType.Key, "ReportStatusUpdated");
        Assert.Throws<JsonException>(() => new JsonWithFhirMessageDeserializer<ReportStatusUpdatedKey>()
            .Deserialize(Encoding.UTF8.GetBytes(json.ToJsonString()), false, context));
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("not-reportable")]
    [InlineData("failure")]
    [InlineData("minimal")]
    public void DeserializeAndRepublish_PreservesContractAndOccurrenceTime(string fixture)
    {
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath(fixture)))!;
        var value = Consume(Encoding.UTF8.GetBytes(expected.ToJsonString()));

        Assert.Equal("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803", value.ReportScheduleId);
        Assert.Equal("patient-123", value.PatientId);
        Assert.Equal("684e43c1-415b-4d85-9a55-cc9905ddcb63", value.CorrelationId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-28T13:30:00Z"), value.Timestamp);
        Assert.Equal(TimeSpan.Zero, value.Timestamp.Offset);
        Assert.Equal(expected["isSuccess"]!.GetValue<bool>(), value.IsSuccess);

        var actual = JsonNode.Parse(Produce(value))!;
        // DateTimeOffset writes +00:00; Java writes Z. Compare the same UTC instant separately.
        Assert.Equal(value.Timestamp, DateTimeOffset.Parse(actual["timestamp"]!.GetValue<string>()));
        actual["timestamp"] = expected["timestamp"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(expected, actual), actual.ToJsonString());

        var retried = Consume(Produce(value));
        Assert.Equal(value.Timestamp, retried.Timestamp);
        if (fixture == "validation")
        {
            Assert.Equal(PatientReportingStatus.Validation, value.Status);
            Assert.Equal(3_000_000_000L, value.ProcessingTime);
            Assert.NotNull(value.ValidationResult);
            Assert.False(value.ValidationResult.Passed);
            Assert.Equal(4_000_000_000L, value.ValidationResult.MessageCount);
        }
        else if (fixture == "not-reportable")
        {
            Assert.False(value.Reportable);
            Assert.Equal(0L, value.ProcessingTime);
            Assert.Null(value.ValidationResult);
        }
        else if (fixture == "failure")
        {
            Assert.Equal("Acquisition failed for log 123.", value.ErrorMessage);
            Assert.Null(value.ProcessingTime);
        }
        else
        {
            Assert.Null(value.Reportable);
            Assert.Null(value.ValidationResult);
            Assert.Null(value.ErrorMessage);
            Assert.Null(value.ProcessingTime);
        }
    }

    [Fact]
    public void Serialize_ProducedValidationEvent_MatchesSharedContract()
    {
        var value = new ReportStatusUpdatedValue
        {
            ReportScheduleId = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803",
            PatientId = "patient-123",
            Status = PatientReportingStatus.Validation,
            IsSuccess = true,
            CorrelationId = "684e43c1-415b-4d85-9a55-cc9905ddcb63",
            ProcessingTime = 3_000_000_000L,
            ValidationResult = new() { Passed = false, MessageCount = 4_000_000_000L },
            Timestamp = DateTimeOffset.Parse("2026-10-28T13:30:00Z")
        };
        var json = JsonNode.Parse(Produce(value))!;
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath("validation")))!;
        json["timestamp"] = expected["timestamp"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(expected, json), json.ToJsonString());
    }

    [Theory]
    [InlineData("reportScheduleId")]
    [InlineData("patientId")]
    [InlineData("status")]
    [InlineData("isSuccess")]
    [InlineData("correlationId")]
    [InlineData("timestamp")]
    public void Deserialize_MissingRequiredField_Throws(string property)
    {
        var json = JsonNode.Parse(File.ReadAllText(FixturePath("minimal")))!.AsObject();
        json.Remove(property);
        Assert.Throws<JsonException>(() => Consume(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void StatusEnums_MatchSharedNamesAndSerializeAsStrings()
    {
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath("statuses")))!;
        Assert.Equal(expected["patientReportingStatuses"]!.AsArray().Select(x => x!.GetValue<string>()),
            Enum.GetNames<PatientReportingStatus>());
        Assert.Equal(expected["reportStatuses"]!.AsArray().Select(x => x!.GetValue<string>()),
            Enum.GetNames<ReportStatus>());
        foreach (var status in Enum.GetValues<PatientReportingStatus>())
        {
            var bytes = new JsonWithFhirMessageSerializer<PatientReportingStatus>().Serialize(status, Context);
            Assert.Equal($"\"{status}\"", Encoding.UTF8.GetString(bytes));
            Assert.Equal(status, JsonSerializer.Deserialize<PatientReportingStatus>(bytes));
        }
        foreach (var status in Enum.GetValues<ReportStatus>())
        {
            var json = JsonSerializer.Serialize(status);
            Assert.Equal($"\"{status}\"", json);
            Assert.Equal(status, JsonSerializer.Deserialize<ReportStatus>(json));
        }
    }

    [Fact]
    public void KafkaTopic_HasRetryAndErrorCompanions()
    {
        Assert.Equal("ReportStatusUpdated", KafkaTopic.ReportStatusUpdated.ToString());
        Assert.Equal("ReportStatusUpdated-Retry", KafkaTopic.ReportStatusUpdatedRetry.GetStringValue());
        Assert.Equal("ReportStatusUpdated-Error", KafkaTopic.ReportStatusUpdatedError.GetStringValue());
    }

    private static byte[] Produce(ReportStatusUpdatedValue value) =>
        new JsonWithFhirMessageSerializer<ReportStatusUpdatedValue>().Serialize(value, Context);

    private static ReportStatusUpdatedValue Consume(byte[] bytes) =>
        new JsonWithFhirMessageDeserializer<ReportStatusUpdatedValue>().Deserialize(bytes, false, Context);

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "ReportStatus", name + ".json");
}
