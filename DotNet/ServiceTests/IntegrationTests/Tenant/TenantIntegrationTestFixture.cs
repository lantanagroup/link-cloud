// TenantIntegrationTestFixture.cs
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Commands;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models;
using LantanaGroup.Link.Tenant.Repository;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
                    services.AddScoped<IFacilityConfigurationService, FacilityConfigurationService>();

                    // Add HttpClient (can be mocked further in tests if needed)
                    services.AddHttpClient();

                    // Configure IOptions<ServiceRegistry>
                    services.Configure<ServiceRegistry>(options =>
                    {
                        options.MeasureServiceUrl = "http://test-measure-service";
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

                    // Stub producer for AuditEventCommand
                    services.AddSingleton<IProducer<string, object>>(new StubProducer<string, object>());

                    // Add the real CreateAuditEventCommand
                    services.AddSingleton<CreateAuditEventCommand>();
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
    }
}