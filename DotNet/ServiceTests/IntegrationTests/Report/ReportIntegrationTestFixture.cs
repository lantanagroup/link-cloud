using Azure.Storage.Blobs;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Testcontainers.Azurite;
using Testcontainers.MongoDb;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [CollectionDefinition("ReportIntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<ReportIntegrationTestFixture> { }

    public class ReportIntegrationTestFixture : IAsyncLifetime, IDisposable
    {
        private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .WithReplicaSet("rs0")
            .Build();

        private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();

        private IHost _host;

        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IServiceScopeFactory ScopeFactory { get; private set; } = null!;

        public Mock<ISchedulerFactory> SchedulerFactoryMock { get; } = new();

        public Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> SubmitPayloadKafkaProducerMock { get; private set; } = new();
        public Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>> ReadyForValidationKafkaProducerMock { get; private set; } = new();
        public Mock<IProducer<string, DataAcquisitionRequestedValue>> DataAcquisitionRequestedKafkaProducerMock { get; private set; } = new();
        public Mock<IProducer<string, AuditEventMessage>> AuditableEventKafkaProducerMock { get; private set; } = new();

        public Mock<ITenantApiService> TenantApiServiceMock { get; } = new();
        public Mock<IHttpClientFactory> HttpClientFactoryMock { get; } = new();
        public Mock<IQuartzJobHelper> QuartzJobHelperMock { get; } = new();
        public Mock<IKafkaConsumerFactory<string, ReportScheduledValue>> ReportScheduledConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<string, ReportScheduledValue>> ReportScheduledTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<string, ReportScheduledValue>> ReportScheduledDeadLetterHandlerMock { get; } = new();

        public Mock<IKafkaConsumerFactory<string, PatientListMessage>> PatientListsAcquiredConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<string, PatientListMessage>> PatientListsAcquiredTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<string, PatientListMessage>> PatientListsAcquiredDeadLetterHandlerMock { get; } = new();

        public Mock<IKafkaConsumerFactory<Null, MeasureReportGeneratedValue>> MeasureReportGeneratedConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<Null, MeasureReportGeneratedValue>> MeasureReportGeneratedTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue>> MeasureReportGeneratedDeadLetterHandlerMock { get; } = new();

        public Mock<IKafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>> PayloadSubmittedConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>> PayloadSubmittedTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>> PayloadSubmittedDeadLetterHandlerMock { get; } = new();

        public Mock<IKafkaConsumerFactory<string, ValidationCompleteValue>> ValidationCompleteConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<string, ValidationCompleteValue>> ValidationCompleteTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<string, ValidationCompleteValue>> ValidationCompleteDeadLetterHandlerMock { get; } = new();

        public Mock<ICreateSystemToken> CreateSystemTokenMock { get; } = new();
        public Mock<IProducer<string, EvaluationRequestedValue>> EvaluationRequestedProducerMock { get; } = new();
        public Mock<IKafkaConsumerFactory<string, GenerateReportValue>> GenerateReportConsumerFactoryMock { get; } = new();
        public Mock<ITransientExceptionHandler<string, GenerateReportValue>> GenerateReportTransientHandlerMock { get; } = new();
        public Mock<IDeadLetterExceptionHandler<string, GenerateReportValue>> GenerateReportDeadLetterHandlerMock { get; } = new();

        public string MongoConnectionString => _mongoContainer.GetConnectionString() + "&replicaSet=rs0";
        public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();

        public async Task InitializeAsync()
        {
            await Task.WhenAll(_mongoContainer.StartAsync(), _azuriteContainer.StartAsync());

            var builder = Host.CreateApplicationBuilder();

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = MongoConnectionString,
                ["Mongo:DatabaseName"] = "report_integration_test",
                ["BlobStorage:ConnectionString"] = AzuriteConnectionString,
                ["BlobStorage:BlobContainerName"] = "report-test-container",
                ["BlobStorage:BlobRoot"] = "test-root",
                ["Authentication:EnableAnonymousAccess"] = "true",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["ServiceRegistry:CensusServiceApiUrl"] = "http://localhost:8080"
            });

            builder.Services.Configure<BlobStorageSettings>(builder.Configuration.GetSection("BlobStorage"));

            var _dbPath = Path.Combine(Path.GetTempPath(), $"testdb_{Guid.NewGuid()}.db");
            var sqliteConnectionString = $"Data Source={_dbPath};";

            builder.Services.AddDbContext<ReportDbContext>(options =>
            {
                options.UseSqlite(sqliteConnectionString);
            });

            builder.Services.AddSingleton<IQuartzJobHelper>(QuartzJobHelperMock.Object);

            builder.Services.AddTransient<IEntityRepository<ReportSchedule>, EntityRepository<ReportSchedule, ReportDbContext>>();
            builder.Services.AddTransient<IEntityRepository<ReportEntry>, EntityRepository<ReportEntry, ReportDbContext>>();
            builder.Services.AddTransient<IEntityRepository<ReportPopulation>, EntityRepository<ReportPopulation, ReportDbContext>>();
            builder.Services.AddTransient<IEntityRepository<ReportResource>, EntityRepository<ReportResource, ReportDbContext>>();

            builder.Services.AddTransient<IDatabase, Database>();
            builder.Services.AddTransient<IReportScheduledManager, ReportScheduledManager>();
            builder.Services.AddTransient<IReportEntryManager, ReportEntryManager>();
            builder.Services.AddTransient<IReportPopulationManager, ReportPopulationManager>();
            builder.Services.AddTransient<IReportResourceManager, ReportResourceManager>();

            builder.Services.AddTransient<IReportServiceMetrics, ReportServiceMetrics>();

            builder.Services.AddTransient<PatientAggregator>();
            builder.Services.AddTransient<MeasureReportAggregator>();
            builder.Services.AddSingleton<BlobStorageService>();
            builder.Services.AddSingleton<ITenantApiService>(TenantApiServiceMock.Object);
            builder.Services.AddSingleton<IHttpClientFactory>(HttpClientFactoryMock.Object);

            builder.Services.AddLogging();

            builder.Services.AddSingleton<ISchedulerFactory>(SchedulerFactoryMock.Object);

            var serviceInformation = new ServiceInformation
            {
                ServiceName = "ReportIntegrationTest",
                ServiceConfigName = "ReportIntegrationTest",
                Version = "1.0.0-test"
            };
            builder.Services.AddSingleton(serviceInformation);

            builder.Services.AddTransient<SubmitPayloadProducer>(sp =>
                new SubmitPayloadProducer(sp.GetRequiredService<IServiceScopeFactory>(), SubmitPayloadKafkaProducerMock.Object, new Mock<ILogger<SubmitPayloadProducer>>().Object));

            builder.Services.AddTransient<ReadyForValidationProducer>(sp =>
                new ReadyForValidationProducer(ReadyForValidationKafkaProducerMock.Object, sp.GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<ReadyForValidationProducer>>().Object));

            builder.Services.AddTransient<AuditableEventOccurredProducer>(sp =>
                new AuditableEventOccurredProducer(new Mock<ILogger<AuditableEventOccurredProducer>>().Object, AuditableEventKafkaProducerMock.Object, sp.GetRequiredService<ServiceInformation>()));

            builder.Services.AddTransient<DataAcquisitionRequestedProducer>(sp =>
                new DataAcquisitionRequestedProducer(sp.GetRequiredService<IServiceScopeFactory>(), DataAcquisitionRequestedKafkaProducerMock.Object));

            builder.Services.AddTransient<ReportManifestProducer>(sp =>
                new ReportManifestProducer(
                    new Mock<ILogger<ReportManifestProducer>>().Object,
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    new MeasureReportAggregator(new Mock<ILogger<MeasureReportAggregator>>().Object, sp.GetRequiredService<IDatabase>()),
                    TenantApiServiceMock.Object,
                    sp.GetRequiredService<BlobStorageService>(),
                    sp.GetRequiredService<SubmitPayloadProducer>(),
                    sp.GetRequiredService<AuditableEventOccurredProducer>()));

            builder.Services.AddSingleton<IKafkaConsumerFactory<string, ReportScheduledValue>>(ReportScheduledConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<string, ReportScheduledValue>>(ReportScheduledTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, ReportScheduledValue>>(ReportScheduledDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<IKafkaConsumerFactory<string, PatientListMessage>>(PatientListsAcquiredConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<string, PatientListMessage>>(PatientListsAcquiredTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, PatientListMessage>>(PatientListsAcquiredDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<IKafkaConsumerFactory<Null, MeasureReportGeneratedValue>>(MeasureReportGeneratedConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<Null, MeasureReportGeneratedValue>>(MeasureReportGeneratedTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue>>(MeasureReportGeneratedDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<ICreateSystemToken>(CreateSystemTokenMock.Object);
            builder.Services.AddSingleton<IProducer<string, EvaluationRequestedValue>>(EvaluationRequestedProducerMock.Object);

            builder.Services.AddSingleton<IKafkaConsumerFactory<string, GenerateReportValue>>(GenerateReportConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<string, GenerateReportValue>>(GenerateReportTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, GenerateReportValue>>(GenerateReportDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<IKafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue>>(PayloadSubmittedConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>>(PayloadSubmittedTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue>>(PayloadSubmittedDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<IKafkaConsumerFactory<string, ValidationCompleteValue>>(ValidationCompleteConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<string, ValidationCompleteValue>>(ValidationCompleteTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(ValidationCompleteDeadLetterHandlerMock.Object);

            builder.Services.AddTransient<PatientListsAcquiredListener>();
            builder.Services.AddTransient<ReportScheduledListener>();
            builder.Services.AddTransient<MeasureReportGeneratedListener>();
            builder.Services.AddTransient<PayloadSubmittedListener>();
            builder.Services.AddTransient<ValidationCompleteListener>();
            builder.Services.AddTransient<GenerateReportListener>();

            builder.Services.Configure<ServiceRegistry>(opts => opts.CensusServiceUrl = "http://localhost:8080");
            builder.Services.Configure<LinkTokenServiceSettings>(opts => opts.SigningKey = "test-signing-key");
            builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts => opts.AllowAnonymous = true);

            _host = builder.Build();
            await _host.StartAsync();

            ServiceProvider = _host.Services;
            ScopeFactory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
            var blobConnectionString = configuration["BlobStorage:ConnectionString"];
            var containerName = configuration["BlobStorage:BlobContainerName"] ?? "report-test-container";
            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            // Ensure database is created and set PRAGMAs
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            dbContext.Database.EnsureCreated();
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            await _mongoContainer.DisposeAsync();
            await _azuriteContainer.DisposeAsync();
        }

        public void Dispose() { }
    }
}