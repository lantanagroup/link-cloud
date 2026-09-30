using System.Net;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class LokiScraperFailureTests
{
    [Fact]
    public void Http_timeout_is_a_missed_scrape()
    {
        using var cts = new CancellationTokenSource();

        LokiScraper.ClassifyScrapeFailure(new TaskCanceledException(), cts.Token)
            .Should().Be(LokiScraper.ScrapeFailure.MissedScrape);
    }

    [Fact]
    public void Caller_cancel_is_rethrown()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        LokiScraper.ClassifyScrapeFailure(new TaskCanceledException(), cts.Token)
            .Should().Be(LokiScraper.ScrapeFailure.Rethrow);
    }

    [Fact]
    public void Other_scrape_failures_keep_pages_already_read()
    {
        LokiScraper.ClassifyScrapeFailure(new HttpRequestException("reset"), CancellationToken.None)
            .Should().Be(LokiScraper.ScrapeFailure.KeepPartial);
    }

    [Fact]
    public void Shutdown_cycle_does_not_start_a_validation_scrape()
    {
        ProgressMonitor.ShouldScrapeValidationActivity(CancellationToken.None).Should().BeFalse();

        using var cts = new CancellationTokenSource();
        ProgressMonitor.ShouldScrapeValidationActivity(cts.Token).Should().BeTrue();

        cts.Cancel();
        ProgressMonitor.ShouldScrapeValidationActivity(cts.Token).Should().BeFalse();
    }

    [Fact]
    public void Cancelled_token_does_not_start_the_later_activity_scrapes()
    {
        using var cts = new CancellationTokenSource();
        ProgressMonitor.ShouldContinueActivityScrapes(cts.Token).Should().BeTrue();
        ProgressMonitor.ShouldContinueActivityScrapes(CancellationToken.None).Should().BeTrue();

        cts.Cancel();
        ProgressMonitor.ShouldContinueActivityScrapes(cts.Token).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelled_validation_scrape_returns_without_waiting_for_http_timeout()
    {
        var handler = new HoldUntilCancelHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://loki.test"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var scraper = new LokiScraper(
            client,
            new NullOutput(),
            new AutomationConfig { LokiBaseUrl = "http://loki.test", LokiAppLabel = "link" });
        using var cts = new CancellationTokenSource();

        var task = scraper.GetValidationActivitySummaryAsync(TimeSpan.FromSeconds(60), "facility", "report", cts.Token);
        var started = await Task.WhenAny(handler.Started.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        started.Should().Be(handler.Started.Task);

        cts.Cancel();
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
        finished.Should().Be(task);
        (await task).Should().BeNull();
    }

    [Fact]
    public async Task Cancelled_exception_line_query_rethrows_without_waiting_for_http_timeout()
    {
        var handler = new HoldUntilCancelHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://loki.test"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        var scraper = new LokiScraper(
            client,
            new NullOutput(),
            new AutomationConfig { LokiBaseUrl = "http://loki.test", LokiAppLabel = "link" });
        using var cts = new CancellationTokenSource();

        var task = scraper.GetServiceExceptionLinesAsync(
            LokiScraper.Components.Validation,
            TimeSpan.FromMinutes(5),
            facilityId: "facility",
            reportId: "report",
            cancellationToken: cts.Token);
        var started = await Task.WhenAny(handler.Started.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        started.Should().Be(handler.Started.Task);

        cts.Cancel();
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
        finished.Should().Be(task);
        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Validation_sample_uses_the_newest_matching_log_time()
    {
        const long olderNs = 1_780_000_000_000_000_000;
        const long newestNs = 1_780_000_105_000_000_000;
        const long otherRunNs = 1_780_000_200_000_000_000;
        var body = $$"""
            {
              "status": "success",
              "data": {
                "resultType": "streams",
                "result": [
                  {
                    "values": [
                      ["{{olderNs}}", "facility-1 report-9 validation still in progress: categorize (elapsed 40s)"],
                      ["{{otherRunNs}}", "elsewhere else-id validation still in progress: ignore (elapsed 1s)"],
                      ["{{newestNs}}", "facility-1 report-9 validation still in progress: categorize (elapsed 40s)"]
                    ]
                  }
                ]
              }
            }
            """;
        using var client = new HttpClient(new JsonHandler(body))
        {
            BaseAddress = new Uri("http://loki.test")
        };
        var scraper = new LokiScraper(
            client,
            new NullOutput(),
            new AutomationConfig { LokiBaseUrl = "http://loki.test", LokiAppLabel = "link" });

        var sample = await scraper.GetValidationActivitySummaryAsync(TimeSpan.FromSeconds(105), "facility-1", "report-9");

        sample.Should().NotBeNull();
        sample!.NewestUtc.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(newestNs / 1_000_000).UtcDateTime);
        sample.Summary.Should().Contain("elapsed 40s");
    }

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly string _body;

        public JsonHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
    }

    private sealed class HoldUntilCancelHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class NullOutput : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
