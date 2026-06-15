using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class ReportScheduledTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public ReportScheduledTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "ReportScheduledTest")]
    public async Task ExecuteReportScheduledTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.ScheduledReportScenarioId,
            "Scheduled Report Test",
            "BackendE2ETests.ReportScheduledTest",
            timeoutCts.Token);
    }
}
