using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Automation.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class OperationFailureFactoryTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 11, 5, 14, 30, 0, TimeSpan.Zero);

    private const string OperationOutcome = """
        {
          "resourceType": "OperationOutcome",
          "issue": [
            {
              "severity": "error",
              "code": "exception",
              "diagnostics": "JpaSystemException: could not extract ResultSet"
            }
          ]
        }
        """;

    [Fact]
    public void Patient_chunk_summary_names_the_step_chunk_and_status()
    {
        var failure = OperationFailureFactory.FromHttp(Sample(
            name: "patient-1_chunk01",
            progressPrefix: "[patient-1] ",
            statusCode: 500,
            body: OperationOutcome));

        failure.Step.Should().Be("Upload patient chunk");
        failure.PatientOrChunk.Should().Be("patient-1_chunk01");
        failure.OneLineSummary.Should().Be("Patient chunk failed: patient-1_chunk01 (HTTP 500)");
        failure.FormattedBlock.Should().Contain("Step: Upload patient chunk");
        failure.FormattedBlock.Should().Contain("Patient or chunk: patient-1_chunk01");
        failure.Details.Should().Be("JpaSystemException: could not extract ResultSet");
        failure.UnderlyingCause.Should().Be(OperationFailureText.UnderlyingCauseNotInResponse);
        failure.TechnicalDetails.Should().Contain("could not extract ResultSet");
        failure.FormattedBlock.Should().Contain("Technical details:");
        failure.FormattedBlock.Should().Contain(failure.TechnicalDetails);
        failure.SearchGuidance.Should().Contain("same log UI");
        failure.SearchGuidance.Should().Contain("run ID");
        failure.SearchGuidance.Should().Contain("trace ID");
        failure.FormattedBlock.Should().NotContain("\r\n");
    }

    [Theory]
    [InlineData("[shared] ", "shared_chunk01", "Upload shared infrastructure", "Shared infrastructure upload failed: shared_chunk01 (HTTP 500)")]
    [InlineData("[imported:patient-9] ", "patient-9_chunk01", "Upload imported patient", "Imported patient upload failed: patient-9_chunk01 (HTTP 500)")]
    [InlineData("[replace:patient-9] ", "patient-9_chunk01", "Replace patient bundle", "Patient replacement failed: patient-9_chunk01 (HTTP 500)")]
    public void Summary_follows_the_upload_step(string prefix, string name, string step, string summary)
    {
        var failure = OperationFailureFactory.FromHttp(Sample(name, prefix, 500, OperationOutcome));

        failure.Step.Should().Be(step);
        failure.OneLineSummary.Should().Be(summary);
    }

    [Fact]
    public void Missing_identifiers_are_marked_not_provided()
    {
        var failure = OperationFailureFactory.FromHttp(new OperationFailureInput
        {
            Name = "patient-1_chunk01",
            ProgressPrefix = "[patient-1] ",
            Method = "POST",
            Endpoint = "http://fhir.example/fhir",
            StatusCode = 500,
            ResponseBody = OperationOutcome,
            TimestampUtc = FixedTime
        });

        failure.RunId.Should().Be(OperationFailureText.NotProvided);
        failure.CorrelationId.Should().Be(OperationFailureText.NotProvided);
        failure.TraceId.Should().Be(OperationFailureText.NotProvided);
        failure.RequestId.Should().Be(OperationFailureText.NotProvided);
        failure.FacilityId.Should().BeNull();
        failure.FormattedBlock.Should().Contain("Correlation ID: Not provided");
        failure.FormattedBlock.Should().Contain("Trace ID: Not provided");
        failure.FormattedBlock.Should().Contain("Request ID: Not provided");
        failure.FormattedBlock.Should().Contain("Run ID: Not provided");
        failure.FormattedBlock.Should().NotContain("Facility ID:");
        failure.FormattedBlock.Should().NotContain("00000000-0000-0000-0000-000000000000");
    }

    [Fact]
    public void Missing_http_status_is_marked_not_provided()
    {
        var failure = OperationFailureFactory.FromHttp(new OperationFailureInput
        {
            Name = "patient-1_chunk01",
            ProgressPrefix = "[patient-1] ",
            Method = "POST",
            Endpoint = "http://fhir.example/fhir",
            StatusCode = null,
            ResponseBody = null,
            TimestampUtc = FixedTime,
            RespondingSystem = "FHIR server"
        });

        failure.OneLineSummary.Should().Be("Patient chunk failed: patient-1_chunk01 (HTTP Not provided)");
        failure.FormattedBlock.Should().Contain("HTTP status: Not provided");
        failure.TechnicalDetails.Should().Be(OperationFailureText.NotProvided);
    }

    [Fact]
    public void Trace_id_prefers_traceparent_then_header_then_the_id_that_was_sent()
    {
        var fromParent = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            traceParent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            traceHeader: "from-header",
            sentTraceId: "from-activity"));
        fromParent.TraceId.Should().Be("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        var fromHeader = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            traceHeader: "from-header",
            sentTraceId: "from-activity"));
        fromHeader.TraceId.Should().Be("from-header");

        var fromSent = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            sentTraceId: "from-activity"));
        fromSent.TraceId.Should().Be("from-activity");
    }

    [Theory]
    [InlineData("not-a-trace")]
    [InlineData("garbage-not-a-trace-id")]
    [InlineData("00-short-bbbbbbbbbbbbbbbb-01")]
    [InlineData("ff-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01")]
    [InlineData("FF-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01")]
    [InlineData("00-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-bbbbbbbbbbbbbbbb-01")]
    [InlineData("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01-extra")]
    [InlineData("00-00000000000000000000000000000000-bbbbbbbbbbbbbbbb-01")]
    [InlineData("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-0000000000000000-01")]
    [InlineData("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-0g")]
    [InlineData("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-BBBBBBBBBBBBBBBB-01")]
    [InlineData("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-0A")]
    [InlineData("0A-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01")]
    public void Malformed_or_all_zero_traceparent_falls_back_to_the_response_trace_header(string traceParent)
    {
        var failure = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            traceParent: traceParent,
            traceHeader: "from-header",
            sentTraceId: "from-activity"));

        failure.TraceId.Should().Be("from-header");
    }

    [Fact]
    public void Invalid_traceparent_falls_back_to_the_sent_trace_id_when_the_header_is_absent()
    {
        var failure = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            traceParent: "00-00000000000000000000000000000000-bbbbbbbbbbbbbbbb-01",
            sentTraceId: "from-activity"));

        failure.TraceId.Should().Be("from-activity");
    }

    [Fact]
    public void Sanitizer_strips_userinfo_bearer_tokens_and_secret_query_values()
    {
        var body = "{\"resourceType\":\"OperationOutcome\",\"issue\":[{\"diagnostics\":\"see Authorization: Bearer secret-token and access_token Bearer secret-token \"}],\"password\":\"hunter2\",\"client_secret\":\"s3cret\"}"; // trufflehog:ignore
        var failure = OperationFailureFactory.FromHttp(new OperationFailureInput
        {
            Name = "patient-1_chunk01",
            ProgressPrefix = "[patient-1] ",
            Method = "POST",
            Endpoint = "https://user:pass@fhir.example/fhir?token=abc&access_token=def&code=ghi&patient=1", // trufflehog:ignore
            StatusCode = 500,
            ResponseBody = body,
            CorrelationId = "corr-1",
            RequestId = "req-1",
            RunId = "run-1",
            TimestampUtc = FixedTime
        });

        failure.SanitizedEndpoint.Should().NotContain("user:pass"); // trufflehog:ignore
        failure.SanitizedEndpoint.Should().NotContain("pass@");
        failure.SanitizedEndpoint.Should().Contain("token=[redacted]");
        failure.SanitizedEndpoint.Should().Contain("access_token=[redacted]");
        failure.SanitizedEndpoint.Should().Contain("code=[redacted]");
        failure.SanitizedEndpoint.Should().Contain("patient=1");
        failure.TechnicalDetails.Should().NotContain("secret-token"); // trufflehog:ignore
        failure.TechnicalDetails.Should().NotContain("hunter2"); // trufflehog:ignore
        failure.TechnicalDetails.Should().NotContain("s3cret"); // trufflehog:ignore
        failure.TechnicalDetails.Should().Contain("Bearer [redacted]");
        failure.TechnicalDetails.Should().Contain("\"password\":\"[redacted]\"");
        failure.Details.Should().Be("see Authorization: [redacted] and access_token Bearer [redacted]");
        failure.Details.Should().NotContain("secret-token"); // trufflehog:ignore
        failure.FormattedBlock.Should().NotContain("patient-1_chunk01\":");
    }

    [Fact]
    public void Http_500_operation_outcome_keeps_diagnostics_and_does_not_invent_a_database_cause()
    {
        var failure = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            reason: "InternalServerError",
            runId: "run-1",
            correlationId: "corr-1"));

        failure.Details.Should().Be("JpaSystemException: could not extract ResultSet");
        failure.UnderlyingCause.Should().Be("Not provided in the response.");
        failure.TechnicalDetails.Should().Contain("\"resourceType\": \"OperationOutcome\"");
        failure.OneLineSummary.Should().Be("Patient chunk failed: patient-1_chunk01 (HTTP 500)");
        failure.RespondingSystem.Should().Be("FHIR server");
        failure.FormattedBlock.Should().Contain("HTTP status: 500 InternalServerError");
        failure.FormattedBlock.Should().Contain("Correlation ID: corr-1");
        failure.FormattedBlock.Should().Contain("Timestamp (UTC): 2026-11-05T14:30:00.0000000Z");
    }

    [Fact]
    public void Deeper_sql_exception_text_is_the_underlying_cause()
    {
        const string body = """
            {
              "resourceType": "OperationOutcome",
              "issue": [{
                "diagnostics": "JpaSystemException: could not extract ResultSet; SQLException: relation \"patient\" does not exist"
              }]
            }
            """;

        var failure = OperationFailureFactory.FromHttp(Sample("patient-1_chunk01", "[patient-1] ", 500, body));

        failure.UnderlyingCause.Should().Be("relation \"patient\" does not exist");
    }

    [Fact]
    public void Deeper_sql_exception_redacts_a_secret_before_the_cause_is_capped()
    {
        var password = new string('p', 520);
        var diagnostics = "JpaSystemException: could not extract ResultSet; PSQLException: Connection failed {\"password\":\"" // trufflehog:ignore
            + password
            + "\"}";
        var body = "{\"resourceType\":\"OperationOutcome\",\"issue\":[{\"diagnostics\":"
            + JsonSerializer.Serialize(diagnostics)
            + "}]}";

        var failure = OperationFailureFactory.FromHttp(Sample("patient-1_chunk01", "[patient-1] ", 500, body));

        failure.UnderlyingCause.Should().Be("Connection failed {\"password\":\"[redacted]\"}"); // trufflehog:ignore
        failure.UnderlyingCause.Should().NotContain(password);
    }

    [Fact]
    public void Non_operation_outcome_body_stays_in_technical_details_only()
    {
        const string body = "gateway timed out Bearer abc.def.ghi"; // trufflehog:ignore
        var failure = OperationFailureFactory.FromHttp(Sample("patient-1_chunk01", "[patient-1] ", 502, body, reason: "Bad Gateway"));

        failure.Details.Should().Be("The server returned HTTP 502 Bad Gateway.");
        failure.Details.Should().NotContain("gateway timed out");
        failure.UnderlyingCause.Should().Be(OperationFailureText.NotProvided);
        failure.TechnicalDetails.Should().Contain("gateway timed out");
        failure.TechnicalDetails.Should().Contain("Bearer [redacted]");
        failure.TechnicalDetails.Should().NotContain("abc.def.ghi");
    }

    [Fact]
    public void Empty_body_is_not_provided_in_technical_details()
    {
        var failure = OperationFailureFactory.FromHttp(Sample("patient-1_chunk01", "[patient-1] ", 500, "  "));

        failure.TechnicalDetails.Should().Be(OperationFailureText.NotProvided);
        failure.Details.Should().Be("The server returned HTTP 500 with an empty body.");
    }

    [Fact]
    public void Technical_details_are_capped()
    {
        var failure = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            new string('x', 4500)));

        failure.TechnicalDetails.Should().EndWith("\n[truncated]");
        failure.TechnicalDetails.Length.Should().BeLessThan(4500);
    }

    [Fact]
    public void Collect_unwraps_aggregate_exceptions_and_ignores_other_errors()
    {
        var failure = OperationFailureFactory.FromHttp(Sample("patient-1_chunk01", "[patient-1] ", 500, OperationOutcome));
        var wrapped = new AggregateException(
            new InvalidOperationException("outer", new OperationFailedException(failure)),
            new InvalidOperationException("unrelated"));

        var collected = OperationFailedException.Collect(wrapped);

        collected.Should().ContainSingle().Which.OneLineSummary.Should().Be(failure.OneLineSummary);
        OperationFailedException.Collect(new InvalidOperationException("nope")).Should().BeEmpty();
    }

    [Fact]
    public void Debug_flatten_removes_line_breaks_and_the_warning_has_no_body()
    {
        OperationFailureLog.FlattenForLog("line1\nline2\r\nBearer").Should().Be("line1 line2  Bearer");

        var provider = new ListLogger();
        var failure = OperationFailureFactory.FromHttp(Sample(
            "patient-1_chunk01",
            "[patient-1] ",
            500,
            OperationOutcome,
            runId: "run-1"));
        OperationFailureLog.Write(provider, failure);

        provider.Entries.Should().ContainSingle();
        provider.Entries[0].Level.Should().Be(LogLevel.Warning);
        provider.Entries[0].Message.Should().Contain("Patient chunk failed");
        provider.Entries[0].Message.Should().Contain("run-1");
        provider.Entries[0].Message.Should().NotContain("JpaSystemException");
        provider.Entries[0].Exception.Should().BeNull();
    }

    private static OperationFailureInput Sample(
        string name,
        string progressPrefix,
        int statusCode,
        string? body,
        string? reason = null,
        string? traceParent = null,
        string? traceHeader = null,
        string? sentTraceId = null,
        string? runId = null,
        string? correlationId = null) => new()
    {
        Name = name,
        ProgressPrefix = progressPrefix,
        Method = "POST",
        Endpoint = "http://fhir.example/fhir",
        StatusCode = statusCode,
        ReasonPhrase = reason,
        ResponseBody = body,
        TraceParent = traceParent,
        TraceIdHeader = traceHeader,
        SentTraceId = sentTraceId,
        RunId = runId,
        CorrelationId = correlationId,
        RespondingSystem = "FHIR server",
        TimestampUtc = FixedTime
    };

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
