using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class MultiMeasureTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;

    public MultiMeasureTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "MultiMeasureTest")]
    public async Task ExecuteMultiMeasureTest()
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await AutomationUiScenarioRunner.RunScenarioAsync(
            _sp,
            AutomationUiScenarioRunner.MultiMeasureScenarioId,
            "Multi Measure Test",
            "BackendE2ETests.MultiMeasureTest",
            timeoutCts.Token);
    }
}
