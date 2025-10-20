using Azure.Storage.Blobs;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq.Expressions;
using Testcontainers.Azurite;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [CollectionDefinition("ReportIntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<ReportIntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class ReportIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;
        private readonly AzuriteContainer _azuriteContainer;
        public static Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> SubmitPayloadProducerMock { get; private set; }
        public static Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>> ReadyForValidationProducerMock { get; private set; }

        public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();

        public ReportIntegrationTestFixture()
        {
            SubmitPayloadProducerMock = new Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>>();
            ReadyForValidationProducerMock = new Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>>();

            _azuriteContainer = new AzuriteBuilder()
                .WithImage("mcr.microsoft.com/azure-storage/azurite")
                .Build();
            _azuriteContainer.StartAsync().GetAwaiter().GetResult();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register logging for real ILogger instances, but mock ILogger<ValidationCompleteListener> and ILogger<ResourceEvaluatedListener>
                    services.AddLogging();
                    services.AddTransient<ILogger<ValidationCompleteListener>>(sp => Mock.Of<ILogger<ValidationCompleteListener>>());
                    services.AddTransient<ILogger<ResourceEvaluatedListener>>(sp => Mock.Of<ILogger<ResourceEvaluatedListener>>());
                    services.AddTransient<ILogger<UseLatestStrategy>>(sp => Mock.Of<ILogger<UseLatestStrategy>>());

                    // Register InMemoryDatabase as Singleton
                    services.AddSingleton<IDatabase, InMemoryDatabase>();

                    // Register actual internal components
                    services.AddTransient<MeasureReportAggregator>();
                    services.AddTransient<SubmitPayloadProducer>();
                    services.AddTransient<DataAcquisitionRequestedProducer>();
                    services.AddTransient<ReadyForValidationProducer>();
                    services.AddTransient<ReportManifestProducer>();
                    services.AddTransient<EndOfReportPeriodJob>();
                    services.AddTransient<PatientReportSubmissionBundler>();
                    services.AddTransient<ValidationCompleteListener>();
                    services.AddTransient<ResourceEvaluatedListener>();

                    // Register real managers
                    services.AddTransient<IResourceManager, ResourceManager>();
                    services.AddTransient<ISubmissionEntryManager, SubmissionEntryManager>();
                    services.AddTransient<IReportScheduledManager, ReportScheduledManager>();

                    // Factories
                    services.AddTransient<MeasureReportSummaryFactory>();
                    services.AddTransient<ResourceSummaryFactory>();
                    services.AddTransient<ScheduledReportFactory>();

                    // BlobStorageService dependencies (use Azurite emulator via Testcontainers)
                    services.AddSingleton<IOptions<BlobStorageSettings>>(sp =>
                    {
                        var settings = new BlobStorageSettings
                        {
                            ConnectionString = _azuriteContainer.GetConnectionString(),
                            BlobContainerName = "report-test-container"
                        };
                        return Options.Create(settings);
                    });
                    services.AddTransient<BlobStorageService>();

                    // Mock Metrics for PatientReportSubmissionBundler
                    services.AddTransient<IReportServiceMetrics>(sp => Mock.Of<IReportServiceMetrics>());

                    // Mocks for external dependencies
                    services.AddTransient(sp =>
                    {
                        var tenantApiServiceMock = new Mock<ITenantApiService>();
                        tenantApiServiceMock.Setup(t => t.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });
                        return tenantApiServiceMock.Object;
                    });

                    // Mock Kafka consumer factories to produce consumers that can commit
                    var mockFactoryValidation = new Mock<IKafkaConsumerFactory<string, ValidationCompleteValue>>();
                    var mockConsumerValidation = new Mock<IConsumer<string, ValidationCompleteValue>>();
                    mockConsumerValidation.Setup(c => c.Commit(It.IsAny<ConsumeResult<string, ValidationCompleteValue>>())).Verifiable();
                    mockFactoryValidation.Setup(f => f.CreateConsumer(It.IsAny<ConsumerConfig>(), null, null)).Returns(mockConsumerValidation.Object);
                    services.AddTransient(sp => mockFactoryValidation.Object);

                    var mockFactoryResource = new Mock<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
                    var mockConsumerResource = new Mock<IConsumer<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
                    mockConsumerResource.Setup(c => c.Commit(It.IsAny<ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue>>())).Verifiable();
                    mockFactoryResource.Setup(f => f.CreateConsumer(It.IsAny<ConsumerConfig>(), null, null)).Returns(mockConsumerResource.Object);
                    services.AddTransient(sp => mockFactoryResource.Object);

                    services.AddScoped(sp => Mock.Of<ITransientExceptionHandler<string, ValidationCompleteValue>>());
                    services.AddScoped(sp => Mock.Of<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>());
                    services.AddScoped(sp => Mock.Of<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>());
                    services.AddScoped(sp => Mock.Of<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>());

                    // Mock Kafka producers with correct generic types
                    services.AddTransient(sp => Mock.Of<IProducer<string, DataAcquisitionRequestedValue>>());
                    services.AddTransient<IProducer<ReadyForValidationKey, ReadyForValidationValue>>(sp => ReadyForValidationProducerMock.Object);
                    services.AddTransient<IProducer<SubmitPayloadKey, SubmitPayloadValue>>(sp => SubmitPayloadProducerMock.Object);

                    // Register repositories as Scoped delegates (pulling from the Singleton IDatabase)
                    services.AddScoped<IBaseEntityRepository<PatientResourceModel>>(sp => sp.GetRequiredService<IDatabase>().PatientResourceRepository);
                    services.AddScoped<IBaseEntityRepository<SharedResourceModel>>(sp => sp.GetRequiredService<IDatabase>().SharedResourceRepository);
                    services.AddScoped<IBaseEntityRepository<ReportScheduleModel>>(sp => sp.GetRequiredService<IDatabase>().ReportScheduledRepository);
                    services.AddScoped<IBaseEntityRepository<MeasureReportSubmissionEntryModel>>(sp => sp.GetRequiredService<IDatabase>().SubmissionEntryRepository);
                })
                .Build();

            ServiceProvider = _host.Services;

            // Ensure the Azurite container exists and is clean
            SetupAzuriteContainer().GetAwaiter().GetResult();

            using var scope = ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            InitializeDatabase(database).GetAwaiter().GetResult();
        }

        private async Task SetupAzuriteContainer()
        {
            var settings = ServiceProvider.GetRequiredService<IOptions<BlobStorageSettings>>().Value;
            var containerClient = new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);
            await containerClient.DeleteIfExistsAsync();
            await containerClient.CreateAsync();
        }

        public void Dispose()
        {
            _azuriteContainer.StopAsync().GetAwaiter().GetResult();
            _azuriteContainer.DisposeAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        private async Task InitializeDatabase(IDatabase database)
        {
            // Seed data if necessary
            await Task.CompletedTask;
        }
    }

    public class InMemoryDatabase : IDatabase
    {
        public IBaseEntityRepository<PatientResourceModel> PatientResourceRepository { get; set; } = new InMemoryEntityRepository<PatientResourceModel>();
        public IBaseEntityRepository<SharedResourceModel> SharedResourceRepository { get; set; } = new InMemoryEntityRepository<SharedResourceModel>();
        public IBaseEntityRepository<ReportScheduleModel> ReportScheduledRepository { get; set; } = new InMemoryEntityRepository<ReportScheduleModel>();
        public IBaseEntityRepository<MeasureReportSubmissionEntryModel> SubmissionEntryRepository { get; set; } = new InMemoryEntityRepository<MeasureReportSubmissionEntryModel>();
    }

    public class InMemoryEntityRepository<T> : IBaseEntityRepository<T> where T : class, new()
    {
        private readonly List<T> _items = new List<T>();

        public T Add(T entity)
        {
            return AddAsync(entity).GetAwaiter().GetResult();
        }

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            var idProp = typeof(T).GetProperty("Id");
            if (idProp != null && idProp.PropertyType == typeof(string) && string.IsNullOrEmpty((string)idProp.GetValue(entity)))
            {
                idProp.SetValue(entity, Guid.NewGuid().ToString());
            }
            _items.Add(entity);
            return await Task.FromResult(entity);
        }

        public T Get(object id)
        {
            return GetAsync(id).GetAwaiter().GetResult();
        }

        public async Task<T> GetAsync(object id, CancellationToken cancellationToken = default)
        {
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null) throw new InvalidOperationException("No Id property");
            return await Task.FromResult(_items.FirstOrDefault(e => idProp.GetValue(e).Equals(id)));
        }

        public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_items.ToList());
        }

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.Where(compiled).ToList());
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.First(compiled));
        }

        public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.SingleOrDefault(compiled));
        }

        public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.Single(compiled));
        }

        public T Update(T entity)
        {
            return UpdateAsync(entity).GetAwaiter().GetResult();
        }

        public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null) throw new InvalidOperationException("No Id property");
            var id = idProp.GetValue(entity);
            var existing = await GetAsync(id, cancellationToken);
            if (existing != null)
            {
                _items.Remove(existing);
                _items.Add(entity);
                return entity;
            }
            throw new KeyNotFoundException();
        }

        public void Delete(object id)
        {
            DeleteAsync(id).GetAwaiter().GetResult();
        }

        public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            var entity = await GetAsync(id, cancellationToken);
            if (entity != null)
            {
                _items.Remove(entity);
            }
        }

        public async Task DeleteAsync(T? entity, CancellationToken cancellationToken)
        {
            if (entity != null)
            {
                _items.Remove(entity);
            }
            await Task.CompletedTask;
        }

        public async Task RemoveAsync(T entity)
        {
            if (entity != null)
            {
                _items.Remove(entity);
            }
            await Task.CompletedTask;
        }

        public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var query = _items.Where(compiled).AsQueryable();

            if (!string.IsNullOrEmpty(sortBy))
            {
                var prop = typeof(T).GetProperty(sortBy);
                if (prop != null)
                {
                    if (sortOrder == SortOrder.Descending)
                    {
                        query = query.OrderByDescending(e => prop.GetValue(e));
                    }
                    else
                    {
                        query = query.OrderBy(e => prop.GetValue(e));
                    }
                }
            }

            var total = query.Count();
            var paged = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var metadata = new PaginationMetadata
            {
                TotalCount = total,
                PageSize = pageSize,
                PageNumber = pageNumber
            };

            return await Task.FromResult((paged, metadata));
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return await Task.FromResult(_items.Any(compiled));
        }

        public async Task<HealthCheckResult> HealthCheck(int eventId)
        {
            return await Task.FromResult(HealthCheckResult.Healthy());
        }

        public void StartTransaction() { }

        public void CommitTransaction() { }

        public void RollbackTransaction() { }

        public async Task StartTransactionAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }
    }
}