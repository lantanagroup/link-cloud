using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
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
            var readFhirCommand = new Mock<IReadFhirCommand>().Object;
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            return new ReferenceResourceService(
                logger,
                refQueries,
                refMgr,
                readFhirCommand,
                dbContext);
        }

        [Fact]
        public async Task ProcessReferences_CachesAndLinksExistingResources()
        {
            // Arrange
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var refMgr = scope.ServiceProvider.GetRequiredService<IReferenceResourcesManager>();

            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"TestFacility_{tag}";
            var correlationId = Guid.NewGuid().ToString();
            var reportTrackingId = $"TestReport_{tag}";

            // Pre-seed a canonical reference resource
            await refMgr.CreateBatchAsync(new[]
            {
                new CreateReferenceResourcesModel
                {
                    FacilityId = facilityId,
                    ResourceId = "test-loc-1",
                    ResourceType = "Location",
                    ReferenceResource = "{}",
                    QueryPhase = QueryPhase.Referential
                }
            });

            var parentLog = await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ScheduledReport = new ScheduledReport { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Search,
                Status = RequestStatusEnum.Pending,
                Priority = AcquisitionPriority.Normal
            });

            var logModel = await scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>().GetAsync(parentLog.Id);

            var refResources = new List<ResourceReference>
            {
                new ResourceReference { Reference = "Location/test-loc-1" }
            };

            var fhirQueryConfig = new FhirQueryConfigurationModel
            {
                FhirServerBaseUrl = "http://localhost/fhir"
            };

            var service = CreateService(scope);

            // Act — the resource already exists in cache, so no FHIR fetch needed
            await service.ProcessReferences(logModel, refResources, fhirQueryConfig);

            // Assert — the resource should be linked to the log via junction table
            var linkedResources = await dbContext.DataAcquisitionLogs
                .Where(l => l.Id == parentLog.Id)
                .SelectMany(l => l.ReferenceResources)
                .ToListAsync();

            Assert.Single(linkedResources);
            Assert.Equal("test-loc-1", linkedResources[0].ResourceId);
        }
    }
}
