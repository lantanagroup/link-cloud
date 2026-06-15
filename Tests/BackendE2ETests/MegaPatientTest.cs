using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class MegaPatientTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public MegaPatientTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "MegaPatientTest")]
    [Trait("Category", "LongRunning")]
    public async Task ExecuteMegaPatientTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(40));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.MegaPatientScenarioId,
            "Mega Patient Test",
            "BackendE2ETests.MegaPatientTest",
            timeoutCts.Token);
    }
}
