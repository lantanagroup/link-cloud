using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class MegaMultiPatientTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public MegaMultiPatientTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "MegaMultiPatientTest")]
    [Trait("Category", "LongRunning")]
    public async Task ExecuteMegaMultiPatientTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(45));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.MegaMultiPatientScenarioId,
            "Mega Multi Patient Test",
            "BackendE2ETests.MegaMultiPatientTest",
            timeoutCts.Token);
    }
}
