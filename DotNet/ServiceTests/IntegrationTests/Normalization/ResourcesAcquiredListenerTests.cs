using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Confluent.Kafka;
using IntegrationTests.Normalization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
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
        public async Task Consume_Redis_Event() 
        {
            _fixture.ResourcesNormalizedProducerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ResourcesAcquiredListener>();

            var correlationId = Guid.NewGuid().ToString();

            UploadResourceCacheRedis(correlationId);

            string patientId = "Patient1";

            var key = new ResourceKey() { FacilityId = "Facility1", PatientId = patientId };
            var value = new ResourcesAcquiredValue()
            {
                QueryType = QueryType.Initial.ToString(),
                CacheType = ResourceCacheType.Redis,
                CacheKeys = new List<string>() { correlationId + ":Patient" },
                ReportableEvent = ReportableEvent.Discharge.ToString(),
                ScheduledReports = new List<ScheduledReport>() { 
                    new ScheduledReport() 
                    { 
                        ReportTrackingId = "Report1",
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now,
                        Frequency = Frequency.Discharge,
                        ReportTypes = new List<string>() { "NHSNAcuteCareHospitalMonthlyInitialPopulation" }
                    } 
                }
            };
            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId) } };

            var consumeResult = new ConsumeResult<ResourceKey, ResourcesAcquiredValue>
            {
                Message = new Message<ResourceKey, ResourcesAcquiredValue> { Key = key, Value = value, Headers = headers }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            _fixture.ResourcesNormalizedProducerMock.Verify(
                p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.Is<Message<ResourceKey, ResourcesNormalizedValue>>(m => m.Key.PatientId == patientId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
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
