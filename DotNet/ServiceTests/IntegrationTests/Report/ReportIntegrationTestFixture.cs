using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Quartz;
using Testcontainers.Azurite;
using Testcontainers.MongoDb;

namespace IntegrationTests.Report
{
    [CollectionDefinition("ReportIntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<ReportIntegrationTestFixture> { }

    public class ReportIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        public IServiceScopeFactory ScopeFactory { get; private set; }
        private readonly IHost _host;
        private readonly AzuriteContainer _azuriteContainer;
        private readonly MongoDbContainer _mongoContainer;

        // Public static mocks
        public static Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> SubmitPayloadProducerMock { get; private set; }
        public static Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>> ReadyForValidationProducerMock { get; private set; }
        public static Mock<IProducer<string, DataAcquisitionRequestedValue>> DataAcquisitionRequestedProducerMock { get; private set; }
        public static Mock<IProducer<string, AuditEventMessage>> AuditableEventOccurredProducerMock { get; private set; }
        public static Mock<IProducer<string, EvaluationRequestedValue>> EvaluationRequestedProducerMock { get; private set; }
        public static Mock<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>> ResourceEvaluatedConsumerFactoryMock { get; private set; }
        public static Mock<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>> ResourceEvaluatedTransientHandlerMock { get; private set; }
        public static Mock<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>> ResourceEvaluatedDeadLetterHandlerMock { get; private set; }
        public static Mock<IKafkaConsumerFactory<string, ValidationCompleteValue>> ValidationCompleteConsumerFactoryMock { get; private set; }
        public static Mock<ITransientExceptionHandler<string, ValidationCompleteValue>> ValidationCompleteTransientHandlerMock { get; private set; }
        public static Mock<IDeadLetterExceptionHandler<string, ValidationCompleteValue>> ValidationCompleteDeadLetterHandlerMock { get; private set; }
        public static Mock<BlobStorageService> BlobStorageMock { get; private set; }
        public static Mock<ISchedulerFactory> SchedulerFactoryMock { get; private set; }
        public static Mock<IReportServiceMetrics> ReportServiceMetricsMock { get; private set; }
        public static Mock<ITenantApiService> TenantApiServiceMock { get; private set; }

        public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();
        public string MongoConnectionString { get; }

        public ReportIntegrationTestFixture()
        {
            // Mocks setup
            SubmitPayloadProducerMock = new Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>>();
            ReadyForValidationProducerMock = new Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>>();
            DataAcquisitionRequestedProducerMock = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
            AuditableEventOccurredProducerMock = new Mock<IProducer<string, AuditEventMessage>>();
            EvaluationRequestedProducerMock = new Mock<IProducer<string, EvaluationRequestedValue>>();
            ResourceEvaluatedConsumerFactoryMock = new Mock<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
            ResourceEvaluatedTransientHandlerMock = new Mock<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
            ResourceEvaluatedDeadLetterHandlerMock = new Mock<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
            ValidationCompleteConsumerFactoryMock = new Mock<IKafkaConsumerFactory<string, ValidationCompleteValue>>();
            ValidationCompleteTransientHandlerMock = new Mock<ITransientExceptionHandler<string, ValidationCompleteValue>>();
            ValidationCompleteDeadLetterHandlerMock = new Mock<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>();
            SchedulerFactoryMock = new Mock<ISchedulerFactory>();
            ReportServiceMetricsMock = new Mock<IReportServiceMetrics>();
            TenantApiServiceMock = new Mock<ITenantApiService>();

            // Azurite
            _azuriteContainer = new AzuriteBuilder()
                .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
                .Build();
            _azuriteContainer.StartAsync().GetAwaiter().GetResult();

            // MongoDB
            _mongoContainer = new MongoDbBuilder()
                .WithImage("mongo:7.0")
                .WithName($"mongo-report-{Guid.NewGuid():N}")
                .WithReplicaSet("rs0")
                .Build();

            _mongoContainer.StartAsync().GetAwaiter().GetResult();
            MongoConnectionString = $"{_mongoContainer.GetConnectionString()}&replicaSet=rs0";

            // Blob settings
            var blobSettings = new BlobStorageSettings
            {
                ConnectionString = _azuriteContainer.GetConnectionString(),
                BlobContainerName = "report-test-container",
                BlobRoot = "root"
            };
            var options = Options.Create(blobSettings);
            BlobStorageMock = new Mock<BlobStorageService>(MockBehavior.Default, options);

            // Host & DI setup
            var schedulerMock = new Mock<IScheduler>();
            SchedulerFactoryMock
                .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
                .ReturnsAsync(schedulerMock.Object);

            var builder = Host.CreateApplicationBuilder();

            // Get assembly version for ServiceInformation
            var assemblyVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0-test";

            // Setup ServiceInformation with the correct Mongo connection string
            var serviceInformation = builder.SetupServiceInformation(
                ReportConstants.ServiceName,
                assemblyVersion,
                MongoConnectionString
            );

            builder.Services.AddLogging(logging => logging.AddConsole());
            builder.Services.AddTransient<ILogger<ValidationCompleteListener>>(sp => Mock.Of<ILogger<ValidationCompleteListener>>());
            builder.Services.AddTransient<ILogger<ResourceEvaluatedListener>>(sp => Mock.Of<ILogger<ResourceEvaluatedListener>>());
            builder.Services.AddTransient<ILogger<ReportManifestProducer>>(sp => Mock.Of<ILogger<ReportManifestProducer>>());
            builder.Services.AddTransient<ILogger<UseLatestStrategy>>(sp => Mock.Of<ILogger<UseLatestStrategy>>());

            // Register IMongoClient as a singleton using the configured connection string
            builder.Services.AddSingleton<IMongoClient>(sp =>
            {
                return new MongoClient(MongoConnectionString);
            });

            // Register IMongoDatabase as a singleton using the shared client and database name
            builder.Services.AddSingleton<IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase("reportTestDb");
            });

            // Add the MongoDbContext to the DI container, using the shared client
            builder.Services.AddDbContext<MongoDbContext>((sp, options) =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                options.UseMongoDB(client, "reportTestDb");
            });

            builder.Services.AddTransient<IEntityRepository<ReportSchedule>, EntityRepository<ReportSchedule, MongoDbContext>>();
            builder.Services.AddTransient<IEntityRepository<PatientSubmissionEntry>, EntityRepository<PatientSubmissionEntry, MongoDbContext>>();
            builder.Services.AddTransient<IEntityRepository<ReportModel>, EntityRepository<ReportModel, MongoDbContext>>();
            builder.Services.AddTransient<IEntityRepository<FhirResource>, EntityRepository<FhirResource, MongoDbContext>>();
            builder.Services.AddTransient<IEntityRepository<PatientSubmissionEntryResourceMap>, EntityRepository<PatientSubmissionEntryResourceMap, MongoDbContext>>();
            builder.Services.AddTransient<IDatabase, Database>();

            builder.Services.AddTransient<IReportScheduledManager, ReportScheduledManager>();
            builder.Services.AddTransient<ISubmissionEntryManager, SubmissionEntryManager>();
            builder.Services.AddTransient<ISubmissionEntryQueries, SubmissionEntryQueries>();
            builder.Services.AddTransient<IResourceManager, ResourceManager>();

            builder.Services.AddTransient<ScheduledReportFactory>();
            builder.Services.AddTransient<MeasureReportSummaryFactory>();
            builder.Services.AddTransient<MeasureReportAggregator>();

            builder.Services.AddTransient<SubmitPayloadProducer>(sp =>
                new SubmitPayloadProducer(sp.GetRequiredService<IServiceScopeFactory>(), SubmitPayloadProducerMock.Object));
            builder.Services.AddTransient<DataAcquisitionRequestedProducer>(sp =>
                new DataAcquisitionRequestedProducer(sp.GetRequiredService<IServiceScopeFactory>(), DataAcquisitionRequestedProducerMock.Object));
            builder.Services.AddTransient<ReadyForValidationProducer>(sp =>
                new ReadyForValidationProducer(ReadyForValidationProducerMock.Object, sp.GetRequiredService<IServiceScopeFactory>()));
            builder.Services.AddTransient<AuditableEventOccurredProducer>(sp =>
                new AuditableEventOccurredProducer(sp.GetRequiredService<ILogger<AuditableEventOccurredProducer>>(), AuditableEventOccurredProducerMock.Object, serviceInformation));

            builder.Services.AddSingleton(Options.Create(blobSettings));
            builder.Services.AddSingleton<BlobStorageService>(BlobStorageMock.Object);

            builder.Services.AddTransient<ReportManifestProducer>(sp =>
                new ReportManifestProducer(
                    sp.GetRequiredService<ILogger<ReportManifestProducer>>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<MeasureReportAggregator>(),
                    TenantApiServiceMock.Object,
                    BlobStorageMock.Object,
                    sp.GetRequiredService<SubmitPayloadProducer>(),
                    sp.GetRequiredService<AuditableEventOccurredProducer>()
                ));

            builder.Services.AddTransient<EndOfReportPeriodJob>();
            builder.Services.AddTransient<PatientReportSubmissionBundler>();
            builder.Services.AddTransient<ValidationCompleteListener>();

            builder.Services.AddSingleton<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(ResourceEvaluatedConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(ResourceEvaluatedTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(ResourceEvaluatedDeadLetterHandlerMock.Object);
            builder.Services.AddSingleton<IKafkaConsumerFactory<string, ValidationCompleteValue>>(ValidationCompleteConsumerFactoryMock.Object);
            builder.Services.AddSingleton<ITransientExceptionHandler<string, ValidationCompleteValue>>(ValidationCompleteTransientHandlerMock.Object);
            builder.Services.AddSingleton<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(ValidationCompleteDeadLetterHandlerMock.Object);

            builder.Services.AddSingleton<IReportServiceMetrics>(ReportServiceMetricsMock.Object);
            builder.Services.AddSingleton<ITenantApiService>(TenantApiServiceMock.Object);

            //Add as a singleton so we can retrieve the instance for tests.
            builder.Services.AddSingleton<ResourceEvaluatedListener>();

            builder.Services.AddKeyedSingleton<ISchedulerFactory>("MongoScheduler", (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<ReportMongoSchedulerFactory>>();
                var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
                return new ReportMongoSchedulerFactory(scopeFactory, logger);
            });

            _host = builder.Build();

            ServiceProvider = _host.Services;
            ScopeFactory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        public void ResetMocks()
        {
            SubmitPayloadProducerMock.Reset();
            ReadyForValidationProducerMock.Reset();
            DataAcquisitionRequestedProducerMock.Reset();
            AuditableEventOccurredProducerMock.Reset();
            EvaluationRequestedProducerMock.Reset();
            ResourceEvaluatedConsumerFactoryMock.Reset();
            ResourceEvaluatedTransientHandlerMock.Reset();
            ResourceEvaluatedDeadLetterHandlerMock.Reset();
            ValidationCompleteConsumerFactoryMock.Reset();
            ValidationCompleteTransientHandlerMock.Reset();
            ValidationCompleteDeadLetterHandlerMock.Reset();
            BlobStorageMock.Reset();
            SchedulerFactoryMock.Reset();
            ReportServiceMetricsMock.Reset();
            TenantApiServiceMock.Reset();
        }

        public void Dispose()
        {
            _azuriteContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _mongoContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _host?.Dispose();
        }
    }
}