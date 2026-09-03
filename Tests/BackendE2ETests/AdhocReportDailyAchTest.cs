using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class AdhocReportDailyAchTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public AdhocReportDailyAchTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "AdhocReportDailyAchTest")]
    public async Task ExecuteAdhocReportDailyAchTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.AdhocReportDailyAchScenarioId,
            "Adhoc Report Daily ACH Test",
            "BackendE2ETests.AdhocReportDailyAchTest",
            timeoutCts.Token);
    }
}
