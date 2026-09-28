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
