using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ScheduledFrequency = LantanaGroup.Link.Shared.Application.Models.Frequency;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services
{
    [Collection("DataAcquisitionIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ReferenceResourceServiceTests
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

            return new ReferenceResourceService(
                logger,
                refQueries,
                refMgr);
        }

        [Fact]
        public async Task ProcessReferences_StagesDiscoveredIdsIntoPendingReferenceIds()
        {
            // Arrange
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var refMgr = scope.ServiceProvider.GetRequiredService<IReferenceResourcesManager>();

            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"TestFacility_{tag}";
            var correlationId = Guid.NewGuid().ToString();
            var reportTrackingId = Guid.NewGuid().ToString();

            // Pre-seed a canonical reference resource — under the new flow this has no
            // effect on the primary-phase ProcessReferences behavior (cache lookups and
            // junction linking are deferred to the promoter / referential log execution).
            await refMgr.CreateBatchAsync(new[]
            {
                new CreateReferenceResourcesModel
                {
                    FacilityId = facilityId,
                    ResourceId = "test-loc-1",
                    ResourceType = "Location",
                    ReferenceResource = "{\"resourceType\":\"Location\",\"id\":\"test-loc-1\",\"status\":\"active\"}",
                    QueryPhase = QueryPhase.Referential
                }
            });

            dbContext.ScheduledReports.Add(new ScheduledReportEntity
            {
                ReportTrackingId = Guid.Parse(reportTrackingId),
                Frequency = ScheduledFrequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            var parentLog = await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ReportTrackingId = reportTrackingId,
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Search,
                Status = RequestStatus.Pending,
                Priority = AcquisitionPriority.Normal,
                ReportableEvent = ReportableEvent.Adhoc
            });

            var logModel = await scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>().GetAsync(parentLog.Id);

            var refResources = new List<ResourceReference>
            {
                new ResourceReference { Reference = "Location/test-loc-1" },
                // duplicate reference should be deduped by the staging unique index
                new ResourceReference { Reference = "Location/test-loc-1" },
                new ResourceReference { Reference = "Medication/med-9" }
            };

            var fhirQueryConfig = new FhirQueryConfigurationModel
            {
                FhirServerBaseUrl = "http://localhost/fhir"
            };

            var service = CreateService(scope);

            // Act — new flow: only stages ids onto PendingReferenceIds, no FHIR reads,
            // no junction links on the primary log, no Kafka publish.
            await service.ProcessReferences(logModel, refResources, fhirQueryConfig);

            // Assert — primary log has NO reference-resource junction rows.
            var linkedResources = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .Where(l => l.Id == parentLog.Id)
                .SelectMany(l => l.ReferenceResources)
                .ToListAsync();

            Assert.Empty(linkedResources);

            // Assert — staging table has one row per distinct (ResourceType, ResourceId)
            // for this correlation.
            var pending = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .Where(p => p.FacilityId == facilityId && p.CorrelationId == correlationId)
                .OrderBy(p => p.ResourceType).ThenBy(p => p.ResourceId)
                .ToListAsync();

            Assert.Equal(2, pending.Count);
            Assert.Equal("Location", pending[0].ResourceType);
            Assert.Equal("test-loc-1", pending[0].ResourceId);
            Assert.Equal("Medication", pending[1].ResourceType);
            Assert.Equal("med-9", pending[1].ResourceId);
        }

        [Fact]
        public async Task ProcessReferences_IsIdempotentAcrossRepeatedCalls()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"TestFacility_{tag}";
            var correlationId = Guid.NewGuid().ToString();
            var reportTrackingId = Guid.NewGuid().ToString();

            dbContext.ScheduledReports.Add(new ScheduledReportEntity
            {
                ReportTrackingId = Guid.Parse(reportTrackingId),
                Frequency = ScheduledFrequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            var parentLog = await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ReportTrackingId = reportTrackingId,
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Search,
                Status = RequestStatus.Pending,
                Priority = AcquisitionPriority.Normal,
                ReportableEvent = ReportableEvent.Adhoc
            });

            var logModel = await scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>().GetAsync(parentLog.Id);

            var refResources = new List<ResourceReference>
            {
                new ResourceReference { Reference = "Location/loc-42" }
            };

            var fhirQueryConfig = new FhirQueryConfigurationModel
            {
                FhirServerBaseUrl = "http://localhost/fhir"
            };

            var service = CreateService(scope);

            await service.ProcessReferences(logModel, refResources, fhirQueryConfig);
            await service.ProcessReferences(logModel, refResources, fhirQueryConfig);

            var pendingCount = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId
                              && p.CorrelationId == correlationId
                              && p.ResourceType == "Location"
                              && p.ResourceId == "loc-42");

            Assert.Equal(1, pendingCount);
        }
    }
}

