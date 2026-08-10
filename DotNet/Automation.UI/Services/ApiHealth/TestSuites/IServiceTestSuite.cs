using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Base interface for a service test suite. Each implementation exercises a full
/// CRUD lifecycle using LinkSdk clients and returns step-by-step results.
/// </summary>
public interface IServiceTestSuite
{
    /// <summary>Service name this suite tests (matches <see cref="ApiEndpointDefinition.ServiceName"/>).</summary>
    string ServiceName { get; }

    /// <summary>
    /// Returns the endpoint definitions (steps) this suite exercises.
    /// Used to populate the dashboard before any tests are run.
    /// </summary>
    IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions();

    /// <summary>
    /// Executes the full CRUD test suite, returning results for each step.
    /// The suite handles its own setup and teardown (cleanup).
    /// </summary>
    Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Declares seed fixtures required for this suite's stateful tests.
    /// Stateless suites should return an empty list.
    /// </summary>
    IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() => [];
}
