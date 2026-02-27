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

        // Placeholder for future FhirQueryManager tests.
    }
}
