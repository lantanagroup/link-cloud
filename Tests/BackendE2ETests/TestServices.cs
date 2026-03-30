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

    public FacilityServiceClient FacilityClient => _services.GetRequiredService<FacilityServiceClient>();
    public NormalizationServiceClient NormalizationClient => _services.GetRequiredService<NormalizationServiceClient>();
    public DataAcquisitionServiceClient DataAcquisitionClient => _services.GetRequiredService<DataAcquisitionServiceClient>();
    public ReportServiceClient ReportClient => _services.GetRequiredService<ReportServiceClient>();
    public MeasureEvalServiceClient MeasureEvalClient => _services.GetRequiredService<MeasureEvalServiceClient>();
    public ValidationServiceClient SdkValidationClient => _services.GetRequiredService<ValidationServiceClient>();
    public CensusServiceClient CensusClient => _services.GetRequiredService<CensusServiceClient>();

    public ValidationApiHelper CreateValidationHelper() => _services.GetRequiredService<ValidationApiHelper>();

    public ReportApiHelper CreateReportHelper(TestScenarioConfig config) =>
        new(_services.GetRequiredService<ReportServiceClient>(), Output, AutomationCfg, config);

    public ReportDatabaseValidator CreateReportValidator() => _services.GetRequiredService<ReportDatabaseValidator>();
    public ReportAbsManifestValidator CreateReportAbsManifestValidator() => _services.GetRequiredService<ReportAbsManifestValidator>();
    public DataAcquisitionDatabaseValidator CreateDataAcqValidator() => _services.GetRequiredService<DataAcquisitionDatabaseValidator>();
    public NormalizationDatabaseValidator CreateNormalizationValidator() => _services.GetRequiredService<NormalizationDatabaseValidator>();
    public TenantDatabaseValidator CreateTenantValidator() => _services.GetRequiredService<TenantDatabaseValidator>();
    public ValidationResultsValidator CreateValidationResultsValidator() => _services.GetRequiredService<ValidationResultsValidator>();

    public PipelineSnapshot CreatePipelineSnapshot() => _services.GetRequiredService<PipelineSnapshot>();
}
