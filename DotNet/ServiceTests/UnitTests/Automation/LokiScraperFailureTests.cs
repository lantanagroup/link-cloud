using FluentAssertions;
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
}
