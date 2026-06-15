using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class RegenerateReportTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public RegenerateReportTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "RegenerateReportTest")]
    public async Task ExecuteRegenerateReportTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.RegenerateReportScenarioId,
            "Regenerate Report Test",
            "BackendE2ETests.RegenerateReportTest",
            timeoutCts.Token);
    }
}
