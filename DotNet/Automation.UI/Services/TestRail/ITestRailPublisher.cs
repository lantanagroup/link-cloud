namespace Automation.UI.Services.TestRail;

public interface ITestRailPublisher
{
    Task PublishScenarioRunAsync(
        ScenarioTestRailPublishRequest request,
        CancellationToken cancellationToken = default);

    Task PublishApiHealthRunAsync(
        ApiHealthTestRailPublishRequest request,
        CancellationToken cancellationToken = default);
}
