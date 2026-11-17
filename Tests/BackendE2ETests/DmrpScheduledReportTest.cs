using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class DmrpScheduledReportTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public DmrpScheduledReportTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "DmrpScheduledReportTest")]
    public async Task ExecuteDmrpScheduledReportTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.DmrpScheduledReportScenarioId,
            "DMRP Scheduled Report Test",
            "BackendE2ETests.DmrpScheduledReportTest",
            timeoutCts.Token);
    }
}
