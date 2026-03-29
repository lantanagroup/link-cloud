using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

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
