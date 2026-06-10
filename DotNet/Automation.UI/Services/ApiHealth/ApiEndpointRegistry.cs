using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.TestSuites;

namespace Automation.UI.Services.ApiHealth;

/// <summary>
/// Central registry of all API endpoint test suites. Each service has a corresponding
/// <see cref="IServiceTestSuite"/> that defines and executes its CRUD test steps.
/// Health-only endpoints (for services without full CRUD suites) are kept as simple
/// HTTP-check definitions.
/// </summary>
public sealed class ApiEndpointRegistry
{
    private readonly IReadOnlyList<IServiceTestSuite> _suites;

    public ApiEndpointRegistry(IEnumerable<IServiceTestSuite> suites)
    {
        _suites = suites.ToList();
    }

    /// <summary>
    /// Returns all registered endpoint definitions across all suites.
    /// </summary>
    public IReadOnlyList<ApiEndpointDefinition> GetAll() =>
        _suites.SelectMany(s => s.GetEndpointDefinitions()).ToList();

    /// <summary>
    /// Returns endpoints filtered to a single service.
    /// </summary>
    public IReadOnlyList<ApiEndpointDefinition> GetByService(string serviceName) =>
        _suites
            .Where(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(s => s.GetEndpointDefinitions())
            .ToList();

    /// <summary>
    /// Returns all distinct service names registered.
    /// </summary>
    public IReadOnlyList<string> GetServiceNames() =>
        _suites.Select(s => s.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();

    /// <summary>
    /// Returns the test suite(s) for a given service name.
    /// </summary>
    public IReadOnlyList<IServiceTestSuite> GetSuitesForService(string serviceName) =>
        _suites.Where(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Returns all registered test suites.
    /// </summary>
    public IReadOnlyList<IServiceTestSuite> GetAllSuites() => _suites;

    /// <summary>
    /// Finds the suite that owns a specific endpoint key.
    /// </summary>
    public IServiceTestSuite? FindSuiteForEndpoint(string endpointKey) =>
        _suites.FirstOrDefault(s => s.GetEndpointDefinitions().Any(e => e.Key == endpointKey));
}
