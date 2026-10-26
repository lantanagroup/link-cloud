using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.Shared;

/// <summary>
/// Checks the retention messages against the JSON fixtures shared with the Java tests
/// (RetentionMessageContractTest in Java/shared). The requests are what Report produces and the Java
/// participants read; the completions are what the Java participants produce and Report reads.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionMessageContractTests
{
    private const string CleanupId = "6f1c2a9e-4b7d-4c1e-9a53-2d8e7f0b1c34";
    private const string FirstReportId = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803";
    private const string SecondReportId = "c3d94a17-5e2b-4f60-8d1a-9b7e2f4c6a05";

    [Fact]
    public void Serialize_ScheduledRequest_MatchesSharedFixture()
    {
        var request = new RetentionCheckRequestedValue
        {
            RetentionCleanupId = CleanupId,
            InitiatedBy = RetentionInitiatedBy.Scheduled,
            InitiatedDateTime = Utc("2026-10-01T06:00:00Z"),
            Mode = RetentionMode.Scrub,
            CutoffDateTime = Utc("2025-10-01T06:00:00Z"),
            Reports =
            [
                new RetentionReport
                {
                    FacilityId = "Hospital1",
                    ReportScheduleId = FirstReportId,
                    RetentionWindow = new RetentionWindow { Unit = RetentionWindowUnit.Months, Count = 12 },
                    CutoffDateTime = Utc("2025-10-01T06:00:00Z")
                },
                new RetentionReport
                {
                    FacilityId = "Hospital2",
                    ReportScheduleId = SecondReportId,
                    RetentionWindow = new RetentionWindow { Unit = RetentionWindowUnit.Days, Count = 400 },
                    CutoffDateTime = Utc("2025-08-27T06:00:00Z")
                }
            ],
            Participants = ["MeasureEval", "Validation"]
        };

        AssertMatchesFixture("retention-check-requested-scheduled.json", Produce(request));
    }

    [Fact]
    public void Serialize_SingleReportRequest_OmitsAbsentFields()
    {
        var request = new RetentionCheckRequestedValue
        {
            RetentionCleanupId = "a8e5d2c1-7f3b-4a96-8e0d-5c2b1f9a7e46",
            InitiatedBy = RetentionInitiatedBy.SingleReport,
            InitiatedDateTime = Utc("2026-10-22T15:30:00Z"),
            Mode = RetentionMode.Purge,
            CutoffDateTime = Utc("2026-10-22T15:30:00Z"),
            Reports =
            [
                new RetentionReport
                {
                    FacilityId = "Hospital1",
                    ReportScheduleId = FirstReportId,
                    CutoffDateTime = Utc("2026-10-22T15:30:00Z")
                }
            ]
        };

        AssertMatchesFixture("retention-check-requested-single-report.json", Produce(request));
    }

    [Fact]
    public void Deserialize_JavaCompletionWithReports_ReadsEveryField()
    {
        var completion = Consume<RetentionCheckCompleteValue>("retention-check-complete-reports.json");

        Assert.Equal(CleanupId, completion.RetentionCleanupId);
        Assert.Equal("MeasureEval", completion.ServiceName);
        Assert.Equal(Utc("2026-10-01T06:12:30Z"), completion.CompletedDateTime);
        Assert.Null(completion.Summary);
        Assert.NotNull(completion.Reports);
        Assert.Equal(2, completion.Reports.Count);

        var completed = completion.Reports[0];
        Assert.Equal(FirstReportId, completed.ReportScheduleId);
        Assert.Equal(RetentionResultStatus.Completed, completed.Status);
        Assert.Equal(3_000_000_000L, completed.RecordsAffected);
        Assert.Null(completed.ErrorMessage);
        Assert.Equal(Utc("2026-10-01T06:10:05Z"), completed.CompletedDateTime);

        var failed = completion.Reports[1];
        Assert.Equal(SecondReportId, failed.ReportScheduleId);
        Assert.Equal(RetentionResultStatus.Failed, failed.Status);
        Assert.Equal(5000, failed.RecordsAffected);
        Assert.Equal("Deleting the report's resources failed: MongoTimeoutException", failed.ErrorMessage);
        Assert.Equal(Utc("2026-10-01T06:12:30Z"), failed.CompletedDateTime);
    }

    [Fact]
    public void Deserialize_JavaCompletionWithSummary_ReadsSummaryWithoutReportId()
    {
        var completion = Consume<RetentionCheckCompleteValue>("retention-check-complete-summary.json");

        Assert.Equal(CleanupId, completion.RetentionCleanupId);
        Assert.Equal("Census", completion.ServiceName);
        Assert.Null(completion.Reports);
        Assert.NotNull(completion.Summary);
        Assert.Null(completion.Summary.ReportScheduleId);
        Assert.Equal(RetentionResultStatus.Completed, completion.Summary.Status);
        Assert.Equal(0, completion.Summary.RecordsAffected);
        Assert.Equal(Utc("2026-10-01T06:01:45Z"), completion.Summary.CompletedDateTime);
    }

    [Fact]
    public void Deserialize_CompletionMissingServiceName_Throws()
    {
        var json = """
                   {
                     "retentionCleanupId": "6f1c2a9e-4b7d-4c1e-9a53-2d8e7f0b1c34",
                     "completedDateTime": "2026-10-01T06:12:30Z",
                     "reports": []
                   }
                   """;
        var context = new SerializationContext(MessageComponentType.Value, "test");
        var deserializer = new JsonWithFhirMessageDeserializer<RetentionCheckCompleteValue>();

        Assert.Throws<JsonException>(() => deserializer.Deserialize(Encoding.UTF8.GetBytes(json), false, context));
    }

    [Fact]
    public void KafkaTopic_RetentionTopics_HaveRetryAndErrorCompanions()
    {
        Assert.Equal("RetentionCheckRequested", KafkaTopic.RetentionCheckRequested.ToString());
        Assert.Equal("RetentionCheckRequested-Retry", KafkaTopic.RetentionCheckRequestedRetry.GetStringValue());
        Assert.Equal("RetentionCheckRequested-Error", KafkaTopic.RetentionCheckRequestedError.GetStringValue());
        Assert.Equal("RetentionCheckComplete", KafkaTopic.RetentionCheckComplete.ToString());
        Assert.Equal("RetentionCheckComplete-Retry", KafkaTopic.RetentionCheckCompleteRetry.GetStringValue());
        Assert.Equal("RetentionCheckComplete-Error", KafkaTopic.RetentionCheckCompleteError.GetStringValue());
    }

    private static DateTimeOffset Utc(string value)
    {
        return DateTimeOffset.Parse(value).ToUniversalTime();
    }

    private static byte[] Produce<T>(T value)
    {
        var context = new SerializationContext(MessageComponentType.Value, "test");
        return new JsonWithFhirMessageSerializer<T>().Serialize(value, context);
    }

    private static T Consume<T>(string fixture)
    {
        var bytes = File.ReadAllBytes(FixturePath(fixture));
        var context = new SerializationContext(MessageComponentType.Value, "test");
        var value = new JsonWithFhirMessageDeserializer<T>().Deserialize(bytes, false, context);
        Assert.NotNull(value);
        return value;
    }

    private static void AssertMatchesFixture(string fixture, byte[] produced)
    {
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath(fixture)));
        var actual = JsonNode.Parse(Encoding.UTF8.GetString(produced));

        Assert.True(JsonNode.DeepEquals(expected, actual),
                    $"Produced JSON does not match {fixture}.{Environment.NewLine}" +
                    $"Expected: {expected?.ToJsonString()}{Environment.NewLine}" +
                    $"Actual:   {actual?.ToJsonString()}");
    }

    private static string FixturePath(string fixture)
    {
        return Path.Combine(AppContext.BaseDirectory, "Resources", "Retention", fixture);
    }
}
