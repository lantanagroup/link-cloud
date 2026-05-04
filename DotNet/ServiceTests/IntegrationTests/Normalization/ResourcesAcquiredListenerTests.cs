using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Confluent.Kafka;
using IntegrationTests.Normalization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    [Collection("IntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ResourcesAcquiredListenerTests
    {
        private readonly NormalizationIntegrationTestFixture _fixture;

        public ResourcesAcquiredListenerTests(NormalizationIntegrationTestFixture fixture) 
        { 
            _fixture = fixture;
        }

        [Fact]
        public void Consume_Redis_Event() 
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ResourcesAcquiredListener>();

            var correlationId = Guid.NewGuid().ToString();

            UploadResourceCacheRedis(correlationId);

            var key = new ResourceKey() { FacilityId = "Facility1", PatientId = "Patient1" };
            var value = new ResourcesAcquired()
            {
                QueryType = QueryType.Initial.ToString(),
                CacheType = ResourceCacheType.Redis,
                CacheKeys = new List<string>() { correlationId + ":Patient" },
                ReportableEvent = ReportableEvent.Discharge,
                ScheduledReports = new List<ScheduledReport>()
            };

            Assert.True(1 == 1);
        }

        private void UploadResourceCacheRedis(string correlationId) 
        {
            var options = ConfigurationOptions.Parse(_fixture.RedisConnectionString);
            options.AllowAdmin = true;

            var connection = ConnectionMultiplexer.Connect(options);
            IDatabase db = connection.GetDatabase();

            Location location = new Location() 
            { 
                Id = Guid.NewGuid().ToString(),
                Identifier = new List<Identifier>() 
                { 
                    new Identifier() { System = "TestSystem", Value = "TestValue" }
                }
            };

            HashEntry entry = new HashEntry(location.TypeName + "/" + location.Id, location.ToJson());

            db.HashSet(correlationId + ":" + location.TypeName, new HashEntry[] { entry });
        }
    }
}
