using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using IntegrationTests.DataAcquisition;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using RequestStatusEnum = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;

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

        private ReferenceResourceService CreateService(IServiceScope scope, IFhirQueryManager fqManager)
        {
            var logger = new Mock<ILogger<ReferenceResourceService>>().Object;
            var refMgr = new Mock<IReferenceResourcesManager>().Object;
            var refQueries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var kafkaProducer = _fixture.ResourceAcquiredProducerMock.Object;

            var metrics = new Mock<IDataAcquisitionServiceMetrics>();
            metrics.Setup(m => m.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>()));
            metrics.Setup(m => m.IncrementResourceAcquiredCounter(It.IsAny<List<KeyValuePair<string, object?>>>()));

            var daLogMgr = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var daLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            return new ReferenceResourceService(
                logger,
                refMgr,
                refQueries,
                kafkaProducer,
                metrics.Object,
                daLogMgr,
                daLogQueries,
                fqManager);
        }

        [Fact]
        public async System.Threading.Tasks.Task ProcessReferences_SynchronizesQueryTypeWithLog()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var dalManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            var createModel = new CreateDataAcquisitionLogModel
            {
                FacilityId = "FacilityB",
                CorrelationId = System.Guid.NewGuid().ToString(),
                ScheduledReport = new ScheduledReport { ReportTrackingId = "Rpt-2", StartDate = System.DateTime.UtcNow.AddDays(-1), EndDate = System.DateTime.UtcNow },
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Read,
                Status = RequestStatusEnum.Pending,
                Priority = AcquisitionPriority.Normal,
                FhirQuery =
                [
                    new CreateFhirQueryModel
                    {
                        FacilityId = "FacilityB",
                        IsReference = false,
                        Paged = 25,
                        QueryType = FhirQueryType.Read,
                        QueryParameters = ["_id=xyz"],
                        ResourceTypes = [ ResourceType.Patient ],
                        ResourceReferenceTypes = []
                    }
                ]
            };

            var log = await dalManager.CreateAsync(createModel);
            Assert.NotNull(log);
            Assert.Equal(FhirQueryType.Read, log.QueryType);
            var fhirQuery = log.FhirQuery.First();

            // Make FhirQuery have a different QueryType than the log
            var fqManager = new FhirQueryManager(new Mock<ILogger<FhirQueryManager>>().Object,
                scope.ServiceProvider.GetRequiredService<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase>());

            await fqManager.UpdateAsync(new FhirQueryModel
            {
                Id = fhirQuery.Id,
                FacilityId = log.FacilityId,
                QueryType = FhirQueryType.Search
            });

            // Force the log back to a stale QueryType to simulate inconsistency
            await dalManager.UpdateAsync(new UpdateDataAcquisitionLogModel
            {
                Id = log.Id,
                QueryType = FhirQueryType.Read
            });

            var service = CreateService(scope, fqManager);

            var rr = new List<ResourceReference>
            {
                new ResourceReference("Patient/123")
            };

            // Act - this will call FhirQueryManager.UpdateAsync internally
            await service.ProcessReferences(log, rr);

            // Assert synchronized
            var refreshed = await logQueries.GetAsync(log.Id);
            Assert.NotNull(refreshed);
            Assert.Equal(FhirQueryType.Search, refreshed!.QueryType);
            Assert.Equal(FhirQueryType.Search, refreshed.FhirQuery!.First().QueryType);
        }
    }
}
