using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RequestStatusEnum = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services
{
    [Collection("DataAcquisitionIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ReferenceResourceServiceTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public ReferenceResourceServiceTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private ReferenceResourceService CreateService(IServiceScope scope)
        {
            var logger = new Mock<ILogger<ReferenceResourceService>>().Object;
            var refMgr = scope.ServiceProvider.GetRequiredService<IReferenceResourcesManager>();
            var refQueries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var kafkaProducer = _fixture.ResourceAcquiredProducerMock.Object;

            var metrics = new Mock<IDataAcquisitionServiceMetrics>();
            metrics.Setup(m => m.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>()));
            metrics.Setup(m => m.IncrementResourceAcquiredCounter(It.IsAny<List<KeyValuePair<string, object?>>>()));

            var daLogMgr = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var daLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            var fhirQueryManager = scope.ServiceProvider.GetRequiredService<IFhirQueryManager>();

            return new ReferenceResourceService(
                logger,
                refMgr,
                refQueries,
                kafkaProducer,
                metrics.Object,
                daLogMgr,
                daLogQueries,
                fhirQueryManager);
        }

        [Fact]
        public async Task ProcessReferences_EnsuresQueryTypeConsistency()
        {
            // Arrange
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var service = CreateService(scope);

            var facilityId = "TestFacility";
            var correlationId = Guid.NewGuid().ToString();

            // 1. Create a parent log for the main resource
            var parentLog = await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ScheduledReport = new ScheduledReport { ReportTrackingId = "TestReport", StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Search,
                Status = RequestStatusEnum.Pending,
                Priority = AcquisitionPriority.Normal
            });

            // 2. Create a log for the reference resource (e.g. Patient) that will be updated
            var referenceLog = await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ScheduledReport = new ScheduledReport { ReportTrackingId = "TestReport", StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Search,
                Status = RequestStatusEnum.Pending,
                Priority = AcquisitionPriority.Normal,
                FhirQuery = new List<CreateFhirQueryModel>
                {
                    new CreateFhirQueryModel
                    {
                        FacilityId = facilityId,
                        QueryType = FhirQueryType.Search,
                        ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                        IsReference = true
                    }
                }
            });

            var logModel = await scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>().GetAsync(parentLog.Id);
            var refResources = new List<ResourceReference>
            {
                new ResourceReference { Reference = "Patient/test-patient-id" }
            };

            // Act
            await service.ProcessReferences(logModel, refResources);

            // Assert
            var updatedLog = await scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>().GetAsync(referenceLog.Id);
            Assert.NotNull(updatedLog);
            Assert.NotEmpty(updatedLog.FhirQuery);

            var fhirQuery = updatedLog.FhirQuery.First();
            Assert.Equal(updatedLog.QueryType, fhirQuery.QueryType);
            Assert.Contains("test-patient-id", fhirQuery.IdQueryParameterValues);
        }
    }
}
