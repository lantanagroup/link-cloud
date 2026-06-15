using Confluent.Kafka;
using LantanaGroup.Link.Account.Persistence.Interceptors;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Data.Repository;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Spi;
using System.Diagnostics;
using System.Net;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    public class TenantIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;

        public TenantIntegrationTestFixture()
        {
            var builder = Host.CreateApplicationBuilder();

            var assemblyVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

            // Register ServiceInformation using the extension method
            var serviceInformation = builder.SetupServiceInformation(TenantConstants.ServiceName, assemblyVersion);

            // Register the interceptor and DbContext
            builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();
            string dbName = $"testdb_{Guid.NewGuid()}.db";
            var _dbPath = Path.Combine(Path.GetTempPath(), dbName);
            var sqliteConnectionString = $"Data Source={_dbPath};";

            builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
                options.UseSqlite(sqliteConnectionString);
                options.AddInterceptors(updateBaseEntityInterceptor);
            });

            // Configure Quartz
            builder.Services.RegisterQuartzDatabaseInTest();

            // Register repositories
            builder.Services.AddScoped<IEntityRepository<Facility>, FacilityRepository>();

            // Register the service
            builder.Services.AddScoped<IFacilityQueries, FacilityQueries>();
            builder.Services.AddScoped<IFacilityManager, FacilityManager>();

            // Add IHttpClientFactory
            builder.Services.AddHttpClient();

            // Add HttpClient as singleton with stub handler
            var stubHandler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
            var stubClient = new HttpClient(stubHandler);
            builder.Services.AddSingleton<HttpClient>(stubClient);

            // Configure IOptions<ServiceRegistry>
            builder.Services.Configure<ServiceRegistry>(options =>
            {
                options.MeasureServiceUrl = "http://test-measure-service";
                options.ReportServiceUrl = "http://test-report-service";
            });

            // Configure IOptions<MeasureConfig> (disable external measure check for simplicity)
            builder.Services.Configure<MeasureConfig>(options =>
            {
                options.CheckIfMeasureExists = false;
            });

            // Configure IOptions<LinkTokenServiceSettings> (dummy values)
            builder.Services.Configure<LinkTokenServiceSettings>(options =>
            {
                options.SigningKey = "dummy-signing-key";
            });

            // Stub ICreateSystemToken (returns a dummy token)
            builder.Services.AddSingleton<ICreateSystemToken, StubCreateSystemToken>();

            // Configure IOptions<LinkBearerServiceOptions> (dummy values)
            builder.Services.Configure<LinkBearerServiceOptions>(options =>
            {
                options.AllowAnonymous = true;
            });

            builder.Services.Configure<FacilityIdSettings>(options =>
            {
                options.NumericOnlyFacilityId = false;
            });

            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FacilityIdSettings>>().Value);

            // Stub producer for AuditEventCommand
            builder.Services.AddSingleton<IProducer<string, AuditEventMessage>>(new StubProducer<string, AuditEventMessage>());

            // Add the real CreateAuditEventCommand
            builder.Services.AddSingleton<CreateAuditEventCommand>();

            // Add logging
            builder.Services.AddLogging();

            // ReportScheduledJob depends on ITenantServiceMetrics, which needs IMeterFactory (AddMetrics)
            builder.Services.AddMetrics();
            builder.Services.AddSingleton<ITenantServiceMetrics, TenantServiceMetrics>();

            // Add job classes
            builder.Services.AddTransient<ReportScheduledJob>();

            // Add ScheduleService
            builder.Services.AddScoped<ScheduleService>();

            builder.Services.AddSingleton<ScheduleService>();
            //builder.Services.AddHostedService(sp => sp.GetRequiredService<ScheduleService>());

            // Add Kafka producer factories (mirrors Program.cs):
            //   <string, GenerateReportValue> -> FacilityController (ad-hoc report path)
            //   <string, object>              -> ReportScheduledJob / RetentionCheckScheduledJob
            builder.Services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, StubKafkaProducerFactory<string, GenerateReportValue>>();
            builder.Services.AddTransient<IKafkaProducerFactory<string, object>, StubKafkaProducerFactory<string, object>>();

            // Add AutoMapper
            builder.Services.AddAutoMapper(cfg =>
            {
                cfg.CreateMap<Facility, FacilityModel>();
                cfg.CreateMap<PagedConfigModel<Facility>, PagedFacilityConfigDto>();
                cfg.CreateMap<ScheduledReportModel, TenantScheduledReportConfig>();

                cfg.CreateMap<FacilityModel, Facility>();
                cfg.CreateMap<PagedFacilityConfigDto, PagedConfigModel<Facility>>();
                cfg.CreateMap<TenantScheduledReportConfig, ScheduledReportModel>();
            });

            _host = builder.Build();

            // Start the host
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;

            // Ensure database is created and set PRAGMAs
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            // TenantDbContext is scoped, so it must be resolved from a scope, not the root provider
            using (var disposeScope = ServiceProvider.CreateScope())
            {
                var ctx = disposeScope.ServiceProvider.GetRequiredService<TenantDbContext>();
                ctx.Database.EnsureDeleted();   // forces the in-memory store to clear
            }

            // ---- QUARTZ: immediate shutdown (no waiting) ----
            var scheduler = ServiceProvider.GetService<IScheduler>();
            scheduler?.Shutdown(waitForJobsToComplete: false).GetAwaiter().GetResult();

            // ---- HOST: short timeout ----
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                _host.StopAsync(cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { /* ignore – already stopped */ }

            var threads = Process.GetCurrentProcess().Threads;
            Console.WriteLine($"Threads alive: {threads.Count}");
            foreach (ProcessThread t in threads)
                Console.WriteLine($"  ID:{t.Id} StartTime:{t.StartTime} State:{t.ThreadState}");

            _host.Dispose();
        }

        private class StubCreateSystemToken : ICreateSystemToken
        {
            public Task<string> ExecuteAsync(string signingKey, int expirationMinutes)
            {
                return Task.FromResult("dummy-token");
            }
        }

        private class StubProducer<TKey, TValue> : IProducer<TKey, TValue>
        {
            public Handle Handle => null;

            public string Name => "stub";

            public Task<DeliveryResult<TKey, TValue>> ProduceAsync(string topic, Message<TKey, TValue> message, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new DeliveryResult<TKey, TValue>
                {
                    Topic = topic,
                    Partition = new Partition(0),
                    Offset = new Offset(0),
                    Status = PersistenceStatus.Persisted
                });
            }

            public void Produce(string topic, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>> deliveryHandler = null)
            {
                deliveryHandler?.Invoke(new DeliveryReport<TKey, TValue> { Status = PersistenceStatus.Persisted });
            }

            public int AddBrokers(string brokers) => 0;

            public void Flush(TimeSpan timeout) { }

            public int Flush(CancellationToken cancellationToken = default) => 0;

            public int Poll(TimeSpan timeout) => 0;

            public void Dispose() { }

            public Task<DeliveryResult<TKey, TValue>> ProduceAsync(TopicPartition topicPartition, Message<TKey, TValue> message, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public void Produce(TopicPartition topicPartition, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>> deliveryHandler = null)
            {
                throw new NotImplementedException();
            }

            int IProducer<TKey, TValue>.Flush(TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            void IProducer<TKey, TValue>.Flush(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public void InitTransactions(TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            public void BeginTransaction()
            {
                throw new NotImplementedException();
            }

            public void CommitTransaction(TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            public void CommitTransaction()
            {
                throw new NotImplementedException();
            }

            public void AbortTransaction(TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            public void AbortTransaction()
            {
                throw new NotImplementedException();
            }

            public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            public void SetSaslCredentials(string username, string password)
            {
                throw new NotImplementedException();
            }
        }

        private class TestJobFactory : IJobFactory
        {
            private readonly IServiceProvider _provider;

            public TestJobFactory(IServiceProvider provider)
            {
                _provider = provider;
            }

            public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
            {
                return (IJob)_provider.GetService(bundle.JobDetail.JobType);
            }

            public void ReturnJob(IJob job) { }
        }

        private class StubKafkaProducerFactory<TKey, TValue> : IKafkaProducerFactory<TKey, TValue>
        {
            public IProducer<string, AuditEventMessage> CreateAuditEventProducer(bool useOpenTelemetry = true)
            {
                throw new NotImplementedException();
            }

            public IProducer<TKey, TValue> CreateProducer(ProducerConfig config = null)
            {
                return new StubProducer<TKey, TValue>();
            }

            public IProducer<TKey, TValue> CreateProducer(ProducerConfig config, ISerializer<TKey>? keySerializer = null, ISerializer<TValue>? valueSerializer = null, bool useOpenTelemetry = true)
            {
                return new StubProducer<TKey, TValue>();
            }
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public StubHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}