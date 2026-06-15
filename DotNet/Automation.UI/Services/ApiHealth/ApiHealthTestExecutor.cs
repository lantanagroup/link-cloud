using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services.ApiHealth;

/// <summary>
/// Orchestrates API health test execution by delegating to the appropriate
/// <see cref="TestSuites.IServiceTestSuite"/> implementations. Each suite runs a full CRUD
/// lifecycle using real LinkSdk clients (or raw HTTP for AdminBff) and returns per-step results.
/// Results are persisted to MongoDB for history tracking.
/// </summary>
public sealed class ApiHealthTestExecutor
{
    private readonly ApiEndpointRegistry _registry;
    private readonly IApiHealthRunStore _store;
    private readonly ILogger<ApiHealthTestExecutor> _logger;

    public ApiHealthTestExecutor(
        ApiEndpointRegistry registry,
        IApiHealthRunStore store,
        ILogger<ApiHealthTestExecutor> logger)
    {
        _registry = registry;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Run all endpoints for a specific service by executing its test suites.
    /// </summary>
    public async Task<IReadOnlyList<ApiTestRunResult>> RunServiceTestsAsync(string serviceName, CancellationToken ct = default)
    {
        var suites = _registry.GetSuitesForService(serviceName);
        var allResults = new List<ApiTestRunResult>();

        foreach (var suite in suites)
        {
            try
            {
                var results = await suite.ExecuteAsync(ct);
                allResults.AddRange(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test suite {ServiceName} failed unexpectedly.", suite.ServiceName);
                allResults.Add(new ApiTestRunResult
                {
                    EndpointKey = $"{suite.ServiceName}::SuiteError",
                    ServiceName = suite.ServiceName,
                    Passed = false,
                    ErrorMessage = $"Suite execution failed: {ex.Message}",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _store.SaveRunResultsAsync(allResults, ct);
        return allResults;
    }

    /// <summary>
    /// Run all registered endpoint tests across all services.
    /// </summary>
    public async Task<IReadOnlyList<ApiTestRunResult>> RunAllTestsAsync(CancellationToken ct = default)
    {
        var allResults = new List<ApiTestRunResult>();

        foreach (var suite in _registry.GetAllSuites())
        {
            try
            {
                var results = await suite.ExecuteAsync(ct);
                allResults.AddRange(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test suite {ServiceName} failed unexpectedly.", suite.ServiceName);
                allResults.Add(new ApiTestRunResult
                {
                    EndpointKey = $"{suite.ServiceName}::SuiteError",
                    ServiceName = suite.ServiceName,
                    Passed = false,
                    ErrorMessage = $"Suite execution failed: {ex.Message}",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _store.SaveRunResultsAsync(allResults, ct);
        return allResults;
    }
}
