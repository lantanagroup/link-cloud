using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class LokiLogLineParserTests
{
    [Fact]
    public void FormatLogLine_reads_grafana_loki_exception_string()
    {
        var json = """
            {"Message":"Failed to process event. FacilityId: fac-1 (Context: None)","level":"error","Exception":"LantanaGroup.Link.Shared.Application.Error.Exceptions.DeadLetterException: boom\n   at Listener.Handle()"}
            """;

        var formatted = LokiLogLineParser.FormatLogLine(json);

        formatted.Should().StartWith("ERROR | Failed to process event");
        formatted.Should().Contain("DeadLetterException");
        formatted.Should().Contain("|||");
        formatted.Should().Contain("Stack Trace:");
        formatted.Should().Contain("at Listener.Handle()");
    }

    [Fact]
    public void FormatLogLine_reads_serilog_exceptions_exception_detail_object()
    {
        var json = """
            {
              "Message":"Normalization failed",
              "level":"error",
              "ExceptionDetail": {
                "Type":"System.InvalidOperationException",
                "Message":"cache miss",
                "StackTrace":"at Norm.Run()",
                "InnerException": { "Type":"System.TimeoutException", "Message":"redis" }
              }
            }
            """;

        var formatted = LokiLogLineParser.FormatLogLine(json);

        formatted.Should().Contain("ERROR | Normalization failed");
        formatted.Should().Contain("System.InvalidOperationException: cache miss");
        formatted.Should().Contain("at Norm.Run()");
        formatted.Should().Contain("TimeoutException");
    }

    [Fact]
    public void FormatLogLine_leaves_java_logback_text_intact()
    {
        var line = "[http-nio-8080-exec-1] ERROR  c.l.l.m.s.EvaluateMeasureService    Measure evaluation failed [measure=m, patient=p1, facility=fac-1, correlationId=c]: boom";

        LokiLogLineParser.FormatLogLine(line).Should().Be(line);
    }

    [Fact]
    public void IsErrorLike_accepts_dotnet_failed_to_process_and_java_error_level()
    {
        LokiLogLineParser.IsErrorLike("ERROR | Failed to process event. FacilityId: fac-1").Should().BeTrue();
        LokiLogLineParser.IsErrorLike("[thread] ERROR  c.l.l.m.s.EvaluateMeasureService    Measure evaluation failed [facility=fac-1]: boom")
            .Should().BeTrue();
        LokiLogLineParser.IsErrorLike("{\"level\":\"error\",\"Message\":\"Failed to process event\"}").Should().BeTrue();
        LokiLogLineParser.IsErrorLike("[DIAG] pipeline progress identified=3").Should().BeFalse();
        LokiLogLineParser.IsErrorLike("ERROR=140 WARNING=2").Should().BeFalse();
    }
}
