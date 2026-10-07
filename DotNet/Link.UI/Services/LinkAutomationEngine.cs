using Automation.UI.Services;
using Automation.UI.Services.ConfigurationGeneration;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Normalization.Engine;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Link.UI.Services;

/// <summary>
/// Hosts the automation run engine in this process. Startup recovery is the
/// existing single-engine behavior. Do not run another engine against the same database.
/// </summary>
public sealed class LinkAutomationEngineStatus
{
    public bool Ready { get; init; }

    public string? Message { get; init; }

    public const string BlobMissingMessage =
        "Bundle storage is not configured. Set InternalBlobStorage:ConnectionString and InternalBlobStorage:BlobContainerName.";

    public const string KafkaMissingMessage =
        "Kafka is not configured. Set KafkaConnection:BootstrapServers before starting a run.";

    public const string LokiMissingMessage =
        "Loki is not configured. Set Loki:Url to an absolute http or https URI and set Loki:App.";

    public const string PipelineAbortMissingMessage =
        "Pipeline abort storage is not configured. Set ConnectionStrings:Redis, or PipelineAbort:AllowInMemory for this single process.";
}

public static class LinkAutomationEngine
{
    public static LinkAutomationEngineStatus Add(IServiceCollection services, IConfiguration configuration)
    {
        var status = Register(services, configuration);
        services.AddSingleton(status);
        return status;
    }

    private static LinkAutomationEngineStatus Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"]?.Trim();
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
        {
            return NotReady(AutomationRunReader.NotConfiguredMessage);
        }

        if (string.IsNullOrWhiteSpace(configuration["InternalBlobStorage:ConnectionString"])
            || string.IsNullOrWhiteSpace(configuration["InternalBlobStorage:BlobContainerName"]))
        {
            return NotReady(LinkAutomationEngineStatus.BlobMissingMessage);
        }

        var kafka = configuration.GetSection(KafkaConstants.SectionName).Get<KafkaConnection>();
        if (kafka is null || kafka.BootstrapServers is null || kafka.BootstrapServers.Count == 0
            || kafka.BootstrapServers.All(string.IsNullOrWhiteSpace))
        {
            return NotReady(LinkAutomationEngineStatus.KafkaMissingMessage);
        }

        var lokiUrl = configuration["Loki:Url"]?.Trim();
        var lokiApp = configuration["Loki:App"]?.Trim();
        if (string.IsNullOrWhiteSpace(lokiApp)
            || !Uri.TryCreate(lokiUrl, UriKind.Absolute, out var lokiUri)
            || (lokiUri.Scheme != Uri.UriSchemeHttp && lokiUri.Scheme != Uri.UriSchemeHttps))
        {
            return NotReady(LinkAutomationEngineStatus.LokiMissingMessage);
        }

        connectionString = NormalizeLocalConnectionString(connectionString);
        MongoClientSettings probeSettings;
        try
        {
            probeSettings = MongoClientSettings.FromConnectionString(connectionString);
        }
        catch (Exception)
        {
            return NotReady(AutomationRunReader.SettingsRejectedMessage);
        }

        try
        {
            probeSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            probeSettings.ConnectTimeout = TimeSpan.FromSeconds(3);
            var probe = new MongoClient(probeSettings);
            probe.GetDatabase(databaseName).RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception)
        {
            return NotReady(AutomationRunReader.UnreachableMessage);
        }

        try
        {
            services.AddPipelineAbortRegistry(configuration);
        }
        catch (InvalidOperationException)
        {
            return NotReady(LinkAutomationEngineStatus.PipelineAbortMissingMessage);
        }

        services.Configure<AutomationConfig>(configuration.GetSection("Automation"));
        services.PostConfigure<AutomationConfig>(cfg =>
        {
            cfg.LokiBaseUrl = lokiUrl!.TrimEnd('/');
            cfg.LokiAppLabel = lokiApp!;
        });
        services.Configure<LeftoverRunCleanupOptions>(configuration.GetSection(LeftoverRunCleanupOptions.SectionName));
        services.Configure<ImportedBundleBlobStorageSettings>(configuration.GetSection(ImportedBundleBlobStorageSettings.Key));

        var runtimeSettings = MongoClientSettings.FromConnectionString(connectionString);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(runtimeSettings));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
        services.AddSingleton(kafka);

        services.AddSingleton<MongoIndexManager>();
        services.AddSingleton<IImportedBundleContentStore, AzureBlobImportedBundleContentStore>();
        services.AddSingleton<ISnapshotPayloadStore, AzureBlobSnapshotPayloadStore>();
        services.AddSingleton<IGeneratedPatientTemplateCache, MongoGeneratedPatientTemplateCache>();
        services.AddSingleton<GeneratedTemplateCacheVersionStore>();
        services.AddSingleton<IGeneratedTemplateCacheVersionLookup>(sp => sp.GetRequiredService<GeneratedTemplateCacheVersionStore>());
        services.AddSingleton<GeneratedPatientBundleReplayService>();
        services.AddSingleton<ImportedBundleExecutionResolver>();
        services.AddSingleton<ISnapshotStore, MongoSnapshotStore>();
        services.AddSingleton<ICleanupSettingsStore, MongoCleanupSettingsStore>();
        services.AddSingleton<ICleanupReportStore, MongoCleanupReportStore>();
        services.AddSingleton<IScenarioStore, MongoScenarioStore>();
        services.AddSingleton<IQueryPlanTemplateStore, MongoQueryPlanTemplateStore>();
        services.AddSingleton<IMeasureTemplateStore, MongoMeasureTemplateStore>();
        services.AddSingleton<INormalizationStore, MongoNormalizationStore>();
        services.AddSingleton<IOrganizationResourceMapTemplateStore, MongoOrganizationResourceMapTemplateStore>();
        services.AddSingleton<IPatientConfigurationStore, MongoPatientConfigurationStore>();
        services.AddSingleton<IFacilityTemplateStore, MongoFacilityTemplateStore>();
        services.AddSingleton<IGenerationCatalogStore, MongoGenerationCatalogStore>();
        services.AddHttpClient("TerminologyLookup");
        services.AddSingleton<ITerminologyCodeLookup, TerminologyCodeLookup>();
        services.AddNormalizationEngine();
        services.AddSingleton<BundleConfigurationGenerationService>();

        services.AddScoped<LantanaGroup.Link.Automation.Link.Helpers.PipelineDataReader>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<PatientConfigurationSeedService>();
        services.AddHostedService<ScenarioSeedService>();
        services.AddHostedService<MeasureTemplateSeedService>();
        services.AddHostedService<QueryPlanTemplateSeedService>();
        services.AddHostedService<NormalizationSuiteSeedService>();
        services.AddHostedService<OrganizationResourceMapTemplateSeedService>();
        services.AddHostedService<FacilityTemplateSeedService>();
        services.AddHostedService<GenerationCatalogSeedService>();
        services.AddHostedService<ScenarioRunStartupRecoveryService>();

        services.AddSingleton<RunSnapshotOrchestrator>();
        services.AddHostedService(sp => sp.GetRequiredService<RunSnapshotOrchestrator>());
        services.AddSingleton<LeftoverRunCleanupService>();
        services.AddSingleton<ILeftoverRunCleanup>(sp => sp.GetRequiredService<LeftoverRunCleanupService>());
        services.AddHostedService(sp => sp.GetRequiredService<LeftoverRunCleanupService>());
        services.AddSingleton<ILivePatientEventInjector, LivePatientEventInjector>();
        services.AddSingleton<PatientReplacementManager>();
        services.AddSingleton<IAutomationRunManager, AutomationRunManager>();

        return new LinkAutomationEngineStatus { Ready = true };
    }

    private static LinkAutomationEngineStatus NotReady(string message) =>
        new()
        {
            Ready = false,
            Message = message
        };

    private static string NormalizeLocalConnectionString(string connectionString)
    {
        if (!connectionString.Contains("localhost:17017", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("127.0.0.1:17017", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var hasReplicaSet = connectionString.Contains("replicaSet=", StringComparison.OrdinalIgnoreCase);
        var hasDirectConnection = connectionString.Contains("directConnection=", StringComparison.OrdinalIgnoreCase);
        if (hasReplicaSet && hasDirectConnection)
            return connectionString;

        var separator = connectionString.Contains('?') ? "&" : "?";
        if (!hasReplicaSet)
        {
            connectionString += $"{separator}replicaSet=rs0";
            separator = "&";
        }

        if (!hasDirectConnection)
            connectionString += $"{separator}directConnection=true";

        return connectionString;
    }
}
