using LantanaGroup.Link.Automation.Link.Models;

namespace Automation.UI.Services.TestRail;

public interface ITestRailPublisher
{
    /// <summary>
    /// Posts the scenario run. Returns the TestRail runs that accepted results,
    /// or an empty list when publishing was skipped or failed.
    /// </summary>
    Task<IReadOnlyList<TestRailRunReference>> PublishScenarioRunAsync(
        ScenarioTestRailPublishRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the API Health run. Returns the TestRail runs that accepted results,
    /// or an empty list when publishing was skipped or failed.
    /// </summary>
    Task<IReadOnlyList<TestRailRunReference>> PublishApiHealthRunAsync(
        ApiHealthTestRailPublishRequest request,
        CancellationToken cancellationToken = default);
}
