using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Domain.Application.Models;
using IntegrationTests.DataAcquisition;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using RequestStatusEnum = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace IntegrationTests.DataAcquisition.Managers
{
    [Collection("DataAcquisitionIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class FhirQueryManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public FhirQueryManagerTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private IFhirQueryManager CreateManager(IServiceScope scope)
        {
            var logger = new Mock<ILogger<FhirQueryManager>>().Object;
            var database = scope.ServiceProvider.GetRequiredService<LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase>();
            return new FhirQueryManager(logger, database);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateAsync_PropagatesQueryTypeToParentLog()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var dalManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var logQueries = scope.ServiceProvider.GetRequiredService<LantanaGroup.Link.DataAcquisition.Domain.Application.Queries.IDataAcquisitionLogQueries>();

            var createModel = new CreateDataAcquisitionLogModel
            {
                FacilityId = "FacilityA",
                CorrelationId = System.Guid.NewGuid().ToString(),
                ScheduledReport = new ScheduledReport { ReportTrackingId = "Rpt-1", StartDate = System.DateTime.UtcNow.AddDays(-1), EndDate = System.DateTime.UtcNow },
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Read,
                Status = RequestStatusEnum.Pending,
                Priority = AcquisitionPriority.Normal,
                FhirQuery =
                [
                    new CreateFhirQueryModel
                    {
                        FacilityId = "FacilityA",
                        IsReference = false,
                        Paged = 25,
                        QueryType = FhirQueryType.Read,
                        QueryParameters = ["_id=abc"],
                        ResourceTypes = [ ResourceType.Patient ],
                        ResourceReferenceTypes = []
                    }
                ]
            };

            var log = await dalManager.CreateAsync(createModel);
            Assert.NotNull(log);
            Assert.Equal(FhirQueryType.Read, log.QueryType);
            Assert.Single(log.FhirQuery);

            var fhirQuery = log.FhirQuery.First();

            var fqManager = CreateManager(scope);
            await fqManager.UpdateAsync(new FhirQueryModel
            {
                Id = fhirQuery.Id,
                FacilityId = log.FacilityId,
                QueryType = FhirQueryType.Search
            });

            var refreshed = await logQueries.GetAsync(log.Id);
            Assert.NotNull(refreshed);
            Assert.Equal(FhirQueryType.Search, refreshed!.QueryType);
            Assert.Equal(FhirQueryType.Search, refreshed.FhirQuery!.First().QueryType);
        }
    }
}
