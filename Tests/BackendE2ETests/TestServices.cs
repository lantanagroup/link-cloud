using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Per-test accessor that resolves automation services from the shared
/// <see cref="TestServices"/> DI container.
/// </summary>
public sealed class TestServices
{
    private readonly IServiceProvider _services;

    public TestServices(IServiceProvider services)
    {
        _services = services;
    }

    public AutomationConfig AutomationCfg => _services.GetRequiredService<AutomationConfig>();
    public DualOutputHelper Output => _services.GetRequiredService<DualOutputHelper>();
    public RestClient AdminBffClient => _services.GetRequiredService<RestClient>();
    public LokiScraper LokiScraper => _services.GetRequiredService<LokiScraper>();
    public FhirDataLoader FhirDataLoader => _services.GetRequiredService<FhirDataLoader>();
    public DatabaseConnectionFactory DbFactory => _services.GetRequiredService<DatabaseConnectionFactory>();
    public PipelineDataReader DataReader => _services.GetRequiredService<PipelineDataReader>();
    public ReportServiceClient ReportClient => _services.GetRequiredService<ReportServiceClient>();

    public FacilityApiClient CreateFacilityApi() => _services.GetRequiredService<FacilityApiClient>();
    public NormalizationApiClient CreateNormalizationApi() => _services.GetRequiredService<NormalizationApiClient>();
    public QueryConfigApiClient CreateQueryConfigApi() => _services.GetRequiredService<QueryConfigApiClient>();
    public ValidationApiClient CreateValidationApi() => _services.GetRequiredService<ValidationApiClient>();

    public ReportApiClient CreateReportApi(TestScenarioConfig config) =>
        new(_services.GetRequiredService<ReportServiceClient>(), Output, LokiScraper, AutomationCfg, config);

    public ReportDatabaseValidator CreateReportValidator() => _services.GetRequiredService<ReportDatabaseValidator>();
    public ReportAbsManifestValidator CreateReportAbsManifestValidator() => _services.GetRequiredService<ReportAbsManifestValidator>();
    public DataAcquisitionDatabaseValidator CreateDataAcqValidator() => _services.GetRequiredService<DataAcquisitionDatabaseValidator>();
    public NormalizationDatabaseValidator CreateNormalizationValidator() => _services.GetRequiredService<NormalizationDatabaseValidator>();
    public TenantDatabaseValidator CreateTenantValidator() => _services.GetRequiredService<TenantDatabaseValidator>();
    public ValidationResultsValidator CreateValidationResultsValidator() => _services.GetRequiredService<ValidationResultsValidator>();

    public PipelineSnapshot CreatePipelineSnapshot() => _services.GetRequiredService<PipelineSnapshot>();
}
