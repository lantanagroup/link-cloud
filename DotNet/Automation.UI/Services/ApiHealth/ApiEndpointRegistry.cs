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
    private readonly IReadOnlyList<ApiEndpointDefinition> _allEndpoints;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ApiEndpointDefinition>> _endpointsByService;
    private readonly IReadOnlyList<string> _serviceNames;

    public ApiEndpointRegistry(IEnumerable<IServiceTestSuite> suites)
    {
        _suites = suites.ToList();

        var serviceMap = new Dictionary<string, IReadOnlyList<ApiEndpointDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var suite in _suites)
        {
            var defs = suite.GetEndpointDefinitions().ToList();
            serviceMap[suite.ServiceName] = defs;
        }

        _endpointsByService = serviceMap;
        _allEndpoints = _endpointsByService.Values.SelectMany(v => v).ToList();
        _serviceNames = _suites.Select(s => s.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Returns all registered endpoint definitions across all suites.
    /// </summary>
    public IReadOnlyList<ApiEndpointDefinition> GetAll() =>
        _allEndpoints;

    /// <summary>
    /// Returns endpoints filtered to a single service.
    /// </summary>
    public IReadOnlyList<ApiEndpointDefinition> GetByService(string serviceName) =>
        _endpointsByService.TryGetValue(serviceName, out var defs)
            ? defs
            : [];

    /// <summary>
    /// Returns all distinct service names registered.
    /// </summary>
    public IReadOnlyList<string> GetServiceNames() =>
        _serviceNames;

    /// <summary>
    /// Returns the test suite(s) for a given service name.
    /// </summary>
    public IReadOnlyList<IServiceTestSuite> GetSuitesForService(string serviceName) =>
        _suites.Where(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Returns all registered test suites.
    /// </summary>
    public IReadOnlyList<IServiceTestSuite> GetAllSuites() => _suites;

}
