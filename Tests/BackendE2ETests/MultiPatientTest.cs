using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class MultiPatientTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public MultiPatientTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "MultiPatientTest")]
    public async Task ExecuteMultiPatientTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.MultiPatientScenarioId,
            "Multi Patient Test",
            "BackendE2ETests.MultiPatientTest",
            timeoutCts.Token);
    }
}
