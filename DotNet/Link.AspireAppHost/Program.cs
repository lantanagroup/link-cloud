using Link.AspireAppHost;
using Microsoft.Extensions.Configuration;
using static Link.AspireAppHost.DotNetServiceExtensions;

// The optional local orchestrator for link-cloud. It starts the infrastructure in containers on ports
// of its own, then every service the way docker-compose runs it, with compose's hostnames swapped for
// this host's (see DotNetServiceExtensions and JavaServiceExtensions). Nothing here changes how the
// services build or run anywhere else.
var builder = DistributedApplication.CreateBuilder(args);

var infrastructure = builder.AddLinkInfrastructure();

// ---------------------------------------------------------------------------------------------------
// .NET services
// ---------------------------------------------------------------------------------------------------

var mockDmrp = builder.AddLinkService<Projects.MockDmrpApi>("mock-dmrp-api", Ports.MockDmrpApi)
    .WithDatabase(infrastructure.Database("mock-dmrp"))
    .WithEnvironment("MockDmrpApi__Enabled", "true")
    .WithEnvironment("MockDmrpApi__AuthClientId", "link-cloud-dev")
    .WithEnvironment("MockDmrpApi__AuthClientSecret", "link-cloud-dev-secret")
    .WithEnvironment("MockDmrpApi__SigningKey", "mock-dmrp-local-development-signing-key-not-for-any-deployed-environment");

builder.AddLinkService<Projects.MockFhirServer>("mock-fhir-server", Ports.MockFhirServer)
    .WithEnvironment("MockFhirServer__PreGeneratedPatientCount", "10")
    .WithEnvironment("MockFhirServer__ResourcesPerPatient", "10")
    .WithEnvironment("MockFhirServer__ClinicalPeriodStart", "2026-01-01")
    .WithEnvironment("MockFhirServer__ClinicalPeriodEnd", "2026-01-31");

var tenant = builder.AddLinkService<Projects.Tenant>("tenant", Ports.Tenant)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("tenant"))
    .WithLocalAuthentication()
    .WithEnvironment("ServiceRegistry__MeasureServiceUrl", Url(Ports.MeasureEval))
    .WithEnvironment("ServiceRegistry__ReportServiceUrl", Url(Ports.Report))
    .WithEnvironment("ServiceRegistry__NormalizationServiceUrl", Url(Ports.Normalization))
    .WithEnvironment("DMRP__Api__BaseUrl", Url(Ports.MockDmrpApi))
    .WithEnvironment("DMRP__Api__TokenUrl", Url(Ports.MockDmrpApi) + "/api/mock-dmrp/oauth2/token")
    .WithEnvironment("DMRP__Api__ClientId", "link-cloud-dev")
    .WithEnvironment("DMRP__Api__ClientSecret", "link-cloud-dev-secret")
    // The mock's own switch. Tenant reads it too, to turn on the facility write-through to the mock.
    .WithEnvironment("MockDmrpApi__Enabled", "true")
    .WaitFor(mockDmrp);

builder.AddLinkService<Projects.Account>("account", Ports.Account)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("account"))
    .WithRedis(infrastructure)
    .WithLocalAuthentication();

builder.AddLinkService<Projects.Audit>("audit", Ports.Audit)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("audit"))
    .WithLocalAuthentication();

builder.AddLinkService<Projects.Census>("census", Ports.Census)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("census"))
    .WithLocalAuthentication()
    .WithTenantService()
    .WaitFor(tenant);

// DataAcquisition and its worker share one database. DataAcquisition migrates it; the worker does not.
var dataAcquisitionDatabase = infrastructure.Database("dataacquisition");
var dataAcquisition = builder.AddLinkService<Projects.DataAcquisition>("dataacq", Ports.DataAcquisition)
    .WithKafka(infrastructure)
    .WithDatabase(dataAcquisitionDatabase)
    .WithRedis(infrastructure)
    .WithResourceCache(infrastructure)
    .WithLocalAuthentication()
    .WithTenantService()
    .WaitFor(tenant);

builder.AddLinkService<Projects.DataAcquisition_AcquisitionWorker>("dataacq-worker", Ports.AcquisitionWorker)
    .WithKafka(infrastructure)
    .WithDatabase(dataAcquisitionDatabase, autoMigrate: false)
    .WithRedis(infrastructure)
    .WithResourceCache(infrastructure)
    .WithLocalAuthentication()
    .WithTenantService()
    .WaitFor(dataAcquisition);

builder.AddLinkService<Projects.Normalization>("normalization", Ports.Normalization)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("normalization"))
    .WithRedis(infrastructure)
    .WithResourceCache(infrastructure)
    .WithLocalAuthentication()
    .WithTenantService();

// appsettings.Docker.json gives QueryDispatch Tenant's address with /api on the end; the others without.
builder.AddLinkService<Projects.QueryDispatch>("querydispatch", Ports.QueryDispatch)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("querydispatch"))
    .WithLocalAuthentication()
    .WithTenantService("/api");

var report = builder.AddLinkService<Projects.Report>("report", Ports.Report)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("report"))
    .WithRedis(infrastructure)
    .WithInternalBlobStorage(infrastructure)
    .WithLocalAuthentication()
    .WithTenantService()
    .WithEnvironment("ServiceRegistry__CensusServiceUrl", Url(Ports.Census))
    .WithEnvironment("PatientAggregator__IncludeOrganizationResource", "true");

builder.AddLinkService<Projects.Submission>("submission", Ports.Submission)
    .WithKafka(infrastructure)
    .WithDatabase(infrastructure.Database("submission"))
    .WithInternalBlobStorage(infrastructure)
    .WithLocalAuthentication()
    .WithEnvironment("ExternalBlobStorage__ConnectionString", LinkInfrastructure.BlobConnectionString)
    .WithEnvironment("ExternalBlobStorage__BlobContainerName", LinkInfrastructure.ExternalContainer)
    .WithEnvironment("ExternalBlobStorage__FlattenHierarchy", "false")
    .WithEnvironment("ServiceRegistry__ReportServiceUrl", Url(Ports.Report))
    // appsettings.Docker.json's /data/submission is a container path.
    .WithEnvironment("SubmissionServiceConfig__SubmissionDirectory", Path.Combine(Path.GetTempPath(), "link-aspire", "submission"));

// Terminology loads its code groups from the folder in this host's Terminology:Path setting. Without
// it the service starts with none, and Validation's terminology checks find nothing.
var terminology = builder.AddLinkService<Projects.Terminology>("terminology", Ports.Terminology)
    .WithLocalAuthentication()
    .WithEnvironment("Terminology__Path", builder.Configuration["Terminology:Path"] ?? "")
    .WithEnvironment("ServiceInformation__Name", "Link Terminology");

// ---------------------------------------------------------------------------------------------------
// Java services
// ---------------------------------------------------------------------------------------------------

var (measureEval, validation) = builder.AddJavaServices(infrastructure, infrastructure.Database("validation"));
measureEval.WaitFor(infrastructure.Kafka).WaitFor(infrastructure.Redis).WaitFor(infrastructure.Azurite);
validation.WaitFor(infrastructure.Kafka).WaitFor(infrastructure.Redis).WaitFor(infrastructure.Azurite).WaitFor(terminology);

// ---------------------------------------------------------------------------------------------------
// Admin BFF, Admin UI and Automation UI
// ---------------------------------------------------------------------------------------------------

(string Name, string Cluster, int Port)[] proxiedServices =
[
    ("Account", "AccountService", Ports.Account),
    ("Audit", "AuditService", Ports.Audit),
    ("Census", "CensusService", Ports.Census),
    ("DataAcquisition", "DataAcquisitionService", Ports.DataAcquisition),
    ("Measure", "MeasureEvaluationService", Ports.MeasureEval),
    ("Normalization", "NormalizationService", Ports.Normalization),
    ("QueryDispatch", "QueryDispatchService", Ports.QueryDispatch),
    ("Report", "ReportService", Ports.Report),
    ("Submission", "SubmissionService", Ports.Submission),
    ("Terminology", "TerminologyService", Ports.Terminology),
    ("Validation", "ValidationService", Ports.Validation)
];

var adminBff = builder.AddLinkService<Projects.Admin_BFF>("admin-bff", Ports.AdminBff, healthPath: "/api/health")
    .WithKafka(infrastructure)
    .WithRedis(infrastructure)
    .WithLocalAuthentication()
    .WithEnvironment("Authentication__DefaultChallengeScheme", "link_cookie")
    .WithEnvironment("Authentication__Schemas__Oauth2__Enabled", "false")
    .WithEnvironment("Authentication__Schemas__OpenIdConnect__Enabled", "false")
    .WithEnvironment("Authentication__Schemas__Jwt__Enabled", "false")
    .WithEnvironment("ServiceRegistry__TenantService__TenantServiceUrl", Url(Ports.Tenant))
    .WithEnvironment("ServiceRegistry__TenantService__PublicTenantServiceUrl", Url(Ports.Tenant))
    .WithEnvironment("ReverseProxy__Clusters__TenantService__Destinations__destination1__Address", Url(Ports.Tenant))
    .WithEnvironment("CORS__AllowedOrigins__2", Url(Ports.AdminUi));
foreach (var (name, cluster, port) in proxiedServices)
{
    adminBff
        .WithEnvironment($"ServiceRegistry__{name}ServiceUrl", Url(port))
        .WithEnvironment($"ServiceRegistry__Public{name}ServiceUrl", Url(port))
        .WithEnvironment($"ReverseProxy__Clusters__{cluster}__Destinations__destination1__Address", Url(port));
}

if (builder.Configuration.GetValue("AdminUi:Enabled", true))
    builder.AddAdminUi(adminBff);

var automationUi = builder.AddLinkService<Projects.Automation_UI>("automation-ui", Ports.AutomationUi, healthPath: "/")
    .WithKafka(infrastructure)
    .WithRedis(infrastructure)
    .WithLocalAuthentication()
    .WithEnvironment("Authentication__UseBearerForServiceCalls", "false")
    .WithEnvironment("ApiHealth__EnableAdminBffAuthSuite", "true")
    .WithEnvironment("PatientAggregator__IncludeOrganizationResource", "true")
    .WithEnvironment("pre-qualification.write-pre-qual-operation-outcome", "false")
    .WithEnvironment("LeftoverRunCleanup__Enabled", "false")
    .WithEnvironment("InternalBlobStorage__ConnectionString", LinkInfrastructure.BlobConnectionString)
    .WithEnvironment("InternalBlobStorage__BlobContainerName", LinkInfrastructure.AutomationContainer)
    .WithEnvironment("InternalBlobStorage__GeneratedTemplateBlobRoot", "automation/generated-bundles")
    .WithEnvironment("MongoDB__ConnectionString", LinkInfrastructure.MongoConnectionString)
    .WithEnvironment("Loki__Url", LinkInfrastructure.LokiUrl)
    .WithEnvironment("Automation__LokiBaseUrl", LinkInfrastructure.LokiUrl)
    .WithEnvironment("Automation__FhirServerBase", LinkInfrastructure.HapiFhirBaseUrl)
    .WithEnvironment("Automation__FacilityFhirServerBase", LinkInfrastructure.HapiFhirBaseUrl)
    .WithEnvironment("Automation__Kafka__RestProxyBaseUrl", LinkInfrastructure.KafkaRestProxyUrl)
    .WithEnvironment("ServiceRegistry__TenantService__TenantServiceUrl", Url(Ports.Tenant))
    .WithEnvironment("ServiceRegistry__AdminBffServiceUrl", Url(Ports.AdminBff))
    .WithEnvironment("ServiceRegistry__MockDmrpApiUrl", Url(Ports.MockDmrpApi))
    .WaitFor(infrastructure.Mongo)
    .WaitFor(infrastructure.Azurite)
    .WaitFor(infrastructure.HapiFhir)
    .WaitFor(infrastructure.KafkaRestProxy)
    .WaitFor(adminBff);
foreach (var (name, _, port) in proxiedServices)
    automationUi.WithEnvironment($"ServiceRegistry__{name}ServiceUrl", Url(port));

builder.Build().Run();
