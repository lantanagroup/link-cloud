using Azure.Storage.Blobs;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using System.Text;
using Testcontainers.Azurite;
using ResourceType = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
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
        public async Task ConsumeRedisEvent() 
        {
            _fixture.ResourcesNormalizedProducerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ResourcesAcquiredListener>();
            var dbContext = scope.ServiceProvider.GetRequiredService<NormalizationDbContext>();

            string facilityId = "Facility1";
            string patientId = "Patient1";
            string correlationId = Guid.NewGuid().ToString();

            await LoadFacilityLocationConfig(facilityId, scope);

            UploadResourceCacheRedis(correlationId);

            var key = new ResourceKey() { FacilityId = facilityId, PatientId = patientId };
            var value = new ResourcesAcquiredValue()
            {
                QueryType = QueryType.Initial.ToString(),
                CacheType = ResourceCacheType.Redis,
                CacheKeys = new List<string>() { correlationId + ":Location" },
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

        [Fact]
        public async Task ConsumeABSEvent()
        {
            _fixture.ResourcesNormalizedProducerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();

            var listener = scope.ServiceProvider.GetRequiredService<ResourcesAcquiredListener>();
            var dbContext = scope.ServiceProvider.GetRequiredService<NormalizationDbContext>();

            string facilityId = "Facility1";
            string patientId = "Patient1";
            var correlationId = Guid.NewGuid().ToString();

            await LoadFacilityLocationConfig(facilityId, scope);

            UploadResourceCacheABS(correlationId);

            var key = new ResourceKey() { FacilityId = facilityId, PatientId = patientId };
            var value = new ResourcesAcquiredValue()
            {
                QueryType = QueryType.Initial.ToString(),
                CacheType = ResourceCacheType.ABS,
                CacheKeys = new List<string>() { correlationId + ":Location" },
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

            HashEntry entry = new HashEntry(location.TypeName + ":" + location.Id, location.ToJson());

            db.HashSet(correlationId + ":" + location.TypeName, new HashEntry[] { entry });
        }

        private void UploadResourceCacheABS(string correlationId)
        {
            var blobServiceClient = new BlobServiceClient(_fixture.AzuriteConnectionString);
            BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient("cache");
            _containerClient.CreateIfNotExists();

            Location location = new Location()
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = new List<Identifier>()
                {
                    new Identifier() { System = "TestSystem", Value = "TestValue" }
                }
            };

            var blobClient = _containerClient.GetBlobClient(correlationId + ":Location");

            StringBuilder sb = new StringBuilder();

            sb.Append(location.TypeName + "/" + location.Id);
            sb.Append(Environment.NewLine);
            sb.Append(location.ToJson());
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

            blobClient.Upload(stream, overwrite: true);
        }

        private async Task LoadFacilityLocationConfig(string facilityId, IServiceScope scope) 
        {
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var result = await operationManager.CreateOperation(new CreateOperationModel()
            {
                FacilityId = facilityId,
                OperationJson = "{\"OperationType\":4,\"Name\":\"Copy Location Operation\",\"Description\":\"Copies each Location Identifier 'System' and 'Value' fields into Location.Type as a CodeableConcept\"}",
                OperationType = OperationType.CopyLocation.ToString(),
                IsDisabled = false,
                Name = "Copy Location Operation",
                ResourceTypes = new List<string>() { "Location" }
            });

            var operation = (OperationModel)result.ObjectResult;

            operationManager.CreateOperationSequences(new CreateOperationSequencesModel()
            {
                FacilityId = facilityId,
                ResourceType = "Location",
                OperationSequences = new List<CreateOperationSequenceModel>()
                {
                    new CreateOperationSequenceModel()
                    {
                        OperationId = operation.Id,
                        Sequence = 1
                    }
                }
            });
        }
    }
}
