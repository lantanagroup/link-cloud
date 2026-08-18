using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class AdhocReportTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public AdhocReportTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "AdhocReportTest")]
    public async Task ExecuteAdhocReportTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.AdhocReportScenarioId,
            "Adhoc Report Test",
            "BackendE2ETests.AdhocReportTest",
            timeoutCts.Token);
    }
}
