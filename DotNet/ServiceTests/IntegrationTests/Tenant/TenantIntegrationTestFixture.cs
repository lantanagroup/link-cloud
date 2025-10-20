using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Data.Repository;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System.Collections.Specialized;
using System.Net;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    [CollectionDefinition("TenantIntegrationTests")]
    public class DatabaseCollection : ICollectionFixture<TenantIntegrationTestFixture>
    {
        // This class is a marker for the collection
    }

    public class TenantIntegrationTestFixture : IDisposable
    {
        public IServiceProvider ServiceProvider { get; private set; }
        private readonly IHost _host;

        public TenantIntegrationTestFixture()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add in-memory database with warning suppression
                    services.AddSingleton<UpdateBaseEntityInterceptor>();
                    services.AddDbContext<TenantDbContext>((sp, options) =>
                    {
                        var updateBaseEntityInterceptor = sp.GetRequiredService<UpdateBaseEntityInterceptor>();
                        options.UseInMemoryDatabase("TestDatabase");
                        options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                        options.AddInterceptors(updateBaseEntityInterceptor);
                    });

                    // Register repositories
                    services.AddScoped<IEntityRepository<Facility>, FacilityRepository>();

                    // Register the service
                    services.AddScoped<IFacilityQueries, FacilityQueries>();
                    services.AddScoped<IFacilityManager, FacilityManager>();

                    // Add IHttpClientFactory
                    services.AddHttpClient();

                    // Add HttpClient as singleton with stub handler
                    var stubHandler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
                    var stubClient = new HttpClient(stubHandler);
                    services.AddSingleton<HttpClient>(stubClient);

                    // Configure IOptions<ServiceRegistry>
                    services.Configure<ServiceRegistry>(options =>
                    {
                        options.MeasureServiceUrl = "http://test-measure-service";
                        options.ReportServiceUrl = "http://test-report-service";
                    });

                    // Configure IOptions<MeasureConfig> (disable external measure check for simplicity)
                    services.Configure<MeasureConfig>(options =>
                    {
                        options.CheckIfMeasureExists = false;
                    });

                    // Configure IOptions<LinkTokenServiceSettings> (dummy values)
                    services.Configure<LinkTokenServiceSettings>(options =>
                    {
                        options.SigningKey = "dummy-signing-key";
                    });

                    // Stub ICreateSystemToken (returns a dummy token)
                    services.AddSingleton<ICreateSystemToken, StubCreateSystemToken>();

                    // Configure IOptions<LinkBearerServiceOptions> (dummy values)
                    services.Configure<LinkBearerServiceOptions>(options =>
                    {
                        options.AllowAnonymous = true;
                    });

                    services.Configure<FacilityIdSettings>(options =>
                    {
                        options.NumericOnlyFacilityId = false;
                    });

                    services.AddSingleton(sp => sp.GetRequiredService<IOptions<FacilityIdSettings>>().Value);

                    // Stub producer for AuditEventCommand
                    services.AddSingleton<IProducer<string, AuditEventMessage>>(new StubProducer<string, AuditEventMessage>());

                    // Add the real CreateAuditEventCommand
                    services.AddSingleton<CreateAuditEventCommand>();

                    // Add logging
                    services.AddLogging();

                    // Add ScheduleService
                    services.AddScoped<ScheduleService>();

                    // Add job classes
                    services.AddTransient<ReportScheduledJob>();
                    services.AddTransient<RetentionCheckScheduledJob>();

                    // Add test job factory
                    services.AddSingleton<IJobFactory, TestJobFactory>();

                    // Configure Quartz with RAMJobStore
                    var quartzProps = new NameValueCollection
                    {
                        ["quartz.scheduler.instanceName"] = "TestScheduler",
                        ["quartz.scheduler.instanceId"] = "AUTO",
                        ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
                        ["quartz.threadPool.threadCount"] = "5",
                        ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
                        ["quartz.serializer.type"] = "json"
                    };
                    services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory(quartzProps));

                    // Add Kafka producer factory for GenerateReportValue
                    services.AddTransient<IKafkaProducerFactory<string, GenerateReportValue>, StubKafkaProducerFactory<string, GenerateReportValue>>();

                    // Add AutoMapper
                    services.AddAutoMapper(cfg =>
                    {
                        cfg.CreateMap<Facility, FacilityModel>();
                        cfg.CreateMap<PagedConfigModel<Facility>, PagedFacilityConfigDto>();
                        cfg.CreateMap<ScheduledReportModel, TenantScheduledReportConfig>();

                        cfg.CreateMap<FacilityModel, Facility>();
                        cfg.CreateMap<PagedFacilityConfigDto, PagedConfigModel<Facility>>();
                        cfg.CreateMap<TenantScheduledReportConfig, ScheduledReportModel>();
                    });
                })
                .Build();

            // Start the host
            _host.StartAsync().GetAwaiter().GetResult();
            ServiceProvider = _host.Services;
        }

        public void Dispose()
        {
            _host.StopAsync().GetAwaiter().GetResult();
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