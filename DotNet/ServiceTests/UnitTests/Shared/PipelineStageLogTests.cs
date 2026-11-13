using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;

namespace UnitTests.Shared;

/// <summary>
/// Covers the per-patient pipeline stage line: its fields, its level by outcome, the Kafka wait taken from
/// the message timestamp, and the log scope that carries the ids for the stage's other lines.
/// </summary>
[Trait("Category", "UnitTests")]
public class PipelineStageLogTests
{
    [Fact]
    public void Completed_WritesOneInformationLineWithEveryField()
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "normalization", "facility-1", "patient-1", "corr-1",
                   ["report-1", "report-2", "report-1", null, " "], "Initial"))
        {
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(PipelineStageLog.MessageTemplate, entry.Template);
        Assert.Equal("normalization", entry.Values["PipelineStage"]);
        Assert.Equal("completed", entry.Values["PipelineOutcome"]);
        Assert.Equal("facility-1", entry.Values["FacilityId"]);
        Assert.Equal("patient-1", entry.Values["PatientId"]);
        Assert.Equal("corr-1", entry.Values["CorrelationId"]);
        Assert.Equal("report-1,report-2", entry.Values["ReportIds"]);
        Assert.Equal("initial", entry.Values["Phase"]);
        Assert.Equal("-", entry.Values["WaitedMs"]);
    }

    [Fact]
    public void NoOutcome_IsLoggedAsFailedWarning()
    {
        var logger = new CapturingLogger();

        using (PipelineStageLog.Start(logger, "aggregation", "f", "p", "c", null))
        {
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("failed", entry.Values["PipelineOutcome"]);
        Assert.Equal("-", entry.Values["ReportIds"]);
    }

    [Theory]
    [InlineData(PipelineStageOutcome.Retried, "retried", LogLevel.Warning)]
    [InlineData(PipelineStageOutcome.DeadLettered, "dead-lettered", LogLevel.Warning)]
    [InlineData(PipelineStageOutcome.Skipped, "skipped", LogLevel.Information)]
    [InlineData(PipelineStageOutcome.Cancelled, "cancelled", LogLevel.Information)]
    public void Outcome_SetsNameAndLevel(PipelineStageOutcome outcome, string name, LogLevel level)
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "submission", "f", "p", "c", null))
        {
            stage.SetOutcome(outcome);
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(level, entry.Level);
        Assert.Equal(name, entry.Values["PipelineOutcome"]);
    }

    [Fact]
    public void KafkaMessage_WaitIsTimeSinceItWasProduced()
    {
        var logger = new CapturingLogger();
        var message = new ConsumeResult<string, string>
        {
            Message = new Message<string, string>
            {
                Key = "k",
                Value = "v",
                Timestamp = new Confluent.Kafka.Timestamp(DateTime.UtcNow.AddSeconds(-30), TimestampType.CreateTime)
            }
        };

        using (var stage = PipelineStageLog.Start(logger, "submission", message, "f", "p", "c", null))
        {
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        var waited = long.Parse((string)Assert.Single(logger.Entries).Values["WaitedMs"]!);
        Assert.InRange(waited, 29_000, 60_000);
    }

    [Fact]
    public void TookMsOverride_ReplacesTheMeasuredDuration()
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "acquisition", "f", "p", "c", null))
        {
            stage.TookMsOverride = 12345;
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        Assert.Equal("12345", Assert.Single(logger.Entries).Values["TookMs"]);
    }

    [Fact]
    public void Scope_CarriesTheIdsWhileTheStageIsOpen()
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "normalization", "f", "p", "c", ["r"]))
        {
            var scope = Assert.Single(logger.OpenScopes);
            Assert.Equal("f", scope["FacilityId"]);
            Assert.Equal("p", scope["PatientId"]);
            Assert.Equal("c", scope["CorrelationId"]);
            Assert.Equal("r", scope["ReportIds"]);
            Assert.Equal("normalization", scope["PipelineStage"]);
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        Assert.Empty(logger.OpenScopes);
    }

    [Theory]
    [InlineData("Initial", "initial")]
    [InlineData("INITIAL", "initial")]
    [InlineData("SUPPLEMENTAL", "supplemental")]
    [InlineData("NHSNAcuteCareHospitalMonthlyInitialPopulation", "NHSNAcuteCareHospitalMonthlyInitialPopulation")]
    [InlineData(null, "-")]
    public void Phase_QueryPhasesAreLowerCasedAndOthersKept(string? phase, string expected)
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "evaluation", "f", "p", "c", null, phase))
        {
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        Assert.Equal(expected, Assert.Single(logger.Entries).Values["Phase"]);
    }

    [Theory]
    [InlineData("Acquisition", "Initial", "InitialAcquisition")]
    [InlineData("Normalization", "SUPPLEMENTAL", "SupplementalNormalization")]
    [InlineData("Evaluation", "initial", "InitialEvaluation")]
    [InlineData("Submission", "MeasureReportSubmissionEntry", "Submission")]
    [InlineData("Validation", null, "Validation")]
    public void PhasedStage_FoldsTheQueryPhaseIntoTheName(string stage, string? phase, string expected)
    {
        Assert.Equal(expected, PipelineStageLog.PhasedStage(stage, phase));
    }

    [Fact]
    public void Template_RendersValuesLiterally()
    {
        Assert.DoesNotContain("{FacilityId}", PipelineStageLog.MessageTemplate);
        Assert.Contains("{FacilityId:l}", PipelineStageLog.MessageTemplate);
    }

    [Fact]
    public void ControlCharacters_CannotForgeALine()
    {
        var logger = new CapturingLogger();

        using (var stage = PipelineStageLog.Start(logger, "normalization", "f\r\nFAKE", "p", "c", null))
        {
            stage.Outcome = PipelineStageOutcome.Completed;
        }

        Assert.Equal("f  FAKE", Assert.Single(logger.Entries).Values["FacilityId"]);
    }

    [Fact]
    public void Header_ReadsTheLastValue()
    {
        var headers = new Headers
        {
            { "X-Correlation-Id", "first"u8.ToArray() },
            { "X-Correlation-Id", "second"u8.ToArray() }
        };

        Assert.Equal("second", PipelineStageLog.Header(headers, "X-Correlation-Id"));
        Assert.Null(PipelineStageLog.Header(headers, "missing"));
        Assert.Null(PipelineStageLog.Header(null, "X-Correlation-Id"));
    }

    private sealed record Entry(LogLevel Level, string Template, IReadOnlyDictionary<string, object?> Values);

    private sealed class CapturingLogger : ILogger
    {
        public List<Entry> Entries { get; } = [];
        public List<IReadOnlyDictionary<string, object>> OpenScopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var values = (IReadOnlyDictionary<string, object>)(object)state;
            OpenScopes.Add(values);
            return new ScopeHandle(() => OpenScopes.Remove(values));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var pairs = (IReadOnlyList<KeyValuePair<string, object?>>)state!;
            var values = pairs.ToDictionary(p => p.Key, p => p.Value);
            Entries.Add(new Entry(logLevel, (string)values["{OriginalFormat}"]!, values));
        }

        private sealed class ScopeHandle(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }
}
